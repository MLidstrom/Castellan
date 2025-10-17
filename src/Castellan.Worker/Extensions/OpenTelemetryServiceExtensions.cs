using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Castellan.Worker.Options;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for OpenTelemetry distributed tracing.
/// Part of Phase 2 Week 4 AI Intelligence Upgrades (Observability).
/// </summary>
public static class OpenTelemetryServiceExtensions
{
    /// <summary>
    /// Adds OpenTelemetry distributed tracing for Castellan AI operations.
    /// Instruments LLM calls, embeddings, and vector store operations.
    /// </summary>
    public static IServiceCollection AddCastellanOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure OpenTelemetry options
        services.Configure<OpenTelemetryOptions>(configuration.GetSection("OpenTelemetry"));

        // Get options for conditional configuration
        var options = configuration.GetSection("OpenTelemetry").Get<OpenTelemetryOptions>()
                     ?? new OpenTelemetryOptions();

        if (!options.Enabled)
        {
            // OpenTelemetry disabled, skip configuration
            return services;
        }

        // Configure OpenTelemetry SDK
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceVersion: options.ServiceVersion))
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    // Add ActivitySources for our AI operations
                    .AddSource("Castellan.AI.LLM")
                    .AddSource("Castellan.AI.Embeddings")
                    .AddSource("Castellan.AI.VectorStore");

                // Add built-in instrumentation for ASP.NET Core and HTTP
                tracerProviderBuilder
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        // Filter out health check endpoints from traces
                        opts.Filter = httpContext =>
                        {
                            var path = httpContext.Request.Path.Value ?? string.Empty;
                            return !path.Contains("/health") && !path.Contains("/metrics");
                        };
                    })
                    .AddHttpClientInstrumentation();

                // Add console exporter (for debugging)
                if (options.EnableConsoleExporter)
                {
                    tracerProviderBuilder.AddConsoleExporter();
                }

                // Add OTLP exporter (for production backends like Jaeger, Zipkin)
                if (options.EnableOtlpExporter)
                {
                    tracerProviderBuilder.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            });

        return services;
    }
}
