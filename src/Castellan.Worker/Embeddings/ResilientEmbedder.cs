using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Options;

namespace Castellan.Worker.Embeddings;

/// <summary>
/// Decorator for IEmbedder that adds Polly resilience patterns:
/// - Retry with exponential backoff and jitter
/// - Circuit breaker to prevent cascading failures
/// - Timeout to prevent hanging calls
/// </summary>
public sealed class ResilientEmbedder : IEmbedder
{
    private readonly IEmbedder _inner;
    private readonly ResiliencePipeline<float[]> _pipeline;
    private readonly ILogger<ResilientEmbedder>? _logger;
    private readonly EmbeddingResilienceOptions _options;

    // Metrics
    private long _totalCalls;
    private long _successfulCalls;
    private long _failedCalls;
    private long _retriedCalls;
    private long _circuitBreakerOpens;
    private long _timeouts;

    public ResilientEmbedder(
        IEmbedder inner,
        IOptions<ResilienceOptions> options,
        ILogger<ResilientEmbedder>? logger = null)
    {
        _inner = inner;
        _options = options.Value.Embedding;
        _logger = logger;

        if (!_options.Enabled)
        {
            // Resilience disabled - create pass-through pipeline
            _pipeline = new ResiliencePipelineBuilder<float[]>().Build();
            return;
        }

        // Build resilience pipeline: Retry -> Circuit Breaker -> Timeout
        var pipelineBuilder = new ResiliencePipelineBuilder<float[]>();

        // 1. Retry with exponential backoff and jitter
        pipelineBuilder.AddRetry(new()
        {
            MaxRetryAttempts = _options.RetryCount,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs),
            ShouldHandle = new PredicateBuilder<float[]>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>(ex => ex.InnerException is not TimeoutException)
                .Handle<TimeoutRejectedException>()
                .HandleResult(result => result.Length == 0),
            OnRetry = args =>
            {
                Interlocked.Increment(ref _retriedCalls);
                _logger?.LogWarning(
                    "Embedding retry attempt {Attempt} of {MaxAttempts} after {Delay}ms. Reason: {Exception}",
                    args.AttemptNumber + 1,
                    _options.RetryCount,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.Message ?? "Empty embedding");
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
            ShouldHandle = new PredicateBuilder<float[]>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(result => result.Length == 0),
            OnOpened = args =>
            {
                Interlocked.Increment(ref _circuitBreakerOpens);
                _logger?.LogError(
                    "Embedding circuit breaker OPENED after {Failures} failures. Breaking for {Duration}s",
                    args.Outcome.Exception?.Message ?? "empty embeddings",
                    _options.CircuitBreakerDurationMinutes * 60);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                _logger?.LogInformation("Embedding circuit breaker CLOSED - service recovered");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                _logger?.LogInformation("Embedding circuit breaker HALF-OPEN - testing service");
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
                    "Embedding call timed out after {Timeout}s",
                    _options.TimeoutSeconds);
                return ValueTask.CompletedTask;
            }
        });

        _pipeline = pipelineBuilder.Build();
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        Interlocked.Increment(ref _totalCalls);

        try
        {
            var result = await _pipeline.ExecuteAsync(
                async token => await _inner.EmbedAsync(text, token),
                ct);

            if (result.Length > 0)
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
                "Embedding call rejected by circuit breaker: {Message}",
                ex.Message);
            return Array.Empty<float>(); // Graceful degradation
        }
        catch (TimeoutRejectedException ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                "Embedding call timed out: {Message}",
                ex.Message);
            return Array.Empty<float>(); // Graceful degradation
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedCalls);
            _logger?.LogError(
                ex,
                "Embedding call failed after all retries: {Message}",
                ex.Message);
            return Array.Empty<float>(); // Graceful degradation
        }
    }

    /// <summary>
    /// Get resilience statistics for monitoring
    /// </summary>
    public EmbeddingResilienceStatistics GetStatistics()
    {
        return new EmbeddingResilienceStatistics
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
/// Embedding resilience statistics for monitoring
/// </summary>
public sealed class EmbeddingResilienceStatistics
{
    public long TotalCalls { get; init; }
    public long SuccessfulCalls { get; init; }
    public long FailedCalls { get; init; }
    public long RetriedCalls { get; init; }
    public long CircuitBreakerOpens { get; init; }
    public long Timeouts { get; init; }
    public float SuccessRate { get; init; }
}
