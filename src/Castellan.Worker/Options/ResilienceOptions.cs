namespace Castellan.Worker.Options;

/// <summary>
/// Configuration options for Polly resilience patterns
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>
    /// LLM-specific resilience settings
    /// </summary>
    public LlmResilienceOptions LLM { get; set; } = new();

    /// <summary>
    /// Embedding-specific resilience settings
    /// </summary>
    public EmbeddingResilienceOptions Embedding { get; set; } = new();
}

/// <summary>
/// Resilience settings for LLM calls
/// </summary>
public sealed class LlmResilienceOptions
{
    /// <summary>
    /// Enable resilience patterns for LLM
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 200;

    /// <summary>
    /// Circuit breaker threshold (consecutive failures before opening)
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker duration in minutes
    /// </summary>
    public int CircuitBreakerDurationMinutes { get; set; } = 1;

    /// <summary>
    /// Timeout in seconds for LLM calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Rate limit per second
    /// </summary>
    public int RateLimitPerSecond { get; set; } = 10;
}

/// <summary>
/// Resilience settings for embedding calls
/// </summary>
public sealed class EmbeddingResilienceOptions
{
    /// <summary>
    /// Enable resilience patterns for embeddings
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 200;

    /// <summary>
    /// Timeout in seconds for embedding calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Circuit breaker threshold (consecutive failures before opening)
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker duration in minutes
    /// </summary>
    public int CircuitBreakerDurationMinutes { get; set; } = 1;
}
