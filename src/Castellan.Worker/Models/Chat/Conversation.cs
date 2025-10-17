namespace Castellan.Worker.Models.Chat;

/// <summary>
/// Represents a conversation thread between the user and the AI assistant.
/// Maintains message history and conversation metadata.
/// </summary>
public class Conversation
{
    /// <summary>
    /// Unique identifier for this conversation
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID who owns this conversation
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// Title/summary of the conversation (auto-generated from first message)
    /// </summary>
    public string Title { get; set; } = "New Conversation";

    /// <summary>
    /// When the conversation was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the conversation was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Messages in this conversation (ordered by timestamp)
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// Whether this conversation is archived
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Tags applied to this conversation for organization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// User-assigned rating (1-5 stars, null if not rated)
    /// </summary>
    public int? Rating { get; set; }

    /// <summary>
    /// User's feedback comment
    /// </summary>
    public string? FeedbackComment { get; set; }

    /// <summary>
    /// Number of messages in the conversation
    /// </summary>
    public int MessageCount => Messages.Count;

    /// <summary>
    /// Last message in the conversation
    /// </summary>
    public ChatMessage? LastMessage => Messages.LastOrDefault();

    /// <summary>
    /// Whether this is a new conversation (no messages yet)
    /// </summary>
    public bool IsNew => MessageCount == 0;
}
