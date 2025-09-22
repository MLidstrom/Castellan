using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for the event correlation engine
/// </summary>
public interface ICorrelationEngine
{
    /// <summary>
    /// Analyzes a new security event for correlations with existing events
    /// </summary>
    Task<CorrelationResult> AnalyzeEventAsync(SecurityEvent securityEvent);

    /// <summary>
    /// Performs batch correlation analysis on multiple events
    /// </summary>
    Task<List<EventCorrelation>> AnalyzeBatchAsync(List<SecurityEvent> events, TimeSpan timeWindow);

    /// <summary>
    /// Gets all correlations within a time range
    /// </summary>
    Task<List<EventCorrelation>> GetCorrelationsAsync(DateTime startTime, DateTime endTime);

    /// <summary>
    /// Gets a specific correlation by ID
    /// </summary>
    Task<EventCorrelation?> GetCorrelationAsync(string correlationId);

    /// <summary>
    /// Gets correlations for a specific event
    /// </summary>
    Task<List<EventCorrelation>> GetEventCorrelationsAsync(string eventId);

    /// <summary>
    /// Gets correlation statistics
    /// </summary>
    Task<CorrelationStatistics> GetStatisticsAsync(DateTime? startTime = null, DateTime? endTime = null);

    /// <summary>
    /// Trains ML models with new correlation patterns
    /// </summary>
    Task TrainModelsAsync(List<EventCorrelation> confirmedCorrelations);

    /// <summary>
    /// Gets all active correlation rules
    /// </summary>
    Task<List<CorrelationRule>> GetRulesAsync();

    /// <summary>
    /// Updates a correlation rule
    /// </summary>
    Task UpdateRuleAsync(CorrelationRule rule);

    /// <summary>
    /// Detects attack chains in event sequences
    /// </summary>
    Task<List<AttackChain>> DetectAttackChainsAsync(List<SecurityEvent> events, TimeSpan window);

    /// <summary>
    /// Clears old correlations from memory
    /// </summary>
    Task CleanupOldCorrelationsAsync(TimeSpan maxAge);
}

/// <summary>
/// Represents a detected attack chain
/// </summary>
public class AttackChain
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<AttackStage> Stages { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string AttackType { get; set; } = string.Empty;
    public List<string> MitreTechniques { get; set; } = new();
    public string RiskLevel { get; set; } = "high";
    public List<string> AffectedAssets { get; set; } = new();
}

/// <summary>
/// Represents a stage in an attack chain
/// </summary>
public class AttackStage
{
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MitreTechnique { get; set; }
}