using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services;

/// <summary>
/// Runs daily refresh tasks for MITRE import checks and YARA rule recompilation.
/// </summary>
public class DailyRefreshHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DailyRefreshHostedService> _logger;

    public DailyRefreshHostedService(IServiceProvider services, ILogger<DailyRefreshHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let app warm up
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();

                // 1) MITRE: check if refresh needed (configurable), import if required
                var mitreImport = scope.ServiceProvider.GetRequiredService<MitreAttackImportService>();
                var shouldImport = await mitreImport.ShouldImportTechniquesAsync();
                if (shouldImport)
                {
                    _logger.LogInformation("Daily MITRE check: updates required. Starting import...");
                    await mitreImport.ImportAllTechniquesAsync();
                }
                else
                {
                    _logger.LogInformation("Daily MITRE check: up to date.");
                }

                // 2) YARA: refresh compiled rules so long-running workers stay in sync
                var yara = scope.ServiceProvider.GetRequiredService<IYaraScanService>();
                await yara.RefreshRulesAsync(stoppingToken);
                _logger.LogInformation("Daily YARA refresh: compiled rules reloaded. Compiled count: {Count}", yara.GetCompiledRuleCount());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily refresh encountered an error");
            }

            // Wait ~24h
            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); } catch { }
        }
    }
}
