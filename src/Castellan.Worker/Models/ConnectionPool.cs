using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

/// <summary>
/// Connection pool configuration options
/// </summary>
public class ConnectionPoolOptions
{
    /// <summary>
    /// Qdrant connection pool configuration
    /// </summary>
    public QdrantPoolOptions QdrantPool { get; set; } = new();

    /// <summary>
    /// HTTP client pool configurations
    /// </summary>
    public HttpClientPoolOptions HttpClientPools { get; set; } = new();

    /// <summary>
    /// Connection health monitoring configuration
    /// </summary>
    public ConnectionHealthMonitoringOptions HealthMonitoring { get; set; } = new();

    /// <summary>
    /// Load balancing configuration
    /// </summary>
    public ConnectionLoadBalancingOptions LoadBalancing { get; set; } = new();

    /// <summary>
    /// Global connection timeout settings
    /// </summary>
    public GlobalTimeoutOptions GlobalTimeouts { get; set; } = new();

    /// <summary>
    /// Metrics collection configuration
    /// </summary>
    public ConnectionMetricsOptions Metrics { get; set; } = new();
}

/// <summary>
/// Qdrant connection pool configuration
/// </summary>
public class QdrantPoolOptions
{
    /// <summary>
    /// Qdrant instance configurations
    /// </summary>
    public List<QdrantInstanceConfiguration> Instances { get; set; } = new()
    {
        new QdrantInstanceConfiguration { Host = "localhost", Port = 6333, Weight = 100 }
    };

    /// <summary>
    /// Maximum connections per Qdrant instance
    /// </summary>
    [Range(1, 1000)]
    public int MaxConnectionsPerInstance { get; set; } = 50;

    /// <summary>
    /// Health check interval for Qdrant instances
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Connection timeout for Qdrant
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Request timeout for Qdrant operations
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Enable automatic failover to healthy instances
    /// </summary>
    public bool EnableFailover { get; set; } = true;

    /// <summary>
    /// Minimum healthy instances required
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MinHealthyInstances { get; set; } = 1;
}

/// <summary>
/// Individual Qdrant instance configuration
/// </summary>
public class QdrantInstanceConfiguration
{
    /// <summary>
    /// Qdrant instance hostname or IP address
    /// </summary>
    [Required]
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Qdrant instance port
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 6333;

    /// <summary>
    /// Load balancing weight for this instance
    /// </summary>
    [Range(0, 1000)]
    public int Weight { get; set; } = 100;

    /// <summary>
    /// Enable HTTPS for this instance
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// API key for authentication (if required)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Custom timeout for this specific instance
    /// </summary>
    public TimeSpan? InstanceTimeout { get; set; }
}

/// <summary>
/// HTTP client pool configuration
/// </summary>
public class HttpClientPoolOptions
{
    /// <summary>
    /// Named HTTP client pool configurations
    /// </summary>
    public Dictionary<string, HttpClientPoolConfiguration> Pools { get; set; } = new()
    {
        ["Default"] = new HttpClientPoolConfiguration()
    };

    /// <summary>
    /// Default pool name to use when none specified
    /// </summary>
    public string DefaultPool { get; set; } = "Default";

    /// <summary>
    /// Enable automatic pool creation for unknown pool names
    /// </summary>
    public bool EnableAutoPoolCreation { get; set; } = true;
}

/// <summary>
/// Configuration for individual HTTP client pool
/// </summary>
public class HttpClientPoolConfiguration
{
    /// <summary>
    /// Maximum number of connections in the pool
    /// </summary>
    [Range(1, 1000)]
    public int MaxConnections { get; set; } = 100;

    /// <summary>
    /// Connection establishment timeout
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Individual request timeout
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Circuit breaker failure threshold
    /// </summary>
    [Range(1, 100)]
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker timeout before retry
    /// </summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Enable compression for requests
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Custom headers to include in all requests
    /// </summary>
    public Dictionary<string, string> DefaultHeaders { get; set; } = new();
}

/// <summary>
/// Connection health monitoring configuration
/// </summary>
public class ConnectionHealthMonitoringOptions
{
    /// <summary>
    /// Enable connection health monitoring
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Health check interval for all pools
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for individual health checks
    /// </summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of consecutive failures before marking unhealthy
    /// </summary>
    [Range(1, 100)]
    public int ConsecutiveFailureThreshold { get; set; } = 3;

    /// <summary>
    /// Number of consecutive successes before marking healthy
    /// </summary>
    [Range(1, 100)]
    public int ConsecutiveSuccessThreshold { get; set; } = 2;

    /// <summary>
    /// Enable automatic recovery attempts for failed connections
    /// </summary>
    public bool EnableAutoRecovery { get; set; } = true;

    /// <summary>
    /// Recovery attempt interval for failed connections
    /// </summary>
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum time to retain health history
    /// </summary>
    public TimeSpan HealthHistoryRetention { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Load balancing configuration for connection pools
/// </summary>
public class ConnectionLoadBalancingOptions
{
    /// <summary>
    /// Load balancing algorithm to use
    /// </summary>
    public LoadBalancingAlgorithm Algorithm { get; set; } = LoadBalancingAlgorithm.WeightedRoundRobin;

    /// <summary>
    /// Enable health-aware load balancing
    /// </summary>
    public bool EnableHealthAwareRouting { get; set; } = true;

    /// <summary>
    /// Performance window for dynamic weight adjustment
    /// </summary>
    public TimeSpan PerformanceWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Weight adjustment factors
    /// </summary>
    public WeightAdjustmentOptions WeightAdjustment { get; set; } = new();

    /// <summary>
    /// Sticky session configuration for maintaining client affinity
    /// </summary>
    public StickySessionOptions StickySession { get; set; } = new();
}

/// <summary>
/// Load balancing algorithm options
/// </summary>
public enum LoadBalancingAlgorithm
{
    /// <summary>
    /// Simple round-robin distribution
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Weighted round-robin based on instance weights
    /// </summary>
    WeightedRoundRobin,

    /// <summary>
    /// Least connections algorithm
    /// </summary>
    LeastConnections,

    /// <summary>
    /// Health-aware weighted distribution
    /// </summary>
    HealthAware,

    /// <summary>
    /// Random selection
    /// </summary>
    Random
}

/// <summary>
/// Weight adjustment factors for dynamic load balancing
/// </summary>
public class WeightAdjustmentOptions
{
    /// <summary>
    /// Factor for response time in weight calculation
    /// </summary>
    [Range(0.0, 1.0)]
    public double ResponseTimeFactor { get; set; } = 0.4;

    /// <summary>
    /// Factor for error rate in weight calculation
    /// </summary>
    [Range(0.0, 1.0)]
    public double ErrorRateFactor { get; set; } = 0.3;

    /// <summary>
    /// Factor for concurrent connections in weight calculation
    /// </summary>
    [Range(0.0, 1.0)]
    public double ConcurrencyFactor { get; set; } = 0.3;

    /// <summary>
    /// Minimum weight multiplier to prevent complete exclusion
    /// </summary>
    [Range(0.1, 1.0)]
    public double MinimumWeightMultiplier { get; set; } = 0.1;

    /// <summary>
    /// Maximum weight multiplier to prevent excessive bias
    /// </summary>
    [Range(1.0, 10.0)]
    public double MaximumWeightMultiplier { get; set; } = 3.0;
}

/// <summary>
/// Sticky session configuration for client affinity
/// </summary>
public class StickySessionOptions
{
    /// <summary>
    /// Enable sticky sessions
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Session duration for client affinity
    /// </summary>
    public TimeSpan SessionDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum number of concurrent sticky sessions
    /// </summary>
    [Range(100, 100000)]
    public int MaxSessions { get; set; } = 10000;
}

/// <summary>
/// Global timeout settings for all connection types
/// </summary>
public class GlobalTimeoutOptions
{
    /// <summary>
    /// Default connection timeout when not specified
    /// </summary>
    public TimeSpan DefaultConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default request timeout when not specified
    /// </summary>
    public TimeSpan DefaultRequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum allowed timeout for any operation
    /// </summary>
    public TimeSpan MaxTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// DNS resolution timeout
    /// </summary>
    public TimeSpan DnsTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Connection metrics collection configuration
/// </summary>
public class ConnectionMetricsOptions
{
    /// <summary>
    /// Enable metrics collection
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Metrics collection interval
    /// </summary>
    public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Metrics retention period
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Enable detailed per-connection metrics
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = false;

    /// <summary>
    /// Maximum number of metrics samples to retain
    /// </summary>
    [Range(100, 100000)]
    public int MaxSamples { get; set; } = 10000;
}

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

    /// <summary>
    /// Per-instance metrics
    /// </summary>
    public Dictionary<string, ConnectionPoolMetrics> InstanceMetrics { get; set; } = new();
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
public class ClientConnectionMetrics
{
    /// <summary>
    /// Connection identifier
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Connection creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last used timestamp
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Total number of requests handled by this connection
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Number of successful requests
    /// </summary>
    public long SuccessfulRequests { get; set; }

    /// <summary>
    /// Number of failed requests
    /// </summary>
    public long FailedRequests { get; set; }

    /// <summary>
    /// Total response time for all requests
    /// </summary>
    public long TotalResponseTime { get; set; }

    /// <summary>
    /// Average response time for requests
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Last health check result
    /// </summary>
    public ConnectionHealthCheckResult? LastHealthCheck { get; set; }

    /// <summary>
    /// Current connection health status
    /// </summary>
    public bool IsHealthy { get; set; }
}

/// <summary>
/// Health check result for connection pools
/// </summary>
public class ConnectionHealthCheckResult
{
    /// <summary>
    /// Whether the health check passed
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Instance identifier
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Health check timestamp
    /// </summary>
    public DateTimeOffset CheckedAt { get; set; }

    /// <summary>
    /// Time taken for the health check
    /// </summary>
    public long ResponseTime { get; set; }

    /// <summary>
    /// Health check message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error information if health check failed
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Simple connection health information
/// </summary>
public class ConnectionHealth
{
    /// <summary>
    /// Instance identifier
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the connection is healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Last time the health was checked
    /// </summary>
    public DateTimeOffset LastChecked { get; set; }

    /// <summary>
    /// Health status message
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Response time of last health check
    /// </summary>
    public long? ResponseTime { get; set; }

    /// <summary>
    /// Error information if unhealthy
    /// </summary>
    public string? Error { get; set; }
}
