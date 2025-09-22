using System;
using System.Collections.Generic;

namespace Castellan.Worker.Models;

/// <summary>
/// Represents a correlation between multiple security events
/// </summary>
public class EventCorrelation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string CorrelationType { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public List<string> EventIds { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public TimeSpan TimeWindow { get; set; }
    public string? AttackChainStage { get; set; }
    public List<string> MitreTechniques { get; set; } = new();
    public string RiskLevel { get; set; } = "medium";
    public string Summary { get; set; } = string.Empty;
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Types of correlations the engine can detect
/// </summary>
public enum CorrelationType
{
    /// <summary>
    /// Multiple events from same source in short time
    /// </summary>
    TemporalBurst,

    /// <summary>
    /// Events matching a known attack pattern sequence
    /// </summary>
    AttackChain,

    /// <summary>
    /// Similar events across multiple machines
    /// </summary>
    LateralMovement,

    /// <summary>
    /// Events indicating privilege escalation
    /// </summary>
    PrivilegeEscalation,

    /// <summary>
    /// Data exfiltration patterns
    /// </summary>
    DataExfiltration,

    /// <summary>
    /// Persistence mechanism detection
    /// </summary>
    Persistence,

    /// <summary>
    /// Command and control activity
    /// </summary>
    CommandControl,

    /// <summary>
    /// Anomalous user behavior
    /// </summary>
    UserAnomaly,

    /// <summary>
    /// Machine learning detected pattern
    /// </summary>
    MLPattern
}

/// <summary>
/// Represents a correlation rule for pattern matching
/// </summary>
public class CorrelationRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CorrelationType Type { get; set; }
    public List<EventPattern> Patterns { get; set; } = new();
    public TimeSpan TimeWindow { get; set; }
    public int MinEventCount { get; set; }
    public double MinConfidence { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<string> RequiredEventTypes { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Defines a pattern for event matching
/// </summary>
public class EventPattern
{
    public string EventType { get; set; } = string.Empty;
    public Dictionary<string, string> RequiredFields { get; set; } = new();
    public string? MitreTechnique { get; set; }
    public int? Sequence { get; set; }
    public TimeSpan? MaxTimeSincePrevious { get; set; }
}

/// <summary>
/// Result of correlation analysis
/// </summary>
public class CorrelationResult
{
    public bool HasCorrelation { get; set; }
    public EventCorrelation? Correlation { get; set; }
    public double ConfidenceScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public List<string> MatchedRules { get; set; } = new();
}

/// <summary>
/// Statistics about correlation engine performance
/// </summary>
public class CorrelationStatistics
{
    public int TotalEventsProcessed { get; set; }
    public int CorrelationsDetected { get; set; }
    public Dictionary<string, int> CorrelationsByType { get; set; } = new();
    public double AverageConfidenceScore { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public List<string> TopPatterns { get; set; } = new();
}