using System.Collections.Concurrent;
using Castellan.Worker.Models.ThreatIntelligence;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

/// <summary>
/// In-memory caching service for threat intelligence results
/// </summary>
public interface IThreatIntelligenceCacheService
{
    /// <summary>
    /// Get cached threat intelligence result
    /// </summary>
    /// <param name="key">Cache key (usually file hash or indicator)</param>
    /// <param name="source">Source of the threat intelligence (VirusTotal, MalwareBazaar, OTX)</param>
    /// <returns>Cached result or null if not found/expired</returns>
    T? Get<T>(string key, string source) where T : ThreatIntelligenceResult;

    /// <summary>
    /// Store threat intelligence result in cache
    /// </summary>
    /// <param name="key">Cache key (usually file hash or indicator)</param>
    /// <param name="result">Result to cache</param>
    /// <param name="expiryHours">Hours until expiry (uses service default if not specified)</param>
    void Set<T>(string key, T result, int? expiryHours = null) where T : ThreatIntelligenceResult;

    /// <summary>
    /// Remove specific entry from cache
    /// </summary>
    /// <param name="key">Cache key to remove</param>
    /// <param name="source">Source to remove from (optional, removes all sources if null)</param>
    void Remove(string key, string? source = null);

    /// <summary>
    /// Clear all cached entries
    /// </summary>
    void Clear();

    /// <summary>
    /// Get cache statistics
    /// </summary>
    /// <returns>Cache statistics</returns>
    CacheStatistics GetStatistics();
}

/// <summary>
/// In-memory implementation of threat intelligence cache
/// </summary>
public class ThreatIntelligenceCacheService : IThreatIntelligenceCacheService
{
    private readonly ConcurrentDictionary<string, ThreatIntelligenceCacheEntry> _cache = new();
    private readonly ThreatIntelligenceOptions _options;
    private readonly object _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    public ThreatIntelligenceCacheService(IOptions<ThreatIntelligenceOptions> options)
    {
        _options = options.Value;
    }

    public T? Get<T>(string key, string source) where T : ThreatIntelligenceResult
    {
        if (!_options.Caching.Enabled)
            return null;

        var cacheKey = GenerateCacheKey(key, source);
        
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiryTime > DateTime.UtcNow)
            {
                if (entry.Result is T result)
                {
                    result.FromCache = true;
                    return result;
                }
            }
            else
            {
                // Remove expired entry
                _cache.TryRemove(cacheKey, out _);
            }
        }

        return null;
    }

    public void Set<T>(string key, T result, int? expiryHours = null) where T : ThreatIntelligenceResult
    {
        if (!_options.Caching.Enabled || result == null)
            return;

        var hours = expiryHours ?? _options.Caching.DefaultCacheExpiryHours;
        var cacheKey = GenerateCacheKey(key, result.Source);
        
        var entry = new ThreatIntelligenceCacheEntry
        {
            Hash = key,
            Result = result,
            ExpiryTime = DateTime.UtcNow.AddHours(hours),
            Source = result.Source
        };

        _cache.AddOrUpdate(cacheKey, entry, (k, v) => entry);

        // Periodic cleanup to prevent unlimited growth
        PerformMaintenanceIfNeeded();
    }

    public void Remove(string key, string? source = null)
    {
        if (source != null)
        {
            var cacheKey = GenerateCacheKey(key, source);
            _cache.TryRemove(cacheKey, out _);
        }
        else
        {
            // Remove all entries for this key across all sources
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith($"{key}:")).ToList();
            foreach (var keyToRemove in keysToRemove)
            {
                _cache.TryRemove(keyToRemove, out _);
            }
        }
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public CacheStatistics GetStatistics()
    {
        var now = DateTime.UtcNow;
        var validEntries = _cache.Values.Where(e => e.ExpiryTime > now).ToList();
        var expiredEntries = _cache.Values.Where(e => e.ExpiryTime <= now).ToList();

        var bySource = validEntries
            .GroupBy(e => e.Source)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CacheStatistics
        {
            TotalEntries = _cache.Count,
            ValidEntries = validEntries.Count,
            ExpiredEntries = expiredEntries.Count,
            EntriesBySource = bySource,
            MaxCacheSize = _options.Caching.MaxCacheSize,
            CacheHitRate = CalculateCacheHitRate()
        };
    }

    private string GenerateCacheKey(string key, string source)
    {
        return $"{key.ToUpperInvariant()}:{source}";
    }

    private void PerformMaintenanceIfNeeded()
    {
        // Only perform maintenance every 15 minutes
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(15))
            return;

        lock (_cleanupLock)
        {
            // Double-check pattern
            if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(15))
                return;

            var now = DateTime.UtcNow;
            
            // Remove expired entries
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.ExpiryTime <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            // If still over max size, remove oldest entries
            if (_cache.Count > _options.Caching.MaxCacheSize)
            {
                var oldestKeys = _cache
                    .OrderBy(kvp => kvp.Value.Result.QueryTime)
                    .Take(_cache.Count - _options.Caching.MaxCacheSize)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in oldestKeys)
                {
                    _cache.TryRemove(key, out _);
                }
            }

            _lastCleanup = now;
        }
    }

    private float CalculateCacheHitRate()
    {
        // This is a simplified calculation - in production, you'd want to track
        // actual hit/miss counters
        return 0.0f; // Placeholder
    }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int ValidEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public Dictionary<string, int> EntriesBySource { get; set; } = new();
    public int MaxCacheSize { get; set; }
    public float CacheHitRate { get; set; }
}
