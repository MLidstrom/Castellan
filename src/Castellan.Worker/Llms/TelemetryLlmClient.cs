using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Options;

namespace Castellan.Worker.Llms;

/// <summary>
/// Decorator that adds OpenTelemetry distributed tracing to LLM operations.
/// Part of Phase 2 Week 4 AI Intelligence Upgrades (Observability).
///
/// Architecture:
/// - Wraps an existing ILlmClient implementation
/// - Creates OpenTelemetry spans for each AnalyzeAsync call
/// - Records timing, event context, and errors
/// - Optionally records analysis results (if enabled)
///
/// Tracing Attributes (following OpenTelemetry GenAI Semantic Conventions):
/// - gen_ai.system: "ollama" or "openai"
/// - llm.event_id: Windows Event ID being analyzed
/// - llm.channel: Event log channel
/// - llm.neighbors_count: Number of similar events provided
/// </summary>
public sealed class TelemetryLlmClient : ILlmClient
{
    private readonly ILlmClient _inner;
    private readonly OpenTelemetryOptions _options;
    private readonly ILogger<TelemetryLlmClient>? _logger;

    private static readonly ActivitySource ActivitySource = new("Castellan.AI.LLM");

    public TelemetryLlmClient(
        ILlmClient inner,
        IOptions<OpenTelemetryOptions> options,
        ILogger<TelemetryLlmClient>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<string> AnalyzeAsync(LogEvent e, IEnumerable<LogEvent> neighbors, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.TraceLlmCalls)
        {
            // Telemetry disabled, pass through
            return await _inner.AnalyzeAsync(e, neighbors, ct);
        }

        using var activity = ActivitySource.StartActivity("llm.analyze", ActivityKind.Client);
        if (activity == null)
        {
            // No listener registered, pass through
            return await _inner.AnalyzeAsync(e, neighbors, ct);
        }

        try
        {
            // Add standard GenAI semantic convention tags
            activity.SetTag("gen_ai.operation.name", "security_analysis");
            activity.SetTag("gen_ai.system", GetProviderName());

            // Record event context
            activity.SetTag("llm.event_id", e.EventId);
            activity.SetTag("llm.channel", e.Channel);
            activity.SetTag("llm.host", e.Host);
            activity.SetTag("llm.neighbors_count", neighbors?.Count() ?? 0);

            var startTime = DateTimeOffset.UtcNow;
            var analysis = await _inner.AnalyzeAsync(e, neighbors ?? Enumerable.Empty<LogEvent>(), ct);
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            // Record response metadata
            activity.SetTag("llm.analysis_length", analysis?.Length ?? 0);
            activity.SetTag("llm.duration_ms", duration);

            // Optionally record analysis result (be cautious with sensitive data)
            if (_options.RecordTextContent && !string.IsNullOrEmpty(analysis))
            {
                activity.SetTag("llm.analysis", TruncateText(analysis, _options.MaxTextContentLength));
            }

            activity.SetStatus(ActivityStatusCode.Ok);

            _logger?.LogDebug("LLM analysis traced: EventId={EventId} duration={Duration}ms neighbors={NeighborsCount}",
                e.EventId, duration, neighbors?.Count() ?? 0);

            return analysis ?? string.Empty;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("exception.type", ex.GetType().FullName);
            activity.SetTag("exception.message", ex.Message);
            activity.SetTag("exception.stacktrace", ex.StackTrace);

            _logger?.LogError(ex, "LLM analysis failed and traced for EventId={EventId}", e.EventId);

            throw;
        }
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.TraceLlmCalls)
        {
            // Telemetry disabled, pass through
            return await _inner.GenerateAsync(systemPrompt, userPrompt, ct);
        }

        using var activity = ActivitySource.StartActivity("llm.generate", ActivityKind.Client);
        if (activity == null)
        {
            // No listener registered, pass through
            return await _inner.GenerateAsync(systemPrompt, userPrompt, ct);
        }

        try
        {
            // Add standard GenAI semantic convention tags
            activity.SetTag("gen_ai.operation.name", "chat_generation");
            activity.SetTag("gen_ai.system", GetProviderName());

            // Record prompt metadata
            activity.SetTag("llm.system_prompt_length", systemPrompt?.Length ?? 0);
            activity.SetTag("llm.user_prompt_length", userPrompt?.Length ?? 0);

            var startTime = DateTimeOffset.UtcNow;
            var response = await _inner.GenerateAsync(systemPrompt, userPrompt, ct);
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            // Record response metadata
            activity.SetTag("llm.response_length", response?.Length ?? 0);
            activity.SetTag("llm.duration_ms", duration);

            // Optionally record prompts and response (be cautious with sensitive data)
            if (_options.RecordTextContent)
            {
                activity.SetTag("llm.system_prompt", TruncateText(systemPrompt, _options.MaxTextContentLength));
                activity.SetTag("llm.user_prompt", TruncateText(userPrompt, _options.MaxTextContentLength));
                if (!string.IsNullOrEmpty(response))
                {
                    activity.SetTag("llm.response", TruncateText(response, _options.MaxTextContentLength));
                }
            }

            activity.SetStatus(ActivityStatusCode.Ok);

            _logger?.LogDebug("LLM generation traced: duration={Duration}ms response_length={ResponseLength}",
                duration, response?.Length ?? 0);

            return response ?? string.Empty;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity.SetTag("exception.type", ex.GetType().FullName);
            activity.SetTag("exception.message", ex.Message);
            activity.SetTag("exception.stacktrace", ex.StackTrace);

            _logger?.LogError(ex, "LLM generation failed and traced");

            throw;
        }
    }

    private string GetProviderName()
    {
        // Detect provider from inner client type
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
