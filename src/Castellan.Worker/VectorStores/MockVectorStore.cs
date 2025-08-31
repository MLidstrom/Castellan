using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.VectorStores;

public sealed class MockVectorStore : IVectorStore
{
    private readonly ILogger<MockVectorStore> _logger;
    private readonly List<(LogEvent Event, float[] Vector)> _storage = new();

    public MockVectorStore(ILogger<MockVectorStore> logger)
    {
        _logger = logger;
    }

    public Task EnsureCollectionAsync(CancellationToken ct)
    {
        _logger.LogInformation("Mock vector store initialized (no Qdrant required)");
        return Task.CompletedTask;
    }

    public Task UpsertAsync(LogEvent e, float[] embedding, CancellationToken ct)
    {
        // Remove existing entry if present (use UniqueId for matching)
        _storage.RemoveAll(item => item.Event.UniqueId == e.UniqueId);
        
        // Add new entry
        _storage.Add((e, embedding));
        
        _logger.LogDebug("Upserted event {EventId} in mock vector store", e.UniqueId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<(LogEvent evt, float score)>> SearchAsync(float[] query, int k, CancellationToken ct)
    {
        // Simple mock search - return the most recent events with simulated scores
        var results = _storage
            .OrderByDescending(item => item.Event.Time)
            .Take(Math.Min(k, _storage.Count))
            .Select(item => (item.Event, score: 0.9f))
            .ToList();
        
        return Task.FromResult<IReadOnlyList<(LogEvent evt, float score)>>(results);
    }

    public Task<bool> Has24HoursOfDataAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var hasData = _storage.Any(item => item.Event.Time >= cutoff);
        return Task.FromResult(hasData);
    }

    public Task DeleteVectorsOlderThan24HoursAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var removed = _storage.RemoveAll(item => item.Event.Time < cutoff);
        
        if (removed > 0)
        {
            _logger.LogInformation("Deleted {Count} vectors older than 24 hours from mock vector store", removed);
        }
        
        return Task.CompletedTask;
    }
}