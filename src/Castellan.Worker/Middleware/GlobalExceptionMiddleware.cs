using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;

namespace Castellan.Worker.Middleware;

/// <summary>
/// Global exception handling middleware that provides consistent error responses and correlation ID tracking
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}", 
                context.TraceIdentifier, context.Request.Path, context.Request.Method);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            CorrelationId = context.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method
        };

        switch (exception)
        {
            case UnauthorizedAccessException:
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Title = "Unauthorized";
                response.Detail = "Access denied. Please authenticate and try again.";
                break;

            case ArgumentException argEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Title = "Bad Request";
                response.Detail = argEx.Message;
                break;

            case InvalidOperationException invalidOpEx:
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Title = "Invalid Operation";
                response.Detail = invalidOpEx.Message;
                break;

            case NotImplementedException:
                response.StatusCode = (int)HttpStatusCode.NotImplemented;
                response.Title = "Not Implemented";
                response.Detail = "This feature is not yet implemented.";
                break;

            case TimeoutException:
                response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                response.Title = "Request Timeout";
                response.Detail = "The request took too long to process.";
                break;

            case HttpRequestException httpEx:
                response.StatusCode = (int)HttpStatusCode.BadGateway;
                response.Title = "External Service Error";
                response.Detail = "An error occurred while communicating with an external service.";
                break;

            default:
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Title = "Internal Server Error";
                response.Detail = "An unexpected error occurred. Please contact support if the problem persists.";
                break;
        }

        context.Response.StatusCode = response.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

/// <summary>
/// Standard error response format for consistent API error handling
/// </summary>
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public Dictionary<string, object>? AdditionalData { get; set; }
}

/// <summary>
/// Extension methods for registering the global exception middleware
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    /// <summary>
    /// Registers the global exception handling middleware in the application pipeline
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
