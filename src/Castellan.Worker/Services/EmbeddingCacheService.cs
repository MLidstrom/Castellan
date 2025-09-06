using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Services
{
    /// <summary>
    /// Specialized caching service for embeddings with semantic similarity detection.
    /// Part of Phase 2B intelligent caching layer implementation.
    /// </summary>
    public class EmbeddingCacheService : IDisposable
    {
        private readonly ICacheService _cacheService;
        private readonly TextHashingService _textHashingService;
        private readonly CacheOptions _cacheOptions;
        private readonly ILogger<EmbeddingCacheService> _logger;
        private bool _disposed;

        public EmbeddingCacheService(
            ICacheService cacheService,
            TextHashingService textHashingService,
            IOptionsMonitor<CacheOptions> cacheOptions,
            ILogger<EmbeddingCacheService> logger)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _textHashingService = textHashingService ?? throw new ArgumentNullException(nameof(textHashingService));
            _cacheOptions = cacheOptions?.CurrentValue ?? throw new ArgumentNullException(nameof(cacheOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation(
                "EmbeddingCacheService initialized. Enabled: {Enabled}, MaxEntries: {MaxEntries}, TTL: {TTL}min, Threshold: {Threshold}",
                _cacheOptions.Embedding.Enabled, 
                _cacheOptions.Embedding.MaxEntries,
                _cacheOptions.Embedding.TtlMinutes,
                _cacheOptions.Embedding.SimilarityThreshold);
        }

        /// <summary>
        /// Gets cached embedding for the given text, or null if not found or caching is disabled.
        /// </summary>
        /// <param name="text">The text to get embedding for</param>
        /// <param name="context">Optional context information (e.g., event type, channel)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached embedding array if found, null otherwise</returns>
        public async Task<float[]?> GetCachedEmbeddingAsync(string text, string? context = null, CancellationToken cancellationToken = default)
        {
            if (!_cacheOptions.Embedding.Enabled)
            {
                _logger.LogDebug("Embedding cache is disabled");
                return null;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Cannot cache embedding for null or empty text");
                return null;
            }

            try
            {
                var cacheKey = _textHashingService.GenerateCacheKey(text, context);
                var cachedEntry = await _cacheService.GetAsync<CachedEmbedding>(cacheKey, cancellationToken);

                if (cachedEntry != null)
                {
                    // Additional similarity check for semantic matching
                    var similarity = _textHashingService.CalculateSimilarity(text, cachedEntry.OriginalText);
                    
                    if (similarity >= _cacheOptions.Embedding.SimilarityThreshold)
                    {
                        _logger.LogDebug("Embedding cache HIT for text (similarity: {Similarity:F3})", similarity);
                        
                        // Update last accessed time for cache management
                        cachedEntry.LastAccessed = DateTimeOffset.UtcNow;
                        cachedEntry.AccessCount++;
                        
                        return cachedEntry.Embedding;
                    }
                    else
                    {
                        _logger.LogDebug("Cached embedding found but similarity too low: {Similarity:F3} < {Threshold:F3}", 
                            similarity, _cacheOptions.Embedding.SimilarityThreshold);
                        
                        // Remove the entry that doesn't meet similarity threshold
                        await _cacheService.RemoveAsync(cacheKey, cancellationToken);
                    }
                }

                _logger.LogDebug("Embedding cache MISS for text (length: {Length})", text.Length);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving cached embedding for text (length: {Length})", text.Length);
                return null; // Graceful degradation - return null on cache errors
            }
        }

        /// <summary>
        /// Caches an embedding for the given text.
        /// </summary>
        /// <param name="text">The text that was embedded</param>
        /// <param name="embedding">The embedding array to cache</param>
        /// <param name="context">Optional context information</param>
        /// <param name="generationTimeMs">Time taken to generate the embedding</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task CacheEmbeddingAsync(string text, float[] embedding, string? context = null, 
            double generationTimeMs = 0, CancellationToken cancellationToken = default)
        {
            if (!_cacheOptions.Embedding.Enabled)
            {
                _logger.LogDebug("Embedding cache is disabled, skipping cache");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Cannot cache embedding for null or empty text");
                return;
            }

            if (embedding == null || embedding.Length == 0)
            {
                _logger.LogWarning("Cannot cache null or empty embedding");
                return;
            }

            try
            {
                var cacheKey = _textHashingService.GenerateCacheKey(text, context);
                var ttl = TimeSpan.FromMinutes(_cacheOptions.Embedding.TtlMinutes);

                var cachedEntry = new CachedEmbedding
                {
                    OriginalText = text,
                    Embedding = embedding,
                    Context = context,
                    CachedAt = DateTimeOffset.UtcNow,
                    LastAccessed = DateTimeOffset.UtcNow,
                    AccessCount = 1,
                    GenerationTimeMs = generationTimeMs,
                    TextHash = _textHashingService.GenerateSemanticHash(text)
                };

                await _cacheService.SetAsync(cacheKey, cachedEntry, ttl, cancellationToken);

                _logger.LogDebug(
                    "Cached embedding for text (length: {Length}, generation time: {GenerationTime:F1}ms, TTL: {TTL}min)",
                    text.Length, generationTimeMs, _cacheOptions.Embedding.TtlMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error caching embedding for text (length: {Length})", text.Length);
                // Don't throw - caching failures should not break the main flow
            }
        }

        /// <summary>
        /// Gets or generates an embedding using the cache-first approach with a factory function.
        /// </summary>
        /// <param name="text">The text to get embedding for</param>
        /// <param name="embeddingFactory">Factory function to generate embedding if not cached</param>
        /// <param name="context">Optional context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Embedding array (cached or newly generated)</returns>
        public async Task<EmbeddingResult> GetOrGenerateEmbeddingAsync(string text, 
            Func<string, CancellationToken, Task<float[]>> embeddingFactory,
            string? context = null, 
            CancellationToken cancellationToken = default)
        {
            if (embeddingFactory == null)
                throw new ArgumentNullException(nameof(embeddingFactory));

            var startTime = DateTimeOffset.UtcNow;

            // Try to get from cache first
            var cachedEmbedding = await GetCachedEmbeddingAsync(text, context, cancellationToken);
            if (cachedEmbedding != null)
            {
                var cacheTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                return new EmbeddingResult
                {
                    Embedding = cachedEmbedding,
                    WasCached = true,
                    RetrievalTimeMs = cacheTime,
                    GenerationTimeMs = 0
                };
            }

            // Cache miss - generate new embedding
            _logger.LogDebug("Generating new embedding for text (length: {Length})", text.Length);
            
            var generationStart = DateTimeOffset.UtcNow;
            var newEmbedding = await embeddingFactory(text, cancellationToken);
            var generationTime = (DateTimeOffset.UtcNow - generationStart).TotalMilliseconds;

            // Cache the newly generated embedding
            await CacheEmbeddingAsync(text, newEmbedding, context, generationTime, cancellationToken);

            var totalTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            
            return new EmbeddingResult
            {
                Embedding = newEmbedding,
                WasCached = false,
                RetrievalTimeMs = totalTime,
                GenerationTimeMs = generationTime
            };
        }

        /// <summary>
        /// Gets cache metrics for the embedding cache.
        /// </summary>
        /// <returns>Cache metrics for embeddings</returns>
        public CacheMetrics GetCacheMetrics()
        {
            try
            {
                return _cacheService.GetMetrics("emb");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error retrieving embedding cache metrics");
                return new CacheMetrics { CacheType = "emb" };
            }
        }

        /// <summary>
        /// Clears all cached embeddings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Note: This clears the entire cache, not just embeddings
                // In a production system, you might want prefix-based clearing
                await _cacheService.ClearAsync(cancellationToken);
                _logger.LogInformation("Embedding cache cleared");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error clearing embedding cache");
                throw;
            }
        }

        /// <summary>
        /// Validates cache configuration and logs warnings if settings might cause issues.
        /// </summary>
        public void ValidateConfiguration()
        {
            var config = _cacheOptions.Embedding;

            if (!config.Enabled)
            {
                _logger.LogInformation("Embedding cache is disabled");
                return;
            }

            if (config.MaxEntries < 100)
            {
                _logger.LogWarning("Embedding cache MaxEntries is very low: {MaxEntries}. Consider increasing for better performance.", 
                    config.MaxEntries);
            }

            if (config.TtlMinutes < 10)
            {
                _logger.LogWarning("Embedding cache TTL is very short: {TTL}min. Embeddings are expensive to generate.", 
                    config.TtlMinutes);
            }

            if (config.SimilarityThreshold < 0.8)
            {
                _logger.LogWarning("Embedding cache similarity threshold is low: {Threshold}. This might cause cache hits for dissimilar text.", 
                    config.SimilarityThreshold);
            }

            if (config.MaxTextLength > 2000)
            {
                _logger.LogWarning("Embedding cache MaxTextLength is high: {MaxLength}. This might affect hashing performance.", 
                    config.MaxTextLength);
            }

            _logger.LogInformation("Embedding cache configuration validated successfully");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // MemoryCacheService and TextHashingService are managed by DI container
                _disposed = true;
                _logger.LogInformation("EmbeddingCacheService disposed");
            }
        }
    }

    /// <summary>
    /// Represents a cached embedding entry with metadata.
    /// </summary>
    public class CachedEmbedding
    {
        /// <summary>
        /// The original text that was embedded.
        /// </summary>
        public string OriginalText { get; set; } = string.Empty;

        /// <summary>
        /// The cached embedding array.
        /// </summary>
        public float[] Embedding { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Optional context information used in cache key generation.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// When this embedding was cached.
        /// </summary>
        public DateTimeOffset CachedAt { get; set; }

        /// <summary>
        /// When this cached embedding was last accessed.
        /// </summary>
        public DateTimeOffset LastAccessed { get; set; }

        /// <summary>
        /// Number of times this cached embedding has been accessed.
        /// </summary>
        public int AccessCount { get; set; }

        /// <summary>
        /// Time taken to generate the original embedding in milliseconds.
        /// </summary>
        public double GenerationTimeMs { get; set; }

        /// <summary>
        /// Hash of the original text for similarity comparison.
        /// </summary>
        public string TextHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result of an embedding retrieval operation with performance metrics.
    /// </summary>
    public class EmbeddingResult
    {
        /// <summary>
        /// The embedding array.
        /// </summary>
        public float[] Embedding { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Whether the embedding was retrieved from cache.
        /// </summary>
        public bool WasCached { get; set; }

        /// <summary>
        /// Total time for retrieval (cache lookup or generation) in milliseconds.
        /// </summary>
        public double RetrievalTimeMs { get; set; }

        /// <summary>
        /// Time taken for embedding generation if not cached, in milliseconds.
        /// </summary>
        public double GenerationTimeMs { get; set; }

        /// <summary>
        /// Performance improvement ratio compared to generation (only meaningful for cached results).
        /// </summary>
        public double SpeedupRatio => WasCached && GenerationTimeMs > 0 ? GenerationTimeMs / Math.Max(RetrievalTimeMs, 0.1) : 1.0;
    }
}
