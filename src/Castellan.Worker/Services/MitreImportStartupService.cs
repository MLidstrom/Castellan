using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service that imports MITRE ATT&CK data on startup if needed
/// </summary>
public class MitreImportStartupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MitreImportStartupService> _logger;
    private readonly IConfiguration _configuration;

    public MitreImportStartupService(
        IServiceProvider serviceProvider,
        ILogger<MitreImportStartupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the application to fully start and database to be initialized
        await Task.Delay(10000, stoppingToken);
        
        if (stoppingToken.IsCancellationRequested)
            return;

        try
        {
            // Check if auto-import is enabled
            var autoImportEnabled = _configuration.GetValue<bool>("Mitre:AutoImportOnStartup", true);
            if (!autoImportEnabled)
            {
                _logger.LogInformation("🛡️ MITRE auto-import disabled in configuration");
                return;
            }

            _logger.LogInformation("🔍 Starting MITRE ATT&CK data availability check...");

            using var scope = _serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<MitreAttackImportService>();
            
            var currentCount = await importService.GetTechniqueCountAsync();
            var shouldImport = await importService.ShouldImportTechniquesAsync();
            
            _logger.LogInformation("📊 Current MITRE techniques in database: {CurrentCount}", currentCount);
            
            if (shouldImport)
            {
                _logger.LogInformation("⬇️ MITRE ATT&CK data needs to be imported. Starting automatic import...");
                
                try
                {
                    var result = await importService.ImportAllTechniquesAsync();
                    
                    if (result.HasErrors)
                    {
                        _logger.LogWarning("⚠️ MITRE import completed with {ErrorCount} errors. Imported: {Imported}, Updated: {Updated}",
                            result.Errors.Count, result.TechniquesImported, result.TechniquesUpdated);
                        
                        // Log first few errors for debugging
                        foreach (var error in result.Errors.Take(3))
                        {
                            _logger.LogWarning("Import error: {Error}", error);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("✅ MITRE import completed successfully! Imported: {Imported}, Updated: {Updated}",
                            result.TechniquesImported, result.TechniquesUpdated);
                        _logger.LogInformation("🎯 Total MITRE techniques now available: {Total}", result.TechniquesImported + result.TechniquesUpdated);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "❌ Network error during MITRE import - check internet connectivity");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to import MITRE ATT&CK data during startup");
                    // Don't throw - let the application continue running even if import fails
                }
            }
            else
            {
                _logger.LogInformation("✅ MITRE ATT&CK data is up to date. Current count: {CurrentCount}", currentCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during MITRE import startup check");
            // Don't throw - let the application continue running
        }
    }
}
