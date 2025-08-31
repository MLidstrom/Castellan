using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;
public interface ILogCollector
{
    IAsyncEnumerable<LogEvent> CollectAsync(CancellationToken ct);
}

