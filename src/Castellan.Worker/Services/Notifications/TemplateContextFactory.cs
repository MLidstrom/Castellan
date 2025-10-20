using System.Text.Json;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Notifications;

namespace Castellan.Worker.Services.Notifications;

/// <summary>
/// Factory for creating template rendering context from security events
/// </summary>
public static class TemplateContextFactory
{
    /// <summary>
    /// Creates template context dictionary from a security event
    /// </summary>
    public static Dictionary<string, string> CreateContext(SecurityEvent securityEvent, string detailsUrl)
    {
        var context = new Dictionary<string, string>
        {
            ["DATE"] = securityEvent.OriginalEvent.Time.ToString("yyyy-MM-dd HH:mm:ss"),
            ["TIMESTAMP"] = securityEvent.OriginalEvent.Time.ToString("yyyy-MM-dd HH:mm:ss"),
            ["HOST"] = securityEvent.OriginalEvent.Host ?? "Unknown",
            ["MACHINE_NAME"] = securityEvent.OriginalEvent.Host ?? "Unknown",
            ["USER"] = ExtractUserName(securityEvent),
            ["EVENT_ID"] = securityEvent.OriginalEvent.EventId.ToString(),
            ["SEVERITY"] = securityEvent.RiskLevel,
            ["EVENT_TYPE"] = GetEventTypeDescription(securityEvent.EventType),
            ["SUMMARY"] = securityEvent.Summary ?? "Security event detected",
            ["MITRE_TECHNIQUES"] = FormatMitreTechniques(securityEvent.MitreTechniques),
            ["RECOMMENDED_ACTIONS"] = GetRecommendedActions(securityEvent),
            ["DETAILS_URL"] = detailsUrl,
            ["ALERT_ID"] = securityEvent.Id.ToString(),
            ["CONFIDENCE"] = $"{securityEvent.Confidence}%",
            ["CORRELATION_SCORE"] = securityEvent.CorrelationScore.ToString("F2")
        };

        // Add IP enrichment data if available
        if (!string.IsNullOrEmpty(securityEvent.EnrichmentData))
        {
            try
            {
                var enrichment = JsonSerializer.Deserialize<JsonElement>(securityEvent.EnrichmentData);

                if (enrichment.TryGetProperty("ip", out var ipProp))
                {
                    context["SOURCE_IP"] = ipProp.GetString() ?? "";
                }

                if (enrichment.TryGetProperty("country", out var countryProp))
                {
                    context["LOCATION"] = countryProp.GetString() ?? "";
                }

                if (enrichment.TryGetProperty("city", out var cityProp))
                {
                    var city = cityProp.GetString();
                    if (!string.IsNullOrEmpty(city) && context.ContainsKey("LOCATION"))
                    {
                        context["LOCATION"] = $"{city}, {context["LOCATION"]}";
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore malformed enrichment data
            }
        }

        // Add original event message if available
        if (!string.IsNullOrEmpty(securityEvent.OriginalEvent.Message))
        {
            context["EVENT_MESSAGE"] = securityEvent.OriginalEvent.Message;
        }

        return context;
    }

    /// <summary>
    /// Determines the template type based on security event characteristics
    /// </summary>
    public static NotificationTemplateType DetermineTemplateType(SecurityEvent securityEvent)
    {
        // For now, all security events use SecurityEvent template
        // Future: Could route to different templates based on event type or severity
        return NotificationTemplateType.SecurityEvent;
    }

    private static string ExtractUserName(SecurityEvent securityEvent)
    {
        // Try to extract username from event data
        if (!string.IsNullOrEmpty(securityEvent.OriginalEvent.Message))
        {
            // Simple extraction - could be enhanced
            var message = securityEvent.OriginalEvent.Message;
            var accountNameIndex = message.IndexOf("Account Name:", StringComparison.OrdinalIgnoreCase);
            if (accountNameIndex >= 0)
            {
                var start = accountNameIndex + "Account Name:".Length;
                var end = message.IndexOf('\n', start);
                if (end > start)
                {
                    return message.Substring(start, end - start).Trim();
                }
            }
        }

        return "Unknown User";
    }

    private static string GetEventTypeDescription(SecurityEventType eventType)
    {
        return eventType switch
        {
            SecurityEventType.AuthenticationFailure => "Failed Login Attempt",
            SecurityEventType.AuthenticationSuccess => "Successful Login",
            SecurityEventType.AccountManagement => "Account Management Activity",
            SecurityEventType.ServiceInstallation => "Service Installation",
            SecurityEventType.ScheduledTask => "Scheduled Task Creation",
            SecurityEventType.PrivilegeEscalation => "Privilege Escalation",
            SecurityEventType.ProcessCreation => "Process Creation",
            SecurityEventType.PowerShellExecution => "PowerShell Execution",
            SecurityEventType.NetworkConnection => "Network Connection",
            _ => eventType.ToString()
        };
    }

    private static string FormatMitreTechniques(string[]? techniques)
    {
        if (techniques == null || techniques.Length == 0)
        {
            return "None";
        }

        return string.Join(", ", techniques);
    }

    private static string GetRecommendedActions(SecurityEvent securityEvent)
    {
        // Generate recommended actions based on event type and severity
        var actions = new List<string>();

        actions.Add($"Review event details in Castellan dashboard");

        if (securityEvent.RiskLevel.Equals("critical", StringComparison.OrdinalIgnoreCase) ||
            securityEvent.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase))
        {
            actions.Add("Investigate immediately");
            actions.Add("Check for related security events");
        }

        switch (securityEvent.EventType)
        {
            case SecurityEventType.AuthenticationFailure:
                actions.Add("Verify user credentials");
                actions.Add("Check for brute force patterns");
                break;
            case SecurityEventType.PrivilegeEscalation:
                actions.Add("Review user permissions");
                actions.Add("Audit privilege changes");
                break;
            case SecurityEventType.ProcessCreation:
                actions.Add("Verify process legitimacy");
                actions.Add("Check parent process chain");
                break;
        }

        return string.Join("; ", actions);
    }
}
