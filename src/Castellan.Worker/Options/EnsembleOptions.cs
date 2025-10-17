namespace Castellan.Worker.Options;

/// <summary>
/// Configuration options for multi-model ensemble LLM predictions.
/// Enables aggregating predictions from multiple models for higher accuracy.
/// </summary>
public sealed class EnsembleOptions
{
    /// <summary>
    /// Enable ensemble predictions (requires at least 2 models configured)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// LLM provider to use for ensemble models ("Ollama" or "OpenAI").
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>
    /// List of model names to use in the ensemble.
    /// Example: ["llama3.1:8b-instruct-q8_0", "mistral:7b-instruct", "gemma2:9b"]
    /// </summary>
    public string[] Models { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Minimum number of models that must succeed for ensemble to return result.
    /// If fewer models succeed, falls back to single model result.
    /// </summary>
    public int MinSuccessfulModels { get; set; } = 2;

    /// <summary>
    /// Maximum time to wait for all models to respond (milliseconds).
    /// After timeout, uses results from models that completed successfully.
    /// </summary>
    public int TimeoutMs { get; set; } = 60000; // 60 seconds

    /// <summary>
    /// Voting strategy for categorical fields (risk level, event type).
    /// Options: "majority", "unanimous", "weighted"
    /// </summary>
    public string VotingStrategy { get; set; } = "majority";

    /// <summary>
    /// How to aggregate confidence scores across models.
    /// Options: "mean", "median", "min", "max", "weighted_mean"
    /// </summary>
    public string ConfidenceAggregation { get; set; } = "mean";

    /// <summary>
    /// Weights for each model (optional).
    /// If provided, must match length of Models array.
    /// Used for weighted voting and weighted confidence aggregation.
    /// </summary>
    public float[]? ModelWeights { get; set; }

    /// <summary>
    /// Whether to run models in parallel (faster) or sequentially (lower resource usage)
    /// </summary>
    public bool RunInParallel { get; set; } = true;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (Enabled)
        {
            if (Models == null || Models.Length < 2)
                throw new InvalidOperationException(
                    "Ensemble requires at least 2 models configured. Set EnsembleOptions:Models array.");

            if (MinSuccessfulModels < 1)
                throw new InvalidOperationException(
                    "MinSuccessfulModels must be at least 1.");

            if (MinSuccessfulModels > Models.Length)
                throw new InvalidOperationException(
                    $"MinSuccessfulModels ({MinSuccessfulModels}) cannot exceed number of models ({Models.Length}).");

            if (TimeoutMs <= 0)
                throw new InvalidOperationException(
                    "TimeoutMs must be greater than 0.");

            if (VotingStrategy != "majority" && VotingStrategy != "unanimous" && VotingStrategy != "weighted")
                throw new InvalidOperationException(
                    "VotingStrategy must be one of: majority, unanimous, weighted");

            if (ConfidenceAggregation != "mean" && ConfidenceAggregation != "median" &&
                ConfidenceAggregation != "min" && ConfidenceAggregation != "max" &&
                ConfidenceAggregation != "weighted_mean")
                throw new InvalidOperationException(
                    "ConfidenceAggregation must be one of: mean, median, min, max, weighted_mean");

            if (ModelWeights != null)
            {
                if (ModelWeights.Length != Models.Length)
                    throw new InvalidOperationException(
                        $"ModelWeights length ({ModelWeights.Length}) must match Models length ({Models.Length}).");

                if (ModelWeights.Any(w => w <= 0))
                    throw new InvalidOperationException(
                        "All ModelWeights must be greater than 0.");

                // Normalize weights to sum to 1.0
                var sum = ModelWeights.Sum();
                if (Math.Abs(sum - 1.0f) > 0.001f)
                {
                    for (int i = 0; i < ModelWeights.Length; i++)
                        ModelWeights[i] /= sum;
                }
            }
        }
    }
}
