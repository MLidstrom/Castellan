using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for managing secure refresh tokens with rotation and revocation
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Generate a new secure refresh token for a user
    /// </summary>
    /// <param name="userId">The user ID to generate token for</param>
    /// <param name="expirationDays">Number of days until token expires</param>
    /// <returns>The generated refresh token</returns>
    Task<RefreshToken> GenerateRefreshTokenAsync(string userId, int expirationDays = 30);

    /// <summary>
    /// Validate a refresh token and return the associated token if valid
    /// </summary>
    /// <param name="tokenValue">The refresh token value to validate</param>
    /// <returns>The refresh token if valid, null otherwise</returns>
    Task<RefreshToken?> ValidateRefreshTokenAsync(string tokenValue);

    /// <summary>
    /// Rotate a refresh token (revoke old, generate new)
    /// </summary>
    /// <param name="oldTokenValue">The current refresh token to rotate</param>
    /// <param name="userId">The user ID</param>
    /// <returns>New refresh token if rotation successful, null otherwise</returns>
    Task<RefreshToken?> RotateRefreshTokenAsync(string oldTokenValue, string userId);

    /// <summary>
    /// Revoke a refresh token
    /// </summary>
    /// <param name="tokenValue">The refresh token to revoke</param>
    /// <param name="reason">Reason for revocation</param>
    /// <returns>True if token was revoked, false if not found</returns>
    Task<bool> RevokeRefreshTokenAsync(string tokenValue, string? reason = null);

    /// <summary>
    /// Revoke all refresh tokens for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="reason">Reason for revocation</param>
    /// <returns>Number of tokens revoked</returns>
    Task<int> RevokeAllUserTokensAsync(string userId, string? reason = null);

    /// <summary>
    /// Clean up expired refresh tokens
    /// </summary>
    /// <returns>Number of tokens cleaned up</returns>
    Task<int> CleanupExpiredTokensAsync();
}
