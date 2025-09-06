using System;
using System.Threading;
using System.Threading.Tasks;

namespace Castellan.Worker.Abstractions
{
    /// <summary>
    /// Generic caching service interface for Phase 2B intelligent caching implementation.
    /// Provides TTL support, cache key strategies, and hit/miss metrics tracking.
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// Gets a cached value by key.
        /// </summary>
        /// <typeparam name="T">The type of the cached value</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cached value if found, default(T) otherwise</returns>
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Sets a value in the cache with the specified TTL.
        /// </summary>
        /// <typeparam name="T">The type of the value to cache</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="ttl">Time to live for the cached value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Sets a value in the cache with the default TTL.
        /// </summary>
        /// <typeparam name="T">The type of the value to cache</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to cache</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Removes a value from the cache.
        /// </summary>
        /// <param name="key">The cache key to remove</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the key exists, false otherwise</returns>
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or sets a cached value using a factory function.
        /// </summary>
        /// <typeparam name="T">The type of the cached value</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="factory">Factory function to create the value if not cached</param>
        /// <param name="ttl">Time to live for the cached value</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cached or newly created value</returns>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Gets or sets a cached value using a factory function with default TTL.
        /// </summary>
        /// <typeparam name="T">The type of the cached value</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="factory">Factory function to create the value if not cached</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cached or newly created value</returns>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Clears all cached values.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ClearAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current cache metrics.
        /// </summary>
        /// <returns>Cache metrics including hit/miss rates, memory usage, etc.</returns>
        CacheMetrics GetMetrics();

        /// <summary>
        /// Gets cache statistics for a specific cache type/prefix.
        /// </summary>
        /// <param name="prefix">Cache key prefix to filter by</param>
        /// <returns>Cache metrics for the specified prefix</returns>
        CacheMetrics GetMetrics(string prefix);

        /// <summary>
        /// Generates a cache key with optional prefix and hash for consistent key formatting.
        /// </summary>
        /// <param name="prefix">Cache key prefix (e.g., "embedding", "ip", "llm")</param>
        /// <param name="identifier">Unique identifier for the cached item</param>
        /// <returns>Formatted cache key</returns>
        string GenerateKey(string prefix, string identifier);

        /// <summary>
        /// Generates a cache key with hash for large identifiers.
        /// </summary>
        /// <param name="prefix">Cache key prefix</param>
        /// <param name="identifier">Unique identifier (will be hashed if too long)</param>
        /// <param name="maxIdentifierLength">Maximum length before hashing (default: 100)</param>
        /// <returns>Formatted cache key with hash if needed</returns>
        string GenerateHashedKey(string prefix, string identifier, int maxIdentifierLength = 100);
    }

    /// <summary>
    /// Cache metrics for monitoring cache performance.
    /// </summary>
    public class CacheMetrics
    {
        /// <summary>
        /// Total number of cache requests (hits + misses).
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Number of cache hits.
        /// </summary>
        public long CacheHits { get; set; }

        /// <summary>
        /// Number of cache misses.
        /// </summary>
        public long CacheMisses { get; set; }

        /// <summary>
        /// Cache hit rate (CacheHits / TotalRequests).
        /// </summary>
        public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests * 100 : 0;

        /// <summary>
        /// Current memory usage of the cache in bytes.
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Current number of entries in the cache.
        /// </summary>
        public int CurrentEntries { get; set; }

        /// <summary>
        /// Number of cache evictions due to memory pressure or TTL expiration.
        /// </summary>
        public long EvictionsCount { get; set; }

        /// <summary>
        /// Maximum configured memory usage in bytes.
        /// </summary>
        public long MaxMemoryBytes { get; set; }

        /// <summary>
        /// Memory usage percentage (MemoryUsageBytes / MaxMemoryBytes * 100).
        /// </summary>
        public double MemoryUsagePercent => MaxMemoryBytes > 0 ? (double)MemoryUsageBytes / MaxMemoryBytes * 100 : 0;

        /// <summary>
        /// Timestamp when metrics were last updated.
        /// </summary>
        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Cache type or prefix these metrics relate to.
        /// </summary>
        public string CacheType { get; set; } = string.Empty;
    }
}
