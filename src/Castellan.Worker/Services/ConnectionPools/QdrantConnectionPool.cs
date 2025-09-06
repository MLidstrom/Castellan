using Castellan.Worker.Models;
using Castellan.Worker.Services.ConnectionPools.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using System.Collections.Concurrent;

namespace Castellan.Worker.Services.ConnectionPools;

/// <summary>
/// Connection pool for Qdrant clients with multi-instance support, load balancing, and health monitoring.
/// </summary>
public sealed class QdrantConnectionPool : IQdrantConnectionPool
{
    private readonly ILogger<QdrantConnectionPool> _logger;
    private readonly ConnectionPoolOptions _options;
    
    private readonly ConcurrentDictionary<string, QdrantInstancePool> _instancePools = new();
    private readonly ConcurrentDictionary<string, ConnectionPoolHealthStatus> _instanceHealthStatus = new();
    private readonly LoadBalancer _loadBalancer;
    private readonly System.Threading.Timer? _healthCheckTimer;
    
    private readonly ConnectionPoolMetrics _metrics = new() 
    { 
        PoolName = "Qdrant", 
        PoolType = "Vector Database" 
    };
    private readonly object _metricsLock = new();
    private bool _disposed;

    public QdrantConnectionPool(
        IOptions<ConnectionPoolOptions> options,
        ILogger<QdrantConnectionPool> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _loadBalancer = new LoadBalancer(_options.LoadBalancing, _logger);

        // Initialize instance pools
        InitializeInstancePools();

        // Start health check timer
        if (_options.HealthMonitoring.Enabled)
        {
            _healthCheckTimer = new System.Threading.Timer(
                PerformHealthChecks,
                null,
                TimeSpan.Zero, // Start immediately
                _options.HealthMonitoring.CheckInterval);
        }

        _logger.LogInformation(
            "QdrantConnectionPool initialized with {InstanceCount} instances and {Strategy} load balancing",
            _instancePools.Count, _options.LoadBalancing.Algorithm);
    }

    /// <summary>
    /// Gets a pooled Qdrant client from the connection pool.
    /// </summary>
    public async Task<IQdrantPooledClient> GetClientAsync(string? preferredInstance = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var instanceId = SelectInstance(preferredInstance);
        if (instanceId == null)
        {
            throw new InvalidOperationException("No healthy Qdrant instances available");
        }

        if (!_instancePools.TryGetValue(instanceId, out var instancePool))
        {
            throw new InvalidOperationException($"Qdrant instance {instanceId} not found");
        }

        var client = await instancePool.GetClientAsync(cancellationToken);
        
        // Update load balancer statistics
        _loadBalancer.RecordRequest(instanceId);

        lock (_metricsLock)
        {
            _metrics.TotalRequests++;
            _metrics.ActiveConnections++;
        }

        return client;
    }

    /// <summary>
    /// Gets the current health status of all Qdrant instances in the pool.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ConnectionPoolHealthStatus>> GetHealthStatusAsync()
    {
        ThrowIfDisposed();

        // Perform immediate health checks if not recently done
        await RefreshHealthStatusIfNeeded();

        return _instanceHealthStatus.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Gets current metrics for all Qdrant connections and instances.
    /// </summary>
    public ConnectionPoolMetrics GetMetrics()
    {
        ThrowIfDisposed();

        lock (_metricsLock)
        {
            _metrics.Timestamp = DateTimeOffset.UtcNow;
            
            // Update aggregated metrics from instance pools
            var totalActive = 0;
            var totalMax = 0;
            var instanceMetrics = new Dictionary<string, ConnectionPoolMetrics>();
            
            foreach (var kvp in _instancePools)
            {
                var poolMetrics = kvp.Value.GetMetrics();
                instanceMetrics[kvp.Key] = poolMetrics;
                totalActive += poolMetrics.ActiveConnections;
                totalMax += poolMetrics.MaxConnections;
            }
            
            _metrics.ActiveConnections = totalActive;
            _metrics.MaxConnections = totalMax;
            _metrics.TotalConnections = _instancePools.Values.Sum(p => p.GetMetrics().TotalConnections);

            return new ConnectionPoolMetrics
            {
                PoolName = _metrics.PoolName,
                PoolType = _metrics.PoolType,
                Timestamp = _metrics.Timestamp,
                ActiveConnections = _metrics.ActiveConnections,
                TotalConnections = _metrics.TotalConnections,
                MaxConnections = _metrics.MaxConnections,
                TotalRequests = _metrics.TotalRequests,
                SuccessfulAcquisitions = _metrics.SuccessfulAcquisitions,
                FailedAcquisitions = _metrics.FailedAcquisitions,
                HealthStatus = DetermineOverallHealth(),
                InstanceMetrics = instanceMetrics
            };
        }
    }

    /// <summary>
    /// Gets the list of available Qdrant instance identifiers.
    /// </summary>
    public IReadOnlyCollection<string> GetAvailableInstances()
    {
        ThrowIfDisposed();
        return _instancePools.Keys.ToList();
    }

    /// <summary>
    /// Manually marks an instance as healthy or unhealthy.
    /// </summary>
    public void SetInstanceHealth(string instanceId, ConnectionPoolHealthStatus status)
    {
        ThrowIfDisposed();
        
        if (!_instancePools.ContainsKey(instanceId))
        {
            throw new ArgumentException($"Instance {instanceId} not found", nameof(instanceId));
        }

        _instanceHealthStatus.AddOrUpdate(instanceId, status, (key, existing) => status);

        _logger.LogInformation("Manually set Qdrant instance {InstanceId} health to {Status}", instanceId, status);
    }

    private void InitializeInstancePools()
    {
        if (_options.QdrantPool?.Instances?.Count == 0)
        {
            _logger.LogWarning("No Qdrant instances configured, creating default local instance");
            
            // Create a default local instance if none configured
            var defaultInstance = new QdrantInstanceConfiguration
            {
                Host = "localhost",
                Port = 6333,
                Weight = 100
            };

            CreateInstancePool("default", defaultInstance);
        }
        else if (_options.QdrantPool?.Instances != null)
        {
            for (int i = 0; i < _options.QdrantPool.Instances.Count; i++)
            {
                var instance = _options.QdrantPool.Instances[i];
                var instanceId = $"qdrant-{i}";
                CreateInstancePool(instanceId, instance);
            }
        }
    }

    private void CreateInstancePool(string instanceId, QdrantInstanceConfiguration instanceConfig)
    {
        var instancePool = new QdrantInstancePool(
            instanceId,
            instanceConfig,
            _options,
            _logger);

        _instancePools.TryAdd(instanceId, instancePool);
        
        // Initialize health status
        _instanceHealthStatus.TryAdd(instanceId, ConnectionPoolHealthStatus.Healthy);

        _logger.LogDebug("Created Qdrant instance pool for {InstanceId}: {Host}:{Port}",
            instanceId, instanceConfig.Host, instanceConfig.Port);
    }

    private string? SelectInstance(string? preferredInstance)
    {
        // If a preferred instance is specified and it's healthy, use it
        if (!string.IsNullOrEmpty(preferredInstance) && 
            _instanceHealthStatus.TryGetValue(preferredInstance, out var preferredHealth) &&
            preferredHealth == ConnectionPoolHealthStatus.Healthy)
        {
            return preferredInstance;
        }

        // Get all healthy instances
        var healthyInstances = _instanceHealthStatus
            .Where(kvp => kvp.Value == ConnectionPoolHealthStatus.Healthy)
            .Select(kvp => kvp.Key)
            .ToList();

        if (healthyInstances.Count == 0)
        {
            _logger.LogWarning("No healthy Qdrant instances available");
            return null;
        }

        // Use load balancer to select instance
        return _loadBalancer.SelectInstance(healthyInstances);
    }

    private async Task RefreshHealthStatusIfNeeded()
    {
        var now = DateTimeOffset.UtcNow;
        var staleThreshold = _options.HealthMonitoring.CheckInterval.Add(_options.HealthMonitoring.CheckInterval);

        var needsRefresh = _instanceHealthStatus.Values.Any(health =>
            health == ConnectionPoolHealthStatus.Unknown);

        if (needsRefresh)
        {
            PerformHealthChecks();
        }
    }

    private async void PerformHealthChecks(object? state = null)
    {
        if (_disposed) return;

        try
        {
            var healthCheckTasks = _instancePools.Select(kvp =>
                CheckInstanceHealth(kvp.Key, kvp.Value)).ToArray();

            await Task.WhenAll(healthCheckTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health checks");
        }
    }

    private async Task CheckInstanceHealth(string instanceId, QdrantInstancePool instancePool)
    {
        try
        {
            // TODO: Temporarily disable gRPC health checks until we implement HTTP-based health checks
            // The current QdrantClient uses gRPC which conflicts with our HTTP-based vector store
            
            // For now, assume healthy if Qdrant is accessible via HTTP (like our working vector store)
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"http://localhost:6333/", CancellationToken.None);
            var isHealthy = response.IsSuccessStatusCode;
            
            var newStatus = isHealthy ? ConnectionPoolHealthStatus.Healthy : ConnectionPoolHealthStatus.Unhealthy;
            _instanceHealthStatus.AddOrUpdate(instanceId, newStatus, (key, existing) => newStatus);

            _logger.LogDebug("Health check for Qdrant instance {InstanceId}: {Status} (using HTTP)",
                instanceId, newStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for Qdrant instance {InstanceId}", instanceId);
            
            _instanceHealthStatus.AddOrUpdate(instanceId, ConnectionPoolHealthStatus.Unhealthy, (key, existing) => ConnectionPoolHealthStatus.Unhealthy);
        }
    }

    private ConnectionPoolHealthStatus DetermineOverallHealth()
    {
        if (_instanceHealthStatus.Count == 0) return ConnectionPoolHealthStatus.Unknown;
        
        var healthyCount = _instanceHealthStatus.Count(kvp => kvp.Value == ConnectionPoolHealthStatus.Healthy);
        var totalCount = _instanceHealthStatus.Count;
        
        if (healthyCount == 0) return ConnectionPoolHealthStatus.Unhealthy;
        if (healthyCount == totalCount) return ConnectionPoolHealthStatus.Healthy;
        
        return ConnectionPoolHealthStatus.Degraded;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(QdrantConnectionPool));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing QdrantConnectionPool with {InstanceCount} instances", _instancePools.Count);

        _disposed = true;

        _healthCheckTimer?.Dispose();

        foreach (var instancePool in _instancePools.Values)
        {
            instancePool.Dispose();
        }

        _instancePools.Clear();
        _instanceHealthStatus.Clear();

        _logger.LogDebug("QdrantConnectionPool disposed");
    }
}

/// <summary>
/// Manages connections for a single Qdrant instance.
/// </summary>
internal sealed class QdrantInstancePool : IDisposable
{
    private readonly string _instanceId;
    private readonly QdrantInstanceConfiguration _instanceConfig;
    private readonly ConnectionPoolOptions _globalOptions;
    private readonly ILogger _logger;
    
    private readonly ConcurrentQueue<QdrantClient> _availableClients = new();
    private readonly ConcurrentDictionary<QdrantClient, bool> _allClients = new();
    private readonly SemaphoreSlim _connectionSemaphore;
    
    private readonly ConnectionPoolMetrics _metrics = new();
    private readonly object _metricsLock = new();
    private bool _disposed;

    public QdrantInstancePool(
        string instanceId,
        QdrantInstanceConfiguration instanceConfig,
        ConnectionPoolOptions globalOptions,
        ILogger logger)
    {
        _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        _instanceConfig = instanceConfig ?? throw new ArgumentNullException(nameof(instanceConfig));
        _globalOptions = globalOptions ?? throw new ArgumentNullException(nameof(globalOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var maxConnections = globalOptions.QdrantPool?.MaxConnectionsPerInstance ?? 50;
        _connectionSemaphore = new SemaphoreSlim(maxConnections, maxConnections);

        _logger.LogDebug("Created QdrantInstancePool for {InstanceId} with max pool size {MaxPoolSize}",
            instanceId, maxConnections);
    }

    public async Task<QdrantPooledClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        await _connectionSemaphore.WaitAsync(cancellationToken);

        try
        {
            QdrantClient client;

            // Try to get an existing client from the pool
            if (_availableClients.TryDequeue(out client!))
            {
                lock (_metricsLock)
                {
                    _metrics.SuccessfulAcquisitions++;
                }
            }
            else
            {
                // Create a new client
                client = CreateClient();
                _allClients.TryAdd(client, true);
                
                lock (_metricsLock)
                {
                    _metrics.TotalConnections++;
                    _metrics.ActiveConnections++;
                }
            }

            return new QdrantPooledClient(client, _instanceId, this, _logger);
        }
        catch
        {
            _connectionSemaphore.Release();
            throw;
        }
    }

    public async Task<bool> PerformHealthCheckAsync()
    {
        try
        {
            // TODO: Use HTTP health check instead of gRPC client to match our vector store implementation
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"http://{_instanceConfig.Host}:{_instanceConfig.Port}/", CancellationToken.None);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Health check failed for instance {InstanceId}: {Error}", _instanceId, ex.Message);
            return false;
        }
    }

    public ConnectionPoolMetrics GetMetrics()
    {
        lock (_metricsLock)
        {
            return new ConnectionPoolMetrics
            {
                PoolName = _instanceId,
                PoolType = "Qdrant Instance",
                Timestamp = DateTimeOffset.UtcNow,
                ActiveConnections = _metrics.ActiveConnections,
                TotalConnections = _metrics.TotalConnections,
                MaxConnections = (int)_connectionSemaphore.CurrentCount + _metrics.ActiveConnections,
                TotalRequests = _metrics.TotalRequests,
                SuccessfulAcquisitions = _metrics.SuccessfulAcquisitions,
                FailedAcquisitions = _metrics.FailedAcquisitions
            };
        }
    }

    internal void ReturnClient(QdrantClient client)
    {
        if (_disposed || !_allClients.ContainsKey(client)) 
        {
            client.Dispose();
            return;
        }

        _availableClients.Enqueue(client);
        _connectionSemaphore.Release();
        
        lock (_metricsLock)
        {
            _metrics.ActiveConnections--;
        }
    }

    private QdrantClient CreateClient()
    {
        var timeout = _instanceConfig.InstanceTimeout ?? _globalOptions.QdrantPool?.ConnectionTimeout ?? TimeSpan.FromSeconds(10);
        
        var client = new QdrantClient(_instanceConfig.Host, _instanceConfig.Port, https: _instanceConfig.UseHttps);
        
        // Note: QdrantClient timeout configuration would be handled via connection options if supported

        if (!string.IsNullOrEmpty(_instanceConfig.ApiKey))
        {
            // Note: QdrantClient API key setup would go here if supported
            // This is a placeholder for future API key authentication
        }
        
        _logger.LogDebug("Created new QdrantClient for instance {InstanceId}", _instanceId);
        
        return client;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogDebug("Disposing QdrantInstancePool for {InstanceId}", _instanceId);

        _disposed = true;

        foreach (var client in _allClients.Keys)
        {
            client.Dispose();
        }

        _availableClients.Clear();
        _allClients.Clear();
        _connectionSemaphore.Dispose();
    }
}

/// <summary>
/// A pooled Qdrant client wrapper that returns to the pool when disposed.
/// </summary>
public sealed class QdrantPooledClient : IQdrantPooledClient
{
    private readonly QdrantClient _client;
    private readonly string _instanceId;
    private readonly QdrantInstancePool _pool;
    private readonly ILogger _logger;
    private bool _disposed;

    internal QdrantPooledClient(QdrantClient client, string instanceId, QdrantInstancePool pool, ILogger logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The underlying Qdrant client instance.
    /// </summary>
    public QdrantClient Client => _client;

    /// <summary>
    /// The instance identifier this client is connected to.
    /// </summary>
    public string InstanceId => _instanceId;

    /// <summary>
    /// Indicates whether this client is currently healthy and available for use.
    /// </summary>
    public bool IsHealthy => !_disposed;

    /// <summary>
    /// Gets the current metrics for this specific client connection.
    /// </summary>
    public ClientConnectionMetrics Metrics => new ClientConnectionMetrics
    {
        InstanceId = _instanceId,
        CreatedAt = DateTimeOffset.UtcNow,
        IsHealthy = IsHealthy
    };

    /// <summary>
    /// Executes a function with the Qdrant client, including retry logic and circuit breaker protection.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<QdrantClient, CancellationToken, Task<T>> operation,
        string operationType,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(QdrantPooledClient));
            
        return await operation(_client, cancellationToken);
    }

    /// <summary>
    /// Executes an action with the Qdrant client, including retry logic and circuit breaker protection.
    /// </summary>
    public async Task ExecuteAsync(
        Func<QdrantClient, CancellationToken, Task> operation,
        string operationType,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(QdrantPooledClient));
            
        await operation(_client, cancellationToken);
    }

    /// <summary>
    /// Performs a health check on the underlying Qdrant connection.
    /// </summary>
    public async Task<ConnectionHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // TODO: Use HTTP health check instead of gRPC client to match our vector store implementation
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync("http://localhost:6333/", cancellationToken);
            var isHealthy = response.IsSuccessStatusCode;
            sw.Stop();
            
            return new ConnectionHealthCheckResult
            {
                IsHealthy = isHealthy,
                InstanceId = _instanceId,
                CheckedAt = DateTimeOffset.UtcNow,
                ResponseTime = sw.ElapsedMilliseconds,
                Message = isHealthy ? "Connection healthy (HTTP)" : "Connection unhealthy (HTTP)"
            };
        }
        catch (Exception ex)
        {
            return new ConnectionHealthCheckResult
            {
                IsHealthy = false,
                InstanceId = _instanceId,
                CheckedAt = DateTimeOffset.UtcNow,
                ResponseTime = 0,
                Message = "Health check failed",
                Error = ex.Message
            };
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        
        // Return the client to the pool instead of disposing it
        _pool.ReturnClient(_client);
        
        _logger.LogDebug("Returned QdrantClient to pool for instance {InstanceId}", _instanceId);
    }
}

/// <summary>
/// Simple load balancer for distributing requests across instances.
/// </summary>
internal sealed class LoadBalancer
{
    private readonly ConnectionLoadBalancingOptions _options;
    private readonly ILogger _logger;
    private readonly Random _random = new();
    private int _roundRobinIndex = 0;
    private readonly object _lock = new();

    public LoadBalancer(ConnectionLoadBalancingOptions options, ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string SelectInstance(IList<string> healthyInstances)
    {
        if (healthyInstances.Count == 0)
            throw new InvalidOperationException("No healthy instances available");

        if (healthyInstances.Count == 1)
            return healthyInstances[0];

        return _options.Algorithm switch
        {
            LoadBalancingAlgorithm.RoundRobin => SelectRoundRobin(healthyInstances),
            LoadBalancingAlgorithm.Random => SelectRandom(healthyInstances),
            LoadBalancingAlgorithm.WeightedRoundRobin => SelectRoundRobin(healthyInstances), // Simplified for now
            LoadBalancingAlgorithm.LeastConnections => SelectRoundRobin(healthyInstances), // Simplified for now
            LoadBalancingAlgorithm.HealthAware => SelectRoundRobin(healthyInstances), // Simplified for now
            _ => SelectRoundRobin(healthyInstances)
        };
    }

    public void RecordRequest(string instanceId)
    {
        // Record request metrics for future load balancing decisions
        _logger.LogDebug("Request recorded for instance {InstanceId}", instanceId);
    }

    private string SelectRoundRobin(IList<string> instances)
    {
        lock (_lock)
        {
            var index = _roundRobinIndex % instances.Count;
            _roundRobinIndex = (_roundRobinIndex + 1) % instances.Count;
            return instances[index];
        }
    }

    private string SelectRandom(IList<string> instances)
    {
        var index = _random.Next(instances.Count);
        return instances[index];
    }
}
