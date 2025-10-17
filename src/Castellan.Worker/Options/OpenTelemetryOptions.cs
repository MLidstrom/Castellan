namespace Castellan.Worker.Options;

/// <summary>
/// Configuration options for OpenTelemetry distributed tracing.
/// Part of Phase 2 Week 4 AI Intelligence Upgrades (Observability).
/// </summary>
public sealed class OpenTelemetryOptions
{
    /// <summary>
    /// Enable OpenTelemetry tracing for AI operations.
    /// When disabled, no telemetry data is collected.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Service name for telemetry identification.
    /// Default: "castellan-worker"
    /// </summary>
    public string ServiceName { get; set; } = "castellan-worker";

    /// <summary>
    /// Service version for telemetry identification.
    /// Default: "0.7.0"
    /// </summary>
    public string ServiceVersion { get; set; } = "0.7.0";

    /// <summary>
    /// Enable console exporter for debugging.
    /// Shows traces in console output.
    /// Default: false (only enable for debugging)
    /// </summary>
    public bool EnableConsoleExporter { get; set; } = false;

    /// <summary>
    /// Enable OTLP (OpenTelemetry Protocol) exporter.
    /// Sends traces to an OTLP-compatible backend (Jaeger, Zipkin, etc.).
    /// Default: true
    /// </summary>
    public bool EnableOtlpExporter { get; set; } = true;

    /// <summary>
    /// OTLP endpoint URL.
    /// Default: "http://localhost:4317" (Jaeger/OTLP gRPC)
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Trace embedding operations (EmbedAsync calls).
    /// Default: true
    /// </summary>
    public bool TraceEmbeddings { get; set; } = true;

    /// <summary>
    /// Trace LLM operations (GenerateAsync calls).
    /// Default: true
    /// </summary>
    public bool TraceLlmCalls { get; set; } = true;

    /// <summary>
    /// Trace vector store operations (SearchAsync, UpsertAsync calls).
    /// Default: true
    /// </summary>
    public bool TraceVectorStore { get; set; } = true;

    /// <summary>
    /// Record prompt and completion text in spans.
    /// WARNING: This may expose sensitive data in traces. Only enable for debugging.
    /// Default: false
    /// </summary>
    public bool RecordTextContent { get; set; } = false;

    /// <summary>
    /// Maximum length of text content to record in spans.
    /// Only applies if RecordTextContent is true.
    /// Default: 500 characters
    /// </summary>
    public int MaxTextContentLength { get; set; } = 500;

    /// <summary>
    /// Validates configuration and ensures sensible defaults.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new InvalidOperationException("ServiceName cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(ServiceVersion))
        {
            throw new InvalidOperationException("ServiceVersion cannot be empty");
        }

        if (EnableOtlpExporter && string.IsNullOrWhiteSpace(OtlpEndpoint))
        {
            throw new InvalidOperationException("OtlpEndpoint cannot be empty when OTLP exporter is enabled");
        }

        if (MaxTextContentLength < 0)
        {
            throw new InvalidOperationException($"MaxTextContentLength must be >= 0: {MaxTextContentLength}");
        }
    }
}
