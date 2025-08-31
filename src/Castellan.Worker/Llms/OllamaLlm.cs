using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Llms;

public sealed class OllamaLlm(IOptions<LlmOptions> opt, HttpClient http) : ILlmClient
{
    public async Task<string> AnalyzeAsync(LogEvent e, IEnumerable<LogEvent> nn, CancellationToken ct)
    {
        var ctx = string.Join("\n---\n", nn.Select(x => $"{x.Time:o} [{x.Channel}/{x.EventId}] {x.Message}"));
        var prompt = $@"You are a senior SOC analyst specializing in Windows security event analysis. Analyze the following event and provide a structured security assessment.

IMPORTANT SECURITY CONTEXT:
- Event ID {e.EventId} from {e.Channel} channel
- Focus on authentication events (4624, 4625), privilege escalation (4672, 4728, 4732), account management (4720, 4722, 4724), and security policy changes (4719, 4902, 4904, 4905, 4907, 4908)
- Consider MITRE ATT&CK techniques relevant to Windows security events
- Assess risk based on event type, timing, user context, and similar historical events

Output strict JSON only:
{{
  ""risk"": ""low|medium|high|critical"",
  ""mitre"": [""Txxxx"", ""Txxxx""],
  ""confidence"": 0-100,
  ""summary"": ""brief security assessment"",
  ""recommended_actions"": [""specific action 1"", ""specific action 2""]
}}

NEW EVENT:
{e.Time:o} [{e.Channel}/{e.EventId}] {e.Message}

SIMILAR HISTORICAL EVENTS:
{ctx}

SECURITY ANALYSIS:";

        var payload = new { model = opt.Value.Model, prompt, stream = false };
        using var resp = await http.PostAsJsonAsync($"{opt.Value.Endpoint}/api/generate", payload, ct);
        resp.EnsureSuccessStatusCode();
        
        try
        {
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (json.TryGetProperty("response", out var responseElement))
            {
                return responseElement.GetString() ?? "";
            }
            return "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}

public sealed class LlmOptions
{
    public string Provider { get; set; } = "Ollama";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.1:8b-instruct-q8_0";
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
    public string OpenAIKey { get; set; } = "";
}

