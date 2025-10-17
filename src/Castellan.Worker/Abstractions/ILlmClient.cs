using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;
public interface ILlmClient
{
    Task<string> AnalyzeAsync(LogEvent e, IEnumerable<LogEvent> neighbors, CancellationToken ct);

    /// <summary>
    /// Generates a response using the LLM with system and user prompts.
    /// Used by chat interface and other general-purpose LLM interactions.
    /// </summary>
    /// <param name="systemPrompt">System prompt defining the LLM's role and context</param>
    /// <param name="userPrompt">User's message or query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated response as text</returns>
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

