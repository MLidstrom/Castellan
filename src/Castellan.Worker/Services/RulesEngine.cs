using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Models;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Services;

public class RulesEngine
{
    private readonly ILogger<RulesEngine> logger;
    private readonly CorrelationOptions _correlationOptions;
    private readonly ConcurrentDictionary<string, List<LogEvent>> eventHistory;
    private readonly ConcurrentDictionary<string, int> eventCounters;
    private readonly TimeSpan correlationWindow = TimeSpan.FromMinutes(5);
    private readonly TimeSpan cleanupInterval = TimeSpan.FromMinutes(10);
    private DateTime lastCleanup = DateTime.UtcNow;

    public RulesEngine(ILogger<RulesEngine> logger, IOptions<CorrelationOptions> correlationOptions)
    {
        this.logger = logger;
        this._correlationOptions = correlationOptions.Value;
        this.eventHistory = new ConcurrentDictionary<string, List<LogEvent>>();
        this.eventCounters = new ConcurrentDictionary<string, int>();
    }

    public SecurityEvent? AnalyzeWithCorrelation(LogEvent? logEvent, SecurityEvent? deterministicEvent, SecurityEvent? llmEvent)
    {
        logger.LogDebug("AnalyzeWithCorrelation called with: logEvent={LogEvent}, deterministicEvent={DeterministicEvent}, llmEvent={LlmEvent}", 
            logEvent?.EventId.ToString() ?? "null", deterministicEvent?.RiskLevel ?? "null", llmEvent?.RiskLevel ?? "null");

        // Handle null logEvent
        if (logEvent == null)
        {
            logger.LogWarning("LogEvent is null, returning null");
            return null;
        }

        // Clean up old events periodically
        CleanupOldEvents();

        // Update event history for correlation
        UpdateEventHistory(logEvent);

        // Perform correlation analysis
        var correlationScore = CalculateCorrelationScore(logEvent);
        var burstScore = CalculateBurstScore(logEvent);
        var anomalyScore = CalculateAnomalyScore(logEvent);

        logger.LogDebug("Correlation scores calculated: corr={CorrelationScore}, burst={BurstScore}, anomaly={AnomalyScore}", 
            correlationScore, burstScore, anomalyScore);

        // Fuse scores from different sources
        var fusedEvent = FuseSecurityEvents(logEvent, deterministicEvent, llmEvent, correlationScore, burstScore, anomalyScore);

        logger.LogDebug("FuseSecurityEvents returned: {FusedEvent}", fusedEvent?.RiskLevel ?? "null");

        return fusedEvent;
    }

    private void UpdateEventHistory(LogEvent logEvent)
    {
        var key = $"{logEvent.Channel}_{logEvent.EventId}_{logEvent.User}";
        var now = DateTime.UtcNow;

        eventHistory.AddOrUpdate(key, 
            new List<LogEvent> { logEvent },
            (k, v) =>
            {
                v.Add(logEvent);
                // Remove events older than correlation window
                v.RemoveAll(e => now - e.Time > correlationWindow);
                return v;
            });

        // Update event counters
        eventCounters.AddOrUpdate(key, 1, (k, v) => v + 1);
    }

    private void CleanupOldEvents()
    {
        var now = DateTime.UtcNow;
        if (now - lastCleanup < cleanupInterval)
            return;

        lastCleanup = now;
        var cutoffTime = now - correlationWindow;

        foreach (var kvp in eventHistory)
        {
            kvp.Value.RemoveAll(e => e.Time < cutoffTime);
            if (kvp.Value.Count == 0)
            {
                eventHistory.TryRemove(kvp.Key, out _);
                eventCounters.TryRemove(kvp.Key, out _);
            }
        }

        logger.LogDebug("Cleaned up {Count} empty event histories", eventHistory.Count);
    }

    private double CalculateCorrelationScore(LogEvent logEvent)
    {
        var key = $"{logEvent.Channel}_{logEvent.EventId}_{logEvent.User}";
        
        if (!eventHistory.TryGetValue(key, out var history) || history.Count < 2)
            return 0.0;

        // Calculate correlation based on event frequency and patterns
        var recentEvents = history.Where(e => e.Time >= DateTime.UtcNow - TimeSpan.FromMinutes(2)).Count();
        var totalEvents = history.Count;

        // Higher correlation score for events that occur frequently in short time
        var frequencyScore = Math.Min(1.0, recentEvents / 10.0);
        var patternScore = totalEvents > 5 ? 0.3 : 0.0;

        return Math.Min(1.0, frequencyScore + patternScore);
    }

    private double CalculateBurstScore(LogEvent logEvent)
    {
        var key = $"{logEvent.Channel}_{logEvent.EventId}_{logEvent.User}";
        
        if (!eventCounters.TryGetValue(key, out var count))
            return 0.0;

        // Calculate burst score based on event frequency
        var timeWindow = TimeSpan.FromMinutes(1);
        var recentEvents = eventHistory.GetValueOrDefault(key, new List<LogEvent>())
            .Count(e => e.Time >= DateTime.UtcNow - timeWindow);

        // Higher burst score for rapid event sequences
        if (recentEvents >= 10) return 1.0; // Critical burst
        if (recentEvents >= 5) return 0.8;  // High burst
        if (recentEvents >= 3) return 0.5;  // Medium burst
        if (recentEvents >= 2) return 0.2;  // Low burst

        return 0.0;
    }

    private double CalculateAnomalyScore(LogEvent logEvent)
    {
        // Calculate anomaly score based on unusual patterns
        var anomalyScore = 0.0;

        // Off-hours activity
        if (IsOffHoursActivity(logEvent))
            anomalyScore += 0.3;

        // Unusual user activity
        if (IsUnusualUserActivity(logEvent))
            anomalyScore += 0.2;

        // Unusual event patterns
        if (IsUnusualEventPattern(logEvent))
            anomalyScore += 0.2;

        return Math.Min(1.0, anomalyScore);
    }

    private bool IsOffHoursActivity(LogEvent logEvent)
    {
        var hour = logEvent.Time.Hour;
        // Consider off-hours as 10 PM to 6 AM
        return hour >= 22 || hour <= 6;
    }

    private bool IsUnusualUserActivity(LogEvent logEvent)
    {
        // Check for unusual user patterns (e.g., service accounts during business hours)
        if (string.IsNullOrEmpty(logEvent.User))
            return false;

        var isServiceAccount = logEvent.User.Contains("$") || 
                              logEvent.User.Contains("SERVICE") || 
                              logEvent.User.Contains("SYSTEM");

        var isBusinessHours = logEvent.Time.Hour >= 8 && logEvent.Time.Hour <= 18;
        var isWeekday = logEvent.Time.DayOfWeek >= DayOfWeek.Monday && logEvent.Time.DayOfWeek <= DayOfWeek.Friday;

        // Service account activity during business hours might be unusual
        return isServiceAccount && isBusinessHours && isWeekday;
    }

    private bool IsUnusualEventPattern(LogEvent logEvent)
    {
        // Check for unusual event patterns
        var key = $"{logEvent.Channel}_{logEvent.EventId}_{logEvent.User}";
        
        if (!eventHistory.TryGetValue(key, out var history))
            return false;

        // Unusual if this event type hasn't been seen before for this user
        return history.Count == 1;
    }

    private SecurityEvent? FuseSecurityEvents(LogEvent logEvent, SecurityEvent? deterministicEvent, SecurityEvent? llmEvent, 
        double correlationScore, double burstScore, double anomalyScore)
    {
        logger.LogDebug("FuseSecurityEvents called with: deterministicEvent={DeterministicEvent}, llmEvent={LlmEvent}, corr={CorrelationScore}, burst={BurstScore}, anomaly={AnomalyScore}", 
            deterministicEvent?.RiskLevel ?? "null", llmEvent?.RiskLevel ?? "null", correlationScore, burstScore, anomalyScore);

        // Check if correlation scores meet minimum thresholds
        var totalScore = correlationScore + burstScore + anomalyScore;
        var meetsIndividualThresholds = correlationScore >= _correlationOptions.MinCorrelationScore || 
                                       burstScore >= _correlationOptions.MinBurstScore || 
                                       anomalyScore >= _correlationOptions.MinAnomalyScore;
        var meetsTotalThreshold = totalScore >= _correlationOptions.MinTotalScore;

        // If we have a deterministic event, enhance it only if correlation scores meet thresholds
        if (deterministicEvent != null)
        {
            if (_correlationOptions.EnableLowScoreEvents || (meetsIndividualThresholds && meetsTotalThreshold))
            {
                var enhancedEvent = EnhanceWithCorrelation(deterministicEvent, correlationScore, burstScore, anomalyScore);
                logger.LogDebug("Enhanced deterministic event with correlation scores: corr={CorrelationScore}, burst={BurstScore}, anomaly={AnomalyScore}", 
                    correlationScore, burstScore, anomalyScore);
                return enhancedEvent;
            }
            else
            {
                logger.LogDebug("Correlation scores below thresholds for deterministic event enhancement: corr={CorrelationScore:F2}, burst={BurstScore:F2}, anomaly={AnomalyScore:F2}, total={TotalScore:F2}", 
                    correlationScore, burstScore, anomalyScore, totalScore);
                // Return the original deterministic event without enhancement
                return deterministicEvent;
            }
        }

        // If we have an LLM event, enhance it only if correlation scores meet thresholds
        if (llmEvent != null)
        {
            if (_correlationOptions.EnableLowScoreEvents || (meetsIndividualThresholds && meetsTotalThreshold))
            {
                var enhancedEvent = EnhanceWithCorrelation(llmEvent, correlationScore, burstScore, anomalyScore);
                logger.LogDebug("Enhanced LLM event with correlation scores: corr={CorrelationScore}, burst={BurstScore}, anomaly={AnomalyScore}", 
                    correlationScore, burstScore, anomalyScore);
                return enhancedEvent;
            }
            else
            {
                logger.LogDebug("Correlation scores below thresholds for LLM event enhancement: corr={CorrelationScore:F2}, burst={BurstScore:F2}, anomaly={AnomalyScore:F2}, total={TotalScore:F2}", 
                    correlationScore, burstScore, anomalyScore, totalScore);
                // Return the original LLM event without enhancement
                return llmEvent;
            }
        }

        // If no security event detected, but we have correlation indicators, create a new event
        if (_correlationOptions.EnableLowScoreEvents && meetsIndividualThresholds && meetsTotalThreshold)
        {
            var riskLevel = DetermineRiskLevel(correlationScore, burstScore, anomalyScore);
            var confidence = CalculateFusedConfidence(correlationScore, burstScore, anomalyScore);
            var eventType = DetermineEventType(logEvent, correlationScore, burstScore, anomalyScore);
            var summary = GenerateCorrelationSummary(logEvent, correlationScore, burstScore, anomalyScore);
            var mitreTechniques = DetermineMitreTechniques(logEvent, correlationScore, burstScore, anomalyScore);
            var actions = GenerateRecommendedActions(logEvent, correlationScore, burstScore, anomalyScore);

            logger.LogInformation("Created correlation-based security event: {EventType} ({RiskLevel}) with confidence {Confidence}% (scores: corr={CorrelationScore:F2}, burst={BurstScore:F2}, anomaly={AnomalyScore:F2}, total={TotalScore:F2})", 
                eventType, riskLevel, confidence, correlationScore, burstScore, anomalyScore, totalScore);

            return SecurityEvent.CreateCorrelationBased(
                logEvent,
                eventType,
                riskLevel,
                confidence,
                summary,
                mitreTechniques,
                actions,
                correlationScore,
                burstScore,
                anomalyScore
            );
        }
        else
        {
            logger.LogDebug("Correlation scores below total threshold: corr={CorrelationScore:F2}, burst={BurstScore:F2}, anomaly={AnomalyScore:F2}, total={TotalScore:F2} (min: {MinTotalScore})", 
                correlationScore, burstScore, anomalyScore, totalScore, _correlationOptions.MinTotalScore);
        }

        // No security event detected and no correlation indicators
        logger.LogDebug("No security event created - no correlation indicators detected");
        return null;
    }

    private SecurityEvent EnhanceWithCorrelation(SecurityEvent baseEvent, double correlationScore, double burstScore, double anomalyScore)
    {
        var enhancedRiskLevel = EnhanceRiskLevel(baseEvent.RiskLevel, correlationScore, burstScore, anomalyScore);
        var enhancedConfidence = Math.Min(100, (int)(baseEvent.Confidence + (correlationScore + burstScore + anomalyScore) * 10));
        var enhancedSummary = EnhanceSummary(baseEvent.Summary, correlationScore, burstScore, anomalyScore);
        var enhancedMitre = EnhanceMitreTechniques(baseEvent.MitreTechniques, correlationScore, burstScore, anomalyScore);
        var enhancedActions = EnhanceActions(baseEvent.RecommendedActions, correlationScore, burstScore, anomalyScore);

        return SecurityEvent.CreateEnhanced(
            baseEvent.OriginalEvent,
            baseEvent.EventType,
            enhancedRiskLevel,
            enhancedConfidence,
            enhancedSummary,
            enhancedMitre,
            enhancedActions,
            baseEvent.IsDeterministic,
            correlationScore,
            burstScore,
            anomalyScore
        );
    }

    private string EnhanceRiskLevel(string baseRiskLevel, double correlationScore, double burstScore, double anomalyScore)
    {
        var totalScore = correlationScore + burstScore + anomalyScore;
        
        if (totalScore > 2.0) return "critical";
        if (totalScore > 1.5) return "high";
        if (totalScore > 1.0) return "medium";
        
        return baseRiskLevel;
    }

    private string EnhanceSummary(string baseSummary, double correlationScore, double burstScore, double anomalyScore)
    {
        var enhancements = new List<string>();
        
        if (correlationScore > 0.5)
            enhancements.Add("correlated activity detected");
        if (burstScore > 0.5)
            enhancements.Add("burst pattern identified");
        if (anomalyScore > 0.5)
            enhancements.Add("anomalous behavior observed");

        if (enhancements.Count == 0)
            return baseSummary;

        return $"{baseSummary} - {string.Join(", ", enhancements)}";
    }

    private string[] EnhanceMitreTechniques(string[] baseMitre, double correlationScore, double burstScore, double anomalyScore)
    {
        var enhanced = new List<string>(baseMitre);
        
        if (burstScore > 0.7)
            enhanced.Add("T1110"); // Brute Force
        if (correlationScore > 0.7)
            enhanced.Add("T1078"); // Valid Accounts
        if (anomalyScore > 0.7)
            enhanced.Add("T1078"); // Valid Accounts

        return enhanced.Distinct().ToArray();
    }

    private string[] EnhanceActions(string[] baseActions, double correlationScore, double burstScore, double anomalyScore)
    {
        var enhanced = new List<string>(baseActions);
        
        if (burstScore > 0.5)
            enhanced.Add("Implement rate limiting");
        if (correlationScore > 0.5)
            enhanced.Add("Investigate related events");
        if (anomalyScore > 0.5)
            enhanced.Add("Review user activity patterns");

        return enhanced.Distinct().ToArray();
    }

    private string DetermineRiskLevel(double correlationScore, double burstScore, double anomalyScore)
    {
        var totalScore = correlationScore + burstScore + anomalyScore;
        
        if (totalScore > 2.0) return "critical";
        if (totalScore > 1.5) return "high";
        if (totalScore > 1.0) return "medium";
        return "low";
    }

    private int CalculateFusedConfidence(double correlationScore, double burstScore, double anomalyScore)
    {
        var baseConfidence = 50; // Base confidence for correlation-based detection
        var scoreBonus = (correlationScore + burstScore + anomalyScore) * 20;
        return Math.Min(95, (int)(baseConfidence + scoreBonus));
    }

    private SecurityEventType DetermineEventType(LogEvent logEvent, double correlationScore, double burstScore, double anomalyScore)
    {
        if (burstScore > 0.7)
            return SecurityEventType.BurstActivity;
        if (correlationScore > 0.7)
            return SecurityEventType.CorrelatedActivity;
        if (anomalyScore > 0.7)
            return SecurityEventType.AnomalousActivity;
        
        return SecurityEventType.SuspiciousActivity;
    }

    private string GenerateCorrelationSummary(LogEvent logEvent, double correlationScore, double burstScore, double anomalyScore)
    {
        var patterns = new List<string>();
        
        if (burstScore > 0.5)
            patterns.Add("burst pattern");
        if (correlationScore > 0.5)
            patterns.Add("correlated events");
        if (anomalyScore > 0.5)
            patterns.Add("anomalous behavior");

        return $"Suspicious {logEvent.Channel} activity detected with {string.Join(", ", patterns)}";
    }

    private string[] DetermineMitreTechniques(LogEvent logEvent, double correlationScore, double burstScore, double anomalyScore)
    {
        var techniques = new List<string>();
        
        if (burstScore > 0.7)
            techniques.Add("T1110"); // Brute Force
        if (correlationScore > 0.7)
            techniques.Add("T1078"); // Valid Accounts
        if (anomalyScore > 0.7)
            techniques.Add("T1078"); // Valid Accounts

        return techniques.ToArray();
    }

    private string[] GenerateRecommendedActions(LogEvent logEvent, double correlationScore, double burstScore, double anomalyScore)
    {
        var actions = new List<string>();
        
        if (burstScore > 0.5)
            actions.Add("Implement rate limiting for user");
        if (correlationScore > 0.5)
            actions.Add("Investigate related events");
        if (anomalyScore > 0.5)
            actions.Add("Review user activity patterns");
        
        actions.Add("Monitor for additional suspicious activity");

        return actions.ToArray();
    }
}

