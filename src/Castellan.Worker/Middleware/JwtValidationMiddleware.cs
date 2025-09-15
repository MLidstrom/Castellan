using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Middleware;

/// <summary>
/// Middleware to validate JWT tokens and check blacklist status
/// </summary>
public class JwtValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IJwtTokenBlacklistService _blacklistService;
    private readonly ILogger<JwtValidationMiddleware> _logger;

    public JwtValidationMiddleware(
        RequestDelegate next, 
        IJwtTokenBlacklistService blacklistService,
        ILogger<JwtValidationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _blacklistService = blacklistService ?? throw new ArgumentNullException(nameof(blacklistService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for non-authenticated endpoints
        if (!RequiresAuthentication(context))
        {
            await _next(context);
            return;
        }

        var token = ExtractTokenFromRequest(context);
        if (string.IsNullOrEmpty(token))
        {
            // Let the authentication handler deal with missing tokens
            await _next(context);
            return;
        }

        try
        {
            var jti = ExtractJtiFromToken(token);
            if (!string.IsNullOrEmpty(jti))
            {
                var isBlacklisted = await _blacklistService.IsTokenBlacklistedAsync(jti);
                if (isBlacklisted)
                {
                    _logger.LogWarning("Access denied for blacklisted JWT token: {JwtId} from {RemoteIpAddress}", 
                        jti, context.Connection.RemoteIpAddress);
                    
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("{\"message\":\"Token has been revoked\"}");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating JWT token blacklist status");
            // Continue processing - let the authentication handler validate the token structure
        }

        await _next(context);
    }

    private static bool RequiresAuthentication(HttpContext context)
    {
        // Skip SignalR paths completely
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            return false;
        }
        
        // Check if the endpoint requires authentication
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>() != null)
        {
            return true;
        }

        // Check for Authorization header
        return context.Request.Headers.ContainsKey("Authorization");
    }

    private static string? ExtractTokenFromRequest(HttpContext context)
    {
        var authorizationHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(authorizationHeader))
            return null;

        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        return authorizationHeader["Bearer ".Length..].Trim();
    }

    private string? ExtractJtiFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            // Parse token without validation (just to extract claims)
            var jsonToken = handler.ReadJwtToken(token);
            
            return jsonToken?.Claims?.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract JTI from JWT token");
            return null;
        }
    }
}
