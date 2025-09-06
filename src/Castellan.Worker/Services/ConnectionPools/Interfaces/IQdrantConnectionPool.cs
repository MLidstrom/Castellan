using Qdrant.Client;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services.ConnectionPools.Interfaces;

/// <summary>
/// Interface for managing pooled Qdrant connections with multi-instance support and load balancing.
/// </summary>
public interface IQdrantConnectionPool : IDisposable
{
    /// <summary>
    /// Gets a pooled Qdrant client from the connection pool.
    /// Implements load balancing and automatic failover across multiple instances.
    /// </summary>
    /// <param name="preferredInstance">Optional preferred instance identifier for sticky sessions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A pooled Qdrant client wrapper</returns>
    Task<IQdrantPooledClient> GetClientAsync(string? preferredInstance = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current health status of all Qdrant instances in the pool.
    /// </summary>
    /// <returns>Health status for each configured instance</returns>
    Task<IReadOnlyDictionary<string, ConnectionPoolHealthStatus>> GetHealthStatusAsync();

    /// <summary>
    /// Gets current metrics for all Qdrant connections and instances.
    /// </summary>
    /// <returns>Connection pool metrics</returns>
    ConnectionPoolMetrics GetMetrics();

    /// <summary>
    /// Gets the list of available Qdrant instance identifiers.
    /// </summary>
    /// <returns>Collection of instance identifiers</returns>
    IReadOnlyCollection<string> GetAvailableInstances();

    /// <summary>
    /// Manually marks an instance as healthy or unhealthy.
    /// Used for manual intervention or external health checks.
    /// </summary>
    /// <param name="instanceId">Instance identifier</param>
    /// <param name="status">Health status to set</param>
    void SetInstanceHealth(string instanceId, ConnectionPoolHealthStatus status);
}

/// <summary>
/// Interface for a pooled Qdrant client that provides enhanced functionality over the base QdrantClient.
/// </summary>
public interface IQdrantPooledClient : IDisposable
{
    /// <summary>
    /// The underlying Qdrant client instance.
    /// </summary>
    QdrantClient Client { get; }

    /// <summary>
    /// The instance identifier this client is connected to.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// Indicates whether this client is currently healthy and available for use.
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    /// Gets the current metrics for this specific client connection.
    /// </summary>
    ClientConnectionMetrics Metrics { get; }

    /// <summary>
    /// Executes a function with the Qdrant client, including retry logic and circuit breaker protection.
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="operation">Function to execute with the client</param>
    /// <param name="operationType">Type of operation for metrics tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the operation</returns>
    Task<T> ExecuteAsync<T>(
        Func<QdrantClient, CancellationToken, Task<T>> operation,
        string operationType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action with the Qdrant client, including retry logic and circuit breaker protection.
    /// </summary>
    /// <param name="operation">Action to execute with the client</param>
    /// <param name="operationType">Type of operation for metrics tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExecuteAsync(
        Func<QdrantClient, CancellationToken, Task> operation,
        string operationType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check on the underlying Qdrant connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    Task<ConnectionHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default);
}
