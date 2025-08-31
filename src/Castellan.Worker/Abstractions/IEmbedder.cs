namespace Castellan.Worker.Abstractions;
public interface IEmbedder
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
