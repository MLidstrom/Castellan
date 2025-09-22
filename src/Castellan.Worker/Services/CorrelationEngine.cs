using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using System.Diagnostics;

namespace Castellan.Worker.Services;

/// <summary>
/// Advanced correlation engine for detecting patterns and relationships between security events
/// </summary>
public class CorrelationEngine : ICorrelationEngine
{
    private readonly ILogger<CorrelationEngine> _logger;
    private readonly ISecurityEventStore _eventStore;
    private readonly ConcurrentDictionary<string, EventCorrelation> _correlations = new();
    private readonly ConcurrentDictionary<string, List<SecurityEvent>> _eventCache = new();
    private readonly List<CorrelationRule> _rules;
    private readonly MLContext _mlContext;
    private ITransformer? _mlModel;
    private readonly object _modelLock = new();

    public CorrelationEngine(
        ILogger<CorrelationEngine> logger,
        ISecurityEventStore eventStore)
    {
        _logger = logger;
        _eventStore = eventStore;
        _mlContext = new MLContext(seed: 42);
        _rules = InitializeCorrelationRules();
    }

    public async Task<CorrelationResult> AnalyzeEventAsync(SecurityEvent securityEvent)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            // Get recent events for correlation
            var recentEvents = await GetRecentEventsAsync(TimeSpan.FromMinutes(30));

            // Check each correlation rule
            var matchedRules = new List<string>();
            EventCorrelation? bestCorrelation = null;
            double highestConfidence = 0;

            foreach (var rule in _rules.Where(r => r.IsEnabled))
            {
                var result = await CheckCorrelationRuleAsync(rule, securityEvent, recentEvents);
                if (result != null && result.ConfidenceScore > highestConfidence)
                {
                    highestConfidence = result.ConfidenceScore;
                    bestCorrelation = result;
                    matchedRules.Add(rule.Name);
                }
            }

            // Check ML-based correlations if model is trained
            if (_mlModel != null)
            {
                var mlCorrelation = await DetectMLCorrelationAsync(securityEvent, recentEvents);
                if (mlCorrelation != null && mlCorrelation.ConfidenceScore > highestConfidence)
                {
                    highestConfidence = mlCorrelation.ConfidenceScore;
                    bestCorrelation = mlCorrelation;
                    matchedRules.Add("ML Pattern Detection");
                }
            }

            if (bestCorrelation != null)
            {
                // Store the correlation
                _correlations[bestCorrelation.Id] = bestCorrelation;

                _logger.LogInformation("Correlation detected: Type={Type}, Confidence={Confidence:P}, Events={Count}, Time={Ms}ms",
                    bestCorrelation.CorrelationType, bestCorrelation.ConfidenceScore,
                    bestCorrelation.EventIds.Count, sw.ElapsedMilliseconds);

                return new CorrelationResult
                {
                    HasCorrelation = true,
                    Correlation = bestCorrelation,
                    ConfidenceScore = bestCorrelation.ConfidenceScore,
                    Explanation = GenerateExplanation(bestCorrelation),
                    MatchedRules = matchedRules
                };
            }

            return new CorrelationResult
            {
                HasCorrelation = false,
                ConfidenceScore = 0,
                Explanation = "No correlation patterns detected"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing event for correlations");
            throw;
        }
    }

    public async Task<List<EventCorrelation>> AnalyzeBatchAsync(List<SecurityEvent> events, TimeSpan timeWindow)
    {
        var correlations = new List<EventCorrelation>();

        // Group events by time windows
        var startTime = events.Min(e => e.OriginalEvent.Time);
        var endTime = events.Max(e => e.OriginalEvent.Time);

        // Detect temporal bursts
        var bursts = DetectTemporalBursts(events, timeWindow);
        correlations.AddRange(bursts);

        // Detect attack chains
        var chains = await DetectAttackChainsAsync(events, timeWindow);
        correlations.AddRange(chains.Select(c => ConvertChainToCorrelation(c)));

        // Detect lateral movement
        var lateral = DetectLateralMovement(events);
        correlations.AddRange(lateral);

        // Store all correlations
        foreach (var correlation in correlations)
        {
            _correlations[correlation.Id] = correlation;
        }

        return correlations;
    }

    public async Task<List<EventCorrelation>> GetCorrelationsAsync(DateTime startTime, DateTime endTime)
    {
        return _correlations.Values
            .Where(c => c.DetectedAt >= startTime && c.DetectedAt <= endTime)
            .OrderByDescending(c => c.DetectedAt)
            .ToList();
    }

    public async Task<EventCorrelation?> GetCorrelationAsync(string correlationId)
    {
        _correlations.TryGetValue(correlationId, out var correlation);
        return correlation;
    }

    public async Task<List<EventCorrelation>> GetEventCorrelationsAsync(string eventId)
    {
        return _correlations.Values
            .Where(c => c.EventIds.Contains(eventId))
            .ToList();
    }

    public async Task<CorrelationStatistics> GetStatisticsAsync(DateTime? startTime = null, DateTime? endTime = null)
    {
        var correlations = _correlations.Values.AsEnumerable();

        if (startTime.HasValue)
            correlations = correlations.Where(c => c.DetectedAt >= startTime.Value);
        if (endTime.HasValue)
            correlations = correlations.Where(c => c.DetectedAt <= endTime.Value);

        var correlationList = correlations.ToList();

        var stats = new CorrelationStatistics
        {
            TotalEventsProcessed = _eventCache.Values.Sum(v => v.Count),
            CorrelationsDetected = correlationList.Count,
            AverageConfidenceScore = correlationList.Any() ? correlationList.Average(c => c.ConfidenceScore) : 0,
            LastUpdated = DateTime.UtcNow
        };

        // Group by type
        stats.CorrelationsByType = correlationList
            .GroupBy(c => c.CorrelationType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get top patterns
        stats.TopPatterns = correlationList
            .GroupBy(c => c.Pattern)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        return stats;
    }

    public async Task TrainModelsAsync(List<EventCorrelation> confirmedCorrelations)
    {
        try
        {
            _logger.LogInformation("Training ML model with {Count} confirmed correlations", confirmedCorrelations.Count);

            // This is a placeholder for ML model training
            // In a real implementation, you would:
            // 1. Extract features from confirmed correlations
            // 2. Create training data
            // 3. Train an ML model (e.g., clustering, sequence prediction)
            // 4. Save the model for future use

            lock (_modelLock)
            {
                // Placeholder for model training
                // _mlModel = TrainModel(confirmedCorrelations);
            }

            _logger.LogInformation("ML model training completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training ML models");
        }
    }

    public async Task<List<CorrelationRule>> GetRulesAsync()
    {
        return _rules.ToList();
    }

    public async Task UpdateRuleAsync(CorrelationRule rule)
    {
        var existing = _rules.FirstOrDefault(r => r.Id == rule.Id);
        if (existing != null)
        {
            var index = _rules.IndexOf(existing);
            _rules[index] = rule;
            _logger.LogInformation("Updated correlation rule: {RuleName}", rule.Name);
        }
    }

    public async Task<List<AttackChain>> DetectAttackChainsAsync(List<SecurityEvent> events, TimeSpan window)
    {
        var chains = new List<AttackChain>();

        // Define common attack patterns
        var attackPatterns = new[]
        {
            new[] { "AuthenticationSuccess", "PrivilegeEscalation", "DataAccess" },
            new[] { "ProcessCreation", "NetworkConnection", "DataExfiltration" },
            new[] { "ServiceModification", "RegistryModification", "ProcessCreation" },
            new[] { "AuthenticationFailure", "AuthenticationFailure", "AuthenticationSuccess" }
        };

        foreach (var pattern in attackPatterns)
        {
            var chain = DetectSequentialPattern(events, pattern, window);
            if (chain != null)
            {
                chains.Add(chain);
            }
        }

        return chains;
    }

    public async Task CleanupOldCorrelationsAsync(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var toRemove = _correlations
            .Where(kvp => kvp.Value.DetectedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _correlations.TryRemove(key, out _);
        }

        _logger.LogInformation("Cleaned up {Count} old correlations", toRemove.Count);
    }

    #region Private Methods

    private List<CorrelationRule> InitializeCorrelationRules()
    {
        return new List<CorrelationRule>
        {
            new CorrelationRule
            {
                Id = "temporal-burst",
                Name = "Temporal Burst Detection",
                Description = "Detects multiple events from same source in short time",
                Type = CorrelationType.TemporalBurst,
                TimeWindow = TimeSpan.FromMinutes(5),
                MinEventCount = 5,
                MinConfidence = 0.7,
                IsEnabled = true
            },
            new CorrelationRule
            {
                Id = "brute-force",
                Name = "Brute Force Attack",
                Description = "Multiple failed authentications followed by success",
                Type = CorrelationType.AttackChain,
                TimeWindow = TimeSpan.FromMinutes(10),
                MinEventCount = 3,
                MinConfidence = 0.8,
                IsEnabled = true,
                RequiredEventTypes = new List<string> { "AuthenticationFailure", "AuthenticationSuccess" }
            },
            new CorrelationRule
            {
                Id = "lateral-movement",
                Name = "Lateral Movement Detection",
                Description = "Similar events across multiple machines",
                Type = CorrelationType.LateralMovement,
                TimeWindow = TimeSpan.FromMinutes(30),
                MinEventCount = 3,
                MinConfidence = 0.75,
                IsEnabled = true
            },
            new CorrelationRule
            {
                Id = "privilege-escalation",
                Name = "Privilege Escalation",
                Description = "Events indicating privilege escalation attempts",
                Type = CorrelationType.PrivilegeEscalation,
                TimeWindow = TimeSpan.FromMinutes(15),
                MinEventCount = 2,
                MinConfidence = 0.85,
                IsEnabled = true,
                RequiredEventTypes = new List<string> { "PrivilegeEscalation", "ProcessCreation" }
            }
        };
    }

    private async Task<List<SecurityEvent>> GetRecentEventsAsync(TimeSpan timeWindow)
    {
        var cutoff = DateTime.UtcNow - timeWindow;
        var events = new List<SecurityEvent>();

        // Get from cache first
        foreach (var cached in _eventCache.Values)
        {
            events.AddRange(cached.Where(e => e.OriginalEvent.Time.DateTime >= cutoff));
        }

        // If cache is insufficient, query the store
        if (events.Count < 100)
        {
            var filterDict = new Dictionary<string, object>
            {
                ["StartTime"] = cutoff,
                ["EndTime"] = DateTime.UtcNow
            };
            var storeEvents = _eventStore.GetSecurityEvents(1, 1000, filterDict);
            events.AddRange(storeEvents);
        }

        return events;
    }

    private async Task<EventCorrelation?> CheckCorrelationRuleAsync(
        CorrelationRule rule,
        SecurityEvent newEvent,
        List<SecurityEvent> recentEvents)
    {
        var relevantEvents = recentEvents
            .Where(e => IsEventRelevantToRule(e, rule))
            .ToList();

        if (relevantEvents.Count < rule.MinEventCount - 1)
            return null;

        relevantEvents.Add(newEvent);

        var confidence = CalculateConfidence(relevantEvents, rule);
        if (confidence < rule.MinConfidence)
            return null;

        return new EventCorrelation
        {
            CorrelationType = rule.Type.ToString(),
            ConfidenceScore = confidence,
            Pattern = rule.Name,
            EventIds = relevantEvents.Select(e => e.Id).ToList(),
            TimeWindow = rule.TimeWindow,
            MitreTechniques = relevantEvents.SelectMany(e => e.MitreTechniques).Distinct().ToList(),
            RiskLevel = DetermineRiskLevel(confidence, relevantEvents.Count),
            Summary = $"{rule.Name}: {relevantEvents.Count} related events detected",
            RecommendedActions = GenerateRecommendedActions(rule.Type)
        };
    }

    private bool IsEventRelevantToRule(SecurityEvent evt, CorrelationRule rule)
    {
        if (rule.RequiredEventTypes?.Any() == true)
        {
            return rule.RequiredEventTypes.Contains(evt.EventType.ToString());
        }

        // Check if event occurred within the rule's time window
        var age = DateTime.UtcNow - evt.OriginalEvent.Time;
        return age <= rule.TimeWindow;
    }

    private double CalculateConfidence(List<SecurityEvent> events, CorrelationRule rule)
    {
        var baseConfidence = (double)events.Count / Math.Max(rule.MinEventCount, 1);

        // Adjust confidence based on time clustering
        var timeSpan = events.Max(e => e.OriginalEvent.Time) - events.Min(e => e.OriginalEvent.Time);
        var timeRatio = 1.0 - (timeSpan.TotalSeconds / rule.TimeWindow.TotalSeconds);

        // Adjust confidence based on event diversity
        var uniqueTypes = events.Select(e => e.EventType).Distinct().Count();
        var diversityBonus = uniqueTypes > 1 ? 0.1 : 0;

        return Math.Min(1.0, baseConfidence * 0.6 + timeRatio * 0.3 + diversityBonus);
    }

    private List<EventCorrelation> DetectTemporalBursts(List<SecurityEvent> events, TimeSpan window)
    {
        var correlations = new List<EventCorrelation>();

        // Group events by source (machine/user)
        var grouped = events.GroupBy(e => e.OriginalEvent.Host);

        foreach (var group in grouped)
        {
            var sortedEvents = group.OrderBy(e => e.OriginalEvent.Time).ToList();

            for (int i = 0; i < sortedEvents.Count; i++)
            {
                var burst = new List<SecurityEvent> { sortedEvents[i] };
                var startTime = sortedEvents[i].OriginalEvent.Time;

                for (int j = i + 1; j < sortedEvents.Count; j++)
                {
                    if (sortedEvents[j].OriginalEvent.Time - startTime <= window)
                    {
                        burst.Add(sortedEvents[j]);
                    }
                    else
                    {
                        break;
                    }
                }

                if (burst.Count >= 5)
                {
                    correlations.Add(new EventCorrelation
                    {
                        CorrelationType = CorrelationType.TemporalBurst.ToString(),
                        ConfidenceScore = 0.8 + (burst.Count - 5) * 0.02, // Higher confidence for larger bursts
                        Pattern = "Temporal Burst",
                        EventIds = burst.Select(e => e.Id).ToList(),
                        TimeWindow = window,
                        Summary = $"Temporal burst of {burst.Count} events from {group.Key}",
                        RiskLevel = burst.Count > 10 ? "high" : "medium",
                        Metadata = new Dictionary<string, object>
                        {
                            ["Source"] = group.Key,
                            ["EventCount"] = burst.Count,
                            ["Duration"] = (burst.Last().OriginalEvent.Time - burst.First().OriginalEvent.Time).TotalMinutes
                        }
                    });
                }
            }
        }

        return correlations;
    }

    private List<EventCorrelation> DetectLateralMovement(List<SecurityEvent> events)
    {
        var correlations = new List<EventCorrelation>();

        // Group events by type and time window
        var timeWindow = TimeSpan.FromMinutes(30);
        var grouped = events
            .Where(e => e.EventType == SecurityEventType.NetworkConnection ||
                       e.EventType == SecurityEventType.AuthenticationSuccess)
            .GroupBy(e => new
            {
                Type = e.EventType,
                Window = new DateTime(e.OriginalEvent.Time.Ticks / timeWindow.Ticks * timeWindow.Ticks)
            });

        foreach (var group in grouped)
        {
            var machines = group.Select(e => e.OriginalEvent.Host).Distinct().ToList();

            if (machines.Count >= 3)
            {
                var correlation = new EventCorrelation
                {
                    CorrelationType = CorrelationType.LateralMovement.ToString(),
                    ConfidenceScore = 0.75 + (machines.Count - 3) * 0.05,
                    Pattern = "Lateral Movement",
                    EventIds = group.Select(e => e.Id).ToList(),
                    TimeWindow = timeWindow,
                    Summary = $"Similar {group.Key.Type} events across {machines.Count} machines",
                    RiskLevel = "high",
                    Metadata = new Dictionary<string, object>
                    {
                        ["AffectedMachines"] = machines,
                        ["EventType"] = group.Key.Type.ToString()
                    }
                };

                correlations.Add(correlation);
            }
        }

        return correlations;
    }

    private AttackChain? DetectSequentialPattern(List<SecurityEvent> events, string[] pattern, TimeSpan window)
    {
        var sortedEvents = events.OrderBy(e => e.OriginalEvent.Time).ToList();
        var stages = new List<AttackStage>();
        int patternIndex = 0;
        DateTime? chainStart = null;

        foreach (var evt in sortedEvents)
        {
            if (evt.EventType.ToString() == pattern[patternIndex])
            {
                if (chainStart == null)
                {
                    chainStart = evt.OriginalEvent.Time.DateTime;
                }
                else if (evt.OriginalEvent.Time.DateTime - chainStart.Value > window)
                {
                    // Reset if window exceeded
                    stages.Clear();
                    patternIndex = 0;
                    chainStart = evt.OriginalEvent.Time.DateTime;
                }

                stages.Add(new AttackStage
                {
                    Sequence = patternIndex + 1,
                    Name = evt.EventType.ToString(),
                    EventId = evt.Id,
                    Timestamp = evt.OriginalEvent.Time.DateTime,
                    Description = evt.Summary,
                    MitreTechnique = evt.MitreTechniques.FirstOrDefault()
                });

                patternIndex++;

                if (patternIndex >= pattern.Length)
                {
                    // Pattern complete
                    return new AttackChain
                    {
                        Name = string.Join(" -> ", pattern),
                        Stages = stages,
                        ConfidenceScore = 0.85,
                        StartTime = stages.First().Timestamp,
                        EndTime = stages.Last().Timestamp,
                        AttackType = DetermineAttackType(pattern),
                        MitreTechniques = stages.Where(s => s.MitreTechnique != null)
                            .Select(s => s.MitreTechnique!)
                            .Distinct()
                            .ToList(),
                        RiskLevel = "high",
                        AffectedAssets = events.Select(e => e.OriginalEvent.Host).Distinct().ToList()
                    };
                }
            }
        }

        return null;
    }

    private string DetermineAttackType(string[] pattern)
    {
        if (pattern.Contains("AuthenticationFailure") && pattern.Contains("AuthenticationSuccess"))
            return "Brute Force Attack";
        if (pattern.Contains("ProcessCreation") && pattern.Contains("NetworkConnection"))
            return "Remote Execution";
        if (pattern.Contains("PrivilegeEscalation"))
            return "Privilege Escalation";
        if (pattern.Contains("DataExfiltration"))
            return "Data Exfiltration";
        return "Unknown Attack Pattern";
    }

    private async Task<EventCorrelation?> DetectMLCorrelationAsync(SecurityEvent newEvent, List<SecurityEvent> recentEvents)
    {
        // Placeholder for ML-based correlation detection
        // In a real implementation, this would use the trained model to detect patterns
        return null;
    }

    private EventCorrelation ConvertChainToCorrelation(AttackChain chain)
    {
        return new EventCorrelation
        {
            CorrelationType = CorrelationType.AttackChain.ToString(),
            ConfidenceScore = chain.ConfidenceScore,
            Pattern = chain.Name,
            EventIds = chain.Stages.Select(s => s.EventId).ToList(),
            TimeWindow = chain.EndTime - chain.StartTime,
            AttackChainStage = chain.AttackType,
            MitreTechniques = chain.MitreTechniques,
            RiskLevel = chain.RiskLevel,
            Summary = $"Attack chain detected: {chain.Name}",
            Metadata = new Dictionary<string, object>
            {
                ["AttackType"] = chain.AttackType,
                ["StageCount"] = chain.Stages.Count,
                ["AffectedAssets"] = chain.AffectedAssets
            }
        };
    }

    private string DetermineRiskLevel(double confidence, int eventCount)
    {
        if (confidence > 0.9 || eventCount > 10)
            return "critical";
        if (confidence > 0.75 || eventCount > 5)
            return "high";
        if (confidence > 0.5 || eventCount > 3)
            return "medium";
        return "low";
    }

    private List<string> GenerateRecommendedActions(CorrelationType type)
    {
        return type switch
        {
            CorrelationType.AttackChain => new List<string>
            {
                "Isolate affected systems",
                "Review authentication logs",
                "Reset potentially compromised credentials",
                "Enable additional monitoring"
            },
            CorrelationType.LateralMovement => new List<string>
            {
                "Segment network to prevent spread",
                "Review remote access logs",
                "Scan for malware on affected systems",
                "Update access control lists"
            },
            CorrelationType.PrivilegeEscalation => new List<string>
            {
                "Review privileged account usage",
                "Audit permission changes",
                "Enable enhanced auditing",
                "Review group policy settings"
            },
            CorrelationType.DataExfiltration => new List<string>
            {
                "Block suspicious network connections",
                "Review data access logs",
                "Enable DLP policies",
                "Alert data owners"
            },
            _ => new List<string> { "Investigate events", "Increase monitoring", "Review security policies" }
        };
    }

    private string GenerateExplanation(EventCorrelation correlation)
    {
        var eventCount = correlation.EventIds.Count;
        var timeSpan = correlation.TimeWindow.TotalMinutes;

        return correlation.CorrelationType switch
        {
            "TemporalBurst" => $"Detected {eventCount} events clustered within {timeSpan:F1} minutes, indicating potential automated attack or malware activity.",
            "AttackChain" => $"Identified attack pattern '{correlation.Pattern}' with {eventCount} sequential events matching known attack techniques.",
            "LateralMovement" => $"Detected similar suspicious activity across multiple systems within {timeSpan:F1} minutes, suggesting lateral movement.",
            "PrivilegeEscalation" => $"Detected {eventCount} events indicating attempts to elevate privileges or access restricted resources.",
            _ => $"Correlation pattern '{correlation.Pattern}' detected with {eventCount} related events and {correlation.ConfidenceScore:P0} confidence."
        };
    }

    #endregion
}