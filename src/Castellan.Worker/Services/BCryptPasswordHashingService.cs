using System.Text.RegularExpressions;
using BCrypt.Net;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services;

/// <summary>
/// BCrypt implementation of password hashing service with secure salt generation
/// </summary>
public class BCryptPasswordHashingService : IPasswordHashingService
{
    private const int WorkFactor = 12; // Recommended work factor for BCrypt (2^12 iterations)

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        // Generate salt and hash with BCrypt using secure work factor
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;
        
        if (string.IsNullOrWhiteSpace(hashedPassword))
            return false;

        try
        {
            // BCrypt handles salt extraction and verification internally
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch (Exception)
        {
            // Invalid hash format or other BCrypt errors
            return false;
        }
    }

    public PasswordValidationResult ValidatePasswordComplexity(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return PasswordValidationResult.Failure("Password is required");
        }

        var errors = new List<string>();

        // Minimum length requirement
        if (password.Length < 8)
        {
            errors.Add("Password must be at least 8 characters long");
        }

        // Maximum length to prevent DoS attacks
        if (password.Length > 128)
        {
            errors.Add("Password must be no more than 128 characters long");
        }

        // Require at least one lowercase letter
        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        // Require at least one uppercase letter
        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        // Require at least one digit
        if (!Regex.IsMatch(password, @"\d"))
        {
            errors.Add("Password must contain at least one number");
        }

        // Require at least one special character
        if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?~`]"))
        {
            errors.Add("Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;':\",./<>?~`)");
        }

        // Check for common weak patterns
        if (ContainsCommonWeakPatterns(password))
        {
            errors.Add("Password contains common weak patterns (e.g., 123, abc, password)");
        }

        return errors.Count == 0 
            ? PasswordValidationResult.Success() 
            : PasswordValidationResult.Failure(errors.ToArray());
    }

    private static bool ContainsCommonWeakPatterns(string password)
    {
        var lowerPassword = password.ToLowerInvariant();

        // Common weak patterns
        var weakPatterns = new[]
        {
            "123", "234", "345", "456", "567", "678", "789", "012",
            "abc", "bcd", "cde", "def", "efg", "fgh", "ghi", "hij",
            "password", "admin", "user", "test", "demo", "guest",
            "qwerty", "asdf", "zxcv", "1234", "abcd"
        };

        return weakPatterns.Any(pattern => lowerPassword.Contains(pattern));
    }
}
