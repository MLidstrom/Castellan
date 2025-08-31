using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Llms;

public sealed class OpenAILlm(IOptions<LlmOptions> opt, HttpClient http) : ILlmClient
{
    public async Task<string> AnalyzeAsync(LogEvent e, IEnumerable<LogEvent> nn, CancellationToken ct)
    {
        var ctx = string.Join("\n---\n", nn.Select(x => $"{x.Time:o} [{x.Channel}/{x.EventId}] {x.Message}"));
        var sys = "You are a SOC analyst. Respond with strict JSON only.";
        var user = $@"Assess the NEW EVENT with k-NN context. Output JSON with fields: risk, mitre[], confidence, summary, recommended_actions[].

NEW EVENT:
{e.Time:o} [{e.Channel}/{e.EventId}] {e.Message}

SIMILAR EVENTS:
{ctx}";

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.Value.OpenAIKey);
        req.Content = JsonContent.Create(new {
            model = opt.Value.OpenAIModel ?? "gpt-4o-mini",
            messages = new object[] {
                new { role = "system", content = sys },
                new { role = "user", content = user }
            },
            temperature = 0.2
        });
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}

