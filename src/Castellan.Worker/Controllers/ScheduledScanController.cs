using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Castellan.Worker.Models;
using Castellan.Worker.Models.ThreatIntelligence;
using Castellan.Worker.Services;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduledScanController : ControllerBase
{
    private readonly ILogger<ScheduledScanController> _logger;
    private readonly IOptionsMonitor<ThreatScanOptions> _optionsMonitor;
    private readonly IEnumerable<IHostedService> _hostedServices;
    private readonly IThreatScanConfigurationService _configService;

    public ScheduledScanController(
        ILogger<ScheduledScanController> logger,
        IOptionsMonitor<ThreatScanOptions> optionsMonitor,
        IEnumerable<IHostedService> hostedServices,
        IThreatScanConfigurationService configService)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _hostedServices = hostedServices;
        _configService = configService;
    }

    /// <summary>
    /// Get current scheduled scan configuration and status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ScheduledScanStatusDto>> GetStatusAsync()
    {
        try
        {
            var options = _optionsMonitor.CurrentValue;
            var scheduledScanService = _hostedServices.OfType<ScheduledThreatScanService>().FirstOrDefault();

            var status = new ScheduledScanStatusDto
            {
                IsEnabled = options.Enabled,
                ScanInterval = options.ScheduledScanInterval,
                DefaultScanType = options.DefaultScanType,
                LastScanTime = scheduledScanService?.GetLastScanTime(),
                NextScanTime = scheduledScanService?.GetNextScanTime(),
                IsScanInProgress = scheduledScanService != null && await scheduledScanService.IsScanInProgressAsync(),
                NotificationThreshold = options.NotificationThreshold
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scheduled scan status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get current scheduled scan configuration
    /// </summary>
    [HttpGet("config")]
    public async Task<ActionResult<ThreatScanConfigDto>> GetConfig()
    {
        try
        {
            // Read from the same source that SaveConfig writes to (config/threat-scan-config.json)
            var options = await _configService.GetConfigurationAsync();

            var config = new ThreatScanConfigDto
            {
                Enabled = options.Enabled,
                ScheduledScanInterval = options.ScheduledScanInterval,
                DefaultScanType = options.DefaultScanType,
                ExcludedDirectories = options.ExcludedDirectories,
                ExcludedExtensions = options.ExcludedExtensions,
                MaxFileSizeMB = options.MaxFileSizeMB,
                MaxConcurrentFiles = options.MaxConcurrentFiles,
                QuarantineThreats = options.QuarantineThreats,
                QuarantineDirectory = options.QuarantineDirectory,
                EnableRealTimeProtection = options.EnableRealTimeProtection,
                NotificationThreshold = options.NotificationThreshold
            };

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat scan configuration");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Save threat scan configuration
    /// </summary>
    [HttpPost("config")]
    public async Task<ActionResult> SaveConfigAsync([FromBody] ThreatScanConfigDto configDto)
    {
        try
        {
            _logger.LogInformation("SaveConfig called with Enabled={Enabled}", configDto.Enabled);

            // Convert DTO to options
            var options = new ThreatScanOptions
            {
                Enabled = configDto.Enabled,
                ScheduledScanInterval = configDto.ScheduledScanInterval,
                DefaultScanType = configDto.DefaultScanType,
                ExcludedDirectories = configDto.ExcludedDirectories,
                ExcludedExtensions = configDto.ExcludedExtensions,
                MaxFileSizeMB = configDto.MaxFileSizeMB,
                MaxConcurrentFiles = configDto.MaxConcurrentFiles,
                QuarantineThreats = configDto.QuarantineThreats,
                QuarantineDirectory = configDto.QuarantineDirectory,
                EnableRealTimeProtection = configDto.EnableRealTimeProtection,
                NotificationThreshold = configDto.NotificationThreshold
            };

            _logger.LogInformation("Validating configuration...");
            // Validate configuration
            if (!await _configService.IsConfigurationValidAsync(options))
            {
                _logger.LogWarning("Validation failed!");
                return BadRequest(new { error = "Invalid configuration provided" });
            }

            _logger.LogInformation("Validation passed, saving configuration...");
            // Save configuration
            await _configService.SaveConfigurationAsync(options);

            _logger.LogInformation("Threat scan configuration updated successfully with Enabled={Enabled}", options.Enabled);

            return Ok(new { message = "Configuration saved successfully" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid threat scan configuration provided");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving threat scan configuration");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Force a scheduled scan to run immediately (manual trigger)
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult> TriggerScanAsync([FromBody] TriggerScanRequest? request = null)
    {
        try
        {
            var scheduledScanService = _hostedServices.OfType<ScheduledThreatScanService>().FirstOrDefault();
            if (scheduledScanService == null)
            {
                return BadRequest(new { error = "Scheduled scan service not available" });
            }

            // Check if a scan is already in progress
            if (await scheduledScanService.IsScanInProgressAsync())
            {
                return Conflict(new { error = "A scan is already in progress" });
            }

            // TODO: Implement manual trigger functionality
            // This would require extending ScheduledThreatScanService with a manual trigger method

            _logger.LogInformation("Manual scan trigger requested");
            return Ok(new { message = "Manual scan trigger functionality coming soon" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering manual scan");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class TriggerScanRequest
{
    public ThreatScanType? ScanType { get; set; }
}

public class ScheduledScanStatusDto
{
    public bool IsEnabled { get; set; }
    public TimeSpan ScanInterval { get; set; }
    public ThreatScanType DefaultScanType { get; set; }
    public DateTime? LastScanTime { get; set; }
    public DateTime? NextScanTime { get; set; }
    public bool IsScanInProgress { get; set; }
    public ThreatRiskLevel NotificationThreshold { get; set; }
}

public class ThreatScanConfigDto
{
    public bool Enabled { get; set; }
    public TimeSpan ScheduledScanInterval { get; set; }
    public ThreatScanType DefaultScanType { get; set; }
    public string[] ExcludedDirectories { get; set; } = Array.Empty<string>();
    public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();
    public int MaxFileSizeMB { get; set; }
    public int MaxConcurrentFiles { get; set; }
    public bool QuarantineThreats { get; set; }
    public string QuarantineDirectory { get; set; } = string.Empty;
    public bool EnableRealTimeProtection { get; set; }
    public ThreatRiskLevel NotificationThreshold { get; set; }
}