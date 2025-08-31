using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Embeddings;

public sealed class OllamaEmbedder(IOptions<EmbeddingOptions> opt, HttpClient http) : IEmbedder
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        // First try the modern endpoint /api/embeddings (some Ollama versions return empty arrays here)
        var primaryPayload = new { model = opt.Value.Model, input = text };
        try
        {
            var resp = await http.PostAsJsonAsync($"{opt.Value.Endpoint}/api/embeddings", primaryPayload, ct);
            resp.EnsureSuccessStatusCode();
            var arr = await ParseEmbeddingAsync(resp, ct);
            if (arr.Length > 0)
            {
                Console.WriteLine($"OllamaEmbedder: parsed embedding length={arr.Length} via /api/embeddings");
                return arr;
            }
            Console.WriteLine("OllamaEmbedder: /api/embeddings returned empty embedding array, falling back to /api/embed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OllamaEmbedder: /api/embeddings failed: {ex.Message}. Falling back to /api/embed");
        }

        // Fallback to legacy endpoint /api/embed which returns { embeddings: [[...]] }
        var fallbackPayload = new { model = opt.Value.Model, input = new[] { text } };
        var resp2 = await http.PostAsJsonAsync($"{opt.Value.Endpoint}/api/embed", fallbackPayload, ct);
        resp2.EnsureSuccessStatusCode();
        var arr2 = await ParseEmbeddingAsync(resp2, ct);
        Console.WriteLine($"OllamaEmbedder: parsed embedding length={arr2.Length} via /api/embed");
        return arr2;
    }

    private static async Task<float[]> ParseEmbeddingAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var raw = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            // Handle multiple possible shapes
            if (root.TryGetProperty("embedding", out var embEl) && embEl.ValueKind == JsonValueKind.Array)
            {
                var arr = embEl.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
                if (arr.Length == 0)
                    Console.WriteLine($"OllamaEmbedder: empty embedding array. Raw response prefix={raw.Substring(0, Math.Min(300, raw.Length))}");
                return arr;
            }
            if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array && dataEl.GetArrayLength() > 0)
            {
                var first = dataEl[0];
                if (first.TryGetProperty("embedding", out var emb2) && emb2.ValueKind == JsonValueKind.Array)
                {
                    var arr = emb2.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
                    if (arr.Length == 0)
                        Console.WriteLine($"OllamaEmbedder: empty embedding array in data[0]. Raw response prefix={raw.Substring(0, Math.Min(300, raw.Length))}");
                    return arr;
                }
            }
            if (root.TryGetProperty("embeddings", out var embedsEl) && embedsEl.ValueKind == JsonValueKind.Array && embedsEl.GetArrayLength() > 0)
            {
                // e.g., { embeddings: [[...]] }
                var first = embedsEl[0];
                if (first.ValueKind == JsonValueKind.Array)
                {
                    var arr = first.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
                    if (arr.Length == 0)
                        Console.WriteLine($"OllamaEmbedder: empty embedding array in embeddings[0]. Raw response prefix={raw.Substring(0, Math.Min(300, raw.Length))}");
                    return arr;
                }
            }
            Console.WriteLine($"OllamaEmbedder: unexpected response, returning empty. Raw prefix={raw.Substring(0, Math.Min(200, raw.Length))}");
            return Array.Empty<float>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OllamaEmbedder: failed to parse response: {ex.Message}. Raw prefix={raw.Substring(0, Math.Min(200, raw.Length))}");
            return Array.Empty<float>();
        }
    }
}

public sealed class EmbeddingOptions
{
    public string Provider { get; set; } = "Ollama";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "nomic-embed-text";
    public string OpenAIKey { get; set; } = "";
}

