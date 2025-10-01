using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Services;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for authentication services
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Adds authentication services including JWT, password hashing, and token management
    /// </summary>
    public static IServiceCollection AddCastellanAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register authentication options
        services.Configure<AuthenticationOptions>(
            configuration.GetSection(AuthenticationOptions.SectionName));

        // Add security services
        services.AddSingleton<IPasswordHashingService, BCryptPasswordHashingService>();
        services.AddSingleton<IJwtTokenBlacklistService, MemoryJwtTokenBlacklistService>();
        services.AddSingleton<IRefreshTokenService, MemoryRefreshTokenService>();

        // Add JWT Authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var authOptions = configuration
                    .GetSection(AuthenticationOptions.SectionName)
                    .Get<AuthenticationOptions>();

                if (authOptions?.Jwt == null || string.IsNullOrEmpty(authOptions.Jwt.SecretKey))
                {
                    throw new InvalidOperationException(
                        "JWT SecretKey not configured in Authentication:Jwt:SecretKey");
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = authOptions.Jwt.Issuer,
                    ValidAudience = authOptions.Jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(authOptions.Jwt.SecretKey))
                };

                // Configure JWT for SignalR
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        // If the request is for our SignalR hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        // Don't fail negotiation for SignalR - allow anonymous negotiation
                        if (context.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.NoResult();
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
