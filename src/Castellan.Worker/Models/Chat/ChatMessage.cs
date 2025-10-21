namespace Castellan.Worker.Models.Chat;

/// <summary>
/// Represents a single message in a chat conversation.
/// Can be from the user or the assistant.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Unique identifier for this message
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Conversation this message belongs to
    /// </summary>
    public string ConversationId { get; set; } = "";

    /// <summary>
    /// Role of the message sender (user or assistant)
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// Content of the message
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Intent classified for this message (user messages only)
    /// </summary>
    public ChatIntent? Intent { get; set; }

    /// <summary>
    /// Citations/sources used to generate this response (assistant messages only)
    /// </summary>
    public List<Citation> Citations { get; set; } = new();

    /// <summary>
    /// Suggested follow-up actions (assistant messages only)
    /// </summary>
    public List<SuggestedAction> SuggestedActions { get; set; } = new();

    /// <summary>
    /// Visualizations to display (assistant messages only)
    /// </summary>
    public List<Visualization> Visualizations { get; set; } = new();

    /// <summary>
    /// Metadata about the message (processing time, model used, etc.)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Role of the message sender
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// Message from the user/analyst
    /// </summary>
    User,

    /// <summary>
    /// Message from the AI assistant
    /// </summary>
    Assistant,

    /// <summary>
    /// System message (notifications, errors, etc.)
    /// </summary>
    System
}

/// <summary>
/// Citation/source reference for assistant responses
/// </summary>
public class Citation
{
    /// <summary>
    /// Type of source (event, pattern, metric, documentation)
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// ID of the source (event ID, pattern ID, etc.)
    /// </summary>
    public string SourceId { get; set; } = "";

    /// <summary>
    /// Display text for the citation
    /// </summary>
    public string DisplayText { get; set; } = "";

    /// <summary>
    /// URL or navigation path to the source
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Relevance score (0.0 to 1.0)
    /// </summary>
    public float Relevance { get; set; }
}

/// <summary>
/// Suggested action for the user to take
/// </summary>
public class SuggestedAction
{
    /// <summary>
    /// Type of action (investigate, block, quarantine, report, etc.)
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Display label for the action button
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// Description of what the action does
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Parameters for executing the action
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Icon to display for the action
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Confidence that this action is appropriate (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// ID of the persisted ActionExecution record (0 if not yet persisted)
    /// </summary>
    public int ExecutionId { get; set; }
}

/// <summary>
/// Visualization to display in the chat (chart, table, etc.)
/// </summary>
public class Visualization
{
    /// <summary>
    /// Type of visualization (chart, table, timeline, map)
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Title for the visualization
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Data for the visualization (JSON serializable)
    /// </summary>
    public object Data { get; set; } = new { };

    /// <summary>
    /// Configuration options for the visualization
    /// </summary>
    public Dictionary<string, object> Config { get; set; } = new();
}
