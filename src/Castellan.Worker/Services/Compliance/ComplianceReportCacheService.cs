using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Castellan.Worker.Services.Compliance;

public interface IComplianceReportCacheService
{
    Task<T?> GetCachedReportAsync<T>(string cacheKey) where T : class;
    Task SetCachedReportAsync<T>(string cacheKey, T report, TimeSpan? expiration = null) where T : class;
    Task InvalidateFrameworkCacheAsync(string framework);
    Task InvalidateAllCacheAsync();
    string GenerateCacheKey(string reportType, string framework, object? parameters = null);
}

public class ComplianceReportCacheService : IComplianceReportCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ComplianceReportCacheService> _logger;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(15); // 15-minute default cache
    private readonly HashSet<string> _cacheKeys = new();
    private readonly object _lockObject = new();

    public ComplianceReportCacheService(
        IMemoryCache cache,
        ILogger<ComplianceReportCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetCachedReportAsync<T>(string cacheKey) where T : class
    {
        try
        {
            if (_cache.TryGetValue(cacheKey, out var cachedValue))
            {
                _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);

                if (cachedValue is string jsonString)
                {
                    var result = JsonSerializer.Deserialize<T>(jsonString);
                    return result;
                }

                if (cachedValue is T directValue)
                {
                    return directValue;
                }
            }

            _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving cached report for key: {CacheKey}", cacheKey);
            return null;
        }
    }

    public async Task SetCachedReportAsync<T>(string cacheKey, T report, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var cacheExpiration = expiration ?? _defaultExpiration;

            // Serialize to JSON for consistent storage
            var jsonString = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(5), // Sliding window
                Priority = CacheItemPriority.Normal,
                Size = EstimateSize(jsonString)
            };

            // Add callback to track cache removal
            cacheEntryOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
            {
                EvictionCallback = (key, value, reason, state) =>
                {
                    lock (_lockObject)
                    {
                        _cacheKeys.Remove(key.ToString() ?? string.Empty);
                    }
                    _logger.LogDebug("Cache entry evicted: {CacheKey}, Reason: {Reason}", key, reason);
                }
            });

            _cache.Set(cacheKey, jsonString, cacheEntryOptions);

            lock (_lockObject)
            {
                _cacheKeys.Add(cacheKey);
            }

            _logger.LogDebug("Cached report with key: {CacheKey}, Expiration: {Expiration}",
                cacheKey, cacheExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching report for key: {CacheKey}", cacheKey);
        }
    }

    public async Task InvalidateFrameworkCacheAsync(string framework)
    {
        try
        {
            var keysToRemove = new List<string>();

            lock (_lockObject)
            {
                keysToRemove.AddRange(_cacheKeys.Where(key =>
                    key.Contains(framework, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _logger.LogDebug("Invalidated cache key: {CacheKey}", key);
            }

            _logger.LogInformation("Invalidated {Count} cache entries for framework: {Framework}",
                keysToRemove.Count, framework);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating framework cache for: {Framework}", framework);
        }
    }

    public async Task InvalidateAllCacheAsync()
    {
        try
        {
            var keysToRemove = new List<string>();

            lock (_lockObject)
            {
                keysToRemove.AddRange(_cacheKeys);
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            _logger.LogInformation("Invalidated all {Count} cache entries", keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating all cache entries");
        }
    }

    public string GenerateCacheKey(string reportType, string framework, object? parameters = null)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append("compliance_report:");
        keyBuilder.Append(reportType.ToLowerInvariant());
        keyBuilder.Append(":");
        keyBuilder.Append(framework.ToLowerInvariant());

        if (parameters != null)
        {
            // Create a deterministic hash of parameters
            var paramJson = JsonSerializer.Serialize(parameters);
            var paramHash = paramJson.GetHashCode();
            keyBuilder.Append(":");
            keyBuilder.Append(paramHash);
        }

        return keyBuilder.ToString();
    }

    private static long EstimateSize(string jsonString)
    {
        // Rough estimate: UTF-8 encoding typically uses 1-4 bytes per character
        // Use 2 bytes as average estimate
        return jsonString.Length * 2;
    }
}

// Cache configuration extensions
public static class ComplianceReportCacheExtensions
{
    public static IServiceCollection AddComplianceReportCaching(this IServiceCollection services)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 100; // Limit to 100 cache entries
            options.CompactionPercentage = 0.2; // Remove 20% when limit reached
        });

        services.AddSingleton<IComplianceReportCacheService, ComplianceReportCacheService>();

        return services;
    }
}