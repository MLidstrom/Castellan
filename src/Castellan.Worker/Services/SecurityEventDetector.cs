using Microsoft.Extensions.Logging;
using Castellan.Worker.Models;
using Castellan.Worker.Abstractions;
using System.Text.Json;

namespace Castellan.Worker.Services;

public sealed class SecurityEventDetector(
    ILogger<SecurityEventDetector> logger,
    ICorrelationEngine correlationEngine,
    ISecurityEventRuleStore ruleStore)
{
    private Dictionary<string, SecurityEventRule>? _ruleCache;
    private DateTime _lastCacheRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromMinutes(5);

    // Legacy hard-coded rules dictionary kept as fallback
    private static readonly Dictionary<int, SecurityEventRule> LegacySecurityEventRules = new()
    {
        // Authentication Events
        { 4624, new SecurityEventRule(SecurityEventType.AuthenticationSuccess, "medium", 85, 
            "Successful logon", new[] { "T1078" }, new[] { "Monitor for unusual logon patterns", "Verify user identity" }) },
        { 4625, new SecurityEventRule(SecurityEventType.AuthenticationFailure, "high", 90, 
            "Failed logon attempt", new[] { "T1110" }, new[] { "Investigate failed logon source", "Check for brute force attempts", "Review account lockout policies" }) },
        
        // Privilege Escalation
        { 4672, new SecurityEventRule(SecurityEventType.PrivilegeEscalation, "medium", 75, 
            "Special privileges assigned to new logon", new[] { "T1068", "T1078" }, new[] { "Investigate privilege assignment", "Verify administrative approval", "Monitor for abuse" }) },
        { 4688, new SecurityEventRule(SecurityEventType.ProcessCreation, "medium", 80, 
            "Process creation", new[] { "T1055", "T1059" }, new[] { "Review process parent-child relationships", "Check for suspicious command lines" }) },
        
        // Account Management
        { 4720, new SecurityEventRule(SecurityEventType.AccountManagement, "high", 90, 
            "Account created", new[] { "T1136" }, new[] { "Verify account creation approval", "Review new account permissions", "Monitor for unauthorized accounts" }) },
        { 4722, new SecurityEventRule(SecurityEventType.AccountManagement, "high", 90, 
            "Account enabled", new[] { "T1078" }, new[] { "Verify account enablement approval", "Review account status changes" }) },
        { 4724, new SecurityEventRule(SecurityEventType.AccountManagement, "high", 90, 
            "Account password reset", new[] { "T1098" }, new[] { "Verify password reset approval", "Check for unauthorized password changes" }) },
        { 4728, new SecurityEventRule(SecurityEventType.PrivilegeEscalation, "critical", 95, 
            "Member added to security-enabled global group", new[] { "T1068", "T1098" }, new[] { "Investigate group membership changes", "Verify administrative approval", "Review group permissions" }) },
        { 4732, new SecurityEventRule(SecurityEventType.PrivilegeEscalation, "critical", 95, 
            "Member added to security-enabled local group", new[] { "T1068", "T1098" }, new[] { "Investigate local group changes", "Verify administrative approval", "Monitor for privilege escalation" }) },
        
        // Service and Driver Installation
        { 7045, new SecurityEventRule(SecurityEventType.ServiceInstallation, "high", 85, 
            "Service installed", new[] { "T1543" }, new[] { "Verify service installation approval", "Review service permissions", "Check for persistence mechanisms" }) },
        { 4697, new SecurityEventRule(SecurityEventType.ServiceInstallation, "high", 85, 
            "Service installed", new[] { "T1543" }, new[] { "Verify service installation approval", "Review service configuration", "Monitor for unauthorized services" }) },
        
        // Scheduled Tasks
        { 4698, new SecurityEventRule(SecurityEventType.ScheduledTask, "medium", 75, 
            "Scheduled task created", new[] { "T1053" }, new[] { "Review scheduled task configuration", "Verify task approval", "Monitor for persistence" }) },
        { 4700, new SecurityEventRule(SecurityEventType.ScheduledTask, "medium", 75, 
            "Scheduled task enabled", new[] { "T1053" }, new[] { "Review enabled scheduled tasks", "Verify task approval" }) },
        
        // System Events
        { 6005, new SecurityEventRule(SecurityEventType.SystemStartup, "low", 60, 
            "Event log service was started", new[] { "T1078" }, new[] { "Verify system startup", "Review startup sequence" }) },
        { 6006, new SecurityEventRule(SecurityEventType.SystemShutdown, "low", 60, 
            "Event log service was stopped", new[] { "T1078" }, new[] { "Verify system shutdown", "Review shutdown sequence" }) },
        
        // Security Policy Changes
        { 4719, new SecurityEventRule(SecurityEventType.SecurityPolicyChange, "high", 85, 
            "System audit policy was changed", new[] { "T1562" }, new[] { "Investigate audit policy changes", "Verify administrative approval", "Review logging configuration" }) },
        { 4902, new SecurityEventRule(SecurityEventType.SecurityPolicyChange, "high", 85, 
            "Per-user audit policy table was created", new[] { "T1562" }, new[] { "Review audit policy changes", "Verify policy modifications" }) },
        { 4904, new SecurityEventRule(SecurityEventType.SecurityPolicyChange, "high", 85, 
            "Security event source registration attempt", new[] { "T1562" }, new[] { "Investigate event source registration", "Verify administrative approval" }) },
        { 4905, new SecurityEventRule(SecurityEventType.SecurityPolicyChange, "high", 85, 
            "Security event source unregistration attempt", new[] { "T1562" }, new[] { "Investigate event source unregistration", "Verify administrative approval" }) },
        { 4907, new SecurityEventRule(SecurityEventType.SecurityPolicyChange, "high", 85, 
            "Audit settings on object were changed", new[] { "T1562" }, new[] { "Investigate audit setting changes", "Verify administrative approval" }) },
        { 4908, new SecurityEventRule(SecurityEventType.SecurityPolicyChange, "high", 85, 
            "Special Groups Logon table modified", new[] { "T1562" }, new[] { "Investigate special groups changes", "Verify administrative approval" }) },
        
        // Log Clearing (Potential Anti-Forensics)
        { 1102, new SecurityEventRule(SecurityEventType.SecurityPolicyChange, "critical", 95, 
            "Audit log was cleared", new[] { "T1070", "T1562" }, new[] { "Investigate log clearing", "Verify administrative approval", "Check for anti-forensics activity" }) },
        
        // Network Events (if available)
        { 5156, new SecurityEventRule(SecurityEventType.NetworkConnection, "medium", 70, 
            "Filtering Platform connection", new[] { "T1071" }, new[] { "Review network connections", "Check for suspicious traffic patterns" }) },
        { 5157, new SecurityEventRule(SecurityEventType.NetworkConnection, "medium", 70, 
            "Filtering Platform connection blocked", new[] { "T1071" }, new[] { "Review blocked connections", "Check firewall rules" }) }
    };

    // Legacy PowerShell Operational Event Rules kept as fallback
    private static readonly Dictionary<int, SecurityEventRule> LegacyPowerShellEventRules = new()
    {
        // PowerShell Script Block Logging
        { 4104, new SecurityEventRule(SecurityEventType.PowerShellExecution, "medium", 80, 
            "PowerShell script block execution", new[] { "T1059.001" }, new[] { "Review PowerShell script content", "Check for malicious commands", "Analyze script block patterns" }) },
        
        // PowerShell Module Logging
        { 4103, new SecurityEventRule(SecurityEventType.PowerShellExecution, "low", 60, 
            "PowerShell module logging", new[] { "T1059.001" }, new[] { "Review loaded PowerShell modules", "Check for suspicious module usage" }) },
        
        // PowerShell Pipeline Execution
        { 4105, new SecurityEventRule(SecurityEventType.PowerShellExecution, "medium", 70, 
            "PowerShell pipeline execution started", new[] { "T1059.001" }, new[] { "Review PowerShell pipeline activity", "Monitor for unusual execution patterns" }) },
        { 4106, new SecurityEventRule(SecurityEventType.PowerShellExecution, "medium", 70, 
            "PowerShell pipeline execution stopped", new[] { "T1059.001" }, new[] { "Correlate with pipeline start events", "Review execution duration" }) },
        
        // PowerShell Provider Lifecycle
        { 4100, new SecurityEventRule(SecurityEventType.PowerShellExecution, "low", 50, 
            "PowerShell provider started", new[] { "T1059.001" }, new[] { "Monitor provider activity", "Review provider security" }) },
        { 4101, new SecurityEventRule(SecurityEventType.PowerShellExecution, "low", 50, 
            "PowerShell provider stopped", new[] { "T1059.001" }, new[] { "Correlate with provider start events" }) },
        
        // PowerShell Command Health
        { 4102, new SecurityEventRule(SecurityEventType.PowerShellExecution, "medium", 65, 
            "PowerShell command health violation", new[] { "T1059.001" }, new[] { "Investigate command health issues", "Review PowerShell security policies" }) },
        
        // PowerShell Engine State
        { 400, new SecurityEventRule(SecurityEventType.PowerShellExecution, "low", 55, 
            "PowerShell engine state changed", new[] { "T1059.001" }, new[] { "Monitor PowerShell engine lifecycle", "Review engine configuration" }) },
        { 403, new SecurityEventRule(SecurityEventType.PowerShellExecution, "low", 55, 
            "PowerShell engine stopped", new[] { "T1059.001" }, new[] { "Correlate with engine start events", "Review engine activity" }) }
    };

    /// <summary>
    /// Loads rules from database into cache
    /// </summary>
    private async Task<Dictionary<string, SecurityEventRule>> LoadRulesFromDatabaseAsync()
    {
        try
        {
            var rules = await ruleStore.GetAllEnabledRulesAsync();
            var ruleDict = new Dictionary<string, SecurityEventRule>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rules)
            {
                var key = $"{rule.EventId}:{rule.Channel}";
                var mitreTechniques = JsonSerializer.Deserialize<string[]>(rule.MitreTechniques) ?? Array.Empty<string>();
                var recommendedActions = JsonSerializer.Deserialize<string[]>(rule.RecommendedActions) ?? Array.Empty<string>();

                if (!Enum.TryParse<SecurityEventType>(rule.EventType, out var eventType))
                {
                    logger.LogWarning("Invalid event type {EventType} for rule {RuleId}", rule.EventType, rule.Id);
                    continue;
                }

                ruleDict[key] = new SecurityEventRule(
                    eventType,
                    rule.RiskLevel,
                    rule.Confidence,
                    rule.Summary,
                    mitreTechniques,
                    recommendedActions
                );
            }

            logger.LogInformation("Loaded {Count} security event rules from database", ruleDict.Count);
            return ruleDict;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading security event rules from database, using legacy rules");
            return new Dictionary<string, SecurityEventRule>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Gets the cached rules, refreshing if needed
    /// </summary>
    private async Task<Dictionary<string, SecurityEventRule>> GetRulesAsync()
    {
        if (_ruleCache == null || (DateTime.UtcNow - _lastCacheRefresh) > CacheRefreshInterval)
        {
            _ruleCache = await LoadRulesFromDatabaseAsync();
            _lastCacheRefresh = DateTime.UtcNow;
        }
        return _ruleCache;
    }

    /// <summary>
    /// Detects a security event from a log event using database-backed rules
    /// </summary>
    public SecurityEvent? DetectSecurityEvent(LogEvent logEvent)
    {
        // Use async over sync bridge for rule loading
        var rules = GetRulesAsync().GetAwaiter().GetResult();

        SecurityEventRule? rule = null;
        var key = $"{logEvent.EventId}:{logEvent.Channel}";

        // Try to get rule from database cache
        if (rules.TryGetValue(key, out rule))
        {
            logger.LogDebug("Found rule in database for Event {EventId} on {Channel}", logEvent.EventId, logEvent.Channel);
        }
        // Fallback to legacy hard-coded rules
        else
        {
            // Check for Security channel events
            if (string.Equals(logEvent.Channel, "Security", StringComparison.OrdinalIgnoreCase))
            {
                LegacySecurityEventRules.TryGetValue(logEvent.EventId, out rule);
                if (rule != null)
                {
                    logger.LogDebug("Using legacy rule for Security Event {EventId}", logEvent.EventId);
                }
            }
            // Check for PowerShell Operational channel events
            else if (string.Equals(logEvent.Channel, "Microsoft-Windows-PowerShell/Operational", StringComparison.OrdinalIgnoreCase))
            {
                LegacyPowerShellEventRules.TryGetValue(logEvent.EventId, out rule);
                if (rule != null)
                {
                    logger.LogDebug("Using legacy rule for PowerShell Event {EventId}", logEvent.EventId);
                }
            }
        }

        if (rule == null)
        {
            return null;
        }

        // Apply additional context-based rules
        var enhancedRule = ApplyContextRules(logEvent, rule);
        
        logger.LogDebug("Security event detected: {EventId} -> {EventType} ({RiskLevel})", 
            logEvent.EventId, enhancedRule.EventType, enhancedRule.RiskLevel);

        return SecurityEvent.CreateDeterministic(
            logEvent,
            enhancedRule.EventType,
            enhancedRule.RiskLevel,
            enhancedRule.Confidence,
            enhancedRule.Summary,
            enhancedRule.MitreTechniques,
            enhancedRule.RecommendedActions
        );
    }

    /// <summary>
    /// Detects a security event and enhances it with correlation analysis
    /// </summary>
    public async Task<SecurityEvent?> DetectAndCorrelateSecurityEventAsync(LogEvent logEvent)
    {
        var baseSecurityEvent = DetectSecurityEvent(logEvent);
        if (baseSecurityEvent == null)
        {
            return null;
        }

        try
        {
            // Analyze the event for correlations
            var correlationResult = await correlationEngine.AnalyzeEventAsync(baseSecurityEvent);

            if (correlationResult.HasCorrelation)
            {
                // Enhance the event with correlation information
                var correlationIds = new List<string> { correlationResult.Correlation!.Id };
                var correlationContext = GenerateCorrelationContext(correlationResult);

                // Apply correlation-based risk adjustment
                var adjustedEvent = ApplyCorrelationRiskAdjustment(baseSecurityEvent, correlationResult);

                // Create enhanced event with correlation context
                return SecurityEvent.CreateWithCorrelation(
                    adjustedEvent,
                    correlationIds,
                    correlationContext
                );
            }

            return baseSecurityEvent;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error analyzing event for correlations, returning base event: {EventId}", logEvent.EventId);
            return baseSecurityEvent;
        }
    }

    private string GenerateCorrelationContext(CorrelationResult correlationResult)
    {
        if (!correlationResult.HasCorrelation || correlationResult.Correlation == null)
            return string.Empty;

        var correlation = correlationResult.Correlation;
        var contextParts = new List<string>();

        // Add correlation type and confidence
        contextParts.Add($"Part of {correlation.CorrelationType.ToLower()} pattern");
        contextParts.Add($"with {correlationResult.ConfidenceScore:P0} confidence");

        // Add event count
        if (correlation.EventIds.Count > 1)
        {
            contextParts.Add($"involving {correlation.EventIds.Count} related events");
        }

        // Add time window context
        var timeWindow = correlation.TimeWindow.TotalMinutes;
        if (timeWindow < 60)
        {
            contextParts.Add($"within {timeWindow:F0} minutes");
        }
        else
        {
            contextParts.Add($"within {timeWindow / 60:F1} hours");
        }

        // Add attack chain context if available
        if (!string.IsNullOrEmpty(correlation.AttackChainStage))
        {
            contextParts.Add($"as part of {correlation.AttackChainStage}");
        }

        // Add MITRE techniques if any
        if (correlation.MitreTechniques?.Any() == true)
        {
            contextParts.Add($"matching techniques: {string.Join(", ", correlation.MitreTechniques.Take(3))}");
        }

        return string.Join(", ", contextParts) + ".";
    }

    private SecurityEvent ApplyCorrelationRiskAdjustment(SecurityEvent baseEvent, CorrelationResult correlationResult)
    {
        if (!correlationResult.HasCorrelation || correlationResult.Correlation == null)
            return baseEvent;

        var correlation = correlationResult.Correlation;
        var adjustedRiskLevel = baseEvent.RiskLevel;
        var adjustedConfidence = baseEvent.Confidence;
        var additionalActions = new List<string>(baseEvent.RecommendedActions);

        // Risk level adjustments based on correlation type
        switch (correlation.CorrelationType.ToLower())
        {
            case "attackchain":
                adjustedRiskLevel = UpgradeRiskLevel(baseEvent.RiskLevel, 2); // Significant upgrade
                adjustedConfidence = Math.Min(100, adjustedConfidence + 15);
                additionalActions.Add("Investigate entire attack sequence");
                additionalActions.Add("Consider incident response procedures");
                break;

            case "lateralmovement":
                adjustedRiskLevel = UpgradeRiskLevel(baseEvent.RiskLevel, 1); // Moderate upgrade
                adjustedConfidence = Math.Min(100, adjustedConfidence + 10);
                additionalActions.Add("Investigate lateral movement across systems");
                additionalActions.Add("Check for compromised credentials");
                break;

            case "temporalburst":
                adjustedConfidence = Math.Min(100, adjustedConfidence + 5);
                additionalActions.Add("Investigate burst pattern for automation");
                break;

            case "privilegeescalation":
                adjustedRiskLevel = UpgradeRiskLevel(baseEvent.RiskLevel, 1);
                adjustedConfidence = Math.Min(100, adjustedConfidence + 10);
                additionalActions.Add("Review privilege escalation sequence");
                break;

            case "mldetected":
                adjustedConfidence = Math.Min(100, adjustedConfidence + 5);
                additionalActions.Add("Review ML-detected anomaly pattern");
                additionalActions.Add("Consider updating correlation rules");
                break;
        }

        // Additional confidence boost for high-confidence correlations
        if (correlationResult.ConfidenceScore > 0.8)
        {
            adjustedConfidence = Math.Min(100, adjustedConfidence + 5);
        }

        return SecurityEvent.CreateEnhanced(
            baseEvent.OriginalEvent,
            baseEvent.EventType,
            adjustedRiskLevel,
            adjustedConfidence,
            baseEvent.Summary,
            baseEvent.MitreTechniques,
            additionalActions.ToArray(),
            baseEvent.IsDeterministic,
            correlationResult.ConfidenceScore,
            0.0, // BurstScore will be set by correlation engine
            0.0  // AnomalyScore will be set by correlation engine
        );
    }

    private string UpgradeRiskLevel(string currentLevel, int upgradeSteps)
    {
        var riskLevels = new[] { "low", "medium", "high", "critical" };
        var currentIndex = Array.IndexOf(riskLevels, currentLevel.ToLower());

        if (currentIndex == -1) return currentLevel; // Unknown level, don't change

        var newIndex = Math.Min(riskLevels.Length - 1, currentIndex + upgradeSteps);
        return riskLevels[newIndex];
    }

    private SecurityEventRule ApplyContextRules(LogEvent logEvent, SecurityEventRule baseRule)
    {
        var enhancedRule = baseRule with { };

        // Enhance based on event context
        switch (logEvent.EventId)
        {
            case 4624: // Logon
                if (IsAdministrativeLogon(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "high",
                        Confidence = Math.Min(95, enhancedRule.Confidence + 10),
                        Summary = "Administrative logon detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1068" }).ToArray()
                    };
                }
                else if (IsOffHoursLogon(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "medium",
                        Confidence = Math.Min(90, enhancedRule.Confidence + 5),
                        Summary = "Off-hours logon detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1078" }).ToArray()
                    };
                }
                break;

            case 4625: // Failed logon
                if (IsBruteForceAttempt(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "critical",
                        Confidence = 95,
                        Summary = "Potential brute force attack detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1110.001" }).ToArray(),
                        RecommendedActions = enhancedRule.RecommendedActions.Concat(new[] { "Block source IP", "Enable account lockout", "Investigate source location" }).ToArray()
                    };
                }
                break;

            case 4672: // Special privileges
                if (IsHighPrivilegeAssignment(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "critical",
                        Confidence = 95,
                        Summary = "High-privilege account assignment detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1068" }).ToArray()
                    };
                }
                else if (IsNormalPrivilegeAssignment(logEvent))
                {
                    // Downgrade normal privilege assignments to low risk
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "low",
                        Confidence = 60,
                        Summary = "Normal privilege assignment detected",
                        MitreTechniques = new[] { "T1078" },
                        RecommendedActions = new[] { "Monitor for unusual patterns" }
                    };
                }
                break;

            // PowerShell-specific context rules
            case 4104: // PowerShell script block execution
                if (IsSuspiciousPowerShellScript(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "high",
                        Confidence = Math.Min(95, enhancedRule.Confidence + 15),
                        Summary = "Suspicious PowerShell script block detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1140", "T1027" }).ToArray(),
                        RecommendedActions = enhancedRule.RecommendedActions.Concat(new[] { "Block script execution", "Investigate script origin", "Review PowerShell security policies" }).ToArray()
                    };
                }
                else if (IsEncodedPowerShellCommand(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "high",
                        Confidence = Math.Min(90, enhancedRule.Confidence + 10),
                        Summary = "Encoded PowerShell command detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1027", "T1140" }).ToArray()
                    };
                }
                else if (IsDownloadPowerShellCommand(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "medium",
                        Confidence = Math.Min(85, enhancedRule.Confidence + 10),
                        Summary = "PowerShell download command detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1105" }).ToArray(),
                        RecommendedActions = enhancedRule.RecommendedActions.Concat(new[] { "Review downloaded content", "Check download source" }).ToArray()
                    };
                }
                break;

            case 4103: // PowerShell module logging
                if (IsSuspiciousPowerShellModule(logEvent))
                {
                    enhancedRule = enhancedRule with 
                    { 
                        RiskLevel = "medium",
                        Confidence = Math.Min(80, enhancedRule.Confidence + 10),
                        Summary = "Suspicious PowerShell module usage detected",
                        MitreTechniques = enhancedRule.MitreTechniques.Concat(new[] { "T1562" }).ToArray()
                    };
                }
                break;
        }

        return enhancedRule;
    }

    private bool IsAdministrativeLogon(LogEvent logEvent)
    {
        // Check if the logon involves administrative privileges
        return logEvent.Message.Contains("Administrator", StringComparison.OrdinalIgnoreCase) ||
               logEvent.Message.Contains("S-1-5-32-544", StringComparison.OrdinalIgnoreCase) || // Administrators SID
               logEvent.User.Contains("Administrator", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOffHoursLogon(LogEvent logEvent)
    {
        // Check if logon is outside business hours (6 AM - 6 PM)
        var hour = logEvent.Time.Hour;
        return hour < 6 || hour > 18;
    }

    private bool IsBruteForceAttempt(LogEvent logEvent)
    {
        // This would need to be enhanced with rate limiting logic
        // For now, we'll use a simple heuristic based on the message
        return logEvent.Message.Contains("Unknown user name", StringComparison.OrdinalIgnoreCase) ||
               logEvent.Message.Contains("bad password", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsHighPrivilegeAssignment(LogEvent logEvent)
    {
        // Check for high-privilege SIDs
        var highPrivilegeSids = new[]
        {
            "S-1-5-32-544", // Administrators
            "S-1-5-32-548", // Account Operators
            "S-1-5-32-549", // Server Operators
            "S-1-5-32-550", // Print Operators
            "S-1-5-21-domain-500" // Domain Administrator
        };

        return highPrivilegeSids.Any(sid => logEvent.Message.Contains(sid, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsNormalPrivilegeAssignment(LogEvent logEvent)
    {
        // Check for normal privilege assignments that occur frequently
        var normalPrivileges = new[]
        {
            "SeSecurityPrivilege",
            "SeBackupPrivilege", 
            "SeRestorePrivilege",
            "SeSystemtimePrivilege",
            "SeShutdownPrivilege",
            "SeRemoteShutdownPrivilege",
            "SeTakeOwnershipPrivilege",
            "SeDebugPrivilege",
            "SeSystemEnvironmentPrivilege",
            "SeSystemProfilePrivilege",
            "SeProfileSingleProcessPrivilege",
            "SeIncreaseBasePriorityPrivilege",
            "SeLoadDriverPrivilege",
            "SeCreatePagefilePrivilege",
            "SeIncreaseQuotaPrivilege",
            "SeChangeNotifyPrivilege",
            "SeUndockPrivilege",
            "SeManageVolumePrivilege",
            "SeImpersonatePrivilege",
            "SeCreateGlobalPrivilege",
            "SeTrustedCredManAccessPrivilege",
            "SeRelabelPrivilege",
            "SeIncreaseWorkingSetPrivilege",
            "SeTimeZonePrivilege",
            "SeCreateSymbolicLinkPrivilege"
        };

        return normalPrivileges.Any(privilege => logEvent.Message.Contains(privilege, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSuspiciousPowerShellScript(LogEvent logEvent)
    {
        var suspiciousPatterns = new[]
        {
            "Invoke-Expression",
            "IEX",
            "DownloadString",
            "DownloadFile",
            "WebClient",
            "Net.WebClient",
            "System.Net.WebClient",
            "Invoke-WebRequest",
            "iwr",
            "curl",
            "wget",
            "bitsadmin",
            "certutil",
            "powershell.exe -enc",
            "powershell -enc",
            "FromBase64String",
            "ToBase64String",
            "-EncodedCommand",
            "-enc",
            "Hidden",
            "-WindowStyle Hidden",
            "Bypass",
            "-ExecutionPolicy Bypass",
            "Unrestricted",
            "AllSigned",
            "RemoteSigned",
            "Start-Process",
            "Invoke-Command",
            "icm",
            "New-Object",
            "Add-Type",
            "Reflection.Assembly",
            "[System.Reflection.Assembly]",
            "LoadWithPartialName",
            "-join",
            "Split",
            "Replace",
            "[char]",
            "[Convert]",
            "WScript.Shell",
            "cmd.exe /c",
            "cmd /c",
            "rundll32",
            "regsvr32",
            "mshta",
            "cscript",
            "wscript"
        };

        return suspiciousPatterns.Any(pattern => logEvent.Message.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsEncodedPowerShellCommand(LogEvent logEvent)
    {
        var encodingPatterns = new[]
        {
            "-EncodedCommand",
            "-enc",
            "FromBase64String",
            "ToBase64String",
            "[Convert]::FromBase64String",
            "[System.Convert]::FromBase64String",
            "[System.Text.Encoding]",
            "UTF8.GetString",
            "ASCII.GetString"
        };

        return encodingPatterns.Any(pattern => logEvent.Message.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsDownloadPowerShellCommand(LogEvent logEvent)
    {
        var downloadPatterns = new[]
        {
            "DownloadString",
            "DownloadFile",
            "DownloadData",
            "WebClient",
            "Net.WebClient",
            "System.Net.WebClient",
            "Invoke-WebRequest",
            "Invoke-RestMethod",
            "iwr",
            "irm",
            "wget",
            "curl",
            "bitsadmin",
            "certutil -urlcache",
            "certutil.exe -urlcache"
        };

        return downloadPatterns.Any(pattern => logEvent.Message.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSuspiciousPowerShellModule(LogEvent logEvent)
    {
        var suspiciousModules = new[]
        {
            "PowerSploit",
            "Empire",
            "Nishang",
            "PowerShellEmpire",
            "Invoke-Mimikatz",
            "Invoke-ReflectivePEInjection",
            "Invoke-DllInjection",
            "Invoke-Shellcode",
            "PowerUp",
            "PowerView",
            "BloodHound",
            "SharpHound",
            "Cobalt",
            "Metasploit",
            "Meterpreter",
            "PSReflect",
            "Get-GPPPassword",
            "Invoke-TokenManipulation",
            "Invoke-CredentialInjection",
            "Out-MiniDump"
        };

        return suspiciousModules.Any(module => logEvent.Message.Contains(module, StringComparison.OrdinalIgnoreCase));
    }
}

public record SecurityEventRule(
    SecurityEventType EventType,
    string RiskLevel,
    int Confidence,
    string Summary,
    string[] MitreTechniques,
    string[] RecommendedActions
);

