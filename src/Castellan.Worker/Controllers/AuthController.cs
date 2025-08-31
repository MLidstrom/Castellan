using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly AuthenticationOptions _authOptions;

    public AuthController(ILogger<AuthController> logger, IOptions<AuthenticationOptions> authOptions)
    {
        _logger = logger;
        _authOptions = authOptions.Value;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for user: {Username}", request.Username);

            // Validate credentials from configuration
            // TODO: In production, implement proper password hashing and user store
            if (string.IsNullOrEmpty(_authOptions.AdminUser.Username) || string.IsNullOrEmpty(_authOptions.AdminUser.Password))
            {
                _logger.LogError("Authentication not properly configured. Check AdminUser settings.");
                return StatusCode(500, new { message = "Authentication not configured" });
            }

            if (request.Username == _authOptions.AdminUser.Username && request.Password == _authOptions.AdminUser.Password)
            {
                var token = GenerateJwtToken(request.Username);
                var refreshToken = GenerateRefreshToken();

                var response = new LoginResponse
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds(),
                    User = new UserInfo
                    {
                        Id = "1",
                        Username = request.Username,
                        Email = _authOptions.AdminUser.Email,
                        Roles = new[] { "admin" },
                        Permissions = new[] { "security-events:read", "security-events:write", "compliance-reports:read", "system-status:read" },
                        Profile = new UserProfile
                        {
                            FirstName = _authOptions.AdminUser.FirstName,
                            LastName = _authOptions.AdminUser.LastName,
                            Avatar = null
                        }
                    }
                };

                _logger.LogInformation("Login successful for user: {Username}", request.Username);
                return Ok(response);
            }

            _logger.LogWarning("Login failed for user: {Username}", request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", request.Username);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            // In production, validate the refresh token against a secure store
            // For now, generate a new token
            var newToken = GenerateJwtToken(_authOptions.AdminUser.Username);
            var newRefreshToken = GenerateRefreshToken();

            var response = new RefreshTokenResponse
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            _logger.LogInformation("User logout");
            // In production, invalidate the token on the server side
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private string GenerateJwtToken(string username)
    {
        if (string.IsNullOrEmpty(_authOptions.Jwt.SecretKey))
        {
            throw new InvalidOperationException("JWT SecretKey not configured");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authOptions.Jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, _authOptions.AdminUser.Email),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("permissions", "security-events:read,security-events:write,compliance-reports:read,system-status:read")
        };

        var token = new JwtSecurityToken(
            issuer: _authOptions.Jwt.Issuer,
            audience: _authOptions.Jwt.Audience,
            claims: claims,
            expires: DateTime.Now.AddHours(_authOptions.Jwt.ExpirationHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }
}

// DTOs
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }
    public UserInfo User { get; set; } = new();
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public UserProfile? Profile { get; set; }
}

public class UserProfile
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Avatar { get; set; }
}