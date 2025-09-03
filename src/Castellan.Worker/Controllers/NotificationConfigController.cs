using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Castellan.Worker.Services;
using Castellan.Worker.Models;
using Castellan.Worker.Services.NotificationChannels;
using System.Net.Http.Json;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationConfigController : ControllerBase
{
    private readonly ILogger<NotificationConfigController> _logger;
    private readonly INotificationConfigurationStore _configStore;
    private readonly INotificationManager _notificationManager;
    
    public NotificationConfigController(
        ILogger<NotificationConfigController> logger,
        INotificationConfigurationStore configStore,
        INotificationManager notificationManager)
    {
        _logger = logger;
        _configStore = configStore;
        _notificationManager = notificationManager;
    }

    /// <summary>
    /// Get all notification configurations (React-admin compatible)
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfigurations(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string sort = "name",
        [FromQuery] string order = "asc")
    {
        try
        {
            var allConfigs = await _configStore.GetAllAsync();
            var totalCount = allConfigs.Count();
            
            // Apply sorting
            allConfigs = sort.ToLowerInvariant() switch
            {
                "name" => order.ToLowerInvariant() == "desc" 
                    ? allConfigs.OrderByDescending(c => c.Name) 
                    : allConfigs.OrderBy(c => c.Name),
                "createdat" => order.ToLowerInvariant() == "desc" 
                    ? allConfigs.OrderByDescending(c => c.CreatedAt) 
                    : allConfigs.OrderBy(c => c.CreatedAt),
                _ => allConfigs.OrderBy(c => c.Name)
            };
            
            // Apply pagination
            var pagedConfigs = allConfigs
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToList();

            return Ok(new
            {
                data = pagedConfigs,
                total = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification configurations");
            return StatusCode(500, new { error = "Failed to retrieve configurations" });
        }
    }

    /// <summary>
    /// Get single notification configuration by ID
    /// </summary>
    [HttpGet("config/{id}")]
    public async Task<IActionResult> GetConfiguration(string id)
    {
        try
        {
            var config = await _configStore.GetByIdAsync(id);
            if (config == null)
            {
                return NotFound(new { error = $"Configuration with ID {id} not found" });
            }

            return Ok(new { data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification configuration {ConfigId}", id);
            return StatusCode(500, new { error = "Failed to retrieve configuration" });
        }
    }

    /// <summary>
    /// Create new notification configuration
    /// </summary>
    [HttpPost("config")]
    public async Task<IActionResult> CreateConfiguration([FromBody] NotificationConfiguration configuration)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(configuration.Name))
            {
                return BadRequest(new { error = "Configuration name is required" });
            }

            // Validate webhook URLs if provided
            if (configuration.Teams.Enabled && !string.IsNullOrEmpty(configuration.Teams.WebhookUrl))
            {
                if (!IsValidTeamsWebhookUrl(configuration.Teams.WebhookUrl))
                {
                    return BadRequest(new { error = "Invalid Teams webhook URL format" });
                }
            }

            if (configuration.Slack.Enabled && !string.IsNullOrEmpty(configuration.Slack.WebhookUrl))
            {
                if (!IsValidSlackWebhookUrl(configuration.Slack.WebhookUrl))
                {
                    return BadRequest(new { error = "Invalid Slack webhook URL format" });
                }
            }

            var created = await _configStore.CreateAsync(configuration);
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification configuration");
            return StatusCode(500, new { error = "Failed to create configuration" });
        }
    }

    /// <summary>
    /// Update notification configuration
    /// </summary>
    [HttpPut("config/{id}")]
    public async Task<IActionResult> UpdateConfiguration(string id, [FromBody] NotificationConfiguration configuration)
    {
        try
        {
            if (id != configuration.Id)
            {
                return BadRequest(new { error = "ID in URL must match ID in request body" });
            }

            if (string.IsNullOrWhiteSpace(configuration.Name))
            {
                return BadRequest(new { error = "Configuration name is required" });
            }

            var existing = await _configStore.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { error = $"Configuration with ID {id} not found" });
            }

            // Validate webhook URLs if provided
            if (configuration.Teams.Enabled && !string.IsNullOrEmpty(configuration.Teams.WebhookUrl))
            {
                if (!IsValidTeamsWebhookUrl(configuration.Teams.WebhookUrl))
                {
                    return BadRequest(new { error = "Invalid Teams webhook URL format" });
                }
            }

            if (configuration.Slack.Enabled && !string.IsNullOrEmpty(configuration.Slack.WebhookUrl))
            {
                if (!IsValidSlackWebhookUrl(configuration.Slack.WebhookUrl))
                {
                    return BadRequest(new { error = "Invalid Slack webhook URL format" });
                }
            }

            var updated = await _configStore.UpdateAsync(configuration);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification configuration {ConfigId}", id);
            return StatusCode(500, new { error = "Failed to update configuration" });
        }
    }

    /// <summary>
    /// Delete notification configuration
    /// </summary>
    [HttpDelete("config/{id}")]
    public async Task<IActionResult> DeleteConfiguration(string id)
    {
        try
        {
            var existing = await _configStore.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { error = $"Configuration with ID {id} not found" });
            }

            await _configStore.DeleteAsync(id);
            return Ok(new { data = existing });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification configuration {ConfigId}", id);
            return StatusCode(500, new { error = "Failed to delete configuration" });
        }
    }

    /// <summary>
    /// Test Teams notification connection
    /// </summary>
    [HttpPost("teams/test")]
    public async Task<IActionResult> TestTeamsConnection([FromBody] TestConnectionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.WebhookUrl))
            {
                return BadRequest(new { error = "Webhook URL is required" });
            }

            if (!IsValidTeamsWebhookUrl(request.WebhookUrl))
            {
                return BadRequest(new { error = "Invalid Teams webhook URL format" });
            }

            // Test the webhook URL directly
            var httpClient = new HttpClient();
            var testCard = CreateTestTeamsCard();
            var response = await httpClient.PostAsJsonAsync(request.WebhookUrl, testCard);

            var success = response.IsSuccessStatusCode;
            
            return Ok(new
            {
                success = success,
                message = success ? "Test notification sent successfully" : $"Test failed: {response.StatusCode} {response.ReasonPhrase}",
                statusCode = (int)response.StatusCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Teams connection");
            return Ok(new
            {
                success = false,
                message = $"Test failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Test Slack notification connection
    /// </summary>
    [HttpPost("slack/test")]
    public async Task<IActionResult> TestSlackConnection([FromBody] TestConnectionRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.WebhookUrl))
            {
                return BadRequest(new { error = "Webhook URL is required" });
            }

            if (!IsValidSlackWebhookUrl(request.WebhookUrl))
            {
                return BadRequest(new { error = "Invalid Slack webhook URL format" });
            }

            // Test the webhook URL directly
            var httpClient = new HttpClient();
            var testMessage = CreateTestSlackMessage();
            var response = await httpClient.PostAsJsonAsync(request.WebhookUrl, testMessage);

            var success = response.IsSuccessStatusCode;
            
            return Ok(new
            {
                success = success,
                message = success ? "Test notification sent successfully" : $"Test failed: {response.StatusCode} {response.ReasonPhrase}",
                statusCode = (int)response.StatusCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Slack connection");
            return Ok(new
            {
                success = false,
                message = $"Test failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get notification health status
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthStatus()
    {
        try
        {
            var health = await _notificationManager.GetHealthStatusAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification health status");
            return StatusCode(500, new { error = "Failed to retrieve health status" });
        }
    }

    private bool IsValidTeamsWebhookUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Teams webhooks should be from outlook.office.com or teams.microsoft.com
        return uri.Host.Equals("outlook.office.com", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("teams.microsoft.com", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsValidSlackWebhookUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Slack webhooks should be from hooks.slack.com
        return uri.Host.Equals("hooks.slack.com", StringComparison.OrdinalIgnoreCase);
    }

    private object CreateTestTeamsCard()
    {
        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "✅ Castellan Teams Integration Test",
                                weight = "bolder",
                                size = "large"
                            },
                            new
                            {
                                type = "TextBlock",
                                text = $"Connection test successful at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                                wrap = true
                            }
                        }
                    }
                }
            }
        };
    }

    private object CreateTestSlackMessage()
    {
        return new
        {
            text = "✅ Castellan Slack Integration Test",
            username = "Castellan Security",
            icon_emoji = ":shield:",
            attachments = new[]
            {
                new
                {
                    color = "good",
                    title = "Connection Test Successful",
                    text = $"Castellan can successfully send notifications to this Slack workspace.\nTest performed at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    footer = "Castellan Security",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            }
        };
    }
}

// Request DTOs
public class TestConnectionRequest
{
    public string WebhookUrl { get; set; } = string.Empty;
}