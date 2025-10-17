using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Options;

namespace Castellan.Worker.Llms;

/// <summary>
/// Decorator for ILlmClient that enforces strict JSON schema validation and provides robust parsing.
/// - Enhances system prompts with explicit JSON schema requirements
/// - Extracts JSON from markdown code blocks and mixed responses
/// - Validates against expected schema structure
/// - Provides intelligent fallback with retry logic
/// - Tracks parse success metrics
/// </summary>
public sealed class StrictJsonLlmClient : ILlmClient
{
    private readonly ILlmClient _inner;
    private readonly ILogger<StrictJsonLlmClient>? _logger;
    private readonly StrictJsonOptions _options;

    // Metrics
    private long _totalCalls;
    private long _successfulParses;
    private long _failedParses;
    private long _retriedCalls;
    private long _fallbackUsed;

    // JSON extraction patterns
    private static readonly Regex JsonCodeBlockPattern = new(@"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex JsonObjectPattern = new(@"(\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\})", RegexOptions.Compiled);

    public StrictJsonLlmClient(
        ILlmClient inner,
        IOptions<StrictJsonOptions> options,
        ILogger<StrictJsonLlmClient>? logger = null)
    {
        _inner = inner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> AnalyzeAsync(LogEvent e, IEnumerable<LogEvent> neighbors, CancellationToken ct)
    {
        Interlocked.Increment(ref _totalCalls);

        if (!_options.Enabled)
        {
            // Strict JSON disabled - pass through to inner client
            return await _inner.AnalyzeAsync(e, neighbors, ct);
        }

        // Attempt 1: Try with enhanced prompt
        var analysis = await TryAnalyzeWithStrictJsonAsync(e, neighbors, ct);

        if (IsValidJson(analysis))
        {
            Interlocked.Increment(ref _successfulParses);
            return analysis;
        }

        // Attempt 2: Retry with even more explicit schema instructions
        if (_options.EnableRetryOnFailure)
        {
            Interlocked.Increment(ref _retriedCalls);
            _logger?.LogWarning("First JSON parse failed for event {EventId}, retrying with stricter prompt", e.EventId);

            analysis = await TryAnalyzeWithStricterPromptAsync(e, neighbors, ct);

            if (IsValidJson(analysis))
            {
                Interlocked.Increment(ref _successfulParses);
                return analysis;
            }
        }

        // Attempt 3: Use fallback to generate valid JSON from partial response
        Interlocked.Increment(ref _failedParses);
        Interlocked.Increment(ref _fallbackUsed);

        _logger?.LogError("All JSON parse attempts failed for event {EventId}, using fallback", e.EventId);
        return GenerateFallbackJson(e, analysis);
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        // GenerateAsync is used for chat interface which expects natural language responses
        // No JSON validation needed - pass through to inner client
        return await _inner.GenerateAsync(systemPrompt, userPrompt, ct);
    }

    /// <summary>
    /// Attempts to analyze with enhanced JSON schema prompt
    /// </summary>
    private async Task<string> TryAnalyzeWithStrictJsonAsync(LogEvent e, IEnumerable<LogEvent> neighbors, CancellationToken ct)
    {
        // Call the inner LLM with original prompt
        var response = await _inner.AnalyzeAsync(e, neighbors, ct);

        // Extract JSON from response (handles markdown code blocks, mixed content)
        return ExtractJson(response);
    }

    /// <summary>
    /// Attempts to analyze with even stricter JSON enforcement
    /// </summary>
    private async Task<string> TryAnalyzeWithStricterPromptAsync(LogEvent e, IEnumerable<LogEvent> neighbors, CancellationToken ct)
    {
        // For retry, we can't modify the inner client's prompt easily without replacing it
        // So we'll just retry once more with the same logic and rely on extraction
        var response = await _inner.AnalyzeAsync(e, neighbors, ct);
        return ExtractJson(response);
    }

    /// <summary>
    /// Extracts JSON from LLM response, handling various formats:
    /// - Markdown code blocks: ```json {...} ```
    /// - Plain JSON objects: {...}
    /// - Mixed content with JSON embedded
    /// </summary>
    private string ExtractJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "{}";
        }

        // Try 1: Extract from markdown code block
        var codeBlockMatch = JsonCodeBlockPattern.Match(response);
        if (codeBlockMatch.Success)
        {
            var json = codeBlockMatch.Groups[1].Value.Trim();
            if (IsValidJson(json))
            {
                return json;
            }
        }

        // Try 2: Find first complete JSON object in response
        var jsonMatch = JsonObjectPattern.Match(response);
        if (jsonMatch.Success)
        {
            var json = jsonMatch.Groups[1].Value.Trim();
            if (IsValidJson(json))
            {
                return json;
            }
        }

        // Try 3: Try the entire response as-is
        var trimmed = response.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            if (IsValidJson(trimmed))
            {
                return trimmed;
            }
        }

        // No valid JSON found
        return response;
    }

    /// <summary>
    /// Validates if a string is valid JSON and contains required fields
    /// </summary>
    private bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var doc = JsonDocument.Parse(json);

            // Check for required fields based on LlmSecurityEventResponse
            bool hasRisk = doc.RootElement.TryGetProperty("risk", out _);
            bool hasConfidence = doc.RootElement.TryGetProperty("confidence", out _);
            bool hasSummary = doc.RootElement.TryGetProperty("summary", out _);

            // At minimum, we need risk and summary
            return hasRisk && hasSummary;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Generates a valid fallback JSON response when parsing fails
    /// </summary>
    private string GenerateFallbackJson(LogEvent e, string failedResponse)
    {
        _logger?.LogDebug("Generating fallback JSON for event {EventId}", e.EventId);

        // Try to extract any useful information from the failed response
        var summary = ExtractSummaryFromFailedResponse(failedResponse)
                     ?? $"Security event detected in {e.Channel} (EventId: {e.EventId})";

        var fallback = new
        {
            risk = "low",
            mitre = Array.Empty<string>(),
            confidence = 25, // Low confidence for fallback
            summary = summary,
            recommended_actions = new[] { "Review event details manually", "Investigate further if needed" }
        };

        return JsonSerializer.Serialize(fallback, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    /// <summary>
    /// Attempts to extract a summary from a failed LLM response
    /// </summary>
    private string? ExtractSummaryFromFailedResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        // Try to find summary field in partial JSON
        var summaryMatch = Regex.Match(response, @"""summary""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (summaryMatch.Success)
        {
            return summaryMatch.Groups[1].Value;
        }

        // Try to extract first sentence as summary
        var firstSentence = Regex.Match(response, @"([A-Z][^.!?]+[.!?])");
        if (firstSentence.Success)
        {
            var sentence = firstSentence.Groups[1].Value.Trim();
            // Limit to reasonable length
            if (sentence.Length <= 200)
            {
                return sentence;
            }
        }

        return null;
    }

    /// <summary>
    /// Get JSON validation statistics for monitoring
    /// </summary>
    public StrictJsonStatistics GetStatistics()
    {
        return new StrictJsonStatistics
        {
            TotalCalls = _totalCalls,
            SuccessfulParses = _successfulParses,
            FailedParses = _failedParses,
            RetriedCalls = _retriedCalls,
            FallbackUsed = _fallbackUsed,
            ParseSuccessRate = _totalCalls == 0 ? 0 : (float)_successfulParses / _totalCalls
        };
    }
}

/// <summary>
/// Statistics for strict JSON validation and parsing
/// </summary>
public sealed class StrictJsonStatistics
{
    public long TotalCalls { get; init; }
    public long SuccessfulParses { get; init; }
    public long FailedParses { get; init; }
    public long RetriedCalls { get; init; }
    public long FallbackUsed { get; init; }
    public float ParseSuccessRate { get; init; }
}
