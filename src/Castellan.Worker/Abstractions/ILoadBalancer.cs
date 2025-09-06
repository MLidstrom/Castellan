using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for load balancing events across pipeline instances
/// </summary>
public interface ILoadBalancer : IDisposable
{
    /// <summary>
    /// Select the best instance for processing an event
    /// </summary>
    /// <param name="queuedEvent">Event to be processed</param>
    /// <param name="availableInstances">Available instances for selection</param>
    /// <returns>Selected instance or null if none available</returns>
    PipelineInstance? SelectInstance(QueuedEvent queuedEvent, IReadOnlyList<PipelineInstance> availableInstances);

    /// <summary>
    /// Update instance metrics for load balancing decisions
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <param name="metrics">Updated performance metrics</param>
    Task UpdateInstanceMetricsAsync(string instanceId, InstancePerformanceMetrics metrics);

    /// <summary>
    /// Record processing result for load balancing optimization
    /// </summary>
    /// <param name="instanceId">Instance that processed the event</param>
    /// <param name="eventId">Event that was processed</param>
    /// <param name="processingTime">Time taken to process</param>
    /// <param name="success">Whether processing was successful</param>
    Task RecordProcessingResultAsync(string instanceId, string eventId, TimeSpan processingTime, bool success);

    /// <summary>
    /// Get load balancing metrics and statistics
    /// </summary>
    /// <returns>Load balancing performance metrics</returns>
    LoadBalancingMetrics GetMetrics();

    /// <summary>
    /// Get current load distribution across instances
    /// </summary>
    /// <returns>Dictionary of instance ID to load percentage</returns>
    IReadOnlyDictionary<string, double> GetLoadDistribution();

    /// <summary>
    /// Force recalculation of instance weights
    /// </summary>
    void RefreshInstanceWeights(IReadOnlyList<PipelineInstance> instances);

    /// <summary>
    /// Event fired when load balancing decision is made
    /// </summary>
    event EventHandler<LoadBalancingDecisionEventArgs>? DecisionMade;

    /// <summary>
    /// Event fired when instance weight is updated
    /// </summary>
    event EventHandler<InstanceWeightUpdatedEventArgs>? WeightUpdated;
}

/// <summary>
/// Load balancing strategy enumeration
/// </summary>
public enum LoadBalancingStrategy
{
    RoundRobin,
    WeightedRoundRobin,
    LeastConnections,
    LeastResponseTime,
    CapacityBased,
    Adaptive
}

/// <summary>
/// Load balancing metrics for monitoring
/// </summary>
public class LoadBalancingMetrics
{
    /// <summary>
    /// Total number of load balancing decisions made
    /// </summary>
    public long TotalDecisions { get; init; }

    /// <summary>
    /// Number of decisions where no instance was available
    /// </summary>
    public long NoInstanceAvailable { get; init; }

    /// <summary>
    /// Average decision time in milliseconds
    /// </summary>
    public double AverageDecisionTimeMs { get; init; }

    /// <summary>
    /// Load distribution variance (lower is better)
    /// </summary>
    public double LoadVariance { get; init; }

    /// <summary>
    /// Load balancing effectiveness score (0.0 to 1.0)
    /// </summary>
    public double EffectivenessScore { get; init; }

    /// <summary>
    /// Number of instances being tracked
    /// </summary>
    public int TrackedInstances { get; init; }

    /// <summary>
    /// Average instance utilization percentage
    /// </summary>
    public double AverageUtilization { get; init; }

    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Load balancing efficiency (even distribution indicator)
    /// </summary>
    public double DistributionEfficiency => 1.0 - Math.Min(1.0, LoadVariance);

    /// <summary>
    /// Whether load balancing is performing well
    /// </summary>
    public bool IsPerformingWell => EffectivenessScore > 0.8 && DistributionEfficiency > 0.7;
}

/// <summary>
/// Event arguments for load balancing decisions
/// </summary>
public class LoadBalancingDecisionEventArgs : EventArgs
{
    /// <summary>
    /// Event that was being load balanced
    /// </summary>
    public required QueuedEvent QueuedEvent { get; init; }

    /// <summary>
    /// Selected instance (null if none available)
    /// </summary>
    public PipelineInstance? SelectedInstance { get; init; }

    /// <summary>
    /// All instances that were considered
    /// </summary>
    public required IReadOnlyList<PipelineInstance> AvailableInstances { get; init; }

    /// <summary>
    /// Time taken to make the decision
    /// </summary>
    public TimeSpan DecisionTime { get; init; }

    /// <summary>
    /// Reason for the selection
    /// </summary>
    public string SelectionReason { get; init; } = string.Empty;
}

/// <summary>
/// Event arguments for instance weight updates
/// </summary>
public class InstanceWeightUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Instance whose weight was updated
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Previous weight value
    /// </summary>
    public double PreviousWeight { get; init; }

    /// <summary>
    /// New weight value
    /// </summary>
    public double NewWeight { get; init; }

    /// <summary>
    /// Reason for the weight update
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}
