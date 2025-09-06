using Castellan.Pipeline.Services.ConnectionPools.Interfaces;
using Qdrant.Client;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Castellan.Pipeline.Services.ConnectionPools;

/// <summary>
/// A pooled Qdrant client that provides enhanced functionality including retry logic,
/// circuit breaker protection, and metrics collection.
/// </summary>
internal sealed class QdrantPooledClient : IQdrantPooledClient
{
    private readonly ILogger<QdrantPooledClient> _logger;
    private readonly QdrantClient _client;
    private readonly string _instanceId;
    private readonly ConnectionPoolOptions _options;
    private readonly SimpleCircuitBreaker _circuitBreaker;
    private readonly ClientConnectionMetrics _metrics;
    private readonly object _lockObject = new();
    private bool _disposed;

    public QdrantPooledClient(
        QdrantClient client,
        string instanceId,
        ConnectionPoolOptions options,
        ILogger<QdrantPooledClient> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _circuitBreaker = new SimpleCircuitBreaker(
            options.CircuitBreakerFailureThreshold,
            TimeSpan.FromMilliseconds(options.CircuitBreakerTimeoutMs),
            TimeSpan.FromMilliseconds(options.CircuitBreakerRetryTimeoutMs));

        _metrics = new ClientConnectionMetrics
        {
            InstanceId = instanceId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _logger.LogDebug("Created QdrantPooledClient for instance {InstanceId}", instanceId);
    }

    public QdrantClient Client => _client;
    public string InstanceId => _instanceId;
    public bool IsHealthy => _circuitBreaker.State == CircuitBreakerState.Closed;
    public ClientConnectionMetrics Metrics
    {
        get
        {
            lock (_lockObject)
            {
                return new ClientConnectionMetrics
                {
                    InstanceId = _metrics.InstanceId,
                    CreatedAt = _metrics.CreatedAt,
                    LastUsedAt = _metrics.LastUsedAt,
                    TotalRequests = _metrics.TotalRequests,
                    SuccessfulRequests = _metrics.SuccessfulRequests,
                    FailedRequests = _metrics.FailedRequests,
                    TotalResponseTime = _metrics.TotalResponseTime,
                    AverageResponseTime = _metrics.AverageResponseTime,
                    LastError = _metrics.LastError,
                    LastHealthCheck = _metrics.LastHealthCheck,
                    IsHealthy = IsHealthy
                };
            }
        }
    }

    public async Task<T> ExecuteAsync<T>(
        Func<QdrantClient, CancellationToken, Task<T>> operation,
        string operationType,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_circuitBreaker.State == CircuitBreakerState.Open)
        {
            _logger.LogWarning("Circuit breaker is open for Qdrant instance {InstanceId}", _instanceId);
            throw new InvalidOperationException($"Circuit breaker is open for Qdrant instance {_instanceId}");
        }

        var retryCount = 0;
        var maxRetries = _options.MaxRetryAttempts;
        var baseDelayMs = _options.RetryDelayMs;

        while (retryCount <= maxRetries)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                UpdateMetricsBeforeRequest();

                var result = await operation(_client, cancellationToken);
                
                stopwatch.Stop();
                UpdateMetricsAfterSuccess(stopwatch.ElapsedMilliseconds);
                _circuitBreaker.RecordSuccess();

                _logger.LogDebug(
                    "Qdrant operation {OperationType} completed successfully for instance {InstanceId} in {ElapsedMs}ms",
                    operationType, _instanceId, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex) when (retryCount < maxRetries && IsRetriableException(ex))
            {
                stopwatch.Stop();
                retryCount++;

                var delay = CalculateRetryDelay(retryCount, baseDelayMs);
                
                _logger.LogWarning(
                    "Qdrant operation {OperationType} failed for instance {InstanceId} (attempt {Attempt}/{MaxAttempts}). Retrying in {DelayMs}ms. Error: {Error}",
                    operationType, _instanceId, retryCount, maxRetries + 1, delay, ex.Message);

                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                UpdateMetricsAfterFailure(stopwatch.ElapsedMilliseconds, ex);
                _circuitBreaker.RecordFailure();

                _logger.LogError(ex,
                    "Qdrant operation {OperationType} failed for instance {InstanceId} after {Attempts} attempts",
                    operationType, _instanceId, retryCount + 1);

                throw;
            }
        }

        throw new InvalidOperationException("This should never be reached");
    }

    public async Task ExecuteAsync(
        Func<QdrantClient, CancellationToken, Task> operation,
        string operationType,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async (client, ct) =>
        {
            await operation(client, ct);
            return true; // Convert void operation to func returning dummy value
        }, operationType, cancellationToken);
    }

    public async Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var stopwatch = Stopwatch.StartNew();
        var healthCheckStart = DateTimeOffset.UtcNow;

        try
        {
            // Perform a simple health check by trying to get collection info
            // This is a lightweight operation that verifies connectivity and basic functionality
            await _client.ListCollectionsAsync(cancellationToken);

            stopwatch.Stop();
            var responseTime = stopwatch.ElapsedMilliseconds;

            var result = new HealthCheckResult
            {
                IsHealthy = true,
                InstanceId = _instanceId,
                CheckedAt = healthCheckStart,
                ResponseTime = responseTime,
                Message = $"Health check successful in {responseTime}ms"
            };

            lock (_lockObject)
            {
                _metrics.LastHealthCheck = result;
            }

            _logger.LogDebug("Health check successful for Qdrant instance {InstanceId} in {ResponseTime}ms",
                _instanceId, responseTime);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var responseTime = stopwatch.ElapsedMilliseconds;

            var result = new HealthCheckResult
            {
                IsHealthy = false,
                InstanceId = _instanceId,
                CheckedAt = healthCheckStart,
                ResponseTime = responseTime,
                Message = $"Health check failed: {ex.Message}",
                Error = ex.ToString()
            };

            lock (_lockObject)
            {
                _metrics.LastHealthCheck = result;
            }

            _logger.LogWarning(ex, "Health check failed for Qdrant instance {InstanceId}", _instanceId);

            return result;
        }
    }

    private void UpdateMetricsBeforeRequest()
    {
        lock (_lockObject)
        {
            _metrics.TotalRequests++;
            _metrics.LastUsedAt = DateTimeOffset.UtcNow;
        }
    }

    private void UpdateMetricsAfterSuccess(long responseTimeMs)
    {
        lock (_lockObject)
        {
            _metrics.SuccessfulRequests++;
            _metrics.TotalResponseTime += responseTimeMs;
            _metrics.AverageResponseTime = _metrics.TotalResponseTime / _metrics.TotalRequests;
        }
    }

    private void UpdateMetricsAfterFailure(long responseTimeMs, Exception exception)
    {
        lock (_lockObject)
        {
            _metrics.FailedRequests++;
            _metrics.TotalResponseTime += responseTimeMs;
            _metrics.AverageResponseTime = _metrics.TotalResponseTime / _metrics.TotalRequests;
            _metrics.LastError = exception.Message;
        }
    }

    private static bool IsRetriableException(Exception exception)
    {
        // Determine if an exception is retriable
        return exception switch
        {
            TimeoutException => true,
            TaskCanceledException => false, // Don't retry if operation was explicitly cancelled
            OperationCanceledException => false, // Don't retry if operation was explicitly cancelled
            HttpRequestException => true,
            _ => exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                 exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                 exception.Message.Contains("network", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static int CalculateRetryDelay(int retryAttempt, int baseDelayMs)
    {
        // Exponential backoff with jitter
        var exponentialDelay = baseDelayMs * Math.Pow(2, retryAttempt - 1);
        var jitter = Random.Shared.Next(0, baseDelayMs / 4); // Add up to 25% jitter
        return Math.Min((int)exponentialDelay + jitter, 30000); // Cap at 30 seconds
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(QdrantPooledClient));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogDebug("Disposing QdrantPooledClient for instance {InstanceId}", _instanceId);

        _disposed = true;
        
        // Note: We don't dispose the underlying QdrantClient here because it's managed by the connection pool
        // The pool will handle the lifecycle of the actual client instances
    }
}
