namespace Castellan.Worker.Options;

/// <summary>
/// Configuration options for embedding cache
/// </summary>
public sealed class EmbeddingCacheOptions
{
    /// <summary>
    /// Enable or disable embedding cache
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Time-to-live in minutes for cached embeddings
    /// </summary>
    public int TtlMinutes { get; set; } = 1440; // 24 hours

    /// <summary>
    /// Maximum number of entries in the cache
    /// </summary>
    public int MaxEntries { get; set; } = 50000;

    /// <summary>
    /// Enable SQLite persistence for warm restarts
    /// </summary>
    public bool EnablePersistence { get; set; } = false;

    /// <summary>
    /// Path to SQLite database for persistence
    /// </summary>
    public string PersistencePath { get; set; } = "data/embedding-cache.db";

    /// <summary>
    /// Provider name for cache key generation
    /// </summary>
    public string Provider { get; set; } = "Ollama";

    /// <summary>
    /// Model name for cache key generation
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";
}
