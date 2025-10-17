using Castellan.Worker.Models.Chat;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Main service for processing chat messages and generating AI responses.
/// Orchestrates intent classification, context retrieval, and response generation.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Processes a chat message and generates an AI response.
    /// </summary>
    /// <param name="request">Chat request containing user message and options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Chat response with assistant message and context</returns>
    Task<ChatResponse> ProcessMessageAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates suggested follow-up questions based on the current conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of suggested follow-up questions</returns>
    Task<List<string>> GenerateSuggestedFollowUpsAsync(string conversationId, CancellationToken ct = default);
}
