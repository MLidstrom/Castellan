using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Options;

namespace Castellan.Worker.Llms;

/// <summary>
/// Decorator for ILlmClient that adds Polly resilience patterns:
/// - Retry with exponential backoff and jitter
/// - Circuit breaker to prevent cascading failures
/// - Timeout to prevent hanging calls
/// </summary>
public sealed class ResilientLlmClient : ILlmClient
{
    private readonly ILlmClient _inner;
    private readonly ResiliencePipeline<string> _pipeline;
    private readonly ILogger<ResilientLlmClient>? _logger;
    private readonly LlmResilienceOptions _options;

    // Metrics
    private long _totalCalls;
    private long _successfulCalls;
    private long _failedCalls;
    private long _retriedCalls;
    private long _circuitBreakerOpens;
    private long _timeouts;

    public ResilientLlmClient(
        ILlmClient inner,
        IOptions<ResilienceOptions> options,
        ILogger<ResilientLlmClient>? logger = null)
    {
        _inner = inner;
        _options = options.Value.LLM;
        _logger = logger;

        if (!_options.Enabled)
        {
            // Resilience disabled - create pass-through pipeline
            _pipeline = new ResiliencePipelineBuilder<string>().Build();
            return;
        }

        // Build resilience pipeline: Retry -> Circuit Breaker -> Timeout
        var pipelineBuilder = new ResiliencePipelineBuilder<string>();

        // 1. Retry with exponential backoff and jitter
        pipelineBuilder.AddRetry(new()
        {
            MaxRetryAttempts = _options.RetryCount,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs),
            ShouldHandle = new PredicateBuilder<string>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>(ex => ex.InnerException is not TimeoutException)
                .Handle<TimeoutRejectedException>()
                .HandleResult(result => string.IsNullOrEmpty(result)),
            OnRetry = args =>
            {
                Interlocked.Increment(ref _retriedCalls);
                _logger?.LogWarning(
                    "LLM retry attempt {Attempt} of {MaxAttempts} after {Delay}ms. Reason: {Exception}",
                    args.AttemptNumber + 1,
                    _options.RetryCount,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.Message ?? "Empty response");
                return ValueTask.CompletedTask;
            }
        });

        // 2. Circuit breaker to prevent cascading failures
        pipelineBuilder.AddCircuitBreaker(new()
        {
            FailureRatio = 0.5,
            MinimumThroughput = _options.CircuitBreakerThreshold,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromMinutes(_options.CircuitBreakerDurationMinutes),
            ShouldHandle = new PredicateBuilder<string>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(result => string.IsNullOrEmpty(result)),
            OnOpened = args =>
            {
                Interlocked.Increment(ref _circuitBreakerOpens);
                _logger?.LogError(
                    "LLM circuit breaker OPENED after {Failures} failures. Breaking for {Duration}s",
                    args.Outcome.Exception?.Message ?? "empty responses",
                    _options.CircuitBreakerDurationMinutes * 60);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                _logger?.LogInformation("LLM circuit breaker CLOSED - service recovered");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                _logger?.LogInformation("LLM circuit breaker HALF-OPEN - testing service");
                return ValueTask.CompletedTask;
            }
        });

        // 3. Timeout to prevent hanging calls
        pipelineBuilder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
            OnTimeout = args =>
            {
                Interlocked.Increment(ref _timeouts);
                _logger?.LogWarning(
                    "LLM call timed out after {Timeout}s",
                    _options.TimeoutSeconds);
                return ValueTask.CompletedTask;
            }
        });

        _pipeline = pipelineBuilder.Build();
    }

    public async Task<string> AnalyzeAsync(LogEvent e, IEnumerable<LogEvent> neighbors, CancellationToken ct)
    {
        Interlocked.Increment(ref _totalCalls);

        try
        {
            var result = await _pipeline.ExecuteAsync(
                async token => await _inner.AnalyzeAsync(e, neighbors, token),
                ct);

            if (!string.IsNullOrEmpty(result))
            {
                Interlocked.Increment(ref _successfulCalls);
            }
            else
            {
                Interlocked.Increment(ref _failedCalls);
            }

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                "LLM call rejected by circuit breaker: {Message}",
                ex.Message);
            return ""; // Graceful degradation
        }
        catch (TimeoutRejectedException ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                "LLM call timed out: {Message}",
                ex.Message);
            return ""; // Graceful degradation
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                ex,
                "LLM call failed after all retries: {Message}",
                ex.Message);
            return ""; // Graceful degradation
        }
    }

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        Interlocked.Increment(ref _totalCalls);

        try
        {
            var result = await _pipeline.ExecuteAsync(
                async token => await _inner.GenerateAsync(systemPrompt, userPrompt, token),
                ct);

            if (!string.IsNullOrEmpty(result))
            {
                Interlocked.Increment(ref _successfulCalls);
            }
            else
            {
                Interlocked.Increment(ref _failedCalls);
            }

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                "LLM call rejected by circuit breaker: {Message}",
                ex.Message);
            return ""; // Graceful degradation
        }
        catch (TimeoutRejectedException ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                "LLM call timed out: {Message}",
                ex.Message);
            return ""; // Graceful degradation
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                ex,
                "LLM call failed after all retries: {Message}",
                ex.Message);
            return ""; // Graceful degradation
        }
    }

    /// <summary>
    /// Get resilience statistics for monitoring
    /// </summary>
    public ResilienceStatistics GetStatistics()
    {
        return new ResilienceStatistics
        {
            TotalCalls = _totalCalls,
            SuccessfulCalls = _successfulCalls,
            FailedCalls = _failedCalls,
            RetriedCalls = _retriedCalls,
            CircuitBreakerOpens = _circuitBreakerOpens,
            Timeouts = _timeouts,
            SuccessRate = _totalCalls == 0 ? 0 : (float)_successfulCalls / _totalCalls
        };
    }
}

/// <summary>
/// Resilience statistics for monitoring
/// </summary>
public sealed class ResilienceStatistics
{
    public long TotalCalls { get; init; }
    public long SuccessfulCalls { get; init; }
    public long FailedCalls { get; init; }
    public long RetriedCalls { get; init; }
    public long CircuitBreakerOpens { get; init; }
    public long Timeouts { get; init; }
    public float SuccessRate { get; init; }
}
