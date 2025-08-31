using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Embeddings;

public sealed class OpenAIEmbedder(IOptions<EmbeddingOptions> opt, HttpClient http) : IEmbedder
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.Value.OpenAIKey);
        req.Content = JsonContent.Create(new { model = "text-embedding-3-large", input = text });
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var emb = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return emb.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
    }
}

