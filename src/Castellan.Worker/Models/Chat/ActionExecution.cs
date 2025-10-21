namespace Castellan.Worker.Models.Chat;

/// <summary>
/// Represents an executed action with full lifecycle tracking and rollback capability.
/// Actions are suggested by AI but require explicit user confirmation before execution.
/// </summary>
public class ActionExecution
{
    /// <summary>
    /// Unique identifier for this action execution
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Conversation this action belongs to
    /// </summary>
    public string ConversationId { get; set; } = "";

    /// <summary>
    /// Message that suggested this action
    /// </summary>
    public string ChatMessageId { get; set; } = "";

    /// <summary>
    /// Type of action (BlockIP, IsolateHost, QuarantineFile, etc.)
    /// </summary>
    public ActionType Type { get; set; }

    /// <summary>
    /// Action-specific data serialized as JSON.
    /// For BlockIP: { "ipAddress": "192.168.1.100", "reason": "Malware detected" }
    /// For IsolateHost: { "hostname": "WORKSTATION01", "reason": "Suspicious activity" }
    /// For QuarantineFile: { "filePath": "C:\\malware.exe", "hash": "abc123..." }
    /// </summary>
    public string ActionData { get; set; } = "{}";

    /// <summary>
    /// Current status of the action
    /// </summary>
    public ActionStatus Status { get; set; } = ActionStatus.Pending;

    /// <summary>
    /// When the action was suggested by the AI
    /// </summary>
    public DateTime SuggestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the action was executed by a user (null if still pending)
    /// </summary>
    public DateTime? ExecutedAt { get; set; }

    /// <summary>
    /// When the action was rolled back (null if not rolled back)
    /// </summary>
    public DateTime? RolledBackAt { get; set; }

    /// <summary>
    /// User who executed the action
    /// </summary>
    public string? ExecutedBy { get; set; }

    /// <summary>
    /// User who rolled back the action
    /// </summary>
    public string? RolledBackBy { get; set; }

    /// <summary>
    /// System state before the action was executed (JSON snapshot).
    /// Used to restore state during rollback.
    /// For BlockIP: { "ipAddress": "...", "wasBlocked": false, "existingRules": [...] }
    /// </summary>
    public string? BeforeState { get; set; }

    /// <summary>
    /// System state after the action was executed (JSON snapshot).
    /// Used for verification and audit trail.
    /// </summary>
    public string? AfterState { get; set; }

    /// <summary>
    /// Reason provided for rolling back the action
    /// </summary>
    public string? RollbackReason { get; set; }

    /// <summary>
    /// Execution log capturing all events during action lifecycle.
    /// JSON array of log entries: [{ "timestamp": "...", "event": "...", "details": "..." }]
    /// </summary>
    public string ExecutionLog { get; set; } = "[]";

    /// <summary>
    /// Navigation property to the conversation
    /// </summary>
    public Conversation? Conversation { get; set; }

    /// <summary>
    /// Navigation property to the chat message
    /// </summary>
    public ChatMessage? ChatMessage { get; set; }

    /// <summary>
    /// Whether this action can still be rolled back (within undo window)
    /// </summary>
    public bool CanRollback(TimeSpan undoWindow)
    {
        if (Status != ActionStatus.Executed) return false;
        if (!ExecutedAt.HasValue) return false;

        var timeSinceExecution = DateTime.UtcNow - ExecutedAt.Value;
        return timeSinceExecution < undoWindow;
    }

    /// <summary>
    /// Add a log entry to the execution log
    /// </summary>
    public void AddLogEntry(string eventType, string details)
    {
        var logEntry = new
        {
            timestamp = DateTime.UtcNow,
            @event = eventType,
            details = details
        };

        // Deserialize existing log, add entry, serialize back
        var log = System.Text.Json.JsonSerializer.Deserialize<List<object>>(ExecutionLog) ?? new List<object>();
        log.Add(logEntry);
        ExecutionLog = System.Text.Json.JsonSerializer.Serialize(log);
    }
}

/// <summary>
/// Type of security action that can be executed
/// </summary>
public enum ActionType
{
    /// <summary>
    /// Block an IP address in firewall rules
    /// </summary>
    BlockIP,

    /// <summary>
    /// Isolate a host from the network (disable network adapter)
    /// </summary>
    IsolateHost,

    /// <summary>
    /// Quarantine a suspicious file (move to quarantine location)
    /// </summary>
    QuarantineFile,

    /// <summary>
    /// Add an entity (IP, user, host) to watchlist for monitoring
    /// </summary>
    AddToWatchlist,

    /// <summary>
    /// Create an incident ticket in external system
    /// </summary>
    CreateTicket
}

/// <summary>
/// Current status of an action execution
/// </summary>
public enum ActionStatus
{
    /// <summary>
    /// Action has been suggested but not yet executed
    /// </summary>
    Pending,

    /// <summary>
    /// Action has been successfully executed
    /// </summary>
    Executed,

    /// <summary>
    /// Action has been rolled back/undone
    /// </summary>
    RolledBack,

    /// <summary>
    /// Action execution failed
    /// </summary>
    Failed,

    /// <summary>
    /// Action's undo window has expired
    /// </summary>
    Expired
}
