using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace Castellan.Worker.Middleware;

/// <summary>
/// Middleware that ensures correlation ID tracking for all requests
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Set the correlation ID in response headers for client tracking
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;
        
        // Set trace identifier to ensure consistent correlation ID usage
        context.TraceIdentifier = correlationId;
        
        // Add correlation ID to logging scope
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.Value ?? "",
            ["RequestMethod"] = context.Request.Method
        });

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Log request completion with correlation ID
            _logger.LogInformation("Request completed. CorrelationId: {CorrelationId}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                correlationId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check if correlation ID is provided in request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.ToString();
        }

        // Check if we already have a trace identifier
        if (!string.IsNullOrWhiteSpace(context.TraceIdentifier) &&
            !context.TraceIdentifier.StartsWith("0HN", StringComparison.OrdinalIgnoreCase))
        {
            return context.TraceIdentifier;
        }

        // Generate a new correlation ID
        return GenerateCorrelationId();
    }

    private static string GenerateCorrelationId()
    {
        // Generate a compact but unique correlation ID
        return $"COR-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
    }
}

/// <summary>
/// Extension methods for registering the correlation ID middleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Registers the correlation ID middleware in the application pipeline
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
