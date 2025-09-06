namespace Castellan.Worker.Models;

/// <summary>
/// Represents a pipeline instance in the scaling architecture
/// </summary>
public class PipelineInstance
{
    /// <summary>
    /// Unique identifier for this instance
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for this instance
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// HTTP endpoint for this instance
    /// </summary>
    public required string EndpointUrl { get; init; }

    /// <summary>
    /// Current status of this instance
    /// </summary>
    public InstanceStatus Status { get; set; } = InstanceStatus.Unknown;

    /// <summary>
    /// Current health status
    /// </summary>
    public HealthStatus Health { get; set; } = HealthStatus.Unknown;

    /// <summary>
    /// Last health check result
    /// </summary>
    public HealthCheckResult? LastHealthCheck { get; set; }

    /// <summary>
    /// Current performance metrics
    /// </summary>
    public InstancePerformanceMetrics? Metrics { get; set; }

    /// <summary>
    /// Instance capabilities and configuration
    /// </summary>
    public InstanceCapabilities Capabilities { get; init; } = new();

    /// <summary>
    /// Timestamp when instance was registered
    /// </summary>
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Last time instance was seen (heartbeat)
    /// </summary>
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of consecutive failed health checks
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Current load weight for load balancing (0.0 to 1.0)
    /// </summary>
    public double LoadWeight { get; set; } = 1.0;

    /// <summary>
    /// Whether this instance is currently available for processing
    /// </summary>
    public bool IsAvailable => Status == InstanceStatus.Running && 
                                Health == HealthStatus.Healthy && 
                                ConsecutiveFailures < 3;

    /// <summary>
    /// Instance uptime
    /// </summary>
    public TimeSpan Uptime => DateTimeOffset.UtcNow - RegisteredAt;

    /// <summary>
    /// Time since last heartbeat
    /// </summary>
    public TimeSpan TimeSinceLastSeen => DateTimeOffset.UtcNow - LastSeen;

    /// <summary>
    /// Update heartbeat timestamp
    /// </summary>
    public void UpdateHeartbeat()
    {
        LastSeen = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Update load weight based on current performance
    /// </summary>
    public void UpdateLoadWeight()
    {
        if (Metrics == null || !IsAvailable)
        {
            LoadWeight = 0.0;
            return;
        }

        // Calculate weight based on CPU utilization and response time
        var cpuFactor = Math.Max(0.1, 1.0 - Metrics.CpuUtilizationPercent / 100.0);
        var responseTimeFactor = Math.Max(0.1, 1.0 - Math.Min(1.0, Metrics.AverageResponseTimeMs / 1000.0));
        
        LoadWeight = Math.Max(0.1, (cpuFactor + responseTimeFactor) / 2.0);
    }
}

/// <summary>
/// Instance status enumeration
/// </summary>
public enum InstanceStatus
{
    Unknown = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Stopped = 4,
    Failed = 5
}

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3,
    Critical = 4
}

/// <summary>
/// Instance capabilities and configuration
/// </summary>
public class InstanceCapabilities
{
    /// <summary>
    /// Maximum concurrent events this instance can process
    /// </summary>
    public int MaxConcurrentEvents { get; init; } = 100;

    /// <summary>
    /// Supported event types
    /// </summary>
    public IReadOnlyList<string> SupportedEventTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Instance version
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Available CPU cores
    /// </summary>
    public int CpuCores { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Available memory in MB
    /// </summary>
    public long AvailableMemoryMb { get; init; } = 1024;

    /// <summary>
    /// Instance-specific features
    /// </summary>
    public Dictionary<string, object> Features { get; init; } = new();
}

/// <summary>
/// Instance performance metrics
/// </summary>
public class InstancePerformanceMetrics
{
    /// <summary>
    /// Current CPU utilization percentage (0-100)
    /// </summary>
    public double CpuUtilizationPercent { get; init; }

    /// <summary>
    /// Current memory utilization percentage (0-100)
    /// </summary>
    public double MemoryUtilizationPercent { get; init; }

    /// <summary>
    /// Current number of events being processed
    /// </summary>
    public int CurrentEventCount { get; init; }

    /// <summary>
    /// Events processed per second
    /// </summary>
    public double EventsPerSecond { get; init; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTimeMs { get; init; }

    /// <summary>
    /// Error rate percentage (0-100)
    /// </summary>
    public double ErrorRatePercent { get; init; }

    /// <summary>
    /// Queue length (if applicable)
    /// </summary>
    public int QueueLength { get; init; }

    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Instance efficiency score (0.0 to 1.0)
    /// </summary>
    public double EfficiencyScore
    {
        get
        {
            var cpuScore = Math.Max(0, 1.0 - CpuUtilizationPercent / 100.0);
            var memoryScore = Math.Max(0, 1.0 - MemoryUtilizationPercent / 100.0);
            var responseScore = Math.Max(0, 1.0 - Math.Min(1.0, AverageResponseTimeMs / 1000.0));
            var errorScore = Math.Max(0, 1.0 - ErrorRatePercent / 100.0);
            
            return (cpuScore + memoryScore + responseScore + errorScore) / 4.0;
        }
    }
}

/// <summary>
/// Aggregated metrics for all instances
/// </summary>
public class AggregatedInstanceMetrics
{
    /// <summary>
    /// Total number of registered instances
    /// </summary>
    public int TotalInstances { get; init; }

    /// <summary>
    /// Number of healthy instances
    /// </summary>
    public int HealthyInstances { get; init; }

    /// <summary>
    /// Number of available instances
    /// </summary>
    public int AvailableInstances { get; init; }

    /// <summary>
    /// Average CPU utilization across all instances
    /// </summary>
    public double AverageCpuUtilization { get; init; }

    /// <summary>
    /// Average memory utilization across all instances
    /// </summary>
    public double AverageMemoryUtilization { get; init; }

    /// <summary>
    /// Total events per second across all instances
    /// </summary>
    public double TotalEventsPerSecond { get; init; }

    /// <summary>
    /// Average response time across all instances
    /// </summary>
    public double AverageResponseTime { get; init; }

    /// <summary>
    /// Overall error rate across all instances
    /// </summary>
    public double OverallErrorRate { get; init; }

    /// <summary>
    /// Total processing capacity
    /// </summary>
    public int TotalCapacity { get; init; }

    /// <summary>
    /// Current utilization of total capacity
    /// </summary>
    public double CapacityUtilization { get; init; }

    /// <summary>
    /// Timestamp when metrics were aggregated
    /// </summary>
    public DateTimeOffset AggregatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Health check result for an instance
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Health status
    /// </summary>
    public HealthStatus Status { get; init; } = HealthStatus.Unknown;

    /// <summary>
    /// Health check message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Response time for the health check
    /// </summary>
    public TimeSpan ResponseTime { get; init; }

    /// <summary>
    /// Timestamp when health check was performed
    /// </summary>
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional health check details
    /// </summary>
    public Dictionary<string, object> Details { get; init; } = new();

    /// <summary>
    /// Whether the health check was successful
    /// </summary>
    public bool IsHealthy => Status == HealthStatus.Healthy;
}

/// <summary>
/// Event arguments for instance status changes
/// </summary>
public class InstanceStatusChangedEventArgs : EventArgs
{
    public required PipelineInstance Instance { get; init; }
    public InstanceStatus PreviousStatus { get; init; }
    public InstanceStatus NewStatus { get; init; }
}

/// <summary>
/// Event arguments for instance addition
/// </summary>
public class InstanceAddedEventArgs : EventArgs
{
    public required PipelineInstance Instance { get; init; }
}

/// <summary>
/// Event arguments for instance removal
/// </summary>
public class InstanceRemovedEventArgs : EventArgs
{
    public required PipelineInstance Instance { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Instance command for remote operations
/// </summary>
public class InstanceCommand
{
    /// <summary>
    /// Command type
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Command parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Command timeout
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Command ID for tracking
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Response from an instance command
/// </summary>
public class InstanceCommandResponse
{
    /// <summary>
    /// Instance ID that generated the response
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Command ID this response is for
    /// </summary>
    public required string CommandId { get; init; }

    /// <summary>
    /// Whether the command was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Response message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Response data
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = new();

    /// <summary>
    /// Response timestamp
    /// </summary>
    public DateTimeOffset ResponseAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Response duration
    /// </summary>
    public TimeSpan Duration { get; init; }
}
