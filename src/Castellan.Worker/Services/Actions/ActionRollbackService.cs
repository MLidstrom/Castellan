using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Castellan.Worker.Services.Actions;

/// <summary>
/// Service for managing security action execution lifecycle with full rollback capability.
/// Implements human-in-the-loop pattern with explicit confirmation requirements.
/// </summary>
public class ActionRollbackService : IActionRollbackService
{
    private readonly IDbContextFactory<CastellanDbContext> _contextFactory;
    private readonly IEnumerable<IActionHandler> _actionHandlers;
    private readonly ActionRollbackOptions _options;
    private readonly ILogger<ActionRollbackService> _logger;

    public ActionRollbackService(
        IDbContextFactory<CastellanDbContext> contextFactory,
        IEnumerable<IActionHandler> actionHandlers,
        IOptions<ActionRollbackOptions> options,
        ILogger<ActionRollbackService> logger)
    {
        _contextFactory = contextFactory;
        _actionHandlers = actionHandlers;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ActionExecution> SuggestActionAsync(
        string conversationId,
        string messageId,
        ActionType type,
        object actionData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Suggesting action {ActionType} for conversation {ConversationId}",
            type, conversationId);

        // Get handler for validation
        var handler = GetHandlerForType(type);
        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for action type {type}");
        }

        // Validate action data
        var validation = await handler.ValidateAsync(actionData);
        if (!validation.IsValid)
        {
            throw new ArgumentException($"Invalid action data: {validation.ErrorMessage}");
        }

        // Check if max pending actions limit is reached
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        if (_options.MaxPendingActionsPerConversation > 0)
        {
            var pendingCount = await context.ActionExecutions
                .CountAsync(a => a.ConversationId == conversationId &&
                                 a.Status == ActionStatus.Pending,
                           cancellationToken);

            if (pendingCount >= _options.MaxPendingActionsPerConversation)
            {
                throw new InvalidOperationException(
                    $"Maximum pending actions ({_options.MaxPendingActionsPerConversation}) reached for this conversation");
            }
        }

        // Create action execution record
        var action = new ActionExecution
        {
            ConversationId = conversationId,
            ChatMessageId = messageId,
            Type = type,
            ActionData = JsonSerializer.Serialize(actionData),
            Status = ActionStatus.Pending,
            SuggestedAt = DateTime.UtcNow,
            ExecutionLog = "[]"
        };

        action.AddLogEntry("ActionSuggested", $"Action {type} suggested for execution");

        context.ActionExecutions.Add(action);
        
        _logger.LogInformation(
            "Attempting to save action {ActionType} for conversation {ConversationId}",
            type, conversationId);
        
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Successfully saved action {ActionId} ({ActionType}) to database",
                action.Id, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to save action {ActionType} for conversation {ConversationId}: {Error}",
                type, conversationId, ex.Message);
            throw;
        }

        _logger.LogInformation(
            "Action {ActionId} ({ActionType}) suggested successfully",
            action.Id, type);

        return action;
    }

    public async Task<ActionExecution> ExecuteActionAsync(
        int actionId,
        string executedBy,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing action {ActionId} by user {User}",
            actionId, executedBy);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        _logger.LogInformation(
            "Searching for action {ActionId} in database",
            actionId);

        var action = await context.ActionExecutions
            .Include(a => a.Conversation)
            .Include(a => a.ChatMessage)
            .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken);

        if (action == null)
        {
            _logger.LogWarning(
                "Action {ActionId} not found in database",
                actionId);
            throw new InvalidOperationException($"Action {actionId} not found");
        }

        _logger.LogInformation(
            "Found action {ActionId} with status {Status}",
            action.Id, action.Status);

        if (action.Status != ActionStatus.Pending)
        {
            throw new InvalidOperationException($"Action {actionId} is not in Pending status (current: {action.Status})");
        }

        // Check if action has expired
        if (_options.AutoExpirePendingActions)
        {
            var expirationTime = action.SuggestedAt.AddHours(_options.PendingActionExpirationHours);
            if (DateTime.UtcNow > expirationTime)
            {
                action.Status = ActionStatus.Expired;
                action.AddLogEntry("ActionExpired", $"Action expired after {_options.PendingActionExpirationHours} hours");
                await context.SaveChangesAsync(cancellationToken);

                throw new InvalidOperationException($"Action {actionId} has expired");
            }
        }

        // Get handler
        var handler = GetHandlerForType(action.Type);
        if (handler == null)
        {
            action.Status = ActionStatus.Failed;
            action.AddLogEntry("ExecutionFailed", $"No handler found for action type {action.Type}");
            await context.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException($"No handler registered for action type {action.Type}");
        }

        // Deserialize action data
        var actionData = JsonSerializer.Deserialize<object>(action.ActionData);

        try
        {
            // Capture before state
            action.AddLogEntry("CapturingState", "Capturing system state before execution");
            action.BeforeState = await handler.CaptureBeforeStateAsync(actionData!, cancellationToken);

            // Execute action
            action.AddLogEntry("ExecutingAction", $"Executing {action.Type} action");
            ActionExecutionResult result = await handler.ExecuteAsync(actionData!, cancellationToken);

            if (result.Success)
            {
                action.Status = ActionStatus.Executed;
                action.ExecutedAt = DateTime.UtcNow;
                action.ExecutedBy = executedBy;
                action.AfterState = result.AfterState;
                action.AddLogEntry("ExecutionSucceeded", result.Message);

                // Add detailed logs from handler
                foreach (var log in result.Logs)
                {
                    action.AddLogEntry("HandlerLog", log);
                }

                _logger.LogInformation(
                    "Action {ActionId} ({ActionType}) executed successfully by {User}",
                    actionId, action.Type, executedBy);
            }
            else
            {
                action.Status = ActionStatus.Failed;
                action.AddLogEntry("ExecutionFailed", result.Message);
                if (result.ErrorDetails != null)
                {
                    action.AddLogEntry("ErrorDetails", result.ErrorDetails);
                }

                _logger.LogWarning(
                    "Action {ActionId} ({ActionType}) execution failed: {Message}",
                    actionId, action.Type, result.Message);
            }
        }
        catch (Exception ex)
        {
            action.Status = ActionStatus.Failed;
            action.AddLogEntry("ExecutionException", $"Exception during execution: {ex.Message}");

            _logger.LogError(ex,
                "Exception during execution of action {ActionId} ({ActionType})",
                actionId, action.Type);
        }

        await context.SaveChangesAsync(cancellationToken);
        return action;
    }

    public async Task<ActionExecution> RollbackActionAsync(
        int actionId,
        string rolledBackBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Rolling back action {ActionId} by user {User}. Reason: {Reason}",
            actionId, rolledBackBy, reason);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var action = await context.ActionExecutions
            .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken);

        if (action == null)
        {
            throw new InvalidOperationException($"Action {actionId} not found");
        }

        if (action.Status != ActionStatus.Executed)
        {
            throw new InvalidOperationException($"Action {actionId} is not in Executed status (current: {action.Status})");
        }

        // Check if undo is allowed for this action type
        var settings = _options.GetSettingsForActionType(action.Type);
        if (!settings.AllowUndo)
        {
            throw new InvalidOperationException($"Undo is not allowed for action type {action.Type}");
        }

        // Check undo window
        var undoWindow = TimeSpan.FromHours(settings.UndoWindowHours);
        if (!action.CanRollback(undoWindow))
        {
            throw new InvalidOperationException($"Action {actionId} is outside the undo window ({settings.UndoWindowHours} hours)");
        }

        // Get handler
        var handler = GetHandlerForType(action.Type);
        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for action type {action.Type}");
        }

        // Deserialize action data
        var actionData = JsonSerializer.Deserialize<object>(action.ActionData);

        try
        {
            action.AddLogEntry("InitiatingRollback", $"Rollback initiated by {rolledBackBy}. Reason: {reason}");

            // Rollback action
            ActionExecutionResult result = await handler.RollbackAsync(actionData!, action.BeforeState!, cancellationToken);

            if (result.Success)
            {
                action.Status = ActionStatus.RolledBack;
                action.RolledBackAt = DateTime.UtcNow;
                action.RolledBackBy = rolledBackBy;
                action.RollbackReason = reason;
                action.AddLogEntry("RollbackSucceeded", result.Message);

                // Add detailed logs from handler
                foreach (var log in result.Logs)
                {
                    action.AddLogEntry("HandlerLog", log);
                }

                _logger.LogInformation(
                    "Action {ActionId} ({ActionType}) rolled back successfully by {User}",
                    actionId, action.Type, rolledBackBy);
            }
            else
            {
                action.AddLogEntry("RollbackFailed", result.Message);
                if (result.ErrorDetails != null)
                {
                    action.AddLogEntry("ErrorDetails", result.ErrorDetails);
                }

                _logger.LogWarning(
                    "Action {ActionId} ({ActionType}) rollback failed: {Message}",
                    actionId, action.Type, result.Message);

                throw new InvalidOperationException($"Rollback failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            action.AddLogEntry("RollbackException", $"Exception during rollback: {ex.Message}");

            _logger.LogError(ex,
                "Exception during rollback of action {ActionId} ({ActionType})",
                actionId, action.Type);

            throw;
        }

        await context.SaveChangesAsync(cancellationToken);
        return action;
    }

    public async Task<List<ActionExecution>> GetPendingActionsAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ActionExecutions
            .Where(a => a.ConversationId == conversationId && a.Status == ActionStatus.Pending)
            .OrderByDescending(a => a.SuggestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ActionExecution>> GetActionHistoryAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ActionExecutions
            .Where(a => a.ConversationId == conversationId)
            .OrderByDescending(a => a.SuggestedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ActionExecution?> GetActionByIdAsync(
        int actionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ActionExecutions
            .Include(a => a.Conversation)
            .Include(a => a.ChatMessage)
            .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken);
    }

    public async Task<bool> CanRollbackAsync(
        int actionId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var action = await context.ActionExecutions
            .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken);

        if (action == null || action.Status != ActionStatus.Executed)
        {
            return false;
        }

        var settings = _options.GetSettingsForActionType(action.Type);
        if (!settings.AllowUndo)
        {
            return false;
        }

        var undoWindow = TimeSpan.FromHours(settings.UndoWindowHours);
        return action.CanRollback(undoWindow);
    }

    public TimeSpan GetUndoWindow(ActionType type)
    {
        var settings = _options.GetSettingsForActionType(type);
        return TimeSpan.FromHours(settings.UndoWindowHours);
    }

    public async Task<ActionStatistics> GetActionStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var allActions = await context.ActionExecutions.ToListAsync(cancellationToken);

        var stats = new ActionStatistics
        {
            TotalSuggested = allActions.Count,
            TotalExecuted = allActions.Count(a => a.Status == ActionStatus.Executed),
            TotalRolledBack = allActions.Count(a => a.Status == ActionStatus.RolledBack),
            TotalFailed = allActions.Count(a => a.Status == ActionStatus.Failed),
            PendingActions = allActions.Count(a => a.Status == ActionStatus.Pending)
        };

        // Count rollback-eligible actions
        foreach (var action in allActions.Where(a => a.Status == ActionStatus.Executed))
        {
            var settings = _options.GetSettingsForActionType(action.Type);
            if (settings.AllowUndo)
            {
                var undoWindow = TimeSpan.FromHours(settings.UndoWindowHours);
                if (action.CanRollback(undoWindow))
                {
                    stats.RollbackEligible++;
                }
            }
        }

        // Actions by type
        stats.ActionsByType = allActions
            .GroupBy(a => a.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        // Most common action
        stats.MostCommonAction = stats.ActionsByType
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault().Key;

        // Average execution delay
        var executedActions = allActions.Where(a => a.ExecutedAt.HasValue).ToList();
        if (executedActions.Any())
        {
            stats.AverageExecutionDelayMinutes = executedActions
                .Average(a => (a.ExecutedAt!.Value - a.SuggestedAt).TotalMinutes);
        }

        return stats;
    }

    private IActionHandler? GetHandlerForType(ActionType type)
    {
        return _actionHandlers.FirstOrDefault(h => h.ActionType == type);
    }
}
