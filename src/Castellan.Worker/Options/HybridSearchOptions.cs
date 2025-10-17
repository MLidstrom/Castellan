namespace Castellan.Worker.Options;

/// <summary>
/// Configuration options for hybrid vector + metadata search.
/// Part of Phase 2 Week 3-4 AI Intelligence Upgrades (Hybrid Retrieval).
/// </summary>
public sealed class HybridSearchOptions
{
    /// <summary>
    /// Enable hybrid search (vector similarity + metadata scoring).
    /// When disabled, falls back to pure vector similarity search.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Weight for vector similarity score (0.0 - 1.0).
    /// Recommended: 0.7 (70% vector similarity)
    /// </summary>
    public float VectorSimilarityWeight { get; set; } = 0.7f;

    /// <summary>
    /// Weight for metadata score (0.0 - 1.0).
    /// Recommended: 0.3 (30% metadata signals)
    /// Should sum to 1.0 with VectorSimilarityWeight
    /// </summary>
    public float MetadataWeight { get; set; } = 0.3f;

    /// <summary>
    /// Recency decay factor for exponential decay calculation.
    /// Higher values = faster decay, more emphasis on recent events.
    /// Formula: exp(-ageHours / RecencyDecayHours)
    /// Recommended: 24.0 (events lose ~63% weight after 24 hours)
    /// </summary>
    public float RecencyDecayHours { get; set; } = 24.0f;

    /// <summary>
    /// Weight for recency component within metadata score (0.0 - 1.0).
    /// Recommended: 0.3 (30% of metadata score from recency)
    /// </summary>
    public float RecencyWeight { get; set; } = 0.3f;

    /// <summary>
    /// Weight for risk level component within metadata score (0.0 - 1.0).
    /// Recommended: 0.7 (70% of metadata score from risk level)
    /// </summary>
    public float RiskLevelWeight { get; set; } = 0.7f;

    /// <summary>
    /// Over-fetching multiplier for initial vector search.
    /// Fetch (k * OverFetchMultiplier) results, then re-rank to top k.
    /// Recommended: 3.0 (fetch 3x more results for better re-ranking quality)
    /// </summary>
    public float OverFetchMultiplier { get; set; } = 3.0f;

    /// <summary>
    /// Risk level scoring configuration.
    /// Maps risk levels to scores (0.0 - 1.0).
    /// </summary>
    public Dictionary<string, float> RiskLevelScores { get; set; } = new()
    {
        ["critical"] = 1.0f,
        ["high"] = 0.75f,
        ["medium"] = 0.5f,
        ["low"] = 0.25f,
        ["unknown"] = 0.1f
    };

    /// <summary>
    /// Validates configuration and ensures weights sum correctly.
    /// </summary>
    public void Validate()
    {
        if (VectorSimilarityWeight + MetadataWeight != 1.0f)
        {
            throw new InvalidOperationException(
                $"HybridSearch weights must sum to 1.0: VectorSimilarityWeight ({VectorSimilarityWeight}) + MetadataWeight ({MetadataWeight}) = {VectorSimilarityWeight + MetadataWeight}");
        }

        if (RecencyWeight + RiskLevelWeight > 1.0f)
        {
            throw new InvalidOperationException(
                $"HybridSearch metadata weights must not exceed 1.0: RecencyWeight ({RecencyWeight}) + RiskLevelWeight ({RiskLevelWeight}) = {RecencyWeight + RiskLevelWeight}");
        }

        if (OverFetchMultiplier < 1.0f)
        {
            throw new InvalidOperationException(
                $"HybridSearch OverFetchMultiplier must be >= 1.0: {OverFetchMultiplier}");
        }

        if (RecencyDecayHours <= 0)
        {
            throw new InvalidOperationException(
                $"HybridSearch RecencyDecayHours must be > 0: {RecencyDecayHours}");
        }
    }

    /// <summary>
    /// Gets the risk level score for a given risk level string.
    /// </summary>
    public float GetRiskLevelScore(string? riskLevel)
    {
        if (string.IsNullOrEmpty(riskLevel))
            return RiskLevelScores.GetValueOrDefault("unknown", 0.1f);

        var normalizedRiskLevel = riskLevel.ToLowerInvariant();
        return RiskLevelScores.GetValueOrDefault(normalizedRiskLevel, 0.1f);
    }
}
