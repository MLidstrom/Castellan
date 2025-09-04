using Microsoft.Extensions.Caching.Memory;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services;

/// <summary>
/// In-memory implementation of JWT token blacklist service using IMemoryCache
/// </summary>
public class MemoryJwtTokenBlacklistService : IJwtTokenBlacklistService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryJwtTokenBlacklistService> _logger;
    private const string BlacklistKeyPrefix = "jwt_blacklist:";

    public MemoryJwtTokenBlacklistService(IMemoryCache cache, ILogger<MemoryJwtTokenBlacklistService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task BlacklistTokenAsync(string jti, DateTimeOffset expirationTime)
    {
        if (string.IsNullOrWhiteSpace(jti))
            throw new ArgumentException("JWT ID cannot be null or empty", nameof(jti));

        var key = GetBlacklistKey(jti);
        
        // Store in cache until the token would naturally expire
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expirationTime,
            Priority = CacheItemPriority.High // Keep blacklisted tokens in memory
        };

        _cache.Set(key, true, options);
        
        _logger.LogInformation("JWT token blacklisted: {JwtId} (expires: {ExpirationTime})", 
            jti, expirationTime);

        return Task.CompletedTask;
    }

    public Task<bool> IsTokenBlacklistedAsync(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti))
            return Task.FromResult(false);

        var key = GetBlacklistKey(jti);
        var isBlacklisted = _cache.TryGetValue(key, out _);

        if (isBlacklisted)
        {
            _logger.LogWarning("Blocked access attempt with blacklisted JWT: {JwtId}", jti);
        }

        return Task.FromResult(isBlacklisted);
    }

    public Task<int> CleanupExpiredEntriesAsync()
    {
        // MemoryCache automatically removes expired entries, so no manual cleanup needed
        _logger.LogDebug("Memory cache cleanup requested (automatic cleanup in effect)");
        return Task.FromResult(0);
    }

    private static string GetBlacklistKey(string jti) => $"{BlacklistKeyPrefix}{jti}";
}
