using Microsoft.Extensions.Options;

namespace Castellan.Worker.Configuration.Validation;

/// <summary>
/// Validates AuthenticationOptions configuration at startup
/// </summary>
public class AuthenticationOptionsValidator : IValidateOptions<AuthenticationOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthenticationOptions options)
    {
        var failures = new List<string>();

        // Validate JWT configuration
        if (options.Jwt == null)
        {
            failures.Add("JWT configuration is required");
        }
        else
        {
            if (string.IsNullOrEmpty(options.Jwt.SecretKey))
            {
                failures.Add("JWT SecretKey is required");
            }
            else if (options.Jwt.SecretKey.Length < 64)
            {
                failures.Add("JWT SecretKey must be at least 64 characters long for security");
            }

            if (string.IsNullOrEmpty(options.Jwt.Issuer))
            {
                failures.Add("JWT Issuer is required");
            }

            if (string.IsNullOrEmpty(options.Jwt.Audience))
            {
                failures.Add("JWT Audience is required");
            }

            if (options.Jwt.ExpirationHours <= 0 || options.Jwt.ExpirationHours > 24)
            {
                failures.Add("JWT ExpirationHours must be between 1 and 24 hours");
            }
        }

        // Validate AdminUser configuration
        if (options.AdminUser == null)
        {
            failures.Add("AdminUser configuration is required");
        }
        else
        {
            if (string.IsNullOrEmpty(options.AdminUser.Username))
            {
                failures.Add("AdminUser Username is required");
            }

            if (string.IsNullOrEmpty(options.AdminUser.Password))
            {
                failures.Add("AdminUser Password is required");
            }
            else if (options.AdminUser.Password.Length < 8)
            {
                failures.Add("AdminUser Password must be at least 8 characters long");
            }

            if (string.IsNullOrEmpty(options.AdminUser.Email))
            {
                failures.Add("AdminUser Email is required");
            }
            else if (!IsValidEmail(options.AdminUser.Email))
            {
                failures.Add("AdminUser Email must be a valid email address");
            }
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail($"Authentication configuration validation failed: {string.Join(", ", failures)}");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
