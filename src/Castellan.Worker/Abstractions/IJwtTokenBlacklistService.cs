namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for managing JWT token blacklisting/invalidation
/// </summary>
public interface IJwtTokenBlacklistService
{
    /// <summary>
    /// Add a JWT token to the blacklist to prevent its further use
    /// </summary>
    /// <param name="jti">JWT ID (jti claim) to blacklist</param>
    /// <param name="expirationTime">When the token would naturally expire</param>
    /// <returns>Task representing the async operation</returns>
    Task BlacklistTokenAsync(string jti, DateTimeOffset expirationTime);

    /// <summary>
    /// Check if a JWT token is blacklisted
    /// </summary>
    /// <param name="jti">JWT ID (jti claim) to check</param>
    /// <returns>True if token is blacklisted, false otherwise</returns>
    Task<bool> IsTokenBlacklistedAsync(string jti);

    /// <summary>
    /// Clean up expired blacklist entries
    /// </summary>
    /// <returns>Number of entries removed</returns>
    Task<int> CleanupExpiredEntriesAsync();
}
