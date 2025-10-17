using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Factory for creating ILlmClient instances with specific model configurations.
/// Enables multi-model ensemble predictions by instantiating multiple clients with different models.
/// </summary>
public interface ILlmClientFactory
{
    /// <summary>
    /// Creates a new ILlmClient instance configured for the specified model.
    /// The client is fully decorated with resilience, strict JSON validation, and telemetry.
    /// </summary>
    /// <param name="modelName">Model identifier (e.g., "llama3.1:8b-instruct-q8_0", "mistral:7b-instruct")</param>
    /// <param name="provider">Provider name ("Ollama" or "OpenAI"). Defaults to "Ollama".</param>
    /// <returns>Fully configured and decorated ILlmClient instance</returns>
    /// <exception cref="ArgumentException">Thrown when modelName is null or empty</exception>
    /// <exception cref="NotSupportedException">Thrown when provider is not supported</exception>
    ILlmClient CreateClient(string modelName, string provider = "Ollama");
}
