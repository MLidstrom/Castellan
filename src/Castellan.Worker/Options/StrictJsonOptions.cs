namespace Castellan.Worker.Options;

/// <summary>
/// Configuration options for strict JSON schema validation in LLM responses
/// </summary>
public sealed class StrictJsonOptions
{
    /// <summary>
    /// Enable strict JSON schema validation and extraction
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enable retry with stricter prompt when first parse fails
    /// </summary>
    public bool EnableRetryOnFailure { get; set; } = true;

    /// <summary>
    /// Enable intelligent fallback JSON generation on parse failure
    /// </summary>
    public bool EnableFallbackGeneration { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts before using fallback
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 1;

    /// <summary>
    /// Minimum required confidence score for accepting LLM response (0-100)
    /// </summary>
    public int MinConfidenceThreshold { get; set; } = 0;
}
