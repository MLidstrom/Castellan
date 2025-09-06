using Castellan.Pipeline.Services.ConnectionPools.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using System.Collections.Concurrent;

namespace Castellan.Pipeline.Services.ConnectionPools;

/// <summary>
/// Connection pool manager for Qdrant clients with multi-instance support, load balancing, and health monitoring.
/// </summary>
internal sealed class QdrantConnectionPool : IQdrantConnectionPool
{
    private readonly ILogger<QdrantConnectionPool> _logger;
    private readonly ConnectionPoolOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    
    private readonly ConcurrentDictionary<string, QdrantInstancePool> _instancePools = new();
    private readonly ConcurrentDictionary<string, ConnectionHealth> _instanceHealthStatus = new();
    private readonly LoadBalancer _loadBalancer;
    private readonly Timer _healthCheckTimer;
    
    private readonly ConnectionPoolMetrics _metrics = new();
    private readonly object _metricsLock = new();
    private bool _disposed;

    public QdrantConnectionPool(
        IOptions<ConnectionPoolOptions> options,
        ILoggerFactory loggerFactory)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<QdrantConnectionPool>();

        _loadBalancer = new LoadBalancer(_options.LoadBalancing, _logger);

        // Initialize instance pools
        InitializeInstancePools();

        // Start health check timer
        if (_options.HealthCheck.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(
                PerformHealthChecks,
                null,
                TimeSpan.Zero, // Start immediately
                TimeSpan.FromMilliseconds(_options.HealthCheck.HealthCheckIntervalMs));
        }

        _logger.LogInformation(
            "QdrantConnectionPool initialized with {InstanceCount} instances and {Strategy} load balancing",
            _instancePools.Count, _options.LoadBalancing.Strategy);
    }

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
            _metrics.TotalConnections++;
        }

        return client;
    }

    public async Task<IReadOnlyDictionary<string, ConnectionHealth>> GetHealthStatusAsync()
    {
        ThrowIfDisposed();

        // Perform immediate health checks if not recently done
        await RefreshHealthStatusIfNeeded();

        return _instanceHealthStatus.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public ConnectionPoolMetrics GetMetrics()
    {
        ThrowIfDisposed();

        lock (_metricsLock)
        {
            var poolMetrics = new Dictionary<string, InstanceMetrics>();
            
            foreach (var kvp in _instancePools)
            {
                poolMetrics[kvp.Key] = kvp.Value.GetMetrics();
            }

            return new ConnectionPoolMetrics
            {
                TotalConnections = _metrics.TotalConnections,
                ActiveConnections = _metrics.ActiveConnections,
                HealthyInstances = _instanceHealthStatus.Count(kvp => kvp.Value.IsHealthy),
                TotalInstances = _instancePools.Count,
                InstanceMetrics = poolMetrics,
                LastUpdated = DateTimeOffset.UtcNow
            };
        }
    }

    public IReadOnlyCollection<string> GetAvailableInstances()
    {
        ThrowIfDisposed();
        return _instancePools.Keys.ToList();
    }

    public void SetInstanceHealth(string instanceId, bool isHealthy)
    {
        ThrowIfDisposed();
        
        if (!_instancePools.ContainsKey(instanceId))
        {
            throw new ArgumentException($"Instance {instanceId} not found", nameof(instanceId));
        }

        var health = _instanceHealthStatus.GetOrAdd(instanceId, _ => new ConnectionHealth
        {
            InstanceId = instanceId
        });

        health.IsHealthy = isHealthy;
        health.LastChecked = DateTimeOffset.UtcNow;
        health.Status = isHealthy ? "Manually set to healthy" : "Manually set to unhealthy";

        _logger.LogInformation("Manually set Qdrant instance {InstanceId} health to {IsHealthy}", instanceId, isHealthy);
    }

    private void InitializeInstancePools()
    {
        if (_options.QdrantPools?.Count == 0)
        {
            _logger.LogWarning("No Qdrant instances configured, creating default local instance");
            
            // Create a default local instance if none configured
            var defaultOptions = new QdrantInstanceOptions
            {
                Host = "localhost",
                Port = 6334,
                ApiKey = null,
                MaxPoolSize = _options.DefaultMaxPoolSize,
                MaxIdleConnections = 2,
                ConnectionTimeoutMs = _options.RequestTimeoutMs
            };

            CreateInstancePool("default", defaultOptions);
        }
        else
        {
            foreach (var instanceConfig in _options.QdrantPools)
            {
                CreateInstancePool(instanceConfig.Key, instanceConfig.Value);
            }
        }
    }

    private void CreateInstancePool(string instanceId, QdrantInstanceOptions options)
    {
        var instancePool = new QdrantInstancePool(
            instanceId,
            options,
            _options,
            _loggerFactory.CreateLogger<QdrantInstancePool>(),
            _loggerFactory.CreateLogger<QdrantPooledClient>());

        _instancePools.TryAdd(instanceId, instancePool);
        
        // Initialize health status
        _instanceHealthStatus.TryAdd(instanceId, new ConnectionHealth
        {
            InstanceId = instanceId,
            IsHealthy = true, // Start as healthy until proven otherwise
            Status = "Initialized",
            LastChecked = DateTimeOffset.UtcNow
        });

        _logger.LogDebug("Created Qdrant instance pool for {InstanceId}: {Host}:{Port}",
            instanceId, options.Host, options.Port);
    }

    private string? SelectInstance(string? preferredInstance)
    {
        // If a preferred instance is specified and it's healthy, use it
        if (!string.IsNullOrEmpty(preferredInstance) && 
            _instanceHealthStatus.TryGetValue(preferredInstance, out var preferredHealth) &&
            preferredHealth.IsHealthy)
        {
            return preferredInstance;
        }

        // Get all healthy instances
        var healthyInstances = _instanceHealthStatus
            .Where(kvp => kvp.Value.IsHealthy)
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
        var staleThreshold = TimeSpan.FromMilliseconds(_options.HealthCheck.HealthCheckIntervalMs * 2);

        var needsRefresh = _instanceHealthStatus.Values.Any(health =>
            now - health.LastChecked > staleThreshold);

        if (needsRefresh)
        {
            await PerformHealthChecks();
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
            var healthResult = await instancePool.PerformHealthCheckAsync();
            
            _instanceHealthStatus.AddOrUpdate(instanceId,
                _ => new ConnectionHealth
                {
                    InstanceId = instanceId,
                    IsHealthy = healthResult.IsHealthy,
                    Status = healthResult.Message,
                    LastChecked = healthResult.CheckedAt,
                    ResponseTime = healthResult.ResponseTime,
                    Error = healthResult.Error
                },
                (_, existing) =>
                {
                    existing.IsHealthy = healthResult.IsHealthy;
                    existing.Status = healthResult.Message;
                    existing.LastChecked = healthResult.CheckedAt;
                    existing.ResponseTime = healthResult.ResponseTime;
                    existing.Error = healthResult.Error;
                    return existing;
                });

            _logger.LogDebug("Health check for Qdrant instance {InstanceId}: {IsHealthy} ({ResponseTime}ms)",
                instanceId, healthResult.IsHealthy, healthResult.ResponseTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for Qdrant instance {InstanceId}", instanceId);
            
            _instanceHealthStatus.AddOrUpdate(instanceId,
                _ => new ConnectionHealth
                {
                    InstanceId = instanceId,
                    IsHealthy = false,
                    Status = "Health check exception",
                    LastChecked = DateTimeOffset.UtcNow,
                    Error = ex.Message
                },
                (_, existing) =>
                {
                    existing.IsHealthy = false;
                    existing.Status = "Health check exception";
                    existing.LastChecked = DateTimeOffset.UtcNow;
                    existing.Error = ex.Message;
                    return existing;
                });
        }
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
    private readonly QdrantInstanceOptions _instanceOptions;
    private readonly ConnectionPoolOptions _globalOptions;
    private readonly ILogger<QdrantInstancePool> _logger;
    private readonly ILogger<QdrantPooledClient> _clientLogger;
    
    private readonly ConcurrentQueue<QdrantClient> _availableClients = new();
    private readonly ConcurrentDictionary<QdrantClient, bool> _allClients = new();
    private readonly SemaphoreSlim _connectionSemaphore;
    
    private readonly InstanceMetrics _metrics = new();
    private readonly object _metricsLock = new();
    private bool _disposed;

    public QdrantInstancePool(
        string instanceId,
        QdrantInstanceOptions instanceOptions,
        ConnectionPoolOptions globalOptions,
        ILogger<QdrantInstancePool> logger,
        ILogger<QdrantPooledClient> clientLogger)
    {
        _instanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        _instanceOptions = instanceOptions ?? throw new ArgumentNullException(nameof(instanceOptions));
        _globalOptions = globalOptions ?? throw new ArgumentNullException(nameof(globalOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientLogger = clientLogger ?? throw new ArgumentNullException(nameof(clientLogger));

        _connectionSemaphore = new SemaphoreSlim(instanceOptions.MaxPoolSize, instanceOptions.MaxPoolSize);

        _logger.LogDebug("Created QdrantInstancePool for {InstanceId} with max pool size {MaxPoolSize}",
            instanceId, instanceOptions.MaxPoolSize);
    }

    public async Task<IQdrantPooledClient> GetClientAsync(CancellationToken cancellationToken = default)
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
                    _metrics.ConnectionsFromPool++;
                }
            }
            else
            {
                // Create a new client
                client = CreateClient();
                _allClients.TryAdd(client, true);
                
                lock (_metricsLock)
                {
                    _metrics.NewConnections++;
                    _metrics.ActiveConnections++;
                }
            }

            return new QdrantPooledClient(client, _instanceId, _globalOptions, _clientLogger);
        }
        catch
        {
            _connectionSemaphore.Release();
            throw;
        }
    }

    public async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        try
        {
            using var client = CreateClient();
            using var pooledClient = new QdrantPooledClient(client, _instanceId, _globalOptions, _clientLogger);
            
            return await pooledClient.PerformHealthCheckAsync();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                IsHealthy = false,
                InstanceId = _instanceId,
                CheckedAt = DateTimeOffset.UtcNow,
                Message = $"Health check failed: {ex.Message}",
                Error = ex.ToString()
            };
        }
    }

    public InstanceMetrics GetMetrics()
    {
        lock (_metricsLock)
        {
            return new InstanceMetrics
            {
                InstanceId = _instanceId,
                ActiveConnections = _metrics.ActiveConnections,
                TotalConnections = _metrics.TotalConnections,
                ConnectionsFromPool = _metrics.ConnectionsFromPool,
                NewConnections = _metrics.NewConnections,
                MaxPoolSize = _instanceOptions.MaxPoolSize,
                AvailableConnections = _availableClients.Count,
                LastUpdated = DateTimeOffset.UtcNow
            };
        }
    }

    private QdrantClient CreateClient()
    {
        var connectionOptions = new QdrantClientOptions(_instanceOptions.Host, _instanceOptions.Port)
        {
            Timeout = TimeSpan.FromMilliseconds(_instanceOptions.ConnectionTimeoutMs)
        };

        if (!string.IsNullOrEmpty(_instanceOptions.ApiKey))
        {
            connectionOptions.ApiKey = _instanceOptions.ApiKey;
        }

        var client = new QdrantClient(connectionOptions);
        
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
