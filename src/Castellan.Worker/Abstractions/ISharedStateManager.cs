using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for managing shared state across pipeline instances
/// </summary>
public interface ISharedStateManager : IDisposable
{
    /// <summary>
    /// Set a shared state value
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="value">State value</param>
    /// <param name="timeToLive">Optional TTL for the state entry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a shared state value
    /// </summary>
    /// <typeparam name="T">Type of the state value</typeparam>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>State value or default if not found</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a shared state value with metadata
    /// </summary>
    /// <typeparam name="T">Type of the state value</typeparam>
    /// <param name="key">State key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>State entry with metadata or null if not found</returns>
    Task<SharedStateEntry<T>?> GetWithMetadataAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Try to set a value only if the key doesn't exist (atomic operation)
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="value">State value</param>
    /// <param name="timeToLive">Optional TTL for the state entry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the value was set, false if key already exists</returns>
    Task<bool> TrySetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare and swap operation for atomic updates
    /// </summary>
    /// <param name="key">State key</param>
    /// <param name="expectedVersion">Expected version of the current value</param>
    /// <param name="newValue">New value to set</param>
    /// <param name="timeToLive">Optional TTL for the state entry</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the swap was successful, false if version mismatch</returns>
    Task<bool> CompareAndSwapAsync<T>(string key, long expectedVersion, T newValue, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a shared state entry
    /// </summary>
    /// <param name="key">State key to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the key was deleted, false if it didn't exist</returns>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all keys matching a pattern
    /// </summary>
    /// <param name="pattern">Pattern to match (supports wildcards)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of matching keys</returns>
    Task<IReadOnlyList<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default);

    /// <summary>
    /// Get multiple state values in a batch operation
    /// </summary>
    /// <param name="keys">Keys to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of key-value pairs</returns>
    Task<IReadOnlyDictionary<string, object?>> GetBatchAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set multiple state values in a batch operation
    /// </summary>
    /// <param name="values">Dictionary of key-value pairs to set</param>
    /// <param name="timeToLive">Optional TTL for all entries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetBatchAsync(IReadOnlyDictionary<string, object?> values, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to changes for a specific key or pattern
    /// </summary>
    /// <param name="keyPattern">Key or pattern to watch</param>
    /// <param name="callback">Callback to invoke when changes occur</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Subscription token that can be used to unsubscribe</returns>
    Task<string> SubscribeToChangesAsync(string keyPattern, Func<SharedStateChangeNotification, Task> callback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from change notifications
    /// </summary>
    /// <param name="subscriptionId">Subscription ID returned from SubscribeToChangesAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current state synchronization metrics
    /// </summary>
    /// <returns>Shared state metrics</returns>
    SharedStateMetrics GetMetrics();

    /// <summary>
    /// Force synchronization with other instances
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SynchronizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when state synchronization occurs
    /// </summary>
    event EventHandler<StateSynchronizationEventArgs>? StateSynchronized;

    /// <summary>
    /// Event fired when a conflict is detected and resolved
    /// </summary>
    event EventHandler<StateConflictEventArgs>? ConflictResolved;
}

/// <summary>
/// Shared state entry with metadata
/// </summary>
/// <typeparam name="T">Type of the state value</typeparam>
public class SharedStateEntry<T>
{
    /// <summary>
    /// State key
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// State value
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Version number for conflict resolution
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Instance ID that last modified this entry
    /// </summary>
    public string? LastModifiedBy { get; init; }

    /// <summary>
    /// Timestamp when the entry was last modified
    /// </summary>
    public DateTimeOffset LastModified { get; init; }

    /// <summary>
    /// Timestamp when the entry was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Expiration time (null if no TTL)
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Whether the entry is expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Time remaining until expiration
    /// </summary>
    public TimeSpan? TimeToExpiration => ExpiresAt.HasValue 
        ? ExpiresAt.Value - DateTimeOffset.UtcNow 
        : null;
}

/// <summary>
/// Notification for shared state changes
/// </summary>
public class SharedStateChangeNotification
{
    /// <summary>
    /// Type of change that occurred
    /// </summary>
    public StateChangeType ChangeType { get; init; }

    /// <summary>
    /// State key that changed
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// New value (null for deletions)
    /// </summary>
    public object? NewValue { get; init; }

    /// <summary>
    /// Previous value (null for insertions)
    /// </summary>
    public object? OldValue { get; init; }

    /// <summary>
    /// Instance that made the change
    /// </summary>
    public string? ModifiedBy { get; init; }

    /// <summary>
    /// Timestamp when the change occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Version after the change
    /// </summary>
    public long Version { get; init; }
}

/// <summary>
/// Type of state change
/// </summary>
public enum StateChangeType
{
    Created,
    Updated,
    Deleted,
    Expired
}

/// <summary>
/// Shared state metrics for monitoring
/// </summary>
public class SharedStateMetrics
{
    /// <summary>
    /// Total number of state entries
    /// </summary>
    public int TotalEntries { get; init; }

    /// <summary>
    /// Number of expired entries
    /// </summary>
    public int ExpiredEntries { get; init; }

    /// <summary>
    /// Total memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Number of synchronization operations
    /// </summary>
    public long SynchronizationCount { get; init; }

    /// <summary>
    /// Number of conflicts detected
    /// </summary>
    public long ConflictCount { get; init; }

    /// <summary>
    /// Number of conflicts resolved
    /// </summary>
    public long ConflictResolutionCount { get; init; }

    /// <summary>
    /// Average synchronization time
    /// </summary>
    public TimeSpan AverageSyncTime { get; init; }

    /// <summary>
    /// Last synchronization timestamp
    /// </summary>
    public DateTimeOffset LastSyncAt { get; init; }

    /// <summary>
    /// Number of active subscriptions
    /// </summary>
    public int ActiveSubscriptions { get; init; }

    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Conflict resolution rate
    /// </summary>
    public double ConflictResolutionRate => ConflictCount > 0 
        ? (double)ConflictResolutionCount / ConflictCount 
        : 1.0;

    /// <summary>
    /// Whether state management is healthy
    /// </summary>
    public bool IsHealthy => ConflictResolutionRate > 0.95 && ExpiredEntries < TotalEntries * 0.1;
}

/// <summary>
/// Event arguments for state synchronization
/// </summary>
public class StateSynchronizationEventArgs : EventArgs
{
    /// <summary>
    /// Number of entries synchronized
    /// </summary>
    public int EntriesSynchronized { get; init; }

    /// <summary>
    /// Time taken for synchronization
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Whether synchronization was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if synchronization failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Instances involved in synchronization
    /// </summary>
    public IReadOnlyList<string> ParticipatingInstances { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Event arguments for state conflicts
/// </summary>
public class StateConflictEventArgs : EventArgs
{
    /// <summary>
    /// Key where conflict occurred
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Conflicting versions
    /// </summary>
    public IReadOnlyList<ConflictingEntry> ConflictingEntries { get; init; } = Array.Empty<ConflictingEntry>();

    /// <summary>
    /// Resolution strategy used
    /// </summary>
    public string ResolutionStrategy { get; init; } = string.Empty;

    /// <summary>
    /// Final resolved value
    /// </summary>
    public object? ResolvedValue { get; init; }

    /// <summary>
    /// Whether conflict was successfully resolved
    /// </summary>
    public bool Resolved { get; init; }
}

/// <summary>
/// Conflicting state entry information
/// </summary>
public class ConflictingEntry
{
    /// <summary>
    /// Instance that created this version
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Version number
    /// </summary>
    public long Version { get; init; }

    /// <summary>
    /// Value of this version
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// Timestamp when this version was created
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
