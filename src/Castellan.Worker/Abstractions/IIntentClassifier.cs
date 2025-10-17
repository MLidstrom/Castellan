using Castellan.Worker.Models.Chat;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for classifying user intent from chat messages.
/// Uses LLM to analyze messages and extract intent, entities, and suggested actions.
/// </summary>
public interface IIntentClassifier
{
    /// <summary>
    /// Classifies the intent of a user's message.
    /// </summary>
    /// <param name="message">User's message text</param>
    /// <param name="conversationHistory">Previous messages in the conversation for context</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Classified intent with confidence score and extracted entities</returns>
    Task<ChatIntent> ClassifyIntentAsync(
        string message,
        List<ChatMessage> conversationHistory,
        CancellationToken ct = default);
}
