using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

/// <summary>
/// In-memory implementation of shared state manager for pipeline coordination
/// </summary>
public class InMemorySharedStateManager : ISharedStateManager
{
    private readonly ILogger<InMemorySharedStateManager> _logger;
    private readonly SharedStateOptions _options;
    private readonly string _instanceId;
    private readonly ConcurrentDictionary<string, InternalStateEntry> _stateStore = new();
    private readonly ConcurrentDictionary<string, StateSubscription> _subscriptions = new();
    private readonly System.Threading.Timer _cleanupTimer;
    private readonly object _syncLock = new();

    // Metrics tracking
    private long _synchronizationCount;
    private long _conflictCount;
    private long _conflictResolutionCount;
    private readonly ConcurrentQueue<TimeSpan> _recentSyncTimes = new();
    private DateTimeOffset _lastSyncAt = DateTimeOffset.UtcNow;

    // Events
    public event EventHandler<StateSynchronizationEventArgs>? StateSynchronized;
    public event EventHandler<StateConflictEventArgs>? ConflictResolved;

    private bool _disposed;

    public InMemorySharedStateManager(
        ILogger<InMemorySharedStateManager> logger, 
        IOptions<PipelineScalingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value?.SharedState ?? throw new ArgumentNullException(nameof(options));
        _instanceId = Environment.MachineName + "-" + Environment.ProcessId;
        
        // Start cleanup timer
        _cleanupTimer = new System.Threading.Timer(
            CleanupExpiredEntries, 
            null, 
            (int)TimeSpan.FromMinutes(1).TotalMilliseconds, 
            (int)TimeSpan.FromMinutes(1).TotalMilliseconds);

        _logger.LogInformation("InMemorySharedStateManager initialized for instance {InstanceId}", _instanceId);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var now = DateTimeOffset.UtcNow;
        var ttl = timeToLive ?? TimeSpan.FromMinutes(_options.StateTimeoutMinutes);
        var expiresAt = now.Add(ttl);

        var entry = new InternalStateEntry
        {
            Key = key,
            Value = JsonSerializer.Serialize(value),
            Version = DateTimeOffset.UtcNow.Ticks,
            LastModifiedBy = _instanceId,
            LastModified = now,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            ValueType = typeof(T).FullName ?? typeof(T).Name
        };

        var previousEntry = _stateStore.AddOrUpdate(key, entry, (k, existing) => 
        {
            entry.CreatedAt = existing.CreatedAt;
            entry.Version = existing.Version + 1;
            return entry;
        });

        var changeType = previousEntry.Key == entry.Key && previousEntry != entry 
            ? StateChangeType.Updated 
            : StateChangeType.Created;

        // Notify subscribers
        await NotifySubscribersAsync<T>(key, changeType, value, 
            previousEntry.Key == entry.Key ? JsonSerializer.Deserialize<T>(previousEntry.Value) : default,
            entry.Version);

        _logger.LogDebug("Set state key {Key} with TTL {TTL}", key, ttl);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_stateStore.TryGetValue(key, out var entry) || entry.IsExpired)
        {
            if (entry?.IsExpired == true)
            {
                _stateStore.TryRemove(key, out _);
                await NotifySubscribersAsync<T>(key, StateChangeType.Expired, default(T), default(T), entry.Version);
            }
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(entry.Value);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize state value for key {Key}", key);
            return default;
        }
    }

    public async Task<SharedStateEntry<T>?> GetWithMetadataAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_stateStore.TryGetValue(key, out var entry) || entry.IsExpired)
        {
            if (entry?.IsExpired == true)
            {
                _stateStore.TryRemove(key, out _);
                await NotifySubscribersAsync<T>(key, StateChangeType.Expired, default(T), default(T), entry.Version);
            }
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(entry.Value);
            return new SharedStateEntry<T>
            {
                Key = entry.Key,
                Value = value,
                Version = entry.Version,
                LastModifiedBy = entry.LastModifiedBy,
                LastModified = entry.LastModified,
                CreatedAt = entry.CreatedAt,
                ExpiresAt = entry.ExpiresAt
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize state value for key {Key}", key);
            return null;
        }
    }

    public async Task<bool> TrySetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_stateStore.ContainsKey(key))
        {
            return false;
        }

        await SetAsync(key, value, timeToLive, cancellationToken);
        return true;
    }

    public async Task<bool> CompareAndSwapAsync<T>(string key, long expectedVersion, T newValue, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_syncLock)
        {
            if (!_stateStore.TryGetValue(key, out var currentEntry))
            {
                return false;
            }

            if (currentEntry.Version != expectedVersion)
            {
                return false;
            }

            if (currentEntry.IsExpired)
            {
                _stateStore.TryRemove(key, out _);
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            var ttl = timeToLive ?? TimeSpan.FromMinutes(_options.StateTimeoutMinutes);
            
            var newEntry = new InternalStateEntry
            {
                Key = key,
                Value = JsonSerializer.Serialize(newValue),
                Version = currentEntry.Version + 1,
                LastModifiedBy = _instanceId,
                LastModified = now,
                CreatedAt = currentEntry.CreatedAt,
                ExpiresAt = now.Add(ttl),
                ValueType = typeof(T).FullName ?? typeof(T).Name
            };

            _stateStore[key] = newEntry;

            // Notify subscribers asynchronously
            _ = Task.Run(async () => 
            {
                var previousValue = JsonSerializer.Deserialize<T>(currentEntry.Value);
                await NotifySubscribersAsync<T>(key, StateChangeType.Updated, newValue, previousValue, newEntry.Version);
            });

            return true;
        }
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_stateStore.TryRemove(key, out var removedEntry))
        {
            return false;
        }

        await NotifySubscribersAsync<object>(key, StateChangeType.Deleted, null, null, removedEntry.Version);
        
        _logger.LogDebug("Deleted state key {Key}", key);
        return true;
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default)
    {
        var regex = ConvertWildcardToRegex(pattern);
        var matchingKeys = _stateStore.Keys
            .Where(key => regex.IsMatch(key))
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<string>>(matchingKeys);
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var key in keys)
        {
            if (_stateStore.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                try
                {
                    // We don't know the type, so return raw JSON string
                    result[key] = entry.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve value for key {Key} in batch operation", key);
                    result[key] = null;
                }
            }
            else
            {
                result[key] = null;
            }
        }

        return result.AsReadOnly();
    }

    public async Task SetBatchAsync(IReadOnlyDictionary<string, object?> values, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var ttl = timeToLive ?? TimeSpan.FromMinutes(_options.StateTimeoutMinutes);
        var expiresAt = now.Add(ttl);

        foreach (var kvp in values)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
                continue;

            var entry = new InternalStateEntry
            {
                Key = kvp.Key,
                Value = JsonSerializer.Serialize(kvp.Value),
                Version = now.Ticks,
                LastModifiedBy = _instanceId,
                LastModified = now,
                CreatedAt = now,
                ExpiresAt = expiresAt,
                ValueType = kvp.Value?.GetType().FullName ?? "object"
            };

            var previousEntry = _stateStore.AddOrUpdate(kvp.Key, entry, (k, existing) => 
            {
                entry.CreatedAt = existing.CreatedAt;
                entry.Version = existing.Version + 1;
                return entry;
            });

            var changeType = previousEntry.Key == entry.Key && previousEntry != entry 
                ? StateChangeType.Updated 
                : StateChangeType.Created;

            // Notify subscribers (fire and forget)
            _ = Task.Run(() => NotifySubscribersAsync<object>(kvp.Key, changeType, kvp.Value, null, entry.Version));
        }

        _logger.LogDebug("Set batch of {Count} state entries", values.Count);
    }

    public async Task<string> SubscribeToChangesAsync(string keyPattern, Func<SharedStateChangeNotification, Task> callback, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPattern);
        ArgumentNullException.ThrowIfNull(callback);

        var subscriptionId = Guid.NewGuid().ToString();
        var subscription = new StateSubscription
        {
            Id = subscriptionId,
            KeyPattern = keyPattern,
            Callback = callback,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _subscriptions[subscriptionId] = subscription;
        
        _logger.LogDebug("Created subscription {SubscriptionId} for pattern {Pattern}", subscriptionId, keyPattern);
        return subscriptionId;
    }

    public async Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        if (_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            _logger.LogDebug("Removed subscription {SubscriptionId} for pattern {Pattern}", 
                subscriptionId, subscription.KeyPattern);
        }
    }

    public SharedStateMetrics GetMetrics()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredCount = _stateStore.Values.Count(e => e.IsExpired);
        
        // Estimate memory usage
        var memoryUsage = _stateStore.Values.Sum(e => 
            (e.Key?.Length ?? 0) * 2 + 
            (e.Value?.Length ?? 0) * 2 + 
            (e.ValueType?.Length ?? 0) * 2 + 
            100); // Overhead estimate

        var avgSyncTime = _recentSyncTimes.IsEmpty 
            ? TimeSpan.Zero 
            : TimeSpan.FromTicks((long)_recentSyncTimes.Average(t => t.Ticks));

        return new SharedStateMetrics
        {
            TotalEntries = _stateStore.Count,
            ExpiredEntries = expiredCount,
            MemoryUsageBytes = memoryUsage,
            SynchronizationCount = _synchronizationCount,
            ConflictCount = _conflictCount,
            ConflictResolutionCount = _conflictResolutionCount,
            AverageSyncTime = avgSyncTime,
            LastSyncAt = _lastSyncAt,
            ActiveSubscriptions = _subscriptions.Count
        };
    }

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var success = true;
        string? errorMessage = null;

        try
        {
            // In a real implementation, this would sync with other instances
            // For now, we'll just clean up expired entries
            CleanupExpiredEntries(null);
            
            Interlocked.Increment(ref _synchronizationCount);
            _lastSyncAt = DateTimeOffset.UtcNow;
            
            stopwatch.Stop();
            _recentSyncTimes.Enqueue(stopwatch.Elapsed);
            CleanupOldSyncTimes();

            _logger.LogDebug("Synchronization completed in {Duration}ms", 
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            _logger.LogError(ex, "Synchronization failed");
        }

        // Fire synchronization event
        StateSynchronized?.Invoke(this, new StateSynchronizationEventArgs
        {
            EntriesSynchronized = _stateStore.Count,
            Duration = stopwatch.Elapsed,
            Success = success,
            ErrorMessage = errorMessage,
            ParticipatingInstances = new[] { _instanceId }
        });
    }

    private async Task NotifySubscribersAsync<T>(string key, StateChangeType changeType, T? newValue, T? oldValue, long version)
    {
        var notification = new SharedStateChangeNotification
        {
            ChangeType = changeType,
            Key = key,
            NewValue = newValue,
            OldValue = oldValue,
            ModifiedBy = _instanceId,
            Version = version
        };

        var tasks = new List<Task>();

        foreach (var subscription in _subscriptions.Values)
        {
            var regex = ConvertWildcardToRegex(subscription.KeyPattern);
            if (regex.IsMatch(key))
            {
                tasks.Add(SafeInvokeCallback(subscription, notification));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private async Task SafeInvokeCallback(StateSubscription subscription, SharedStateChangeNotification notification)
    {
        try
        {
            await subscription.Callback(notification);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Subscription callback failed for {SubscriptionId}", subscription.Id);
        }
    }

    private Regex ConvertWildcardToRegex(string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        return new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = new List<string>();
        var now = DateTimeOffset.UtcNow;

        foreach (var kvp in _stateStore)
        {
            if (kvp.Value.IsExpired)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            if (_stateStore.TryRemove(key, out var removedEntry))
            {
                // Notify subscribers of expiration (fire and forget)
                _ = Task.Run(() => NotifySubscribersAsync<object>(key, StateChangeType.Expired, null, null, removedEntry.Version));
            }
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired state entries", expiredKeys.Count);
        }
    }

    private void CleanupOldSyncTimes()
    {
        while (_recentSyncTimes.Count > 100 && _recentSyncTimes.TryDequeue(out _))
        {
            // Remove oldest entries
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cleanupTimer?.Dispose();
        
        _logger.LogInformation("InMemorySharedStateManager disposed. Final metrics: {TotalEntries} entries, {Subscriptions} subscriptions",
            _stateStore.Count, _subscriptions.Count);
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Internal state entry with serialization support
/// </summary>
internal class InternalStateEntry
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public long Version { get; set; }
    public required string LastModifiedBy { get; init; }
    public DateTimeOffset LastModified { get; init; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; init; }
    public required string ValueType { get; init; }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
}

/// <summary>
/// Subscription information for change notifications
/// </summary>
internal class StateSubscription
{
    public required string Id { get; init; }
    public required string KeyPattern { get; init; }
    public required Func<SharedStateChangeNotification, Task> Callback { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
