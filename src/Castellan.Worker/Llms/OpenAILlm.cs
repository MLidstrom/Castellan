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
        var sys = @"You are a senior SOC analyst. You MUST respond with ONLY valid JSON. No markdown, no code blocks, no explanations outside the JSON object.";
        var user = $@"Analyze this Windows security event and respond with ONLY a JSON object.

REQUIRED JSON SCHEMA:
{{
  ""risk"": ""low|medium|high|critical"" (REQUIRED - exactly one of these values),
  ""mitre"": [""Txxxx""] (array of MITRE ATT&CK technique IDs, empty array if none),
  ""confidence"": 0-100 (REQUIRED - integer between 0 and 100),
  ""summary"": ""brief assessment"" (REQUIRED - 10-500 characters),
  ""recommended_actions"": [""action 1""] (array of strings, can be empty)
}}

VALIDATION RULES:
- ""risk"" MUST be exactly one of: low, medium, high, critical
- ""confidence"" MUST be integer 0-100
- ""summary"" MUST be 10-500 characters
- Output ONLY the JSON object, nothing else

NEW EVENT:
{e.Time:o} [{e.Channel}/{e.EventId}] {e.Message}

SIMILAR HISTORICAL EVENTS:
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

    public async Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.Value.OpenAIKey);
        req.Content = JsonContent.Create(new {
            model = opt.Value.OpenAIModel ?? "gpt-4o-mini",
            messages = new object[] {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.7
        });

        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}

