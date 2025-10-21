using Castellan.Worker.Models.Chat;

namespace Castellan.Worker.Configuration;

/// <summary>
/// Configuration options for action execution and rollback functionality
/// </summary>
public class ActionRollbackOptions
{
    /// <summary>
    /// Default undo window in hours for all actions (24 hours default)
    /// </summary>
    public int UndoWindowHours { get; set; } = 24;

    /// <summary>
    /// Whether to require explicit confirmation before executing actions
    /// </summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>
    /// Whether undo/rollback is allowed
    /// </summary>
    public bool AllowUndo { get; set; } = true;

    /// <summary>
    /// Whether to create audit log entries for all action operations
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Maximum number of pending actions per conversation (0 = unlimited)
    /// </summary>
    public int MaxPendingActionsPerConversation { get; set; } = 50;

    /// <summary>
    /// Whether to automatically expire actions that haven't been executed within a timeframe
    /// </summary>
    public bool AutoExpirePendingActions { get; set; } = true;

    /// <summary>
    /// Hours after which pending actions are automatically expired (72 hours default)
    /// </summary>
    public int PendingActionExpirationHours { get; set; } = 72;

    /// <summary>
    /// Action-specific settings that override defaults
    /// </summary>
    public Dictionary<string, ActionTypeSettings> ActionSettings { get; set; } = new()
    {
        ["BlockIP"] = new ActionTypeSettings
        {
            UndoWindowHours = 24,
            RequireConfirmation = true,
            AllowUndo = true,
            Priority = 3
        },
        ["IsolateHost"] = new ActionTypeSettings
        {
            UndoWindowHours = 12,
            RequireConfirmation = true,
            AllowUndo = true,
            Priority = 5
        },
        ["QuarantineFile"] = new ActionTypeSettings
        {
            UndoWindowHours = 48,
            RequireConfirmation = true,
            AllowUndo = true,
            Priority = 4
        },
        ["AddToWatchlist"] = new ActionTypeSettings
        {
            UndoWindowHours = 168, // 7 days
            RequireConfirmation = false,
            AllowUndo = true,
            Priority = 1
        },
        ["CreateTicket"] = new ActionTypeSettings
        {
            UndoWindowHours = 24,
            RequireConfirmation = false,
            AllowUndo = false, // Tickets can't be undone, only closed
            Priority = 2
        }
    };

    /// <summary>
    /// Gets settings for a specific action type
    /// </summary>
    public ActionTypeSettings GetSettingsForActionType(ActionType type)
    {
        var key = type.ToString();
        if (ActionSettings.TryGetValue(key, out var settings))
        {
            return settings;
        }

        // Return default settings if not configured
        return new ActionTypeSettings
        {
            UndoWindowHours = UndoWindowHours,
            RequireConfirmation = RequireConfirmation,
            AllowUndo = AllowUndo,
            Priority = 3
        };
    }
}

/// <summary>
/// Configuration settings for a specific action type
/// </summary>
public class ActionTypeSettings
{
    /// <summary>
    /// Undo window in hours for this action type
    /// </summary>
    public int UndoWindowHours { get; set; }

    /// <summary>
    /// Whether confirmation is required for this action type
    /// </summary>
    public bool RequireConfirmation { get; set; }

    /// <summary>
    /// Whether this action type can be undone
    /// </summary>
    public bool AllowUndo { get; set; }

    /// <summary>
    /// Priority level (1-5, higher = more critical, requires more scrutiny)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Custom validation rules (future extension)
    /// </summary>
    public Dictionary<string, object>? CustomValidation { get; set; }
}
