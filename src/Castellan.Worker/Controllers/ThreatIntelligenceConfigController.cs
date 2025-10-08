using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Castellan.Worker.Models.ThreatIntelligence;
using System.Text.Json;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/settings/threat-intelligence")]
[Authorize]
public class ThreatIntelligenceConfigController : ControllerBase
{
    private readonly ILogger<ThreatIntelligenceConfigController> _logger;
    private readonly IOptionsMonitor<ThreatIntelligenceOptions> _threatIntelOptions;
    private readonly string _configFilePath;

    public ThreatIntelligenceConfigController(
        ILogger<ThreatIntelligenceConfigController> logger,
        IOptionsMonitor<ThreatIntelligenceOptions> threatIntelOptions,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _threatIntelOptions = threatIntelOptions;
        // Use AppContext.BaseDirectory to get the actual runtime directory (bin/Debug or bin/Release)
        var baseDir = AppContext.BaseDirectory;
        _configFilePath = Path.Combine(baseDir, "data", "threat-intelligence-config.json");

        // Ensure data directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);

        _logger.LogInformation("ThreatIntelligence config file path: {ConfigFilePath}", _configFilePath);
    }

    [HttpGet]
    public async Task<IActionResult> GetConfiguration()
    {
        try
        {
            _logger.LogInformation("Getting threat intelligence configuration from: {FilePath}", _configFilePath);

            ThreatIntelligenceConfigDto config;

            // Try to load from file first
            if (System.IO.File.Exists(_configFilePath))
            {
                _logger.LogInformation("Config file exists, loading from file");
                var json = await System.IO.File.ReadAllTextAsync(_configFilePath);
                _logger.LogDebug("File content length: {Length} bytes", json.Length);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                config = JsonSerializer.Deserialize<ThreatIntelligenceConfigDto>(json, options) ?? GetDefaultConfiguration();
                _logger.LogInformation("Successfully loaded config from file. VirusTotal enabled: {VtEnabled}", config.VirusTotal?.Enabled);
            }
            else
            {
                _logger.LogWarning("Config file not found at {FilePath}, using defaults", _configFilePath);
                // Use default configuration if file doesn't exist
                config = GetDefaultConfiguration();
            }

            // Ensure stable id for React Admin caching
            if (string.IsNullOrEmpty(config.Id))
            {
                config.Id = "threat-intelligence";
            }

            return Ok(new { data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat intelligence configuration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateConfiguration([FromBody] ThreatIntelligenceConfigDto config)
    {
        try
        {
            _logger.LogInformation("Updating threat intelligence configuration");

            if (config == null)
            {
                return BadRequest(new { message = "Configuration data is required" });
            }

            // Add ID if not present
            if (string.IsNullOrEmpty(config.Id))
            {
                config.Id = "threat-intelligence";
            }

            // Validate configuration
            var validationResult = ValidateConfiguration(config);
            if (validationResult != null)
            {
                return BadRequest(validationResult);
            }

            // Save to file with camelCase naming
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Saving threat intelligence config to: {FilePath}", _configFilePath);
            await System.IO.File.WriteAllTextAsync(_configFilePath, json);
            _logger.LogInformation("Threat intelligence configuration updated successfully. File size: {Size} bytes", json.Length);

            return Ok(new { data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating threat intelligence configuration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private ThreatIntelligenceConfigDto GetDefaultConfiguration()
    {
        return new ThreatIntelligenceConfigDto
        {
            Id = "threat-intelligence",
            VirusTotal = new VirusTotalConfigDto
            {
                Enabled = true,
                ApiKey = "",
                RateLimitPerMinute = 4,
                CacheEnabled = true,
                CacheTtlMinutes = 60
            },
            MalwareBazaar = new MalwareBazaarConfigDto
            {
                Enabled = true,
                RateLimitPerMinute = 10,
                CacheEnabled = true,
                CacheTtlMinutes = 30
            },
            AlienVaultOtx = new AlienVaultOtxConfigDto
            {
                Enabled = false,
                ApiKey = "",
                RateLimitPerMinute = 10,
                CacheEnabled = true,
                CacheTtlMinutes = 60
            }
        };
    }

    private object? ValidateConfiguration(ThreatIntelligenceConfigDto config)
    {
        var errors = new List<string>();

        // Validate VirusTotal configuration
        if (config.VirusTotal != null)
        {
            if (config.VirusTotal.RateLimitPerMinute < 1 || config.VirusTotal.RateLimitPerMinute > 1000)
            {
                errors.Add("VirusTotal rate limit must be between 1 and 1000 requests per minute");
            }
            if (config.VirusTotal.CacheTtlMinutes < 1 || config.VirusTotal.CacheTtlMinutes > 1440)
            {
                errors.Add("VirusTotal cache TTL must be between 1 and 1440 minutes");
            }
        }

        // Validate MalwareBazaar configuration
        if (config.MalwareBazaar != null)
        {
            if (config.MalwareBazaar.RateLimitPerMinute < 1 || config.MalwareBazaar.RateLimitPerMinute > 60)
            {
                errors.Add("MalwareBazaar rate limit must be between 1 and 60 requests per minute");
            }
            if (config.MalwareBazaar.CacheTtlMinutes < 1 || config.MalwareBazaar.CacheTtlMinutes > 1440)
            {
                errors.Add("MalwareBazaar cache TTL must be between 1 and 1440 minutes");
            }
        }

        // Validate AlienVault OTX configuration
        if (config.AlienVaultOtx != null)
        {
            if (config.AlienVaultOtx.RateLimitPerMinute < 1 || config.AlienVaultOtx.RateLimitPerMinute > 1000)
            {
                errors.Add("AlienVault OTX rate limit must be between 1 and 1000 requests per minute");
            }
            if (config.AlienVaultOtx.CacheTtlMinutes < 1 || config.AlienVaultOtx.CacheTtlMinutes > 1440)
            {
                errors.Add("AlienVault OTX cache TTL must be between 1 and 1440 minutes");
            }
        }

        return errors.Any() ? new { message = "Validation failed", errors } : null;
    }
}

// DTOs for API responses
public class ThreatIntelligenceConfigDto
{
    public string Id { get; set; } = "threat-intelligence";
    public VirusTotalConfigDto? VirusTotal { get; set; }
    public MalwareBazaarConfigDto? MalwareBazaar { get; set; }
    public AlienVaultOtxConfigDto? AlienVaultOtx { get; set; }
}

public class VirusTotalConfigDto
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = "";
    public int RateLimitPerMinute { get; set; } = 4;
    public bool CacheEnabled { get; set; } = true;
    public int CacheTtlMinutes { get; set; } = 60;
}

public class MalwareBazaarConfigDto
{
    public bool Enabled { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 10;
    public bool CacheEnabled { get; set; } = true;
    public int CacheTtlMinutes { get; set; } = 30;
}

public class AlienVaultOtxConfigDto
{
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = "";
    public int RateLimitPerMinute { get; set; } = 10;
    public bool CacheEnabled { get; set; } = true;
    public int CacheTtlMinutes { get; set; } = 60;
}
