namespace Castellan.Worker.Models.Chat;

/// <summary>
/// Response to a chat message request
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// The assistant's response message
    /// </summary>
    public ChatMessage Message { get; set; } = new();

    /// <summary>
    /// Conversation ID (created if this was a new conversation)
    /// </summary>
    public string ConversationId { get; set; } = "";

    /// <summary>
    /// Conversation title (may be updated based on message content)
    /// </summary>
    public string ConversationTitle { get; set; } = "";

    /// <summary>
    /// Intent classified from the user's message
    /// </summary>
    public ChatIntent? Intent { get; set; }

    /// <summary>
    /// Context retrieved to generate the response
    /// </summary>
    public ChatContext? Context { get; set; }

    /// <summary>
    /// Suggested follow-up questions
    /// </summary>
    public List<string> SuggestedFollowUps { get; set; } = new();

    /// <summary>
    /// Whether this response is complete or requires further action
    /// </summary>
    public bool IsComplete { get; set; } = true;

    /// <summary>
    /// Error message if the request failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Performance metrics for the request
    /// </summary>
    public PerformanceMetrics Metrics { get; set; } = new();

    /// <summary>
    /// Whether the response was successful
    /// </summary>
    public bool Success => string.IsNullOrEmpty(Error);
}

/// <summary>
/// Performance metrics for a chat request
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Total processing time (ms)
    /// </summary>
    public long TotalMs { get; set; }

    /// <summary>
    /// Intent classification time (ms)
    /// </summary>
    public long IntentClassificationMs { get; set; }

    /// <summary>
    /// Context retrieval time (ms)
    /// </summary>
    public long ContextRetrievalMs { get; set; }

    /// <summary>
    /// LLM generation time (ms)
    /// </summary>
    public long LlmGenerationMs { get; set; }

    /// <summary>
    /// Number of events retrieved for context
    /// </summary>
    public int EventsRetrieved { get; set; }

    /// <summary>
    /// Number of tokens used in the LLM call
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// Model used for the response
    /// </summary>
    public string ModelUsed { get; set; } = "";
}
