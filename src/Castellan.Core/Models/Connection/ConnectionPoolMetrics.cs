namespace Castellan.Core.Models.Connection;

/// <summary>
/// Comprehensive metrics for connection pool performance
/// </summary>
public class ConnectionPoolMetrics
{
    /// <summary>
    /// Pool name or identifier
    /// </summary>
    public string PoolName { get; set; } = string.Empty;

    /// <summary>
    /// Type of connection pool (HTTP, Qdrant, etc.)
    /// </summary>
    public string PoolType { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Current number of active connections
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Total number of available connections in pool
    /// </summary>
    public int TotalConnections { get; set; }

    /// <summary>
    /// Maximum allowed connections in pool
    /// </summary>
    public int MaxConnections { get; set; }

    /// <summary>
    /// Pool utilization percentage (0-100)
    /// </summary>
    public double UtilizationPercentage => TotalConnections > 0 
        ? (double)ActiveConnections / TotalConnections * 100 
        : 0;

    /// <summary>
    /// Average time to acquire connection from pool
    /// </summary>
    public TimeSpan AverageAcquisitionTime { get; set; }

    /// <summary>
    /// Average connection lifetime
    /// </summary>
    public TimeSpan AverageConnectionLifetime { get; set; }

    /// <summary>
    /// Total number of connection requests
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Number of successful connection acquisitions
    /// </summary>
    public long SuccessfulAcquisitions { get; set; }

    /// <summary>
    /// Number of failed connection acquisitions
    /// </summary>
    public long FailedAcquisitions { get; set; }

    /// <summary>
    /// Success rate percentage (0-100)
    /// </summary>
    public double SuccessRate => TotalRequests > 0 
        ? (double)SuccessfulAcquisitions / TotalRequests * 100 
        : 0;

    /// <summary>
    /// Number of connection timeouts
    /// </summary>
    public long ConnectionTimeouts { get; set; }

    /// <summary>
    /// Number of connections currently in circuit breaker state
    /// </summary>
    public int CircuitBreakerTrips { get; set; }

    /// <summary>
    /// Health status of the pool
    /// </summary>
    public ConnectionPoolHealthStatus HealthStatus { get; set; } = ConnectionPoolHealthStatus.Healthy;

    /// <summary>
    /// Detailed health information
    /// </summary>
    public string? HealthDetails { get; set; }
}

/// <summary>
/// Health status enumeration for connection pools
/// </summary>
public enum ConnectionPoolHealthStatus
{
    /// <summary>
    /// Pool is operating normally
    /// </summary>
    Healthy,

    /// <summary>
    /// Pool is experiencing some issues but still functional
    /// </summary>
    Degraded,

    /// <summary>
    /// Pool is not functional
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Pool health status cannot be determined
    /// </summary>
    Unknown
}

/// <summary>
/// Performance metrics for individual connections
/// </summary>
public class ConnectionPerformanceMetrics
{
    /// <summary>
    /// Connection identifier
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Pool this connection belongs to
    /// </summary>
    public string PoolName { get; set; } = string.Empty;

    /// <summary>
    /// Connection creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last used timestamp
    /// </summary>
    public DateTimeOffset LastUsedAt { get; set; }

    /// <summary>
    /// Total number of requests handled by this connection
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Average response time for requests
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Number of errors on this connection
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Error rate for this connection (0-100)
    /// </summary>
    public double ErrorRate => RequestCount > 0 
        ? (double)ErrorCount / RequestCount * 100 
        : 0;

    /// <summary>
    /// Current connection state
    /// </summary>
    public ConnectionState State { get; set; } = ConnectionState.Available;

    /// <summary>
    /// Bytes sent through this connection
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// Bytes received through this connection
    /// </summary>
    public long BytesReceived { get; set; }
}

/// <summary>
/// Connection state enumeration
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Connection is available for use
    /// </summary>
    Available,

    /// <summary>
    /// Connection is currently in use
    /// </summary>
    InUse,

    /// <summary>
    /// Connection is being validated
    /// </summary>
    Validating,

    /// <summary>
    /// Connection has failed and is unusable
    /// </summary>
    Failed,

    /// <summary>
    /// Connection is being closed/disposed
    /// </summary>
    Closing
}

/// <summary>
/// Load balancing metrics for connection distribution
/// </summary>
public class LoadBalancingMetrics
{
    /// <summary>
    /// Load balancer identifier
    /// </summary>
    public string LoadBalancerId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Distribution of requests across instances
    /// </summary>
    public Dictionary<string, InstanceDistributionMetrics> InstanceDistribution { get; set; } = new();

    /// <summary>
    /// Current load balancing algorithm
    /// </summary>
    public LoadBalancingAlgorithm CurrentAlgorithm { get; set; }

    /// <summary>
    /// Total requests processed by load balancer
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Number of requests that failed to be balanced (no healthy instances)
    /// </summary>
    public long FailedBalancing { get; set; }

    /// <summary>
    /// Average time spent in load balancing decision
    /// </summary>
    public TimeSpan AverageBalancingTime { get; set; }

    /// <summary>
    /// Number of instances currently healthy
    /// </summary>
    public int HealthyInstances { get; set; }

    /// <summary>
    /// Total number of instances configured
    /// </summary>
    public int TotalInstances { get; set; }

    /// <summary>
    /// Instance health ratio (0-100)
    /// </summary>
    public double InstanceHealthRatio => TotalInstances > 0 
        ? (double)HealthyInstances / TotalInstances * 100 
        : 0;
}

/// <summary>
/// Distribution metrics for individual instances in load balancing
/// </summary>
public class InstanceDistributionMetrics
{
    /// <summary>
    /// Instance identifier
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Number of requests sent to this instance
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Percentage of total requests handled by this instance
    /// </summary>
    public double RequestPercentage { get; set; }

    /// <summary>
    /// Current weight assigned to this instance
    /// </summary>
    public double CurrentWeight { get; set; }

    /// <summary>
    /// Original configured weight
    /// </summary>
    public double OriginalWeight { get; set; }

    /// <summary>
    /// Average response time for this instance
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Error rate for this instance (0-100)
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Current health status
    /// </summary>
    public ConnectionPoolHealthStatus HealthStatus { get; set; }

    /// <summary>
    /// Number of active connections to this instance
    /// </summary>
    public int ActiveConnections { get; set; }
}

/// <summary>
/// Health check result for connection monitoring
/// </summary>
public class ConnectionHealthCheckResult
{
    /// <summary>
    /// Connection or instance identifier
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Health check timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the health check passed
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Time taken for the health check
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// Error message if health check failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional health check details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// Health check type (ping, full validation, etc.)
    /// </summary>
    public string CheckType { get; set; } = "basic";

    /// <summary>
    /// Consecutive successful health checks
    /// </summary>
    public int ConsecutiveSuccesses { get; set; }

    /// <summary>
    /// Consecutive failed health checks
    /// </summary>
    public int ConsecutiveFailures { get; set; }
}

/// <summary>
/// Circuit breaker state and metrics
/// </summary>
public class CircuitBreakerMetrics
{
    /// <summary>
    /// Circuit breaker identifier
    /// </summary>
    public string CircuitBreakerId { get; set; } = string.Empty;

    /// <summary>
    /// Current circuit breaker state
    /// </summary>
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;

    /// <summary>
    /// Number of failures in current window
    /// </summary>
    public int FailuresInWindow { get; set; }

    /// <summary>
    /// Configured failure threshold
    /// </summary>
    public int FailureThreshold { get; set; }

    /// <summary>
    /// When circuit breaker was last opened
    /// </summary>
    public DateTimeOffset? LastOpenedAt { get; set; }

    /// <summary>
    /// When circuit breaker will attempt to close
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// Total number of requests rejected while open
    /// </summary>
    public long RejectedRequests { get; set; }

    /// <summary>
    /// Success rate during half-open state
    /// </summary>
    public double HalfOpenSuccessRate { get; set; }
}

/// <summary>
/// Circuit breaker state enumeration
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed, requests are allowed through
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open, requests are rejected
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open, limited requests allowed for testing
    /// </summary>
    HalfOpen
}
