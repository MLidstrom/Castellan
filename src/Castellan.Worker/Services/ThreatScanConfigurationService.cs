using System.Text.Json;
using Microsoft.Extensions.Options;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

public interface IThreatScanConfigurationService
{
    Task<ThreatScanOptions> GetConfigurationAsync();
    Task SaveConfigurationAsync(ThreatScanOptions configuration);
    Task<bool> IsConfigurationValidAsync(ThreatScanOptions configuration);
}

public class ThreatScanConfigurationService : IThreatScanConfigurationService
{
    private readonly ILogger<ThreatScanConfigurationService> _logger;
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ThreatScanConfigurationService(ILogger<ThreatScanConfigurationService> logger)
    {
        _logger = logger;
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "threat-scan-config.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true  // Add case-insensitive deserialization
        };

        // Ensure config directory exists
        var configDir = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    public async Task<ThreatScanOptions> GetConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("[CONFIG] GetConfigurationAsync called, path: {Path}", _configPath);

            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("[CONFIG] File does not exist at {Path}, returning defaults (Enabled=true)", _configPath);
                return new ThreatScanOptions();
            }

            var json = await File.ReadAllTextAsync(_configPath);
            _logger.LogInformation("[CONFIG] Read JSON (length: {Length}): {Json}", json.Length, json);

            try
            {
                var config = JsonSerializer.Deserialize<ThreatScanOptions>(json, _jsonOptions);

                if (config == null)
                {
                    _logger.LogError("[CONFIG] Deserialization returned null! Returning defaults (Enabled=true)");
                    return new ThreatScanOptions();
                }

                _logger.LogInformation("[CONFIG] Successfully deserialized - Enabled={Enabled}, Interval={Interval}",
                    config.Enabled, config.ScheduledScanInterval);
                return config;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "[CONFIG] JSON deserialization failed! Returning defaults (Enabled=true)");
                return new ThreatScanOptions();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CONFIG] Unexpected error loading config from {Path}, returning defaults (Enabled=true)", _configPath);
            return new ThreatScanOptions();
        }
    }

    public async Task SaveConfigurationAsync(ThreatScanOptions configuration)
    {
        try
        {
            if (!await IsConfigurationValidAsync(configuration))
            {
                throw new ArgumentException("Invalid threat scan configuration");
            }

            var json = JsonSerializer.Serialize(configuration, _jsonOptions);
            await File.WriteAllTextAsync(_configPath, json);

            _logger.LogInformation("Saved threat scan configuration to {ConfigPath}", _configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving threat scan configuration to {ConfigPath}", _configPath);
            throw;
        }
    }

    public async Task<bool> IsConfigurationValidAsync(ThreatScanOptions configuration)
    {
        try
        {
            // Validate scan interval
            if (configuration.ScheduledScanInterval < TimeSpan.FromMinutes(1))
            {
                _logger.LogWarning("Scan interval too short: {Interval}", configuration.ScheduledScanInterval);
                return false;
            }

            if (configuration.ScheduledScanInterval > TimeSpan.FromDays(30))
            {
                _logger.LogWarning("Scan interval too long: {Interval}", configuration.ScheduledScanInterval);
                return false;
            }

            // Validate file size limit
            if (configuration.MaxFileSizeMB <= 0 || configuration.MaxFileSizeMB > 10000)
            {
                _logger.LogWarning("Invalid max file size: {Size}MB", configuration.MaxFileSizeMB);
                return false;
            }

            // Validate concurrent files
            if (configuration.MaxConcurrentFiles <= 0 || configuration.MaxConcurrentFiles > 50)
            {
                _logger.LogWarning("Invalid max concurrent files: {Count}", configuration.MaxConcurrentFiles);
                return false;
            }

            // Validate quarantine directory if quarantine is enabled
            if (configuration.QuarantineThreats)
            {
                if (string.IsNullOrWhiteSpace(configuration.QuarantineDirectory))
                {
                    _logger.LogWarning("Quarantine directory not specified but quarantine is enabled");
                    return false;
                }

                try
                {
                    // Test if directory can be created/accessed
                    Directory.CreateDirectory(configuration.QuarantineDirectory);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot access quarantine directory: {Directory}", configuration.QuarantineDirectory);
                    return false;
                }
            }

            // Validate excluded directories exist (if specified)
            foreach (var dir in configuration.ExcludedDirectories)
            {
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    _logger.LogWarning("Excluded directory does not exist: {Directory}", dir);
                    // This is just a warning, not a validation failure
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating threat scan configuration");
            return false;
        }
    }
}