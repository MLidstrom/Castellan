using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Options;

namespace Castellan.Worker.VectorStores;

/// <summary>
/// Hybrid vector store that combines vector similarity search with metadata scoring.
/// Part of Phase 2 Week 3-4 AI Intelligence Upgrades (Hybrid Retrieval).
///
/// Architecture:
/// - Wraps an existing IVectorStore implementation (decorator pattern)
/// - Performs vector similarity search with over-fetching
/// - Re-ranks results using metadata scoring (recency boost)
/// - Returns top-k results based on hybrid score
///
/// Hybrid Score = (VectorScore * VectorWeight) + (MetadataScore * MetadataWeight)
/// Metadata Score = RecencyScore (exponential decay based on age)
/// </summary>
public sealed class HybridVectorStore : IVectorStore
{
    private readonly IVectorStore _inner;
    private readonly HybridSearchOptions _options;
    private readonly ILogger<HybridVectorStore> _logger;

    // Metrics
    private long _totalSearches;
    private long _hybridSearches;
    private long _fallbackSearches;

    public HybridVectorStore(
        IVectorStore inner,
        IOptions<HybridSearchOptions> options,
        ILogger<HybridVectorStore> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate configuration
        try
        {
            _options.Validate();
            _logger.LogInformation("HybridVectorStore initialized with VectorWeight={VectorWeight}, MetadataWeight={MetadataWeight}, RecencyDecayHours={RecencyDecayHours}",
                _options.VectorSimilarityWeight, _options.MetadataWeight, _options.RecencyDecayHours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid HybridSearchOptions configuration. Hybrid search will be disabled.");
            _options.Enabled = false;
        }
    }

    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        await _inner.EnsureCollectionAsync(ct);
    }

    public async Task UpsertAsync(LogEvent e, float[] embedding, CancellationToken ct)
    {
        await _inner.UpsertAsync(e, embedding, ct);
    }

    public async Task BatchUpsertAsync(List<(LogEvent logEvent, float[] embedding)> items, CancellationToken ct)
    {
        await _inner.BatchUpsertAsync(items, ct);
    }

    public async Task<IReadOnlyList<(LogEvent evt, float score)>> SearchAsync(float[] query, int k, CancellationToken ct)
    {
        Interlocked.Increment(ref _totalSearches);

        if (!_options.Enabled)
        {
            Interlocked.Increment(ref _fallbackSearches);
            _logger.LogDebug("Hybrid search disabled, using pure vector search for k={K}", k);
            return await _inner.SearchAsync(query, k, ct);
        }

        try
        {
            Interlocked.Increment(ref _hybridSearches);

            // Step 1: Over-fetch results (fetch more results than needed for better re-ranking quality)
            var overFetchK = (int)Math.Ceiling(k * _options.OverFetchMultiplier);
            _logger.LogDebug("Hybrid search: over-fetching {OverFetchK} results (k={K}, multiplier={Multiplier})",
                overFetchK, k, _options.OverFetchMultiplier);

            var vectorResults = await _inner.SearchAsync(query, overFetchK, ct);

            if (vectorResults.Count == 0)
            {
                _logger.LogDebug("Hybrid search: no vector results found");
                return vectorResults;
            }

            // Step 2: Calculate hybrid scores for each result
            var now = DateTimeOffset.UtcNow;
            var rerankedResults = vectorResults
                .Select(result =>
                {
                    var vectorScore = result.score;
                    var metadataScore = CalculateMetadataScore(result.evt, now);
                    var hybridScore = (vectorScore * _options.VectorSimilarityWeight) + (metadataScore * _options.MetadataWeight);

                    _logger.LogTrace("Hybrid scoring: event={EventId} vectorScore={VectorScore:F3} metadataScore={MetadataScore:F3} hybridScore={HybridScore:F3}",
                        result.evt.EventId, vectorScore, metadataScore, hybridScore);

                    return (evt: result.evt, score: hybridScore);
                })
                .OrderByDescending(x => x.score)
                .Take(k)
                .ToList();

            _logger.LogDebug("Hybrid search complete: fetched={OverFetchK} re-ranked={RerankedCount} returned={K}",
                overFetchK, vectorResults.Count, k);

            return rerankedResults;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hybrid search failed, falling back to pure vector search");
            Interlocked.Increment(ref _fallbackSearches);
            Interlocked.Decrement(ref _hybridSearches);
            return await _inner.SearchAsync(query, k, ct);
        }
    }

    public async Task<bool> Has24HoursOfDataAsync(CancellationToken ct)
    {
        return await _inner.Has24HoursOfDataAsync(ct);
    }

    public async Task DeleteVectorsOlderThan24HoursAsync(CancellationToken ct)
    {
        await _inner.DeleteVectorsOlderThan24HoursAsync(ct);
    }

    /// <summary>
    /// Calculates metadata score for an event based on recency.
    /// Uses exponential decay: score = exp(-ageHours / decayFactor)
    /// </summary>
    private float CalculateMetadataScore(LogEvent evt, DateTimeOffset now)
    {
        // Recency boost (exponential decay)
        var ageHours = (now - evt.Time).TotalHours;
        var recencyScore = MathF.Exp(-(float)ageHours / _options.RecencyDecayHours);

        // For now, only recency scoring is implemented
        // Future enhancement: Add risk level scoring when SecurityEvent metadata is available in vector store
        var metadataScore = recencyScore * _options.RecencyWeight;

        // Clamp to [0, 1] range
        return Math.Clamp(metadataScore, 0f, 1f);
    }

    /// <summary>
    /// Gets hybrid search statistics for monitoring.
    /// </summary>
    public (long TotalSearches, long HybridSearches, long FallbackSearches, float HybridRate) GetStatistics()
    {
        var total = _totalSearches;
        var hybrid = _hybridSearches;
        var fallback = _fallbackSearches;
        var hybridRate = total > 0 ? (float)hybrid / total : 0f;

        return (total, hybrid, fallback, hybridRate);
    }
}
