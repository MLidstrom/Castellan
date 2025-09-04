using System.Collections.Concurrent;
using System.Security.Cryptography;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// In-memory implementation of refresh token service for development/single-instance deployments
/// Note: In production, consider using a persistent store (database/Redis)
/// </summary>
public class MemoryRefreshTokenService : IRefreshTokenService
{
    private readonly ConcurrentDictionary<string, RefreshToken> _tokens = new();
    private readonly ILogger<MemoryRefreshTokenService> _logger;

    public MemoryRefreshTokenService(ILogger<MemoryRefreshTokenService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<RefreshToken> GenerateRefreshTokenAsync(string userId, int expirationDays = 30)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        var token = new RefreshToken
        {
            Token = GenerateSecureToken(),
            UserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(expirationDays)
        };

        _tokens[token.Token] = token;

        _logger.LogInformation("Generated refresh token for user {UserId}, expires {ExpirationTime}", 
            userId, token.ExpiresAt);

        return Task.FromResult(token);
    }

    public Task<RefreshToken?> ValidateRefreshTokenAsync(string tokenValue)
    {
        if (string.IsNullOrWhiteSpace(tokenValue))
            return Task.FromResult<RefreshToken?>(null);

        if (!_tokens.TryGetValue(tokenValue, out var token))
        {
            _logger.LogWarning("Refresh token validation failed: token not found");
            return Task.FromResult<RefreshToken?>(null);
        }

        if (!token.IsActive)
        {
            _logger.LogWarning("Refresh token validation failed: token is expired or revoked (User: {UserId})", 
                token.UserId);
            return Task.FromResult<RefreshToken?>(null);
        }

        _logger.LogDebug("Refresh token validated successfully for user {UserId}", token.UserId);
        return Task.FromResult<RefreshToken?>(token);
    }

    public async Task<RefreshToken?> RotateRefreshTokenAsync(string oldTokenValue, string userId)
    {
        var oldToken = await ValidateRefreshTokenAsync(oldTokenValue);
        if (oldToken == null || oldToken.UserId != userId)
        {
            _logger.LogWarning("Refresh token rotation failed: invalid token for user {UserId}", userId);
            return null;
        }

        // Generate new token
        var newToken = await GenerateRefreshTokenAsync(userId);

        // Revoke old token
        oldToken.Revoke("Token rotated", newToken.Token);

        _logger.LogInformation("Refresh token rotated for user {UserId}", userId);
        return newToken;
    }

    public Task<bool> RevokeRefreshTokenAsync(string tokenValue, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(tokenValue))
            return Task.FromResult(false);

        if (!_tokens.TryGetValue(tokenValue, out var token))
            return Task.FromResult(false);

        token.Revoke(reason ?? "Manual revocation");

        _logger.LogInformation("Refresh token revoked for user {UserId}: {Reason}", 
            token.UserId, reason ?? "Manual revocation");

        return Task.FromResult(true);
    }

    public Task<int> RevokeAllUserTokensAsync(string userId, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(0);

        var userTokens = _tokens.Values.Where(t => t.UserId == userId && t.IsActive).ToList();
        var revokedCount = 0;

        foreach (var token in userTokens)
        {
            token.Revoke(reason ?? "All tokens revoked");
            revokedCount++;
        }

        _logger.LogInformation("Revoked {Count} refresh tokens for user {UserId}: {Reason}", 
            revokedCount, userId, reason ?? "All tokens revoked");

        return Task.FromResult(revokedCount);
    }

    public Task<int> CleanupExpiredTokensAsync()
    {
        var expiredTokens = _tokens.Values.Where(t => !t.IsActive).ToList();
        var cleanedCount = 0;

        foreach (var token in expiredTokens)
        {
            _tokens.TryRemove(token.Token, out _);
            cleanedCount++;
        }

        if (cleanedCount > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired refresh tokens", cleanedCount);
        }

        return Task.FromResult(cleanedCount);
    }

    private static string GenerateSecureToken()
    {
        // Generate 32 bytes of cryptographically secure random data
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        
        // Convert to base64 for storage/transmission
        return Convert.ToBase64String(randomBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
