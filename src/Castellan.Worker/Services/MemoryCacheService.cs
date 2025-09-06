using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Services
{
    /// <summary>
    /// Memory-based cache service implementation for Phase 2B intelligent caching.
    /// Uses Microsoft.Extensions.Caching.Memory with configurable sizes, LRU eviction, and metrics tracking.
    /// </summary>
    public class MemoryCacheService : ICacheService, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly CacheOptions _options;
        private readonly ILogger<MemoryCacheService> _logger;
        private readonly System.Threading.Timer _cleanupTimer;
        private readonly ConcurrentDictionary<string, CacheMetrics> _metricsPerPrefix;
        private readonly CacheMetrics _globalMetrics;
        private readonly object _metricsLock = new();
        private bool _disposed;

        public MemoryCacheService(
            IMemoryCache memoryCache,
            IOptionsMonitor<CacheOptions> options,
            ILogger<MemoryCacheService> logger)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _options = options?.CurrentValue ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _metricsPerPrefix = new ConcurrentDictionary<string, CacheMetrics>();
            _globalMetrics = new CacheMetrics
            {
                CacheType = "Global",
                MaxMemoryBytes = _options.MaxMemoryMb * 1024 * 1024L
            };

            // Set up periodic cleanup
            var cleanupInterval = TimeSpan.FromSeconds(_options.CleanupIntervalSeconds);
            _cleanupTimer = new System.Threading.Timer(PerformCleanup, null, cleanupInterval, cleanupInterval);

            _logger.LogInformation(
                "MemoryCacheService initialized. MaxMemory: {MaxMemoryMb}MB, Cleanup interval: {CleanupSeconds}s",
                _options.MaxMemoryMb, _options.CleanupIntervalSeconds);
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            var prefix = ExtractPrefix(key);
            var found = _memoryCache.TryGetValue(key, out var value);

            UpdateMetrics(prefix, found, false);

            if (found && value is T typedValue)
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return await Task.FromResult(typedValue);
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return null;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (ttl <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be greater than zero");

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Priority = CacheItemPriority.Normal,
                Size = EstimateSize(value)
            };

            // Add eviction callback to track evictions
            options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                if (evictedKey is string stringKey)
                {
                    var prefix = ExtractPrefix(stringKey);
                    UpdateMetrics(prefix, false, true);
                    _logger.LogDebug("Cache entry evicted. Key: {Key}, Reason: {Reason}", stringKey, reason);
                }
            });

            _memoryCache.Set(key, value, options);
            
            var prefix = ExtractPrefix(key);
            UpdateMetrics(prefix, false, false, true);

            _logger.LogDebug("Cached item with key: {Key}, TTL: {TTL}", key, ttl);
            
            await Task.CompletedTask;
        }

        public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) where T : class
        {
            var defaultTtl = TimeSpan.FromMinutes(_options.DefaultTtlMinutes);
            await SetAsync(key, value, defaultTtl, cancellationToken);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            _memoryCache.Remove(key);
            
            _logger.LogDebug("Removed cache entry with key: {Key}", key);
            
            await Task.CompletedTask;
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            var exists = _memoryCache.TryGetValue(key, out _);
            return await Task.FromResult(exists);
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            // Try to get from cache first
            var cached = await GetAsync<T>(key, cancellationToken);
            if (cached != null)
            {
                return cached;
            }

            // Cache miss - call factory and cache result
            try
            {
                var value = await factory();
                if (value != null)
                {
                    await SetAsync(key, value, ttl, cancellationToken);
                    return value;
                }
                
                // Factory returned null - this is allowed but we don't cache null values
                return value!; // null-forgiving operator since T can be nullable
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Factory function failed for cache key: {Key}", key);
                throw;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken = default) where T : class
        {
            var defaultTtl = TimeSpan.FromMinutes(_options.DefaultTtlMinutes);
            return await GetOrSetAsync(key, factory, defaultTtl, cancellationToken);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            if (_memoryCache is MemoryCache mc)
            {
                // Clear the internal cache
                mc.Compact(1.0); // Compact 100% - effectively clears all
            }

            // Reset metrics
            lock (_metricsLock)
            {
                _metricsPerPrefix.Clear();
                ResetMetrics(_globalMetrics);
            }

            _logger.LogInformation("Cache cleared");
            
            await Task.CompletedTask;
        }

        public CacheMetrics GetMetrics()
        {
            lock (_metricsLock)
            {
                _globalMetrics.LastUpdated = DateTimeOffset.UtcNow;
                _globalMetrics.MemoryUsageBytes = EstimateCurrentMemoryUsage();
                return new CacheMetrics
                {
                    TotalRequests = _globalMetrics.TotalRequests,
                    CacheHits = _globalMetrics.CacheHits,
                    CacheMisses = _globalMetrics.CacheMisses,
                    MemoryUsageBytes = _globalMetrics.MemoryUsageBytes,
                    CurrentEntries = _globalMetrics.CurrentEntries,
                    EvictionsCount = _globalMetrics.EvictionsCount,
                    MaxMemoryBytes = _globalMetrics.MaxMemoryBytes,
                    LastUpdated = _globalMetrics.LastUpdated,
                    CacheType = _globalMetrics.CacheType
                };
            }
        }

        public CacheMetrics GetMetrics(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return GetMetrics();

            lock (_metricsLock)
            {
                if (_metricsPerPrefix.TryGetValue(prefix, out var metrics))
                {
                    metrics.LastUpdated = DateTimeOffset.UtcNow;
                    return new CacheMetrics
                    {
                        TotalRequests = metrics.TotalRequests,
                        CacheHits = metrics.CacheHits,
                        CacheMisses = metrics.CacheMisses,
                        MemoryUsageBytes = metrics.MemoryUsageBytes,
                        CurrentEntries = metrics.CurrentEntries,
                        EvictionsCount = metrics.EvictionsCount,
                        MaxMemoryBytes = metrics.MaxMemoryBytes,
                        LastUpdated = metrics.LastUpdated,
                        CacheType = metrics.CacheType
                    };
                }
            }

            return new CacheMetrics { CacheType = prefix };
        }

        public string GenerateKey(string prefix, string identifier)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));

            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier cannot be null or empty", nameof(identifier));

            return $"{prefix}:{identifier}";
        }

        public string GenerateHashedKey(string prefix, string identifier, int maxIdentifierLength = 100)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));

            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier cannot be null or empty", nameof(identifier));

            if (identifier.Length <= maxIdentifierLength)
            {
                return GenerateKey(prefix, identifier);
            }

            // Hash the identifier if it's too long
            var hash = ComputeSha256Hash(identifier);
            return GenerateKey(prefix, hash);
        }

        private void UpdateMetrics(string prefix, bool wasHit, bool wasEviction, bool wasSet = false)
        {
            if (!_options.EnableMetrics)
                return;

            lock (_metricsLock)
            {
                // Update global metrics
                if (wasHit)
                {
                    _globalMetrics.TotalRequests++;
                    _globalMetrics.CacheHits++;
                }
                else if (!wasEviction && !wasSet)
                {
                    _globalMetrics.TotalRequests++;
                    _globalMetrics.CacheMisses++;
                }
                else if (wasEviction)
                {
                    _globalMetrics.EvictionsCount++;
                    _globalMetrics.CurrentEntries = Math.Max(0, _globalMetrics.CurrentEntries - 1);
                }
                else if (wasSet)
                {
                    _globalMetrics.CurrentEntries++;
                }

                // Update prefix-specific metrics
                if (!string.IsNullOrEmpty(prefix))
                {
                    var prefixMetrics = _metricsPerPrefix.GetOrAdd(prefix, _ => new CacheMetrics
                    {
                        CacheType = prefix,
                        MaxMemoryBytes = _options.MaxMemoryMb * 1024 * 1024L / 4 // Rough estimate per prefix
                    });

                    if (wasHit)
                    {
                        prefixMetrics.TotalRequests++;
                        prefixMetrics.CacheHits++;
                    }
                    else if (!wasEviction && !wasSet)
                    {
                        prefixMetrics.TotalRequests++;
                        prefixMetrics.CacheMisses++;
                    }
                    else if (wasEviction)
                    {
                        prefixMetrics.EvictionsCount++;
                        prefixMetrics.CurrentEntries = Math.Max(0, prefixMetrics.CurrentEntries - 1);
                    }
                    else if (wasSet)
                    {
                        prefixMetrics.CurrentEntries++;
                    }
                }
            }
        }

        private string ExtractPrefix(string key)
        {
            var colonIndex = key.IndexOf(':');
            return colonIndex > 0 ? key.Substring(0, colonIndex) : "unknown";
        }

        private int EstimateSize(object value)
        {
            // Simple size estimation
            return value switch
            {
                string s => s.Length * 2, // Rough estimate for Unicode strings
                byte[] bytes => bytes.Length,
                float[] floats => floats.Length * sizeof(float),
                _ => 1024 // Default estimate
            };
        }

        private long EstimateCurrentMemoryUsage()
        {
            // This is a rough estimate since MemoryCache doesn't expose exact memory usage
            return _globalMetrics.CurrentEntries * 1024; // Rough estimate of 1KB per entry
        }

        private string ComputeSha256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes)[..16]; // Use first 16 characters for brevity
        }

        private void PerformCleanup(object? state)
        {
            try
            {
                if (_memoryCache is MemoryCache mc)
                {
                    // Check memory pressure and compact if needed
                    var currentUsage = EstimateCurrentMemoryUsage();
                    var maxMemory = _options.MaxMemoryMb * 1024 * 1024L;
                    var memoryPressure = (double)currentUsage / maxMemory;

                    if (memoryPressure > _options.MemoryPressureThreshold)
                    {
                        var compactRatio = Math.Min(0.5, memoryPressure - _options.MemoryPressureThreshold + 0.1);
                        mc.Compact(compactRatio);
                        
                        _logger.LogInformation(
                            "Cache compaction performed. Memory pressure: {MemoryPressure:P2}, Compact ratio: {CompactRatio:P2}",
                            memoryPressure, compactRatio);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during cache cleanup");
            }
        }

        private void ResetMetrics(CacheMetrics metrics)
        {
            metrics.TotalRequests = 0;
            metrics.CacheHits = 0;
            metrics.CacheMisses = 0;
            metrics.CurrentEntries = 0;
            metrics.EvictionsCount = 0;
            metrics.MemoryUsageBytes = 0;
            metrics.LastUpdated = DateTimeOffset.UtcNow;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                _disposed = true;
                _logger.LogInformation("MemoryCacheService disposed");
            }
        }
    }
}
