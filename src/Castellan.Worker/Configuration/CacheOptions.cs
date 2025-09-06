using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Configuration
{
    /// <summary>
    /// Configuration options for Phase 2B intelligent caching system.
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// The configuration section name for cache options.
        /// </summary>
        public const string SectionName = "Cache";

        /// <summary>
        /// Whether caching is enabled globally.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Cache provider type (e.g., "Memory", "Redis").
        /// </summary>
        [Required]
        public string Provider { get; set; } = "Memory";

        /// <summary>
        /// Default time-to-live for cached items in minutes.
        /// </summary>
        [Range(1, 1440)] // 1 minute to 24 hours
        public int DefaultTtlMinutes { get; set; } = 30;

        /// <summary>
        /// Maximum memory usage for the cache in MB.
        /// </summary>
        [Range(1, 4096)] // 1MB to 4GB
        public int MaxMemoryMb { get; set; } = 512;

        /// <summary>
        /// Whether to enable cache metrics collection.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Memory pressure threshold (0.0 to 1.0) for cache eviction.
        /// </summary>
        [Range(0.1, 0.95)]
        public double MemoryPressureThreshold { get; set; } = 0.8;

        /// <summary>
        /// Interval in seconds for cache cleanup operations.
        /// </summary>
        [Range(10, 3600)] // 10 seconds to 1 hour
        public int CleanupIntervalSeconds { get; set; } = 300; // 5 minutes

        /// <summary>
        /// Configuration for embedding cache.
        /// </summary>
        public EmbeddingCacheOptions Embedding { get; set; } = new();

        /// <summary>
        /// Configuration for IP enrichment cache.
        /// </summary>
        public IpEnrichmentCacheOptions IpEnrichment { get; set; } = new();

        /// <summary>
        /// Configuration for LLM response cache.
        /// </summary>
        public LlmResponseCacheOptions LlmResponse { get; set; } = new();

        /// <summary>
        /// Configuration for vector search cache.
        /// </summary>
        public VectorSearchCacheOptions VectorSearch { get; set; } = new();
    }

    /// <summary>
    /// Configuration options for embedding cache.
    /// </summary>
    public class EmbeddingCacheOptions
    {
        /// <summary>
        /// Whether embedding caching is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of cached embeddings.
        /// </summary>
        [Range(100, 50000)]
        public int MaxEntries { get; set; } = 5000;

        /// <summary>
        /// Time-to-live for cached embeddings in minutes.
        /// </summary>
        [Range(1, 1440)] // 1 minute to 24 hours
        public int TtlMinutes { get; set; } = 60;

        /// <summary>
        /// Text similarity threshold for cache hits (0.0 to 1.0).
        /// Higher values require more similarity for cache hits.
        /// </summary>
        [Range(0.7, 0.99)]
        public double SimilarityThreshold { get; set; } = 0.95;

        /// <summary>
        /// Whether to enable text normalization before hashing.
        /// </summary>
        public bool EnableTextNormalization { get; set; } = true;

        /// <summary>
        /// Maximum text length before truncation for cache key generation.
        /// </summary>
        [Range(50, 2000)]
        public int MaxTextLength { get; set; } = 1000;
    }

    /// <summary>
    /// Configuration options for IP enrichment cache.
    /// </summary>
    public class IpEnrichmentCacheOptions
    {
        /// <summary>
        /// Whether IP enrichment caching is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of cached IP enrichment results.
        /// </summary>
        [Range(100, 100000)]
        public int MaxEntries { get; set; } = 10000;

        /// <summary>
        /// Time-to-live for cached IP enrichment data in minutes.
        /// IP data changes infrequently, so longer TTL is appropriate.
        /// </summary>
        [Range(60, 10080)] // 1 hour to 1 week
        public int TtlMinutes { get; set; } = 240; // 4 hours

        /// <summary>
        /// Whether to cache enrichment data for private IP addresses.
        /// </summary>
        public bool CachePrivateIPs { get; set; } = false;

        /// <summary>
        /// Whether to cache failed enrichment attempts.
        /// </summary>
        public bool CacheNegativeResults { get; set; } = true;

        /// <summary>
        /// TTL for negative results in minutes.
        /// </summary>
        [Range(5, 240)]
        public int NegativeResultTtlMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Configuration options for LLM response cache.
    /// </summary>
    public class LlmResponseCacheOptions
    {
        /// <summary>
        /// Whether LLM response caching is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of cached LLM responses.
        /// </summary>
        [Range(100, 10000)]
        public int MaxEntries { get; set; } = 2000;

        /// <summary>
        /// Default time-to-live for cached LLM responses in minutes.
        /// </summary>
        [Range(5, 480)] // 5 minutes to 8 hours
        public int TtlMinutes { get; set; } = 30;

        /// <summary>
        /// Time-to-live for high-confidence responses in minutes.
        /// High-confidence responses can be cached longer.
        /// </summary>
        [Range(30, 1440)] // 30 minutes to 24 hours
        public int HighConfidenceTtlMinutes { get; set; } = 60;

        /// <summary>
        /// Time-to-live for low-confidence responses in minutes.
        /// Low-confidence responses should be cached for shorter periods.
        /// </summary>
        [Range(1, 60)] // 1 to 60 minutes
        public int LowConfidenceTtlMinutes { get; set; } = 10;

        /// <summary>
        /// Confidence threshold for high-confidence caching (0.0 to 1.0).
        /// </summary>
        [Range(0.7, 0.99)]
        public double HighConfidenceThreshold { get; set; } = 0.8;

        /// <summary>
        /// Confidence threshold for low-confidence caching (0.0 to 1.0).
        /// </summary>
        [Range(0.1, 0.7)]
        public double LowConfidenceThreshold { get; set; } = 0.4;

        /// <summary>
        /// Whether to include context in cache key generation.
        /// </summary>
        public bool IncludeContextInKey { get; set; } = true;
    }

    /// <summary>
    /// Configuration options for vector search cache.
    /// </summary>
    public class VectorSearchCacheOptions
    {
        /// <summary>
        /// Whether vector search caching is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of cached vector search results.
        /// </summary>
        [Range(100, 20000)]
        public int MaxEntries { get; set; } = 3000;

        /// <summary>
        /// Time-to-live for cached vector search results in minutes.
        /// Vector search results can become stale as new vectors are added.
        /// </summary>
        [Range(1, 120)] // 1 minute to 2 hours
        public int TtlMinutes { get; set; } = 15;

        /// <summary>
        /// Vector similarity threshold for cache hits (0.0 to 1.0).
        /// Higher values require more similarity for cache hits.
        /// </summary>
        [Range(0.8, 0.99)]
        public double SimilarityThreshold { get; set; } = 0.90;

        /// <summary>
        /// Maximum number of results to cache per query.
        /// </summary>
        [Range(1, 50)]
        public int MaxResultsPerQuery { get; set; } = 10;

        /// <summary>
        /// Whether to include query parameters in cache key generation.
        /// </summary>
        public bool IncludeQueryParamsInKey { get; set; } = true;
    }
}
