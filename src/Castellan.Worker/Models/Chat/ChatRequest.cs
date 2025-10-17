namespace Castellan.Worker.Models.Chat;

/// <summary>
/// Request to send a message in a chat conversation
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User's message text
    /// </summary>
    public string Message { get; set; } = "";

    /// <summary>
    /// Conversation ID (null for new conversations)
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// Context retrieval options
    /// </summary>
    public ContextOptions? ContextOptions { get; set; }

    /// <summary>
    /// Whether to include visualizations in the response
    /// </summary>
    public bool IncludeVisualizations { get; set; } = true;

    /// <summary>
    /// Whether to include suggested actions in the response
    /// </summary>
    public bool IncludeSuggestedActions { get; set; } = true;

    /// <summary>
    /// Maximum number of citations to include
    /// </summary>
    public int MaxCitations { get; set; } = 5;

    /// <summary>
    /// User ID making the request
    /// </summary>
    public string UserId { get; set; } = "";
}

/// <summary>
/// Options for retrieving context for the chat response
/// </summary>
public class ContextOptions
{
    /// <summary>
    /// Time range for retrieving security events
    /// </summary>
    public TimeRange? TimeRange { get; set; }

    /// <summary>
    /// Maximum number of similar events to retrieve via vector search
    /// </summary>
    public int MaxSimilarEvents { get; set; } = 5;

    /// <summary>
    /// Maximum number of recent critical events to retrieve
    /// </summary>
    public int MaxRecentCriticalEvents { get; set; } = 10;

    /// <summary>
    /// Whether to include active correlation patterns
    /// </summary>
    public bool IncludeCorrelationPatterns { get; set; } = true;

    /// <summary>
    /// Whether to include current system metrics
    /// </summary>
    public bool IncludeSystemMetrics { get; set; } = true;

    /// <summary>
    /// Minimum similarity score for vector search (0.0 to 1.0)
    /// </summary>
    public float MinSimilarityScore { get; set; } = 0.7f;
}
