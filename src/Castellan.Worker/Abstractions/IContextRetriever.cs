using Castellan.Worker.Models.Chat;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for retrieving relevant context to enhance chat responses.
/// Uses RAG (Retrieval-Augmented Generation) to fetch security events, patterns, and metrics.
/// </summary>
public interface IContextRetriever
{
    /// <summary>
    /// Retrieves relevant context for a chat message.
    /// </summary>
    /// <param name="message">User's message text</param>
    /// <param name="intent">Classified intent</param>
    /// <param name="options">Context retrieval options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Context information to enhance the response</returns>
    Task<ChatContext> RetrieveContextAsync(
        string message,
        ChatIntent intent,
        ContextOptions? options = null,
        CancellationToken ct = default);
}
