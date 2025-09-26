using Microsoft.Extensions.Logging;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Handles normalization of raw Windows events to SecurityEvent objects
/// </summary>
public static class EventNormalizationHandler
{
    /// <summary>
    /// Normalize a raw event to a SecurityEvent
    /// </summary>
    public static SecurityEvent Normalize(RawEvent rawEvent, ILogger? logger = null)
    {
        try
        {
            // Create LogEvent from RawEvent
            var logEvent = new LogEvent(
                Time: new DateTimeOffset(rawEvent.TimeCreated),
                Host: rawEvent.MachineName,
                Channel: rawEvent.ChannelName,
                EventId: rawEvent.EventId,
                Level: GetLogLevel(rawEvent.Level),
                User: rawEvent.UserName,
                Message: rawEvent.Message,
                RawJson: rawEvent.Xml,
                UniqueId: rawEvent.Id
            );

            // Determine event type based on channel and event ID
            var eventType = DetermineEventType(rawEvent.ChannelName, rawEvent.EventId, rawEvent.ProviderName);
            
            // Determine risk level based on event type and context
            var riskLevel = DetermineRiskLevel(eventType, rawEvent.EventId, rawEvent.Level);
            
            // Determine confidence based on event characteristics
            var confidence = DetermineConfidence(eventType, rawEvent.ChannelName, rawEvent.EventId);
            
            // Generate summary
            var summary = GenerateSummary(eventType, rawEvent);
            
            // Determine MITRE techniques based on event type
            var mitreTechniques = DetermineMitreTechniques(eventType, rawEvent.EventId);
            
            // Generate recommended actions
            var recommendedActions = GenerateRecommendedActions(eventType, riskLevel);

            // Create deterministic security event
            var securityEvent = SecurityEvent.CreateDeterministic(
                originalEvent: logEvent,
                eventType: eventType,
                riskLevel: riskLevel,
                confidence: confidence,
                summary: summary,
                mitreTechniques: mitreTechniques,
                recommendedActions: recommendedActions
            );

            logger?.LogDebug("Normalized event {EventId} from channel {Channel} to type {EventType} with risk {RiskLevel}",
                rawEvent.EventId, rawEvent.ChannelName, eventType, riskLevel);

            return securityEvent;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error normalizing event {EventId} from channel {Channel}", 
                rawEvent.EventId, rawEvent.ChannelName);
            
            // Return a fallback event
            var logEvent = new LogEvent(
                Time: new DateTimeOffset(rawEvent.TimeCreated),
                Host: rawEvent.MachineName,
                Channel: rawEvent.ChannelName,
                EventId: rawEvent.EventId,
                Level: GetLogLevel(rawEvent.Level),
                User: rawEvent.UserName,
                Message: rawEvent.Message,
                RawJson: rawEvent.Xml,
                UniqueId: rawEvent.Id
            );

            return SecurityEvent.CreateDeterministic(
                originalEvent: logEvent,
                eventType: SecurityEventType.Unknown,
                riskLevel: "unknown",
                confidence: 0,
                summary: "Failed to normalize event",
                mitreTechniques: Array.Empty<string>(),
                recommendedActions: new[] { "Manual investigation required" }
            );
        }
    }

    /// <summary>
    /// Convert Windows event level to string representation
    /// </summary>
    private static string GetLogLevel(byte level)
    {
        return level switch
        {
            1 => "Critical",
            2 => "Error", 
            3 => "Warning",
            4 => "Information",
            5 => "Verbose",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Determine the security event type based on channel and event ID
    /// </summary>
    private static SecurityEventType DetermineEventType(string channelName, int eventId, string providerName)
    {
        // Security channel events
        if (channelName.Equals("Security", StringComparison.OrdinalIgnoreCase))
        {
            return eventId switch
            {
                4624 => SecurityEventType.AuthenticationSuccess,
                4625 => SecurityEventType.AuthenticationFailure,
                4672 => SecurityEventType.PrivilegeEscalation,
                4688 => SecurityEventType.ProcessCreation,
                4634 => SecurityEventType.AuthenticationSuccess, // Logout is still auth success
                4648 => SecurityEventType.AuthenticationSuccess,
                4776 => SecurityEventType.AuthenticationFailure, // Login attempt failure
                4778 => SecurityEventType.AuthenticationSuccess,
                4779 => SecurityEventType.AuthenticationSuccess,
                _ => SecurityEventType.AuthenticationSuccess
            };
        }

        // Sysmon events
        if (channelName.Contains("Sysmon", StringComparison.OrdinalIgnoreCase))
        {
            return eventId switch
            {
                1 => SecurityEventType.ProcessCreation,
                2 => SecurityEventType.SuspiciousActivity, // File creation time changed
                3 => SecurityEventType.NetworkConnection,
                4 => SecurityEventType.ServiceInstallation,
                5 => SecurityEventType.ProcessCreation, // Process terminated
                6 => SecurityEventType.ServiceInstallation, // Driver loaded
                7 => SecurityEventType.ProcessCreation, // Image loaded
                8 => SecurityEventType.SuspiciousActivity, // Create remote thread
                9 => SecurityEventType.SuspiciousActivity, // Raw access read
                10 => SecurityEventType.ProcessCreation, // Process access
                11 => SecurityEventType.SuspiciousActivity, // File create
                12 => SecurityEventType.SuspiciousActivity, // Registry event
                13 => SecurityEventType.SuspiciousActivity, // Registry event
                14 => SecurityEventType.SuspiciousActivity, // Registry event
                15 => SecurityEventType.SuspiciousActivity, // File create stream hash
                16 => SecurityEventType.SecurityPolicyChange, // Service configuration changed
                17 => SecurityEventType.SuspiciousActivity, // Pipe created
                18 => SecurityEventType.SuspiciousActivity, // Pipe connected
                19 => SecurityEventType.SuspiciousActivity, // WMI event filter
                20 => SecurityEventType.SuspiciousActivity, // WMI event consumer
                21 => SecurityEventType.SuspiciousActivity, // WMI event consumer to filter
                22 => SecurityEventType.NetworkConnection, // DNS query
                23 => SecurityEventType.SuspiciousActivity, // File delete
                24 => SecurityEventType.SuspiciousActivity, // Clipboard change
                25 => SecurityEventType.SuspiciousActivity, // Process tampering
                _ => SecurityEventType.ProcessCreation
            };
        }

        // PowerShell events
        if (channelName.Contains("PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            return eventId switch
            {
                4103 => SecurityEventType.PowerShellExecution,
                4104 => SecurityEventType.PowerShellExecution,
                4105 => SecurityEventType.PowerShellExecution,
                4106 => SecurityEventType.PowerShellExecution,
                _ => SecurityEventType.PowerShellExecution
            };
        }

        // Windows Defender events
        if (channelName.Contains("Defender", StringComparison.OrdinalIgnoreCase))
        {
            return eventId switch
            {
                1116 => SecurityEventType.SuspiciousActivity, // Malware detected
                1117 => SecurityEventType.SuspiciousActivity, // Malware action taken
                1118 => SecurityEventType.SuspiciousActivity, // Malware action failed
                _ => SecurityEventType.SuspiciousActivity
            };
        }

        return SecurityEventType.Unknown;
    }

    /// <summary>
    /// Determine risk level based on event type and characteristics
    /// </summary>
    private static string DetermineRiskLevel(SecurityEventType eventType, int eventId, byte level)
    {
        // High-risk events
        if (eventType == SecurityEventType.PrivilegeEscalation ||
            eventType == SecurityEventType.SuspiciousActivity)
        {
            return "critical";
        }

        // Medium-high risk events
        if (eventType == SecurityEventType.AuthenticationFailure ||
            eventType == SecurityEventType.ProcessCreation ||
            eventType == SecurityEventType.NetworkConnection ||
            eventType == SecurityEventType.PowerShellExecution ||
            eventType == SecurityEventType.ServiceInstallation)
        {
            return "high";
        }

        // Medium risk events
        if (eventType == SecurityEventType.AuthenticationSuccess ||
            eventType == SecurityEventType.AccountManagement ||
            eventType == SecurityEventType.SecurityPolicyChange)
        {
            return "medium";
        }

        // Low risk events
        if (eventType == SecurityEventType.SystemStartup ||
            eventType == SecurityEventType.SystemShutdown)
        {
            return "low";
        }

        // Default based on Windows event level
        return level switch
        {
            1 => "critical",
            2 => "high",
            3 => "medium",
            4 => "low",
            _ => "low"
        };
    }

    /// <summary>
    /// Determine confidence level based on event characteristics
    /// </summary>
    private static int DetermineConfidence(SecurityEventType eventType, string channelName, int eventId)
    {
        // High confidence for well-defined security events
        if (channelName.Equals("Security", StringComparison.OrdinalIgnoreCase) &&
            (eventId == 4624 || eventId == 4625 || eventId == 4672 || eventId == 4688))
        {
            return 95;
        }

        // High confidence for Sysmon events
        if (channelName.Contains("Sysmon", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        // Medium confidence for PowerShell events
        if (channelName.Contains("PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            return 80;
        }

        // Medium confidence for Defender events
        if (channelName.Contains("Defender", StringComparison.OrdinalIgnoreCase))
        {
            return 85;
        }

        // Default confidence
        return 70;
    }

    /// <summary>
    /// Generate a human-readable summary for the event
    /// </summary>
    private static string GenerateSummary(SecurityEventType eventType, RawEvent rawEvent)
    {
        var baseSummary = eventType switch
        {
            SecurityEventType.AuthenticationSuccess => $"Successful login detected from {rawEvent.MachineName}",
            SecurityEventType.AuthenticationFailure => $"Failed login attempt from {rawEvent.MachineName}",
            SecurityEventType.PrivilegeEscalation => $"Privilege escalation detected on {rawEvent.MachineName}",
            SecurityEventType.ProcessCreation => $"New process created on {rawEvent.MachineName}",
            SecurityEventType.NetworkConnection => $"Network connection established from {rawEvent.MachineName}",
            SecurityEventType.PowerShellExecution => $"PowerShell script execution detected on {rawEvent.MachineName}",
            SecurityEventType.SuspiciousActivity => $"Suspicious activity detected on {rawEvent.MachineName}",
            SecurityEventType.ServiceInstallation => $"Service installation detected on {rawEvent.MachineName}",
            SecurityEventType.AccountManagement => $"Account management activity on {rawEvent.MachineName}",
            SecurityEventType.SecurityPolicyChange => $"Security policy change on {rawEvent.MachineName}",
            _ => $"Security event detected on {rawEvent.MachineName}"
        };

        return $"{baseSummary} (Event ID: {rawEvent.EventId}, Channel: {rawEvent.ChannelName})";
    }

    /// <summary>
    /// Determine MITRE ATT&CK techniques based on event type
    /// </summary>
    private static string[] DetermineMitreTechniques(SecurityEventType eventType, int eventId)
    {
        return eventType switch
        {
            SecurityEventType.AuthenticationSuccess or SecurityEventType.AuthenticationFailure => new[] { "T1078" }, // Valid Accounts
            SecurityEventType.PrivilegeEscalation => new[] { "T1548", "T1055" }, // Abuse Elevation Control Mechanism, Process Injection
            SecurityEventType.ProcessCreation => new[] { "T1055", "T1059" }, // Process Injection, Command and Scripting Interpreter
            SecurityEventType.NetworkConnection => new[] { "T1071", "T1090" }, // Application Layer Protocol, Proxy
            SecurityEventType.PowerShellExecution => new[] { "T1059.001" }, // PowerShell
            SecurityEventType.SuspiciousActivity => new[] { "T1204", "T1566" }, // User Execution, Phishing
            SecurityEventType.ServiceInstallation => new[] { "T1542", "T1055" }, // Pre-OS Boot, Process Injection
            SecurityEventType.AccountManagement => new[] { "T1136", "T1078" }, // Create Account, Valid Accounts
            SecurityEventType.SecurityPolicyChange => new[] { "T1484", "T1078" }, // Domain Policy Modification, Valid Accounts
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Generate recommended actions based on event type and risk level
    /// </summary>
    private static string[] GenerateRecommendedActions(SecurityEventType eventType, string riskLevel)
    {
        var actions = new List<string>();

        // Risk-based actions
        if (riskLevel == "critical")
        {
            actions.Add("Immediate investigation required");
            actions.Add("Consider isolating affected systems");
            actions.Add("Notify security team immediately");
        }
        else if (riskLevel == "high")
        {
            actions.Add("Investigate within 1 hour");
            actions.Add("Review related security events");
            actions.Add("Consider enhanced monitoring");
        }
        else if (riskLevel == "medium")
        {
            actions.Add("Investigate within 4 hours");
            actions.Add("Monitor for related activity");
        }
        else
        {
            actions.Add("Review during normal security operations");
        }

        // Event-type specific actions
        switch (eventType)
        {
            case SecurityEventType.AuthenticationFailure:
                actions.Add("Check for brute force attempts");
                actions.Add("Review account lockout policies");
                break;
            case SecurityEventType.ProcessCreation:
                actions.Add("Verify process legitimacy");
                actions.Add("Check for process injection indicators");
                break;
            case SecurityEventType.NetworkConnection:
                actions.Add("Verify network connection legitimacy");
                actions.Add("Check for command and control indicators");
                break;
            case SecurityEventType.PowerShellExecution:
                actions.Add("Review PowerShell execution policy");
                actions.Add("Analyze script content for malicious indicators");
                break;
            case SecurityEventType.SuspiciousActivity:
                actions.Add("Quarantine affected files");
                actions.Add("Run full system scan");
                actions.Add("Review infection vector");
                break;
        }

        return actions.ToArray();
    }
}
