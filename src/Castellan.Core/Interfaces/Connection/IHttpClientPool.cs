using Castellan.Core.Models.Connection;

namespace Castellan.Core.Interfaces.Connection;

/// <summary>
/// Interface for managing pooled HTTP clients across multiple named pools
/// </summary>
public interface IHttpClientPool : IDisposable
{
    /// <summary>
    /// Get an HTTP client from the specified pool
    /// </summary>
    /// <param name="poolName">Name of the pool to get client from (uses default if null)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pooled HTTP client wrapper</returns>
    Task<IPooledHttpClient> GetClientAsync(string? poolName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get an HTTP client from the specified pool with timeout
    /// </summary>
    /// <param name="poolName">Name of the pool to get client from</param>
    /// <param name="timeout">Maximum time to wait for available client</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pooled HTTP client wrapper</returns>
    Task<IPooledHttpClient> GetClientAsync(string? poolName, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Try to get an HTTP client from the specified pool without waiting
    /// </summary>
    /// <param name="poolName">Name of the pool to get client from</param>
    /// <returns>Pooled HTTP client wrapper if immediately available, null otherwise</returns>
    IPooledHttpClient? TryGetClient(string? poolName = null);

    /// <summary>
    /// Return a client to its pool
    /// </summary>
    /// <param name="client">Client to return to pool</param>
    Task ReturnClientAsync(IPooledHttpClient client);

    /// <summary>
    /// Get metrics for all pools
    /// </summary>
    /// <returns>Dictionary of pool metrics by pool name</returns>
    Task<Dictionary<string, ConnectionPoolMetrics>> GetMetricsAsync();

    /// <summary>
    /// Get metrics for a specific pool
    /// </summary>
    /// <param name="poolName">Name of the pool</param>
    /// <returns>Pool metrics</returns>
    Task<ConnectionPoolMetrics?> GetPoolMetricsAsync(string poolName);

    /// <summary>
    /// Get health status for all pools
    /// </summary>
    /// <returns>Dictionary of health status by pool name</returns>
    Task<Dictionary<string, ConnectionPoolHealthStatus>> GetHealthStatusAsync();

    /// <summary>
    /// Perform health check on a specific pool
    /// </summary>
    /// <param name="poolName">Name of the pool to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    Task<ConnectionHealthCheckResult> CheckPoolHealthAsync(string poolName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new named pool with specified configuration
    /// </summary>
    /// <param name="poolName">Name for the new pool</param>
    /// <param name="configuration">Pool configuration</param>
    Task CreatePoolAsync(string poolName, HttpClientPoolConfiguration configuration);

    /// <summary>
    /// Remove a named pool and dispose all its connections
    /// </summary>
    /// <param name="poolName">Name of the pool to remove</param>
    Task RemovePoolAsync(string poolName);

    /// <summary>
    /// Get list of all pool names
    /// </summary>
    /// <returns>List of pool names</returns>
    IReadOnlyList<string> GetPoolNames();

    /// <summary>
    /// Warm up a pool by creating initial connections
    /// </summary>
    /// <param name="poolName">Name of the pool to warm up</param>
    /// <param name="initialConnections">Number of connections to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WarmUpPoolAsync(string poolName, int? initialConnections = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when pool health status changes
    /// </summary>
    event EventHandler<PoolHealthChangedEventArgs>? PoolHealthChanged;

    /// <summary>
    /// Event fired when pool metrics are updated
    /// </summary>
    event EventHandler<PoolMetricsUpdatedEventArgs>? PoolMetricsUpdated;
}

/// <summary>
/// Interface for individual pooled HTTP client with additional functionality
/// </summary>
public interface IPooledHttpClient : IDisposable
{
    /// <summary>
    /// Underlying HttpClient instance
    /// </summary>
    HttpClient HttpClient { get; }

    /// <summary>
    /// Pool this client belongs to
    /// </summary>
    string PoolName { get; }

    /// <summary>
    /// Unique identifier for this client instance
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// When this client was created
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When this client was last used
    /// </summary>
    DateTimeOffset LastUsedAt { get; }

    /// <summary>
    /// Number of requests made with this client
    /// </summary>
    long RequestCount { get; }

    /// <summary>
    /// Whether this client is currently healthy
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    /// Send HTTP request with automatic retry and circuit breaker logic
    /// </summary>
    /// <param name="request">HTTP request message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response message</returns>
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send HTTP request with additional pooling options
    /// </summary>
    /// <param name="request">HTTP request message</param>
    /// <param name="options">Request-specific options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HTTP response message</returns>
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, PooledRequestOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get performance metrics for this client
    /// </summary>
    /// <returns>Client performance metrics</returns>
    ConnectionPerformanceMetrics GetMetrics();

    /// <summary>
    /// Perform health check on this client
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    Task<ConnectionHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset client statistics and error counters
    /// </summary>
    void ResetStatistics();

    /// <summary>
    /// Mark client as unhealthy (will be excluded from pool until recovered)
    /// </summary>
    /// <param name="reason">Reason for marking unhealthy</param>
    void MarkUnhealthy(string reason);

    /// <summary>
    /// Mark client as healthy (will be included in pool rotation)
    /// </summary>
    void MarkHealthy();
}

/// <summary>
/// Request-specific options for pooled HTTP clients
/// </summary>
public class PooledRequestOptions
{
    /// <summary>
    /// Override default retry count for this request
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Override default timeout for this request
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Whether to bypass circuit breaker for this request
    /// </summary>
    public bool BypassCircuitBreaker { get; set; } = false;

    /// <summary>
    /// Custom headers for this request only
    /// </summary>
    public Dictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Enable/disable compression for this request
    /// </summary>
    public bool? EnableCompression { get; set; }

    /// <summary>
    /// Priority for this request (affects retry behavior)
    /// </summary>
    public RequestPriority Priority { get; set; } = RequestPriority.Normal;
}

/// <summary>
/// Request priority enumeration
/// </summary>
public enum RequestPriority
{
    /// <summary>
    /// Low priority request (fewer retries, longer backoff)
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority request (standard retry behavior)
    /// </summary>
    Normal,

    /// <summary>
    /// High priority request (more retries, shorter backoff)
    /// </summary>
    High,

    /// <summary>
    /// Critical request (maximum retries, minimal backoff)
    /// </summary>
    Critical
}

/// <summary>
/// Event arguments for pool health changes
/// </summary>
public class PoolHealthChangedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the pool that changed
    /// </summary>
    public string PoolName { get; set; } = string.Empty;

    /// <summary>
    /// Previous health status
    /// </summary>
    public ConnectionPoolHealthStatus OldStatus { get; set; }

    /// <summary>
    /// New health status
    /// </summary>
    public ConnectionPoolHealthStatus NewStatus { get; set; }

    /// <summary>
    /// Reason for the health change
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When the change occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Event arguments for pool metrics updates
/// </summary>
public class PoolMetricsUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the pool that was updated
    /// </summary>
    public string PoolName { get; set; } = string.Empty;

    /// <summary>
    /// Updated metrics
    /// </summary>
    public ConnectionPoolMetrics Metrics { get; set; } = new();

    /// <summary>
    /// When the metrics were updated
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
