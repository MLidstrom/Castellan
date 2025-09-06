using System.ComponentModel.DataAnnotations;

namespace Castellan.Core.Models.Connection;

/// <summary>
/// Root configuration for all connection pooling options
/// </summary>
public class ConnectionPoolOptions
{
    /// <summary>
    /// HTTP client pool configurations
    /// </summary>
    public HttpClientPoolOptions HttpClientPools { get; set; } = new();

    /// <summary>
    /// Qdrant connection pool configuration
    /// </summary>
    public QdrantPoolOptions QdrantPool { get; set; } = new();

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
/// HTTP client pool configuration with named pools
/// </summary>
public class HttpClientPoolOptions
{
    /// <summary>
    /// Named HTTP client pool configurations
    /// </summary>
    public Dictionary<string, HttpClientPoolConfiguration> Pools { get; set; } = new()
    {
        ["Default"] = new HttpClientPoolConfiguration(),
        ["LLM"] = new HttpClientPoolConfiguration
        {
            MaxConnections = 20,
            ConnectionTimeout = TimeSpan.FromMinutes(1),
            RequestTimeout = TimeSpan.FromMinutes(5),
            MaxRetries = 2
        },
        ["IPEnrichment"] = new HttpClientPoolConfiguration
        {
            MaxConnections = 50,
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            RequestTimeout = TimeSpan.FromMinutes(1),
            MaxRetries = 3
        }
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
