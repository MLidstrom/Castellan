using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;
public interface ILlmClient
{
    Task<string> AnalyzeAsync(LogEvent e, IEnumerable<LogEvent> neighbors, CancellationToken ct);
}

