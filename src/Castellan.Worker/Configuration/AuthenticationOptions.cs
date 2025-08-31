namespace Castellan.Worker.Configuration;

public class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    
    public JwtOptions Jwt { get; set; } = new();
    public AdminUserOptions AdminUser { get; set; } = new();
}

public class JwtOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "castellan-security";
    public string Audience { get; set; } = "castellan-admin";
    public int ExpirationHours { get; set; } = 24;
}

public class AdminUserOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = "admin@castellan.security";
    public string FirstName { get; set; } = "Castellan";
    public string LastName { get; set; } = "Administrator";
}