namespace Castellan.Worker.Models.Chat;

/// <summary>
/// Represents the classified intent of a user's chat message.
/// Used to route requests to appropriate handlers and retrieve relevant context.
/// </summary>
public class ChatIntent
{
    /// <summary>
    /// The classified intent type
    /// </summary>
    public IntentType Type { get; set; }

    /// <summary>
    /// Confidence score for this intent classification (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Extracted entities from the user's message
    /// </summary>
    public Dictionary<string, string> Entities { get; set; } = new();

    /// <summary>
    /// Whether this intent requires an action to be executed
    /// </summary>
    public bool RequiresAction { get; set; }

    /// <summary>
    /// Suggested action to execute (if RequiresAction is true)
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    /// Additional context or parameters for the action
    /// </summary>
    public Dictionary<string, object> ActionParameters { get; set; } = new();
}

/// <summary>
/// Types of intents that can be classified from user messages
/// </summary>
public enum IntentType
{
    /// <summary>
    /// General query about security events or system status
    /// Examples: "How many critical events today?", "What's my system status?"
    /// </summary>
    Query,

    /// <summary>
    /// Investigation of specific events or incidents
    /// Examples: "Show me details about event X", "What caused this alert?"
    /// </summary>
    Investigate,

    /// <summary>
    /// Proactive threat hunting
    /// Examples: "Are there any suspicious login patterns?", "Look for privilege escalation attempts"
    /// </summary>
    Hunt,

    /// <summary>
    /// Compliance or regulatory queries
    /// Examples: "Show me PCI-DSS violations", "Generate SOX compliance report"
    /// </summary>
    Compliance,

    /// <summary>
    /// Request for explanation of AI decisions
    /// Examples: "Why was this classified as high risk?", "Explain this threat score"
    /// </summary>
    Explain,

    /// <summary>
    /// Action request (block IP, quarantine file, etc.)
    /// Examples: "Block this IP address", "Quarantine this file"
    /// </summary>
    Action,

    /// <summary>
    /// Conversational or unclear intent
    /// Examples: "Hello", "Thanks", "I don't understand"
    /// </summary>
    Conversational
}
