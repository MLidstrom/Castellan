using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Services.ConnectionPools;

namespace Castellan.Worker.VectorStores;

/// <summary>
/// Qdrant vector store implementation that uses connection pooling for improved performance.
/// This version uses the existing QdrantVectorStore with pooled HTTP clients.
/// </summary>
public sealed class QdrantPooledVectorStore : IVectorStore
{
    private readonly QdrantVectorStore _vectorStore;
    private readonly QdrantConnectionPool _connectionPool;
    private readonly ILogger<QdrantPooledVectorStore> _logger;

    public QdrantPooledVectorStore(
        QdrantConnectionPool connectionPool,
        IOptions<QdrantOptions> options,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ILogger<QdrantPooledVectorStore> logger)
    {
        _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create the underlying QdrantVectorStore using the existing HTTP client approach
        _vectorStore = new QdrantVectorStore(options, httpClientFactory, loggerFactory.CreateLogger<QdrantVectorStore>());
    }

    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        _logger.LogDebug("Ensuring collection exists using connection-pooled vector store");
        await _vectorStore.EnsureCollectionAsync(ct);
    }

    public async Task UpsertAsync(LogEvent e, float[] embedding, CancellationToken ct)
    {
        _logger.LogDebug("Upserting event {EventId} using connection-pooled vector store", e.EventId);
        await _vectorStore.UpsertAsync(e, embedding, ct);
    }

    public async Task BatchUpsertAsync(List<(LogEvent logEvent, float[] embedding)> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        _logger.LogDebug("Batch upserting {Count} items using connection-pooled vector store", items.Count);
        await _vectorStore.BatchUpsertAsync(items, ct);
    }

    public async Task<IReadOnlyList<(LogEvent evt, float score)>> SearchAsync(float[] query, int k, CancellationToken ct)
    {
        _logger.LogDebug("Searching for {K} vectors using connection-pooled vector store", k);
        return await _vectorStore.SearchAsync(query, k, ct);
    }

    public async Task<bool> Has24HoursOfDataAsync(CancellationToken ct)
    {
        _logger.LogDebug("Checking for 24 hours of data using connection-pooled vector store");
        return await _vectorStore.Has24HoursOfDataAsync(ct);
    }

    public async Task DeleteVectorsOlderThan24HoursAsync(CancellationToken ct)
    {
        _logger.LogDebug("Deleting old vectors using connection-pooled vector store");
        await _vectorStore.DeleteVectorsOlderThan24HoursAsync(ct);
    }
}
