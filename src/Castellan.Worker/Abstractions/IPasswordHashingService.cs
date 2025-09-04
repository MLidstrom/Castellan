namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for secure password hashing and verification using BCrypt
/// </summary>
public interface IPasswordHashingService
{
    /// <summary>
    /// Hash a plaintext password using BCrypt with secure salt
    /// </summary>
    /// <param name="password">The plaintext password to hash</param>
    /// <returns>The secure BCrypt hash</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verify a plaintext password against a BCrypt hash
    /// </summary>
    /// <param name="password">The plaintext password to verify</param>
    /// <param name="hashedPassword">The BCrypt hash to verify against</param>
    /// <returns>True if password matches hash, false otherwise</returns>
    bool VerifyPassword(string password, string hashedPassword);

    /// <summary>
    /// Validate password complexity requirements
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>Validation result with any error messages</returns>
    PasswordValidationResult ValidatePasswordComplexity(string password);
}

/// <summary>
/// Result of password complexity validation
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static PasswordValidationResult Success() => new() { IsValid = true };
    
    public static PasswordValidationResult Failure(params string[] errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };
}
