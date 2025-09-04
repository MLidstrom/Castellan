namespace Castellan.Worker.Models;

/// <summary>
/// Represents a secure refresh token for JWT authentication
/// </summary>
public class RefreshToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? RevokedReason { get; set; }

    /// <summary>
    /// Check if the refresh token is currently active (not expired or revoked)
    /// </summary>
    public bool IsActive => !IsRevoked && DateTimeOffset.UtcNow < ExpiresAt;

    /// <summary>
    /// Revoke this refresh token
    /// </summary>
    /// <param name="reason">Reason for revocation</param>
    /// <param name="replacedBy">Token that replaces this one (for rotation)</param>
    public void Revoke(string? reason = null, string? replacedBy = null)
    {
        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedReason = reason;
        ReplacedByToken = replacedBy;
    }
}
