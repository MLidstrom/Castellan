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
/// Slack notification channel implementation with template support
/// </summary>
public class SlackNotificationChannel : INotificationChannel
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackNotificationChannel> _logger;
    private readonly SlackNotificationOptions _options;
    private readonly ChannelHealthStatus _healthStatus;
    private readonly INotificationTemplateStore _templateStore;
    private readonly ITemplateRenderer _templateRenderer;

    public NotificationChannelType Type => NotificationChannelType.Slack;
    public bool IsEnabled => _options.Enabled && !string.IsNullOrEmpty(_options.WebhookUrl);

    public SlackNotificationChannel(
        HttpClient httpClient,
        ILogger<SlackNotificationChannel> logger,
        IOptions<SlackNotificationOptions> options,
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
            _logger.LogWarning("Slack notification channel is not enabled or configured");
            return false;
        }

        try
        {
            var message = await CreateSlackMessageAsync(securityEvent);
            var response = await _httpClient.PostAsJsonAsync(_options.WebhookUrl, message);

            if (response.IsSuccessStatusCode)
            {
                _healthStatus.SuccessCount++;
                _logger.LogInformation("Successfully sent Slack notification for event {EventId}", 
                    securityEvent.OriginalEvent.EventId);
                return true;
            }

            _healthStatus.FailureCount++;
            _healthStatus.LastError = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
            _logger.LogError("Failed to send Slack notification: {StatusCode} {Reason}", 
                response.StatusCode, response.ReasonPhrase);
            return false;
        }
        catch (Exception ex)
        {
            _healthStatus.FailureCount++;
            _healthStatus.LastError = ex.Message;
            _logger.LogError(ex, "Error sending Slack notification");
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
            var testMessage = CreateTestMessage();
            var response = await _httpClient.PostAsJsonAsync(_options.WebhookUrl, testMessage);
            
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
            _logger.LogError(ex, "Slack connection test failed");
            return false;
        }
    }

    public Task<ChannelHealthStatus> GetHealthStatusAsync()
    {
        return Task.FromResult(_healthStatus);
    }

    private async Task<object> CreateSlackMessageAsync(SecurityEvent securityEvent)
    {
        // Try to use templates first, fall back to legacy if not available
        string messageText;
        try
        {
            var templateType = TemplateContextFactory.DetermineTemplateType(securityEvent);
            var template = await _templateStore.GetEnabledTemplateAsync(NotificationPlatform.Slack, templateType);

            if (template != null)
            {
                var detailsUrl = $"{_options.CastellanUrl}/security-events/{securityEvent.Id}";
                var context = TemplateContextFactory.CreateContext(securityEvent, detailsUrl);
                messageText = _templateRenderer.Render(template, context);
                _logger.LogDebug("Using template '{TemplateName}' for Slack notification", template.Name);
            }
            else
            {
                // Fall back to legacy formatting
                messageText = GetLegacyAlertDescription(securityEvent);
                _logger.LogDebug("No template found, using legacy Slack notification format");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error rendering Slack template, falling back to legacy format");
            messageText = GetLegacyAlertDescription(securityEvent);
        }

        var color = GetSeverityColor(securityEvent.RiskLevel);
        var channel = GetChannelForSeverity(securityEvent.RiskLevel);

        var fields = new List<object>
        {
            new
            {
                title = "Severity",
                value = securityEvent.RiskLevel,
                @short = true
            },
            new
            {
                title = "Event Type",
                value = securityEvent.EventType,
                @short = true
            },
            new
            {
                title = "Event ID",
                value = securityEvent.OriginalEvent.EventId.ToString(),
                @short = true
            },
            new
            {
                title = "Confidence",
                value = $"{securityEvent.Confidence}%",
                @short = true
            }
        };

        if (securityEvent.CorrelationScore > 0)
        {
            fields.Add(new
            {
                title = "Correlation Score",
                value = securityEvent.CorrelationScore.ToString("F2"),
                @short = true
            });
        }

        if (securityEvent.MitreTechniques?.Length > 0)
        {
            fields.Add(new
            {
                title = "MITRE ATT&CK",
                value = string.Join(", ", securityEvent.MitreTechniques),
                @short = false
            });
        }

        // Add IP enrichment data if available
        string? locationInfo = null;
        if (!string.IsNullOrEmpty(securityEvent.EnrichmentData))
        {
            try
            {
                var enrichment = JsonSerializer.Deserialize<JsonElement>(securityEvent.EnrichmentData);
                if (enrichment.TryGetProperty("ip", out var ipProp))
                {
                    var ip = ipProp.GetString();
                    var locationParts = new List<string> { ip ?? "" };
                    
                    if (enrichment.TryGetProperty("city", out var cityProp))
                        locationParts.Add(cityProp.GetString() ?? "");
                    if (enrichment.TryGetProperty("country", out var countryProp))
                        locationParts.Add(countryProp.GetString() ?? "");
                    
                    locationInfo = string.Join(", ", locationParts.Where(p => !string.IsNullOrEmpty(p)));
                    if (!string.IsNullOrEmpty(locationInfo))
                    {
                        fields.Add(new
                        {
                            title = "Source Location",
                            value = locationInfo,
                            @short = false
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed enrichment data
            }
        }

        var slackMessage = new
        {
            text = $"🚨 Security Alert: {GetAlertTitle(securityEvent)}",
            username = "Castellan Security",
            icon_emoji = ":shield:",
            channel = channel,
            attachments = new[]
            {
                new
                {
                    color = color,
                    fallback = $"{securityEvent.RiskLevel} Security Alert: {securityEvent.EventType}",
                    pretext = GetAlertPretext(securityEvent.RiskLevel),
                    title = GetAlertTitle(securityEvent),
                    title_link = $"{_options.CastellanUrl}/security-events/{securityEvent.Id}",
                    text = messageText,
                    fields = fields,
                    footer = "Castellan Security",
                    footer_icon = "https://platform.slack-edge.com/img/default_application_icon.png",
                    ts = securityEvent.OriginalEvent.Time.ToUnixTimeSeconds(),
                    actions = new[]
                    {
                        new
                        {
                            type = "button",
                            text = "View Details",
                            url = $"{_options.CastellanUrl}/security-events/{securityEvent.Id}",
                            style = "primary"
                        },
                        new
                        {
                            type = "button",
                            text = "Acknowledge",
                            url = $"{_options.CastellanUrl}/api/security-events/{securityEvent.Id}/acknowledge",
                            style = "default"
                        }
                    }
                }
            }
        };

        // Add user mentions for critical alerts
        if (securityEvent.RiskLevel.Equals("critical", StringComparison.OrdinalIgnoreCase) && 
            _options.MentionUsersForCritical?.Any() == true)
        {
            var mentions = string.Join(" ", _options.MentionUsersForCritical.Select(u => $"<@{u}>"));
            slackMessage = slackMessage with 
            { 
                text = $"{mentions} 🚨 Critical Security Alert: {GetAlertTitle(securityEvent)}" 
            };
        }

        return slackMessage;
    }

    private object CreateTestMessage()
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
                    text = $"Castellan can successfully send notifications to this Slack workspace.",
                    footer = "Castellan Security",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            }
        };
    }

    private string GetSeverityColor(string riskLevel)
    {
        return riskLevel.ToLowerInvariant() switch
        {
            "critical" => "#d32f2f", // Red
            "high" => "#ff9800",      // Orange
            "medium" => "#ffc107",    // Yellow
            "low" => "#4caf50",       // Green
            _ => "#9e9e9e"           // Grey
        };
    }

    private string? GetChannelForSeverity(string riskLevel)
    {
        // Use configured channel mappings
        if (_options.ChannelMappings?.TryGetValue(riskLevel.ToLowerInvariant(), out var channel) == true)
        {
            return channel;
        }

        // Default channel selection
        return riskLevel.ToLowerInvariant() switch
        {
            "critical" => _options.CriticalChannel ?? _options.DefaultChannel,
            "high" => _options.HighChannel ?? _options.DefaultChannel,
            _ => _options.DefaultChannel
        };
    }

    private string GetAlertPretext(string riskLevel)
    {
        return riskLevel.ToLowerInvariant() switch
        {
            "critical" => "🚨 *CRITICAL SECURITY ALERT*",
            "high" => "⚠️ *High Priority Security Alert*",
            "medium" => "⚡ Medium Priority Security Alert",
            "low" => "ℹ️ Low Priority Security Alert",
            _ => "Security Alert"
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
        var description = "";

        if (!string.IsNullOrEmpty(securityEvent.Summary))
        {
            description = $"*Summary:* {securityEvent.Summary}\n";
        }

        if (!string.IsNullOrEmpty(securityEvent.OriginalEvent.Message))
        {
            description += securityEvent.OriginalEvent.Message;
        }

        return description.Length > 500
            ? description.Substring(0, 497) + "..."
            : description;
    }
}