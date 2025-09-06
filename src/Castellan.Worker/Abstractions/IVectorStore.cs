using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;
public interface IVectorStore
{
    Task EnsureCollectionAsync(CancellationToken ct);
    Task UpsertAsync(LogEvent e, float[] embedding, CancellationToken ct);
    Task BatchUpsertAsync(List<(LogEvent logEvent, float[] embedding)> items, CancellationToken ct);
    Task<IReadOnlyList<(LogEvent evt, float score)>> SearchAsync(float[] query, int k, CancellationToken ct);
    Task<bool> Has24HoursOfDataAsync(CancellationToken ct);
    Task DeleteVectorsOlderThan24HoursAsync(CancellationToken ct);
}

