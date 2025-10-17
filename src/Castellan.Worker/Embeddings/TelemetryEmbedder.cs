using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Options;

namespace Castellan.Worker.Embeddings;

/// <summary>
/// Decorator that adds OpenTelemetry distributed tracing to embedding operations.
/// Part of Phase 2 Week 4 AI Intelligence Upgrades (Observability).
///
/// Architecture:
/// - Wraps an existing IEmbedder implementation
/// - Creates OpenTelemetry spans for each EmbedAsync call
/// - Records timing, input length, embedding dimensions, and errors
/// - Optionally records input text (if enabled)
///
/// Tracing Attributes (following OpenTelemetry GenAI Semantic Conventions):
/// - gen_ai.system: "ollama" or "openai"
/// - gen_ai.request.model: Model name
/// - gen_ai.embedding.dimension: Embedding vector size
/// - embedding.input_length: Input text character count
/// </summary>
public sealed class TelemetryEmbedder : IEmbedder
{
    private readonly IEmbedder _inner;
    private readonly OpenTelemetryOptions _options;
    private readonly ILogger<TelemetryEmbedder>? _logger;

    private static readonly ActivitySource ActivitySource = new("Castellan.AI.Embeddings");

    public TelemetryEmbedder(
        IEmbedder inner,
        IOptions<OpenTelemetryOptions> options,
        ILogger<TelemetryEmbedder>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.TraceEmbeddings)
        {
            // Telemetry disabled, pass through
            return await _inner.EmbedAsync(text, ct);
        }

        using var activity = ActivitySource.StartActivity("embedding.create", ActivityKind.Client);
        if (activity == null)
        {
            // No listener registered, pass through
            return await _inner.EmbedAsync(text, ct);
        }

        try
        {
            // Add standard GenAI semantic convention tags
            activity.SetTag("gen_ai.operation.name", "embedding");
            activity.SetTag("gen_ai.system", GetProviderName());

            // Record input metadata
            activity.SetTag("embedding.input_length", text?.Length ?? 0);

            // Optionally record input text (for debugging only)
            if (_options.RecordTextContent)
            {
                activity.SetTag("embedding.input_text", TruncateText(text, _options.MaxTextContentLength));
            }

            var startTime = DateTimeOffset.UtcNow;
            var embedding = await _inner.EmbedAsync(text ?? string.Empty, ct);
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            // Record response metadata
            activity.SetTag("gen_ai.embedding.dimension", embedding.Length);
            activity.SetTag("embedding.duration_ms", duration);

            activity.SetStatus(ActivityStatusCode.Ok);

            _logger?.LogDebug("Embedding call traced: input_length={InputLength} dimension={Dimension} duration={Duration}ms",
                text?.Length ?? 0, embedding.Length, duration);

            return embedding;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("exception.type", ex.GetType().FullName);
            activity.SetTag("exception.message", ex.Message);
            activity.SetTag("exception.stacktrace", ex.StackTrace);

            _logger?.LogError(ex, "Embedding call failed and traced");

            throw;
        }
    }

    private string GetProviderName()
    {
        // Detect provider from inner embedder type
        var typeName = _inner.GetType().Name.ToLowerInvariant();
        if (typeName.Contains("ollama"))
            return "ollama";
        if (typeName.Contains("openai"))
            return "openai";
        return "unknown";
    }

    private static string? TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "... [truncated]";
    }
}
