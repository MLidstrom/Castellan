using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Configuration;

/// <summary>
/// Configuration options for pipeline scaling architecture
/// </summary>
public class PipelineScalingOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "PipelineScaling";

    /// <summary>
    /// Whether pipeline scaling is enabled
    /// </summary>
    [Required]
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Minimum number of pipeline instances
    /// </summary>
    [Range(1, 100)]
    public int MinInstances { get; set; } = 2;

    /// <summary>
    /// Maximum number of pipeline instances
    /// </summary>
    [Range(1, 100)]
    public int MaxInstances { get; set; } = 8;

    /// <summary>
    /// Default number of instances to start with
    /// </summary>
    [Range(1, 100)]
    public int DefaultInstances { get; set; } = 4;

    /// <summary>
    /// Instance startup timeout in seconds
    /// </summary>
    [Range(10, 300)]
    public int InstanceStartupTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Instance shutdown timeout in seconds
    /// </summary>
    [Range(5, 120)]
    public int InstanceShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Auto-scaling policy configuration
    /// </summary>
    [Required]
    public ScalingPolicyOptions ScalingPolicy { get; set; } = new();

    /// <summary>
    /// Load balancing configuration
    /// </summary>
    [Required]
    public LoadBalancingOptions LoadBalancing { get; set; } = new();

    /// <summary>
    /// Event queue configuration
    /// </summary>
    [Required]
    public EventQueueOptions EventQueue { get; set; } = new();

    /// <summary>
    /// Shared state management configuration
    /// </summary>
    [Required]
    public SharedStateOptions SharedState { get; set; } = new();

    /// <summary>
    /// Health monitoring configuration
    /// </summary>
    [Required]
    public HealthMonitoringOptions HealthMonitoring { get; set; } = new();
}

/// <summary>
/// Auto-scaling policy configuration
/// </summary>
public class ScalingPolicyOptions
{
    /// <summary>
    /// CPU threshold for scaling up (percentage)
    /// </summary>
    [Range(50, 95)]
    public double ScaleUpThreshold { get; set; } = 80.0;

    /// <summary>
    /// CPU threshold for scaling down (percentage)
    /// </summary>
    [Range(10, 50)]
    public double ScaleDownThreshold { get; set; } = 30.0;

    /// <summary>
    /// Cooling period between scaling actions (minutes)
    /// </summary>
    [Range(1, 30)]
    public int CoolingPeriodMinutes { get; set; } = 5;

    /// <summary>
    /// Evaluation period for scaling decisions (minutes)
    /// </summary>
    [Range(1, 10)]
    public int EvaluationPeriodMinutes { get; set; } = 2;

    /// <summary>
    /// Queue length threshold for scaling up
    /// </summary>
    [Range(100, 10000)]
    public int QueueLengthScaleUpThreshold { get; set; } = 1000;

    /// <summary>
    /// Queue length threshold for scaling down
    /// </summary>
    [Range(10, 1000)]
    public int QueueLengthScaleDownThreshold { get; set; } = 100;

    /// <summary>
    /// Response time threshold for scaling up (ms)
    /// </summary>
    [Range(100, 5000)]
    public int ResponseTimeThresholdMs { get; set; } = 1000;

    /// <summary>
    /// Error rate threshold for scaling up (percentage)
    /// </summary>
    [Range(1, 10)]
    public double ErrorRateThreshold { get; set; } = 5.0;
}

/// <summary>
/// Load balancing configuration
/// </summary>
public class LoadBalancingOptions
{
    /// <summary>
    /// Load balancing strategy
    /// </summary>
    [Required]
    public string Strategy { get; set; } = "WeightedRoundRobin";

    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    [Range(5, 300)]
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Number of consecutive failures before marking unhealthy
    /// </summary>
    [Range(1, 10)]
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// Number of consecutive successes before marking healthy
    /// </summary>
    [Range(1, 10)]
    public int HealthyThreshold { get; set; } = 2;

    /// <summary>
    /// Health check timeout in seconds
    /// </summary>
    [Range(1, 60)]
    public int HealthCheckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether to enable sticky sessions (affinity)
    /// </summary>
    public bool EnableStickySession { get; set; } = false;

    /// <summary>
    /// Session timeout for sticky sessions (minutes)
    /// </summary>
    [Range(1, 240)]
    public int StickySessionTimeoutMinutes { get; set; } = 30;
}

/// <summary>
/// Event queue configuration
/// </summary>
public class EventQueueOptions
{
    /// <summary>
    /// Maximum queue size per instance
    /// </summary>
    [Range(100, 100000)]
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>
    /// Dequeue timeout in milliseconds
    /// </summary>
    [Range(100, 10000)]
    public int DequeueTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Whether dead letter queue is enabled
    /// </summary>
    public bool DeadLetterQueueEnabled { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for failed events
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Maximum age for events in queue (minutes)
    /// </summary>
    [Range(5, 240)]
    public int MaxEventAgeMinutes { get; set; } = 30;

    /// <summary>
    /// Priority levels for event processing
    /// </summary>
    public Dictionary<string, int> PriorityLevels { get; set; } = new()
    {
        ["Critical"] = 100,
        ["High"] = 75,
        ["Normal"] = 50,
        ["Low"] = 25
    };
}

/// <summary>
/// Shared state management configuration
/// </summary>
public class SharedStateOptions
{
    /// <summary>
    /// Synchronization interval in seconds
    /// </summary>
    [Range(1, 300)]
    public int SyncIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Conflict resolution strategy
    /// </summary>
    [Required]
    public string ConflictResolution { get; set; } = "LastWriterWins";

    /// <summary>
    /// State timeout in minutes
    /// </summary>
    [Range(1, 240)]
    public int StateTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum number of state entries per type
    /// </summary>
    [Range(100, 10000)]
    public int MaxStateEntries { get; set; } = 1000;

    /// <summary>
    /// State compression enabled
    /// </summary>
    public bool CompressionEnabled { get; set; } = true;

    /// <summary>
    /// State persistence enabled
    /// </summary>
    public bool PersistenceEnabled { get; set; } = false;
}

/// <summary>
/// Health monitoring configuration
/// </summary>
public class HealthMonitoringOptions
{
    /// <summary>
    /// Whether health monitoring is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    [Range(5, 300)]
    public int CheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Health check timeout in seconds
    /// </summary>
    [Range(1, 60)]
    public int CheckTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of health history entries to keep
    /// </summary>
    [Range(10, 1000)]
    public int MaxHistoryEntries { get; set; } = 100;

    /// <summary>
    /// Alert thresholds configuration
    /// </summary>
    public AlertThresholds Alerts { get; set; } = new();
}

/// <summary>
/// Alert threshold configuration
/// </summary>
public class AlertThresholds
{
    /// <summary>
    /// CPU utilization alert threshold (percentage)
    /// </summary>
    [Range(50, 95)]
    public double CpuUtilizationThreshold { get; set; } = 85.0;

    /// <summary>
    /// Memory utilization alert threshold (percentage)
    /// </summary>
    [Range(50, 95)]
    public double MemoryUtilizationThreshold { get; set; } = 90.0;

    /// <summary>
    /// Response time alert threshold (ms)
    /// </summary>
    [Range(100, 10000)]
    public int ResponseTimeThresholdMs { get; set; } = 2000;

    /// <summary>
    /// Error rate alert threshold (percentage)
    /// </summary>
    [Range(1, 20)]
    public double ErrorRateThreshold { get; set; } = 10.0;

    /// <summary>
    /// Queue length alert threshold
    /// </summary>
    [Range(100, 50000)]
    public int QueueLengthThreshold { get; set; } = 5000;

    /// <summary>
    /// Instance unavailable alert threshold (percentage)
    /// </summary>
    [Range(10, 90)]
    public double UnavailableInstancesThreshold { get; set; } = 50.0;
}
