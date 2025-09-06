using Castellan.Core.Interfaces.Connection;
using Castellan.Core.Models.Connection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace Castellan.Core.Services.Connection;

/// <summary>
/// HTTP Client Pool Manager for managing multiple named HTTP client pools
/// </summary>
public class HttpClientPoolManager : IHttpClientPool
{
    private readonly ILogger<HttpClientPoolManager> _logger;
    private readonly ConnectionPoolOptions _options;
    private readonly ConcurrentDictionary<string, HttpClientPool> _pools = new();
    private readonly ConcurrentDictionary<string, ConnectionPoolMetrics> _poolMetrics = new();
    private readonly Timer _metricsTimer;
    private readonly Timer _healthCheckTimer;
    private readonly SemaphoreSlim _poolCreationLock = new(1, 1);
    private bool _disposed;

    public HttpClientPoolManager(
        ILogger<HttpClientPoolManager> logger,
        IOptions<ConnectionPoolOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        // Initialize configured pools
        InitializePools();

        // Start background timers
        _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, 
            TimeSpan.FromSeconds(_options.Metrics.CollectionInterval.TotalSeconds));
        
        _healthCheckTimer = new Timer(PerformHealthChecks, null, TimeSpan.Zero,
            TimeSpan.FromSeconds(_options.HealthMonitoring.CheckInterval.TotalSeconds));

        _logger.LogInformation("HTTP Client Pool Manager initialized with {PoolCount} pools", 
            _options.HttpClientPools.Pools.Count);
    }

    public async Task<IPooledHttpClient> GetClientAsync(string? poolName = null, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HttpClientPoolManager));

        var targetPoolName = poolName ?? _options.HttpClientPools.DefaultPool;
        var pool = await GetOrCreatePoolAsync(targetPoolName);

        return await pool.GetClientAsync(cancellationToken);
    }

    public async Task<IPooledHttpClient> GetClientAsync(string? poolName, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HttpClientPoolManager));

        var targetPoolName = poolName ?? _options.HttpClientPools.DefaultPool;
        var pool = await GetOrCreatePoolAsync(targetPoolName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        return await pool.GetClientAsync(cts.Token);
    }

    public IPooledHttpClient? TryGetClient(string? poolName = null)
    {
        if (_disposed) return null;

        var targetPoolName = poolName ?? _options.HttpClientPools.DefaultPool;
        if (!_pools.TryGetValue(targetPoolName, out var pool))
            return null;

        return pool.TryGetClient();
    }

    public async Task ReturnClientAsync(IPooledHttpClient client)
    {
        if (_disposed || client == null) return;

        if (_pools.TryGetValue(client.PoolName, out var pool))
        {
            await pool.ReturnClientAsync(client);
        }
    }

    public async Task<Dictionary<string, ConnectionPoolMetrics>> GetMetricsAsync()
    {
        var metrics = new Dictionary<string, ConnectionPoolMetrics>();

        foreach (var kvp in _pools)
        {
            var poolMetrics = await kvp.Value.GetMetricsAsync();
            metrics[kvp.Key] = poolMetrics;
        }

        return metrics;
    }

    public async Task<ConnectionPoolMetrics?> GetPoolMetricsAsync(string poolName)
    {
        if (!_pools.TryGetValue(poolName, out var pool))
            return null;

        return await pool.GetMetricsAsync();
    }

    public async Task<Dictionary<string, ConnectionPoolHealthStatus>> GetHealthStatusAsync()
    {
        var healthStatuses = new Dictionary<string, ConnectionPoolHealthStatus>();

        foreach (var kvp in _pools)
        {
            var health = await kvp.Value.GetHealthStatusAsync();
            healthStatuses[kvp.Key] = health;
        }

        return healthStatuses;
    }

    public async Task<ConnectionHealthCheckResult> CheckPoolHealthAsync(string poolName, CancellationToken cancellationToken = default)
    {
        if (!_pools.TryGetValue(poolName, out var pool))
        {
            return new ConnectionHealthCheckResult
            {
                ConnectionId = poolName,
                IsHealthy = false,
                ErrorMessage = "Pool not found",
                CheckType = "existence"
            };
        }

        return await pool.CheckHealthAsync(cancellationToken);
    }

    public async Task CreatePoolAsync(string poolName, HttpClientPoolConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentNullException.ThrowIfNull(configuration);

        await _poolCreationLock.WaitAsync();
        try
        {
            if (_pools.ContainsKey(poolName))
            {
                throw new InvalidOperationException($"Pool '{poolName}' already exists");
            }

            var pool = new HttpClientPool(poolName, configuration, _logger);
            _pools[poolName] = pool;

            _logger.LogInformation("Created HTTP client pool '{PoolName}' with {MaxConnections} max connections",
                poolName, configuration.MaxConnections);

            // Initialize metrics for new pool
            _poolMetrics[poolName] = new ConnectionPoolMetrics
            {
                PoolName = poolName,
                PoolType = "HTTP",
                MaxConnections = configuration.MaxConnections
            };
        }
        finally
        {
            _poolCreationLock.Release();
        }
    }

    public async Task RemovePoolAsync(string poolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);

        await _poolCreationLock.WaitAsync();
        try
        {
            if (_pools.TryRemove(poolName, out var pool))
            {
                await pool.DisposeAsync();
                _poolMetrics.TryRemove(poolName, out _);

                _logger.LogInformation("Removed HTTP client pool '{PoolName}'", poolName);
            }
        }
        finally
        {
            _poolCreationLock.Release();
        }
    }

    public IReadOnlyList<string> GetPoolNames()
    {
        return _pools.Keys.ToList().AsReadOnly();
    }

    public async Task WarmUpPoolAsync(string poolName, int? initialConnections = null, CancellationToken cancellationToken = default)
    {
        var pool = await GetOrCreatePoolAsync(poolName);
        await pool.WarmUpAsync(initialConnections, cancellationToken);
    }

    public event EventHandler<PoolHealthChangedEventArgs>? PoolHealthChanged;
    public event EventHandler<PoolMetricsUpdatedEventArgs>? PoolMetricsUpdated;

    private void InitializePools()
    {
        foreach (var kvp in _options.HttpClientPools.Pools)
        {
            var poolName = kvp.Key;
            var configuration = kvp.Value;

            var pool = new HttpClientPool(poolName, configuration, _logger);
            _pools[poolName] = pool;

            // Initialize metrics
            _poolMetrics[poolName] = new ConnectionPoolMetrics
            {
                PoolName = poolName,
                PoolType = "HTTP",
                MaxConnections = configuration.MaxConnections
            };

            _logger.LogDebug("Initialized HTTP client pool '{PoolName}'", poolName);
        }
    }

    private async Task<HttpClientPool> GetOrCreatePoolAsync(string poolName)
    {
        if (_pools.TryGetValue(poolName, out var existingPool))
            return existingPool;

        if (!_options.HttpClientPools.EnableAutoPoolCreation)
        {
            throw new InvalidOperationException($"Pool '{poolName}' does not exist and auto-creation is disabled");
        }

        await _poolCreationLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_pools.TryGetValue(poolName, out var pool))
                return pool;

            // Create with default configuration
            var defaultConfig = new HttpClientPoolConfiguration();
            await CreatePoolAsync(poolName, defaultConfig);

            return _pools[poolName];
        }
        finally
        {
            _poolCreationLock.Release();
        }
    }

    private void CollectMetrics(object? state)
    {
        try
        {
            foreach (var kvp in _pools)
            {
                var poolName = kvp.Key;
                var pool = kvp.Value;

                var metrics = pool.GetMetricsAsync().GetAwaiter().GetResult();
                _poolMetrics[poolName] = metrics;

                // Fire metrics updated event
                PoolMetricsUpdated?.Invoke(this, new PoolMetricsUpdatedEventArgs
                {
                    PoolName = poolName,
                    Metrics = metrics
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting HTTP client pool metrics");
        }
    }

    private void PerformHealthChecks(object? state)
    {
        if (!_options.HealthMonitoring.Enabled)
            return;

        try
        {
            foreach (var kvp in _pools)
            {
                var poolName = kvp.Key;
                var pool = kvp.Value;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var oldStatus = await pool.GetHealthStatusAsync();
                        var healthResult = await pool.CheckHealthAsync(CancellationToken.None);
                        var newStatus = healthResult.IsHealthy ? ConnectionPoolHealthStatus.Healthy : ConnectionPoolHealthStatus.Unhealthy;

                        if (oldStatus != newStatus)
                        {
                            _logger.LogInformation("HTTP client pool '{PoolName}' health changed from {OldStatus} to {NewStatus}",
                                poolName, oldStatus, newStatus);

                            PoolHealthChanged?.Invoke(this, new PoolHealthChangedEventArgs
                            {
                                PoolName = poolName,
                                OldStatus = oldStatus,
                                NewStatus = newStatus,
                                Reason = healthResult.ErrorMessage
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Health check failed for HTTP client pool '{PoolName}'", poolName);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing HTTP client pool health checks");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _metricsTimer?.Dispose();
        _healthCheckTimer?.Dispose();
        _poolCreationLock?.Dispose();

        // Dispose all pools
        foreach (var pool in _pools.Values)
        {
            try
            {
                pool.DisposeAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing HTTP client pool");
            }
        }

        _pools.Clear();
        _poolMetrics.Clear();

        _logger.LogInformation("HTTP Client Pool Manager disposed");
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Individual HTTP client pool implementation
/// </summary>
internal class HttpClientPool : IAsyncDisposable
{
    private readonly string _poolName;
    private readonly HttpClientPoolConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<PooledHttpClient> _availableClients = new();
    private readonly ConcurrentDictionary<string, PooledHttpClient> _allClients = new();
    private readonly SemaphoreSlim _connectionSemaphore;
    private readonly CircuitBreaker _circuitBreaker;
    private ConnectionPoolMetrics _metrics = new();
    private ConnectionPoolHealthStatus _healthStatus = ConnectionPoolHealthStatus.Healthy;
    private bool _disposed;

    public HttpClientPool(string poolName, HttpClientPoolConfiguration configuration, ILogger logger)
    {
        _poolName = poolName;
        _configuration = configuration;
        _logger = logger;
        _connectionSemaphore = new SemaphoreSlim(configuration.MaxConnections, configuration.MaxConnections);
        _circuitBreaker = new CircuitBreaker(configuration.CircuitBreakerThreshold, configuration.CircuitBreakerTimeout, logger);

        _metrics.PoolName = poolName;
        _metrics.PoolType = "HTTP";
        _metrics.MaxConnections = configuration.MaxConnections;
    }

    public async Task<IPooledHttpClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HttpClientPool));

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Check circuit breaker
            if (!_circuitBreaker.CanExecute())
            {
                throw new InvalidOperationException($"Circuit breaker is open for pool '{_poolName}'");
            }

            // Try to get an existing client first
            if (_availableClients.TryDequeue(out var existingClient) && existingClient.IsHealthy)
            {
                var acquisitionTime = DateTimeOffset.UtcNow - startTime;
                UpdateMetrics(acquisitionTime, true);
                return existingClient;
            }

            // Wait for semaphore to create/acquire a client
            await _connectionSemaphore.WaitAsync(cancellationToken);

            try
            {
                // Try again after acquiring semaphore
                if (_availableClients.TryDequeue(out var availableClient) && availableClient.IsHealthy)
                {
                    var acquisitionTime = DateTimeOffset.UtcNow - startTime;
                    UpdateMetrics(acquisitionTime, true);
                    return availableClient;
                }

                // Create new client
                var client = await CreateNewClientAsync(cancellationToken);
                var finalAcquisitionTime = DateTimeOffset.UtcNow - startTime;
                UpdateMetrics(finalAcquisitionTime, true);
                
                _circuitBreaker.RecordSuccess();
                return client;
            }
            catch
            {
                _connectionSemaphore.Release();
                throw;
            }
        }
        catch (Exception ex)
        {
            var acquisitionTime = DateTimeOffset.UtcNow - startTime;
            UpdateMetrics(acquisitionTime, false);
            _circuitBreaker.RecordFailure();
            
            _logger.LogWarning(ex, "Failed to get HTTP client from pool '{PoolName}'", _poolName);
            throw;
        }
    }

    public IPooledHttpClient? TryGetClient()
    {
        if (_disposed || !_circuitBreaker.CanExecute())
            return null;

        if (_availableClients.TryDequeue(out var client) && client.IsHealthy)
        {
            UpdateMetrics(TimeSpan.Zero, true);
            return client;
        }

        return null;
    }

    public async Task ReturnClientAsync(IPooledHttpClient client)
    {
        if (_disposed || client is not PooledHttpClient pooledClient)
            return;

        try
        {
            if (pooledClient.IsHealthy && _allClients.ContainsKey(pooledClient.ClientId))
            {
                _availableClients.Enqueue(pooledClient);
            }
            else
            {
                // Remove unhealthy client
                _allClients.TryRemove(pooledClient.ClientId, out _);
                await pooledClient.DisposeAsync();
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async Task<ConnectionPoolMetrics> GetMetricsAsync()
    {
        _metrics.Timestamp = DateTimeOffset.UtcNow;
        _metrics.ActiveConnections = _allClients.Count - _availableClients.Count;
        _metrics.TotalConnections = _allClients.Count;
        _metrics.HealthStatus = _healthStatus;

        return _metrics;
    }

    public async Task<ConnectionPoolHealthStatus> GetHealthStatusAsync()
    {
        return _healthStatus;
    }

    public async Task<ConnectionHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = new ConnectionHealthCheckResult
        {
            ConnectionId = _poolName,
            CheckType = "pool",
            IsHealthy = true
        };

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Check circuit breaker state
            if (!_circuitBreaker.CanExecute())
            {
                result.IsHealthy = false;
                result.ErrorMessage = "Circuit breaker is open";
                result.Details["circuit_breaker_state"] = _circuitBreaker.State.ToString();
            }

            // Check pool utilization
            var utilization = _metrics.UtilizationPercentage;
            result.Details["utilization_percentage"] = utilization;
            result.Details["active_connections"] = _metrics.ActiveConnections;
            result.Details["total_connections"] = _metrics.TotalConnections;

            if (utilization > 90)
            {
                result.IsHealthy = false;
                result.ErrorMessage = "Pool utilization too high";
            }

            result.ResponseTime = DateTimeOffset.UtcNow - startTime;

            // Update health status based on result
            _healthStatus = result.IsHealthy ? ConnectionPoolHealthStatus.Healthy : ConnectionPoolHealthStatus.Unhealthy;
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTime = DateTimeOffset.UtcNow - startTime;
            _healthStatus = ConnectionPoolHealthStatus.Unhealthy;
        }

        return result;
    }

    public async Task WarmUpAsync(int? initialConnections = null, CancellationToken cancellationToken = default)
    {
        var connectionsToCreate = initialConnections ?? Math.Min(5, _configuration.MaxConnections / 2);

        for (int i = 0; i < connectionsToCreate && !cancellationToken.IsCancellationRequested; i++)
        {
            try
            {
                var client = await CreateNewClientAsync(cancellationToken);
                _availableClients.Enqueue((PooledHttpClient)client);
                _connectionSemaphore.Release(); // Release since we're not using it immediately
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create warm-up connection {Index} for pool '{PoolName}'", i + 1, _poolName);
            }
        }

        _logger.LogInformation("Warmed up pool '{PoolName}' with {CreatedConnections} connections", 
            _poolName, Math.Min(connectionsToCreate, _availableClients.Count));
    }

    private async Task<PooledHttpClient> CreateNewClientAsync(CancellationToken cancellationToken)
    {
        var httpClient = CreateHttpClient();
        var clientId = Guid.NewGuid().ToString();
        
        var pooledClient = new PooledHttpClient(
            httpClient,
            _poolName,
            clientId,
            _configuration,
            _logger);

        _allClients[clientId] = pooledClient;

        _logger.LogDebug("Created new HTTP client '{ClientId}' for pool '{PoolName}'", clientId, _poolName);

        return pooledClient;
    }

    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler();
        
        if (_configuration.EnableCompression)
        {
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        }

        var httpClient = new HttpClient(handler)
        {
            Timeout = _configuration.RequestTimeout
        };

        // Set default headers
        foreach (var header in _configuration.DefaultHeaders)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        return httpClient;
    }

    private void UpdateMetrics(TimeSpan acquisitionTime, bool success)
    {
        _metrics.TotalRequests++;
        
        if (success)
        {
            _metrics.SuccessfulAcquisitions++;
        }
        else
        {
            _metrics.FailedAcquisitions++;
        }

        // Update average acquisition time (simple moving average)
        var totalTime = (_metrics.AverageAcquisitionTime.TotalMilliseconds * (_metrics.TotalRequests - 1)) + acquisitionTime.TotalMilliseconds;
        _metrics.AverageAcquisitionTime = TimeSpan.FromMilliseconds(totalTime / _metrics.TotalRequests);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _connectionSemaphore?.Dispose();

        // Dispose all clients
        foreach (var client in _allClients.Values)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing HTTP client in pool '{PoolName}'", _poolName);
            }
        }

        _allClients.Clear();
        
        // Clear the queue
        while (_availableClients.TryDequeue(out _)) { }

        _logger.LogDebug("Disposed HTTP client pool '{PoolName}'", _poolName);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Simple circuit breaker implementation for HTTP client pools
/// </summary>
internal class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private readonly ILogger _logger;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private readonly object _lock = new();

    public CircuitBreakerState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public CircuitBreaker(int failureThreshold, TimeSpan timeout, ILogger logger)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout;
        _logger = logger;
    }

    public bool CanExecute()
    {
        lock (_lock)
        {
            if (_state == CircuitBreakerState.Closed)
                return true;

            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime >= _timeout)
                {
                    _state = CircuitBreakerState.HalfOpen;
                    _logger.LogInformation("Circuit breaker moved to Half-Open state");
                    return true;
                }
                return false;
            }

            // Half-open state
            return true;
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            
            if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Closed;
                _logger.LogInformation("Circuit breaker moved to Closed state");
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_failureCount >= _failureThreshold && _state != CircuitBreakerState.Open)
            {
                _state = CircuitBreakerState.Open;
                _logger.LogWarning("Circuit breaker moved to Open state after {FailureCount} failures", _failureCount);
            }
        }
    }
}
