using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Castellan.Worker.Configuration;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly AuthenticationOptions _authOptions;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IJwtTokenBlacklistService _jwtTokenBlacklistService;

    public AuthController(
        ILogger<AuthController> logger, 
        IOptions<AuthenticationOptions> authOptions,
        IPasswordHashingService passwordHashingService,
        IRefreshTokenService refreshTokenService,
        IJwtTokenBlacklistService jwtTokenBlacklistService)
    {
        _logger = logger;
        _authOptions = authOptions.Value;
        _passwordHashingService = passwordHashingService;
        _refreshTokenService = refreshTokenService;
        _jwtTokenBlacklistService = jwtTokenBlacklistService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Login attempt for user: {Username}", request.Username);

            // Validate credentials from configuration
            if (string.IsNullOrEmpty(_authOptions.AdminUser.Username) || string.IsNullOrEmpty(_authOptions.AdminUser.Password))
            {
                _logger.LogError("Authentication not properly configured. Check AdminUser settings.");
                return StatusCode(500, new { message = "Authentication not configured" });
            }

            // Verify username and password using secure BCrypt comparison
            var isValidUser = request.Username == _authOptions.AdminUser.Username;
            var isValidPassword = isValidUser && _passwordHashingService.VerifyPassword(request.Password, _authOptions.AdminUser.Password);
            
            if (isValidUser && isValidPassword)
            {
                var jwtToken = GenerateJwtToken(request.Username);
                var refreshTokenResult = await _refreshTokenService.GenerateRefreshTokenAsync("1", 30);

                var response = new LoginResponse
                {
                    Token = jwtToken,
                    RefreshToken = refreshTokenResult.Token,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(_authOptions.Jwt.ExpirationHours).ToUnixTimeMilliseconds(),
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

            _logger.LogWarning("Login failed for user: {Username}. Invalid credentials.", request.Username);
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
            _logger.LogInformation("Refresh token request received");

            // Validate the refresh token
            var validationResult = await _refreshTokenService.ValidateRefreshTokenAsync(request.RefreshToken);
            if (validationResult == null || !validationResult.IsActive)
            {
                _logger.LogWarning("Invalid refresh token provided");
                return Unauthorized(new { message = "Invalid refresh token" });
            }

            // Rotate the refresh token (revoke old, create new)
            var rotationResult = await _refreshTokenService.RotateRefreshTokenAsync(request.RefreshToken, validationResult.UserId);
            if (rotationResult == null)
            {
                _logger.LogWarning("Failed to rotate refresh token");
                return Unauthorized(new { message = "Token rotation failed" });
            }

            // Generate new JWT token
            var newJwtToken = GenerateJwtToken(validationResult.UserId);

            var response = new RefreshTokenResponse
            {
                Token = newJwtToken,
                RefreshToken = rotationResult.Token,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(_authOptions.Jwt.ExpirationHours).ToUnixTimeMilliseconds()
            };

            _logger.LogInformation("Token refresh successful for user: {UserId}", validationResult.UserId);
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
            var username = User.Identity?.Name ?? "unknown";
            _logger.LogInformation("User logout for: {Username}", username);

            // Get the JWT token from the Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                
                // Extract JTI and expiration from the JWT token for blacklisting
                var tokenHandler = new JwtSecurityTokenHandler();
                try
                {
                    var jsonToken = tokenHandler.ReadJwtToken(token);
                    var jti = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                    var expiration = jsonToken.ValidTo;
                    
                    if (!string.IsNullOrEmpty(jti))
                    {
                        // Blacklist the JWT token
                        await _jwtTokenBlacklistService.BlacklistTokenAsync(jti, expiration);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JWT token for blacklisting");
                }
                
                // Also revoke any refresh tokens for this user (if we had user ID)
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    await _refreshTokenService.RevokeAllUserTokensAsync(userId);
                }
                
                _logger.LogInformation("Successfully logged out user: {Username}", username);
            }
            else
            {
                _logger.LogWarning("Logout attempted without valid Authorization header");
            }

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
            new Claim("permissions", "security-events:read,security-events:write,compliance-reports:read,system-status:read"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // Add JTI for blacklisting
        };

        var token = new JwtSecurityToken(
            issuer: _authOptions.Jwt.Issuer,
            audience: _authOptions.Jwt.Audience,
            claims: claims,
            expires: DateTime.Now.AddHours(_authOptions.Jwt.ExpirationHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
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