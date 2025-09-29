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

                // 2) YARA: check if auto-update is needed, then refresh compiled rules
                await CheckAndUpdateYaraRulesAsync(scope.ServiceProvider, stoppingToken);
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

    private async Task CheckAndUpdateYaraRulesAsync(IServiceProvider serviceProvider, CancellationToken stoppingToken)
    {
        try
        {
            // Load YARA configuration
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "yara-config.json");

            if (!File.Exists(configPath))
            {
                _logger.LogInformation("No YARA configuration found, skipping auto-update check");
                return;
            }

            var configJson = await File.ReadAllTextAsync(configPath, stoppingToken);
            var config = System.Text.Json.JsonSerializer.Deserialize<YaraConfigurationModel>(configJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            if (config?.AutoUpdate?.Enabled != true)
            {
                _logger.LogInformation("YARA auto-update is disabled, skipping check");
                return;
            }

            // Check if update is needed
            var updateNeeded = false;
            if (config.AutoUpdate.LastUpdate.HasValue)
            {
                var daysSinceLastUpdate = (DateTime.UtcNow - config.AutoUpdate.LastUpdate.Value).TotalDays;
                updateNeeded = daysSinceLastUpdate >= config.AutoUpdate.UpdateFrequencyDays;

                if (updateNeeded)
                {
                    _logger.LogInformation("YARA rules update needed: Last update was {Days:F1} days ago (threshold: {Threshold} days)",
                        daysSinceLastUpdate, config.AutoUpdate.UpdateFrequencyDays);
                }
                else
                {
                    _logger.LogInformation("YARA rules update not needed: Last update was {Days:F1} days ago (threshold: {Threshold} days)",
                        daysSinceLastUpdate, config.AutoUpdate.UpdateFrequencyDays);
                }
            }
            else
            {
                updateNeeded = true;
                _logger.LogInformation("YARA rules update needed: No previous update found");
            }

            if (updateNeeded)
            {
                _logger.LogInformation("Starting automatic YARA rules import...");

                // Call the import tool
                var repoRoot = GetRepositoryRoot();
                var importToolPath = Path.Combine(repoRoot, "src", "Tools", "YaraImportTool");
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{importToolPath}\" -- --limit {config.Sources.MaxRulesPerSource}",
                    WorkingDirectory = repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(stoppingToken);
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("YARA rules import completed successfully. Output: {Output}", output);

                        // Update configuration with successful import
                        config.AutoUpdate.LastUpdate = DateTime.UtcNow;
                        config.AutoUpdate.NextUpdate = DateTime.UtcNow.AddDays(config.AutoUpdate.UpdateFrequencyDays);
                        config.Import.LastImportDate = DateTime.UtcNow;

                        // Parse rule count from output if available
                        if (output.Contains("Total rules in DB:"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(output, @"Total rules in DB: (\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out var totalRules))
                            {
                                config.Import.TotalRules = totalRules;
                                config.Import.EnabledRules = totalRules; // Assuming all imported rules are enabled
                            }
                        }

                        // Save updated configuration
                        var updatedConfigJson = System.Text.Json.JsonSerializer.Serialize(config,
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                                WriteIndented = true
                            });
                        await File.WriteAllTextAsync(configPath, updatedConfigJson, stoppingToken);

                        // No need to copy database - we're using the central one directly
                        _logger.LogInformation("YARA rules database updated in central location");
                    }
                    else
                    {
                        _logger.LogError("YARA rules import failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                    }
                }
                else
                {
                    _logger.LogError("Failed to start YARA import process");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during YARA rules auto-update check");
        }
    }

    private static string GetRepositoryRoot()
    {
        // Fixed repository root path
        var repoRoot = @"C:\Users\matsl\Castellan";
        if (Directory.Exists(repoRoot))
        {
            return repoRoot;
        }

        // Fallback: Navigate up from current directory
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
    }
}

// Simple model classes for configuration
public class YaraConfigurationModel
{
    public AutoUpdateSettings? AutoUpdate { get; set; }
    public SourceSettings? Sources { get; set; }
    public ImportSettings? Import { get; set; }
}

public class AutoUpdateSettings
{
    public bool Enabled { get; set; }
    public int UpdateFrequencyDays { get; set; }
    public DateTime? LastUpdate { get; set; }
    public DateTime? NextUpdate { get; set; }
}

public class SourceSettings
{
    public bool Enabled { get; set; }
    public List<string> Urls { get; set; } = new();
    public int MaxRulesPerSource { get; set; }
}

public class ImportSettings
{
    public DateTime? LastImportDate { get; set; }
    public int TotalRules { get; set; }
    public int EnabledRules { get; set; }
    public int FailedRules { get; set; }
}
