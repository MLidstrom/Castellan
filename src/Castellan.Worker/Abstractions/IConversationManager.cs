using Castellan.Worker.Models.Chat;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for managing chat conversations and message history.
/// Handles conversation persistence, retrieval, and lifecycle management.
/// </summary>
public interface IConversationManager
{
    /// <summary>
    /// Creates a new conversation for a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created conversation</returns>
    Task<Conversation> CreateConversationAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a conversation by ID.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The conversation, or null if not found</returns>
    Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all conversations for a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="includeArchived">Whether to include archived conversations</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of conversations</returns>
    Task<List<Conversation>> GetConversationsAsync(
        string userId,
        bool includeArchived = false,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a message to a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="message">Message to add</param>
    /// <param name="ct">Cancellation token</param>
    Task AddMessageAsync(string conversationId, ChatMessage message, CancellationToken ct = default);

    /// <summary>
    /// Adds multiple messages to a conversation in a single transaction.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="messages">Messages to add</param>
    /// <param name="ct">Cancellation token</param>
    Task AddMessagesAsync(string conversationId, IEnumerable<ChatMessage> messages, CancellationToken ct = default);

    /// <summary>
    /// Updates conversation metadata (title, tags, etc.).
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="title">New title (optional)</param>
    /// <param name="tags">New tags (optional)</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateConversationAsync(
        string conversationId,
        string? title = null,
        List<string>? tags = null,
        CancellationToken ct = default);

    /// <summary>
    /// Archives a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    Task ArchiveConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a conversation and all its messages.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteConversationAsync(string conversationId, CancellationToken ct = default);

    /// <summary>
    /// Records user feedback (rating and comment) for a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="rating">Rating (1-5 stars)</param>
    /// <param name="comment">Feedback comment</param>
    /// <param name="ct">Cancellation token</param>
    Task RecordFeedbackAsync(
        string conversationId,
        int rating,
        string? comment = null,
        CancellationToken ct = default);
}
