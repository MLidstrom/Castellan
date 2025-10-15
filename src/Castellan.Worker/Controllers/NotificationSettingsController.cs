using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class NotificationSettingsController : ControllerBase
{
    private readonly ILogger<NotificationSettingsController> _logger;
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public NotificationSettingsController(
        ILogger<NotificationSettingsController> logger,
        IWebHostEnvironment env)
    {
        _logger = logger;

        // Use AppContext.BaseDirectory to get the actual runtime directory (bin/Debug or bin/Release)
        var baseDir = AppContext.BaseDirectory;
        _configFilePath = Path.Combine(baseDir, "data", "notification-config.json");

        // Ensure data directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        _logger.LogInformation("Notification config file path: {ConfigFilePath}", _configFilePath);
    }

    /// <summary>
    /// Get notification configuration
    /// </summary>
    [HttpGet("notification")]
    public async Task<IActionResult> GetConfiguration()
    {
        try
        {
            _logger.LogInformation("Getting notification configuration from: {FilePath}", _configFilePath);

            NotificationConfigDto config;

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
                config = JsonSerializer.Deserialize<NotificationConfigDto>(json, options) ?? GetDefaultConfiguration();
                _logger.LogInformation("Successfully loaded config from file");
            }
            else
            {
                _logger.LogWarning("Config file not found at {FilePath}, using defaults", _configFilePath);
                config = GetDefaultConfiguration();
            }

            // Ensure stable id for React Admin caching
            if (string.IsNullOrEmpty(config.Id))
            {
                config.Id = "notification";
            }

            return Ok(new { data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notification configuration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update notification configuration
    /// </summary>
    [HttpPut("notification")]
    public async Task<IActionResult> UpdateConfiguration([FromBody] NotificationConfigDto config)
    {
        try
        {
            _logger.LogInformation("Updating notification configuration");

            if (config == null)
            {
                return BadRequest(new { message = "Configuration data is required" });
            }

            // Add ID if not present
            if (string.IsNullOrEmpty(config.Id))
            {
                config.Id = "notification";
            }

            // Save to file with camelCase naming
            var json = JsonSerializer.Serialize(config, _jsonOptions);

            _logger.LogInformation("Saving notification config to: {FilePath}", _configFilePath);
            await System.IO.File.WriteAllTextAsync(_configFilePath, json);
            _logger.LogInformation("Notification configuration updated successfully. File size: {Size} bytes", json.Length);

            return Ok(new { data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification configuration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private NotificationConfigDto GetDefaultConfiguration()
    {
        return new NotificationConfigDto
        {
            Id = "notification",
            Teams = new TeamsConfigDto
            {
                Enabled = false,
                WebhookUrl = "",
                NotificationTypes = new NotificationTypesDto
                {
                    CriticalEvents = true,
                    HighRiskEvents = true,
                    MediumRiskEvents = false,
                    CorrelationAlerts = true,
                    MalwareMatches = true
                }
            },
            Slack = new SlackConfigDto
            {
                Enabled = false,
                WebhookUrl = "",
                Channel = "#security",
                NotificationTypes = new NotificationTypesDto
                {
                    CriticalEvents = true,
                    HighRiskEvents = true,
                    MediumRiskEvents = false,
                    CorrelationAlerts = true,
                    MalwareMatches = true
                }
            }
        };
    }
}

// DTOs
public class NotificationConfigDto
{
    public string Id { get; set; } = string.Empty;
    public TeamsConfigDto Teams { get; set; } = new();
    public SlackConfigDto Slack { get; set; } = new();
}

public class TeamsConfigDto
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public NotificationTypesDto NotificationTypes { get; set; } = new();
}

public class SlackConfigDto
{
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public NotificationTypesDto NotificationTypes { get; set; } = new();
}

public class NotificationTypesDto
{
    public bool CriticalEvents { get; set; }
    public bool HighRiskEvents { get; set; }
    public bool MediumRiskEvents { get; set; }
    public bool CorrelationAlerts { get; set; }
    public bool MalwareMatches { get; set; }
}
