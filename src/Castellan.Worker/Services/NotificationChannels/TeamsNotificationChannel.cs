using System.Net.Http.Json;
using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Notifications;
using Castellan.Worker.Services.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services.NotificationChannels;

/// <summary>
/// Microsoft Teams notification channel implementation with template support
/// </summary>
public class TeamsNotificationChannel : INotificationChannel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TeamsNotificationChannel> _logger;
    private readonly TeamsNotificationOptions _options;
    private readonly ChannelHealthStatus _healthStatus;
    private readonly INotificationTemplateStore _templateStore;
    private readonly ITemplateRenderer _templateRenderer;

    public NotificationChannelType Type => NotificationChannelType.Teams;
    public bool IsEnabled => _options.Enabled && !string.IsNullOrEmpty(_options.WebhookUrl);

    public TeamsNotificationChannel(
        HttpClient httpClient,
        ILogger<TeamsNotificationChannel> logger,
        IOptions<TeamsNotificationOptions> options,
        INotificationTemplateStore templateStore,
        ITemplateRenderer templateRenderer)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _healthStatus = new ChannelHealthStatus { LastCheckTime = DateTime.UtcNow };
        _templateStore = templateStore;
        _templateRenderer = templateRenderer;
    }

    public async Task<bool> SendAsync(SecurityEvent securityEvent)
    {
        if (!IsEnabled)
        {
            _logger.LogWarning("Teams notification channel is not enabled or configured");
            return false;
        }

        try
        {
            var card = await CreateAdaptiveCardAsync(securityEvent);
            var response = await _httpClient.PostAsJsonAsync(_options.WebhookUrl, card);

            if (response.IsSuccessStatusCode)
            {
                _healthStatus.SuccessCount++;
                _logger.LogInformation("Successfully sent Teams notification for event {EventId}", 
                    securityEvent.OriginalEvent.EventId);
                return true;
            }

            _healthStatus.FailureCount++;
            _healthStatus.LastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
            _logger.LogError("Failed to send Teams notification: {StatusCode} {Reason}", 
                response.StatusCode, response.ReasonPhrase);
            return false;
        }
        catch (Exception ex)
        {
            _healthStatus.FailureCount++;
            _healthStatus.LastError = ex.Message;
            _logger.LogError(ex, "Error sending Teams notification");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            var testCard = CreateTestCard();
            var response = await _httpClient.PostAsJsonAsync(_options.WebhookUrl, testCard);
            
            _healthStatus.IsHealthy = response.IsSuccessStatusCode;
            _healthStatus.LastCheckTime = DateTime.UtcNow;
            
            if (!response.IsSuccessStatusCode)
            {
                _healthStatus.LastError = $"HTTP {response.StatusCode}";
            }
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _healthStatus.IsHealthy = false;
            _healthStatus.LastError = ex.Message;
            _healthStatus.LastCheckTime = DateTime.UtcNow;
            _logger.LogError(ex, "Teams connection test failed");
            return false;
        }
    }

    public Task<ChannelHealthStatus> GetHealthStatusAsync()
    {
        return Task.FromResult(_healthStatus);
    }

    private async Task<object> CreateAdaptiveCardAsync(SecurityEvent securityEvent)
    {
        // Try to use templates first, fall back to legacy if not available
        string messageText;
        try
        {
            var templateType = TemplateContextFactory.DetermineTemplateType(securityEvent);
            var template = await _templateStore.GetEnabledTemplateAsync(NotificationPlatform.Teams, templateType);

            if (template != null)
            {
                var detailsUrl = $"{_options.CastellanUrl}/security-events/{securityEvent.Id}";
                var context = TemplateContextFactory.CreateContext(securityEvent, detailsUrl);
                messageText = _templateRenderer.Render(template, context);
                _logger.LogDebug("Using template '{TemplateName}' for Teams notification", template.Name);
            }
            else
            {
                // Fall back to legacy formatting
                messageText = GetLegacyAlertDescription(securityEvent);
                _logger.LogDebug("No template found, using legacy Teams notification format");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error rendering Teams template, falling back to legacy format");
            messageText = GetLegacyAlertDescription(securityEvent);
        }

        var severityColor = securityEvent.RiskLevel.ToLowerInvariant() switch
        {
            "critical" => "attention",
            "high" => "warning",
            "medium" => "accent",
            _ => "default"
        };

        var facts = new List<object>
        {
            new { title = "Severity", value = securityEvent.RiskLevel },
            new { title = "Event Type", value = securityEvent.EventType },
            new { title = "Event ID", value = securityEvent.OriginalEvent.EventId.ToString() },
            new { title = "Source", value = securityEvent.OriginalEvent.Channel },
            new { title = "Confidence", value = $"{securityEvent.Confidence}%" },
            new { title = "Timestamp", value = securityEvent.OriginalEvent.Time.ToString("yyyy-MM-dd HH:mm:ss") }
        };

        if (securityEvent.CorrelationScore > 0)
        {
            facts.Add(new { title = "Correlation Score", value = securityEvent.CorrelationScore.ToString("F2") });
        }

        if (securityEvent.MitreTechniques?.Length > 0)
        {
            facts.Add(new { title = "MITRE ATT&CK", value = string.Join(", ", securityEvent.MitreTechniques) });
        }

        // Add IP enrichment data if available
        if (!string.IsNullOrEmpty(securityEvent.EnrichmentData))
        {
            try
            {
                var enrichment = JsonSerializer.Deserialize<JsonElement>(securityEvent.EnrichmentData);
                if (enrichment.TryGetProperty("ip", out var ipProp))
                {
                    facts.Add(new { title = "Source IP", value = ipProp.GetString() });

                    if (enrichment.TryGetProperty("country", out var countryProp))
                    {
                        facts.Add(new { title = "Location", value = countryProp.GetString() });
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed enrichment data
            }
        }

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
                                type = "Container",
                                style = severityColor,
                                items = new object[]
                                {
                                    new
                                    {
                                        type = "TextBlock",
                                        text = "🚨 Castellan Security Alert",
                                        weight = "bolder",
                                        size = "large"
                                    },
                                    new
                                    {
                                        type = "TextBlock",
                                        text = GetAlertTitle(securityEvent),
                                        weight = "bolder",
                                        size = "medium",
                                        color = severityColor
                                    }
                                }
                            },
                            new
                            {
                                type = "FactSet",
                                facts = facts
                            },
                            new
                            {
                                type = "TextBlock",
                                text = messageText,
                                wrap = true
                            }
                        },
                        actions = new[]
                        {
                            new
                            {
                                type = "Action.OpenUrl",
                                title = "View in Castellan",
                                url = $"{_options.CastellanUrl}/security-events/{securityEvent.Id}"
                            }
                        }
                    }
                }
            }
        };
    }

    private object CreateTestCard()
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

    private string GetAlertTitle(SecurityEvent securityEvent)
    {
        return securityEvent.EventType switch
        {
            SecurityEventType.AuthenticationFailure => "Failed Login Attempt Detected",
            SecurityEventType.AuthenticationSuccess => "Successful Login",
            SecurityEventType.AccountManagement => "Account Management Activity",
            SecurityEventType.ServiceInstallation => "New Service Installation",
            SecurityEventType.ScheduledTask => "New Scheduled Task",
            SecurityEventType.PrivilegeEscalation => "Privilege Escalation Detected",
            SecurityEventType.ProcessCreation => "Process Creation Detected",
            SecurityEventType.PowerShellExecution => "PowerShell Execution",
            _ => securityEvent.EventType.ToString()
        };
    }

    private string GetLegacyAlertDescription(SecurityEvent securityEvent)
    {
        var description = securityEvent.Summary ?? "";

        if (!string.IsNullOrEmpty(securityEvent.OriginalEvent.Message))
        {
            description = $"{securityEvent.Summary}\n\n{securityEvent.OriginalEvent.Message}";
        }

        return description.Length > 500
            ? description.Substring(0, 497) + "..."
            : description;
    }
}