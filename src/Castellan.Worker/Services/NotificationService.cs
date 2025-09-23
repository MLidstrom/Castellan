using System.Runtime.InteropServices;
using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Castellan.Worker.Services.NotificationChannels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

public interface INotificationService
{
    Task ShowNotificationAsync(string title, string message, string level = "Info");
    Task SendSecurityNotificationAsync(SecurityEvent securityEvent);
    Task SendCorrelationNotificationAsync(SecurityEvent securityEvent, EventCorrelation correlation);
    Task SendAttackChainNotificationAsync(List<SecurityEvent> events, AttackChain attackChain);
    bool ShouldShowNotification(string level);
}

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly NotificationOptions _options;
    private readonly INotificationManager _notificationManager;

    public NotificationService(
        ILogger<NotificationService> logger, 
        IOptions<NotificationOptions> options,
        INotificationManager notificationManager)
    {
        _logger = logger;
        _options = options.Value;
        _notificationManager = notificationManager;
        
        // DEBUG: Log notification service initialization
        _logger.LogInformation("🔔 NotificationService initialized with settings:");
        _logger.LogInformation("   - EnableDesktopNotifications: {EnableDesktopNotifications}", _options.EnableDesktopNotifications);
        _logger.LogInformation("   - NotificationLevel: {NotificationLevel}", _options.NotificationLevel);
        _logger.LogInformation("   - NotificationTimeout: {NotificationTimeout}ms", _options.NotificationTimeout);
        _logger.LogInformation("   - ShowEventDetails: {ShowEventDetails}", _options.ShowEventDetails);
    }

    public async Task ShowNotificationAsync(string title, string message, string level = "Info")
    {
        _logger.LogInformation("🔔 ShowNotification called with:");
        _logger.LogInformation("   - Title: {Title}", title);
        _logger.LogInformation("   - Message: {Message}", message);
        _logger.LogInformation("   - Level: {Level}", level);
        
        if (!_options.EnableDesktopNotifications)
        {
            _logger.LogWarning("🔔 Desktop notifications are DISABLED in configuration");
            return;
        }

        if (!ShouldShowNotification(level))
        {
            _logger.LogWarning("🔔 Notification level {Level} is below configured minimum {MinLevel}", level, _options.NotificationLevel);
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogInformation("🔔 Showing Windows notification...");
                await ShowWindowsNotificationAsync(title, message, level);
                _logger.LogInformation("🔔 Windows notification method completed");
            }
            else
            {
                _logger.LogWarning("🔔 Desktop notifications are only supported on Windows");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔔 Failed to show notification: {Title}", title);
        }
    }

    public bool ShouldShowNotification(string level)
    {
        var levelPriority = GetLevelPriority(level);
        var minPriority = GetLevelPriority(_options.NotificationLevel);
        var shouldShow = levelPriority >= minPriority;
        
        _logger.LogDebug("🔔 ShouldShowNotification check:");
        _logger.LogDebug("   - Requested level: {Level} (priority: {LevelPriority})", level, levelPriority);
        _logger.LogDebug("   - Minimum level: {MinLevel} (priority: {MinPriority})", _options.NotificationLevel, minPriority);
        _logger.LogDebug("   - Should show: {ShouldShow}", shouldShow);
        
        return shouldShow;
    }

    /// <summary>
    /// Send security notification to all configured channels (desktop, Teams, Slack)
    /// </summary>
    public async Task SendSecurityNotificationAsync(SecurityEvent securityEvent)
    {
        _logger.LogInformation("🔔 Sending security notification for event {EventId} with risk level {RiskLevel}", 
            securityEvent.OriginalEvent.EventId, securityEvent.RiskLevel);

        // Send to all notification channels (Teams, Slack, etc.)
        await _notificationManager.SendSecurityAlertAsync(securityEvent);

        // Also send desktop notification
        var title = $"🚨 Castellan Security Alert - {securityEvent.RiskLevel.ToUpperInvariant()}";
        var message = CreateSecurityNotificationMessage(securityEvent);
        await ShowNotificationAsync(title, message, securityEvent.RiskLevel);
    }

    public async Task SendCorrelationNotificationAsync(SecurityEvent securityEvent, EventCorrelation correlation)
    {
        _logger.LogInformation("🔗 Sending correlation notification for event {EventId} with correlation type {CorrelationType}",
            securityEvent.OriginalEvent.EventId, correlation.CorrelationType);

        // Determine if this correlation warrants a notification
        if (!ShouldSendCorrelationNotification(correlation))
        {
            _logger.LogDebug("Correlation notification suppressed for low-confidence correlation");
            return;
        }

        // Use the original security event for notification

        // Send to notification channels with correlation context
        await _notificationManager.SendCorrelationAlertAsync(securityEvent, correlation);

        // Send enhanced desktop notification
        var title = $"🔗 Castellan Correlation Alert - {correlation.CorrelationType}";
        var message = CreateCorrelationNotificationMessage(securityEvent, correlation);
        var notificationLevel = DetermineCorrelationNotificationLevel(correlation);
        await ShowNotificationAsync(title, message, notificationLevel);
    }

    public async Task SendAttackChainNotificationAsync(List<SecurityEvent> events, AttackChain attackChain)
    {
        _logger.LogInformation("⚔️ Sending attack chain notification for {AttackType} with {EventCount} events",
            attackChain.AttackType, events.Count);

        // Attack chains are always high priority
        await _notificationManager.SendAttackChainAlertAsync(events, attackChain);

        // Send critical desktop notification
        var title = $"⚔️ CRITICAL: {attackChain.AttackType} Detected";
        var message = CreateAttackChainNotificationMessage(events, attackChain);
        await ShowNotificationAsync(title, message, "critical");
    }

    private bool ShouldSendCorrelationNotification(EventCorrelation correlation)
    {
        // Always send notifications for attack chains and lateral movement
        if (correlation.CorrelationType.Equals("AttackChain", StringComparison.OrdinalIgnoreCase) ||
            correlation.CorrelationType.Equals("LateralMovement", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Send notifications for high-confidence correlations
        if (correlation.ConfidenceScore > 0.8)
        {
            return true;
        }

        // Send notifications for high/critical risk correlations
        if (correlation.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase) ||
            correlation.RiskLevel.Equals("critical", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private string DetermineCorrelationNotificationLevel(EventCorrelation correlation)
    {
        if (correlation.RiskLevel.Equals("critical", StringComparison.OrdinalIgnoreCase))
            return "critical";
        if (correlation.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase))
            return "high";
        if (correlation.ConfidenceScore > 0.8)
            return "medium";
        return "info";
    }

    private string CreateCorrelationNotificationMessage(SecurityEvent securityEvent, EventCorrelation correlation)
    {
        var message = $"Correlation Type: {correlation.CorrelationType}\n";
        message += $"Pattern: {correlation.Pattern}\n";
        message += $"Confidence: {correlation.ConfidenceScore:P0}\n";
        message += $"Risk Level: {correlation.RiskLevel}\n";
        message += $"Related Events: {correlation.EventIds.Count}\n";

        if (correlation.TimeWindow.TotalMinutes < 60)
        {
            message += $"Time Window: {correlation.TimeWindow.TotalMinutes:F0} minutes\n";
        }
        else
        {
            message += $"Time Window: {correlation.TimeWindow.TotalHours:F1} hours\n";
        }

        if (!string.IsNullOrEmpty(correlation.AttackChainStage))
        {
            message += $"Attack Stage: {correlation.AttackChainStage}\n";
        }

        if (correlation.MitreTechniques?.Any() == true)
        {
            message += $"MITRE Techniques: {string.Join(", ", correlation.MitreTechniques.Take(3))}\n";
        }

        message += $"\nOriginal Event: {securityEvent.EventType} ({securityEvent.OriginalEvent.EventId})";
        return message;
    }

    private string CreateAttackChainNotificationMessage(List<SecurityEvent> events, AttackChain attackChain)
    {
        var message = $"Attack Type: {attackChain.AttackType}\n";
        message += $"Confidence: {attackChain.ConfidenceScore:P0}\n";
        message += $"Risk Level: {attackChain.RiskLevel}\n";
        message += $"Total Events: {events.Count}\n";
        message += $"Affected Systems: {string.Join(", ", attackChain.AffectedAssets.Take(3))}\n";

        var duration = attackChain.EndTime - attackChain.StartTime;
        if (duration.TotalMinutes < 60)
        {
            message += $"Duration: {duration.TotalMinutes:F0} minutes\n";
        }
        else
        {
            message += $"Duration: {duration.TotalHours:F1} hours\n";
        }

        if (attackChain.MitreTechniques?.Any() == true)
        {
            message += $"MITRE Techniques: {string.Join(", ", attackChain.MitreTechniques.Take(3))}\n";
        }

        message += "\nAttack Stages:\n";
        foreach (var stage in attackChain.Stages.Take(5))
        {
            message += $"  {stage.Sequence}. {stage.Name}\n";
        }

        return message;
    }

    /// <summary>
    /// Create a formatted message for desktop notifications
    /// </summary>
    private string CreateSecurityNotificationMessage(SecurityEvent securityEvent)
    {
        var message = $"Event: {securityEvent.EventType}\n";
        message += $"ID: {securityEvent.OriginalEvent.EventId}\n";
        message += $"Confidence: {securityEvent.Confidence}%\n";

        if (_options.ShowIPEnrichment && !string.IsNullOrEmpty(securityEvent.EnrichmentData))
        {
            try
            {
                var enrichment = JsonSerializer.Deserialize<JsonElement>(securityEvent.EnrichmentData);
                if (enrichment.TryGetProperty("ip", out var ipProp))
                {
                    var ip = ipProp.GetString();
                    message += $"Source IP: {ip}";
                    
                    var locationParts = new List<string>();
                    if (enrichment.TryGetProperty("city", out var cityProp))
                    {
                        var city = cityProp.GetString();
                        if (!string.IsNullOrEmpty(city))
                        {
                            locationParts.Add(city);
                        }
                    }
                    if (enrichment.TryGetProperty("country", out var countryProp))
                    {
                        var country = countryProp.GetString();
                        if (!string.IsNullOrEmpty(country))
                        {
                            locationParts.Add(country);
                        }
                    }
                    if (enrichment.TryGetProperty("asn", out var asnProp))
                    {
                        // Handle ASN as a string (most common format) or number
                        string? asnValue = null;
                        
                        if (asnProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            asnValue = asnProp.GetString();
                        }
                        else if (asnProp.ValueKind == System.Text.Json.JsonValueKind.Number && asnProp.TryGetInt32(out var asnNumber))
                        {
                            asnValue = $"AS{asnNumber}";
                        }
                        
                        if (!string.IsNullOrEmpty(asnValue))
                        {
                            locationParts.Add(asnValue);
                        }
                    }
                    
                    if (locationParts.Any())
                    {
                        message += $" ({string.Join(", ", locationParts)})";
                    }
                    message += "\n";
                }
            }
            catch (JsonException)
            {
                // Ignore malformed enrichment data
            }
        }

        if (_options.ShowEventDetails && securityEvent.MitreTechniques.Length > 0)
        {
            message += $"MITRE ATT&CK: {string.Join(", ", securityEvent.MitreTechniques)}\n";
        }

        if (securityEvent.CorrelationScore > 0)
        {
            message += $"Correlation Score: {securityEvent.CorrelationScore:F2}";
        }

        return message.TrimEnd();
    }

    private async Task ShowWindowsNotificationAsync(string title, string message, string level)
    {
        _logger.LogInformation("🔔 Creating Windows notification with:");
        _logger.LogInformation("   - Title: {Title}", title);
        _logger.LogInformation("   - Message: {Message}", message);
        _logger.LogInformation("   - Level: {Level}", level);
        _logger.LogInformation("   - Timeout: {Timeout}ms", _options.NotificationTimeout);
        
        try
        {
            // Use Windows Forms for notifications on Windows with proper disposal
            using var notification = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Shield,
                Visible = true,
                BalloonTipTitle = title,
                BalloonTipText = message,
                BalloonTipIcon = GetNotificationIcon(level)
            };

            _logger.LogInformation("🔔 NotifyIcon created, showing balloon tip...");
            notification.ShowBalloonTip(_options.NotificationTimeout);
            _logger.LogInformation("🔔 Balloon tip shown successfully");
            
            // Wait for the notification to be displayed before disposing
            // The notification will be automatically disposed at the end of the using block
            await Task.Delay(_options.NotificationTimeout + 500);
            _logger.LogDebug("🔔 Notification timeout completed, disposing automatically");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔔 Error in ShowWindowsNotification");
        }
    }

    private System.Windows.Forms.ToolTipIcon GetNotificationIcon(string level)
    {
        var icon = level.ToLowerInvariant() switch
        {
            // Security risk levels
            "critical" => System.Windows.Forms.ToolTipIcon.Error,
            "high" => System.Windows.Forms.ToolTipIcon.Error,
            "medium" => System.Windows.Forms.ToolTipIcon.Warning,
            "low" => System.Windows.Forms.ToolTipIcon.Info,
            
            // Standard log levels (for backward compatibility)
            "error" => System.Windows.Forms.ToolTipIcon.Error,
            "warning" => System.Windows.Forms.ToolTipIcon.Warning,
            "info" => System.Windows.Forms.ToolTipIcon.Info,
            _ => System.Windows.Forms.ToolTipIcon.None
        };
        
        _logger.LogDebug("🔔 Notification icon for level '{Level}': {Icon}", level, icon);
        return icon;
    }

    private int GetLevelPriority(string level)
    {
        var priority = level.ToLowerInvariant() switch
        {
            // Security risk levels
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            
            // Standard log levels (for backward compatibility)
            "error" => 3,
            "warning" => 2,
            "info" => 1,
            _ => 0
        };
        
        _logger.LogDebug("🔔 Level priority for '{Level}': {Priority}", level, priority);
        return priority;
    }
}

