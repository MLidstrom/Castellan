using Castellan.Worker.Models.Chat;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for managing action execution lifecycle including execution, rollback, and history tracking.
/// Provides human-in-the-loop security action management with full audit trail.
/// </summary>
public interface IActionRollbackService
{
    /// <summary>
    /// Suggests an action to be executed. Action is stored as Pending until explicitly executed.
    /// </summary>
    /// <param name="conversationId">Conversation where action was suggested</param>
    /// <param name="messageId">Message that suggested this action</param>
    /// <param name="type">Type of action (BlockIP, IsolateHost, etc.)</param>
    /// <param name="actionData">Action-specific data (IP address, hostname, file path, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created action execution record</returns>
    Task<ActionExecution> SuggestActionAsync(
        string conversationId,
        string messageId,
        ActionType type,
        object actionData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a pending action. Requires explicit user confirmation.
    /// </summary>
    /// <param name="actionId">ID of the action to execute</param>
    /// <param name="executedBy">Username of the user executing the action</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated action execution record</returns>
    Task<ActionExecution> ExecuteActionAsync(
        int actionId,
        string executedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a previously executed action. Only works within the configured undo window.
    /// </summary>
    /// <param name="actionId">ID of the action to rollback</param>
    /// <param name="rolledBackBy">Username of the user performing the rollback</param>
    /// <param name="reason">Reason for rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated action execution record</returns>
    Task<ActionExecution> RollbackActionAsync(
        int actionId,
        string rolledBackBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending actions for a conversation (not yet executed).
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending actions</returns>
    Task<List<ActionExecution>> GetPendingActionsAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets complete action history for a conversation (all statuses).
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all actions ordered by suggested date descending</returns>
    Task<List<ActionExecution>> GetActionHistoryAsync(
        string conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific action by ID.
    /// </summary>
    /// <param name="actionId">Action ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Action execution or null if not found</returns>
    Task<ActionExecution?> GetActionByIdAsync(
        int actionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an action can still be rolled back (within undo window).
    /// </summary>
    /// <param name="actionId">Action ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if action can be rolled back, false otherwise</returns>
    Task<bool> CanRollbackAsync(
        int actionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured undo window duration for a specific action type.
    /// </summary>
    /// <param name="type">Action type</param>
    /// <returns>TimeSpan representing the undo window</returns>
    TimeSpan GetUndoWindow(ActionType type);

    /// <summary>
    /// Gets statistics about actions across all conversations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Action statistics</returns>
    Task<ActionStatistics> GetActionStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about action executions
/// </summary>
public class ActionStatistics
{
    /// <summary>
    /// Total number of actions suggested
    /// </summary>
    public int TotalSuggested { get; set; }

    /// <summary>
    /// Total number of actions executed
    /// </summary>
    public int TotalExecuted { get; set; }

    /// <summary>
    /// Total number of actions rolled back
    /// </summary>
    public int TotalRolledBack { get; set; }

    /// <summary>
    /// Total number of failed actions
    /// </summary>
    public int TotalFailed { get; set; }

    /// <summary>
    /// Number of pending actions
    /// </summary>
    public int PendingActions { get; set; }

    /// <summary>
    /// Number of actions that can still be rolled back
    /// </summary>
    public int RollbackEligible { get; set; }

    /// <summary>
    /// Breakdown by action type
    /// </summary>
    public Dictionary<ActionType, int> ActionsByType { get; set; } = new();

    /// <summary>
    /// Average time from suggestion to execution (in minutes)
    /// </summary>
    public double AverageExecutionDelayMinutes { get; set; }

    /// <summary>
    /// Most commonly executed action type
    /// </summary>
    public ActionType? MostCommonAction { get; set; }
}
