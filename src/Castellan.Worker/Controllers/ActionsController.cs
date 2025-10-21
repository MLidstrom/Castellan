using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Castellan.Worker.Controllers;

/// <summary>
/// Controller for security action execution and rollback operations.
/// Provides REST API endpoints for human-in-the-loop security actions with undo capability.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ActionsController : ControllerBase
{
    private readonly IActionRollbackService _actionService;
    private readonly ILogger<ActionsController> _logger;

    public ActionsController(
        IActionRollbackService actionService,
        ILogger<ActionsController> logger)
    {
        _actionService = actionService;
        _logger = logger;
    }

    /// <summary>
    /// Suggests a new action for user review (creates pending ActionExecution).
    /// </summary>
    /// <param name="request">Action suggestion request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created action execution record</returns>
    [HttpPost("suggest")]
    [ProducesResponseType(typeof(ActionExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionExecution>> SuggestAction(
        [FromBody] SuggestActionRequest request,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ConversationId))
            {
                return BadRequest(new { Error = "ConversationId is required" });
            }

            if (string.IsNullOrWhiteSpace(request.ChatMessageId))
            {
                return BadRequest(new { Error = "ChatMessageId is required" });
            }

            var action = await _actionService.SuggestActionAsync(
                request.ConversationId,
                request.ChatMessageId,
                request.Type,
                request.ActionData,
                ct);

            return Ok(action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suggest action");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to suggest action", Details = ex.Message });
        }
    }

    /// <summary>
    /// Executes a pending action.
    /// </summary>
    /// <param name="id">Action execution ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated action execution record</returns>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(typeof(ActionExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionExecution>> ExecuteAction(
        int id,
        CancellationToken ct)
    {
        try
        {
            var executedBy = GetUserId();
            var action = await _actionService.ExecuteActionAsync(id, executedBy, ct);
            return Ok(action);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when executing action {ActionId}", id);
            return BadRequest(new { Error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Error = $"Action {id} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute action {ActionId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to execute action", Details = ex.Message });
        }
    }

    /// <summary>
    /// Rolls back an executed action (undo).
    /// </summary>
    /// <param name="id">Action execution ID</param>
    /// <param name="request">Rollback request with reason</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated action execution record</returns>
    [HttpPost("{id}/rollback")]
    [ProducesResponseType(typeof(ActionExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActionExecution>> RollbackAction(
        int id,
        [FromBody] RollbackActionRequest request,
        CancellationToken ct)
    {
        try
        {
            var rolledBackBy = GetUserId();
            var action = await _actionService.RollbackActionAsync(
                id,
                rolledBackBy,
                request.Reason ?? "User requested rollback",
                ct);
            return Ok(action);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when rolling back action {ActionId}", id);
            return BadRequest(new { Error = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { Error = $"Action {id} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback action {ActionId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to rollback action", Details = ex.Message });
        }
    }

    /// <summary>
    /// Gets all pending actions for a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of pending actions</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<ActionExecution>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActionExecution>>> GetPendingActions(
        [FromQuery] string conversationId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return BadRequest(new { Error = "ConversationId is required" });
            }

            var actions = await _actionService.GetPendingActionsAsync(conversationId, ct);
            return Ok(actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending actions for conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to get pending actions" });
        }
    }

    /// <summary>
    /// Gets action history for a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all actions (pending, executed, rolled back)</returns>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<ActionExecution>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActionExecution>>> GetActionHistory(
        [FromQuery] string conversationId,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return BadRequest(new { Error = "ConversationId is required" });
            }

            var actions = await _actionService.GetActionHistoryAsync(conversationId, ct);
            return Ok(actions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get action history for conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to get action history" });
        }
    }

    /// <summary>
    /// Gets a specific action by ID.
    /// </summary>
    /// <param name="id">Action execution ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Action execution record</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ActionExecution), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionExecution>> GetAction(int id, CancellationToken ct)
    {
        try
        {
            var action = await _actionService.GetActionByIdAsync(id, ct);
            if (action == null)
            {
                return NotFound(new { Error = $"Action {id} not found" });
            }
            return Ok(action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get action {ActionId}", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to get action" });
        }
    }

    /// <summary>
    /// Checks if an action can be rolled back.
    /// </summary>
    /// <param name="id">Action execution ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Boolean indicating if rollback is possible</returns>
    [HttpGet("{id}/can-rollback")]
    [ProducesResponseType(typeof(CanRollbackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CanRollbackResponse>> CanRollback(int id, CancellationToken ct)
    {
        try
        {
            var canRollback = await _actionService.CanRollbackAsync(id, ct);
            var action = await _actionService.GetActionByIdAsync(id, ct);

            if (action == null)
            {
                return NotFound(new { Error = $"Action {id} not found" });
            }

            var undoWindow = _actionService.GetUndoWindow(action.Type);

            return Ok(new CanRollbackResponse
            {
                CanRollback = canRollback,
                Reason = canRollback
                    ? "Action can be rolled back"
                    : GetRollbackBlockReason(action, undoWindow)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if action {ActionId} can be rolled back", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to check rollback status" });
        }
    }

    /// <summary>
    /// Gets action execution statistics.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Action statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ActionStatistics), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActionStatistics>> GetStatistics(CancellationToken ct)
    {
        try
        {
            var statistics = await _actionService.GetActionStatisticsAsync(ct);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get action statistics");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to get statistics" });
        }
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";
    }

    private string GetRollbackBlockReason(ActionExecution action, TimeSpan undoWindow)
    {
        if (action.Status != ActionStatus.Executed)
        {
            return $"Action is in {action.Status} status, not Executed";
        }

        if (!action.CanRollback(undoWindow))
        {
            var hoursSinceExecution = (DateTime.UtcNow - action.ExecutedAt!.Value).TotalHours;
            return $"Undo window expired ({hoursSinceExecution:F1}h since execution, window is {undoWindow.TotalHours}h)";
        }

        return "Unknown reason";
    }
}

/// <summary>
/// Request model for suggesting a new action.
/// </summary>
public class SuggestActionRequest
{
    public string ConversationId { get; set; } = "";
    public string ChatMessageId { get; set; } = "";
    public ActionType Type { get; set; }
    public object ActionData { get; set; } = new { };
}

/// <summary>
/// Request model for rolling back an action.
/// </summary>
public class RollbackActionRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// Response model for can-rollback check.
/// </summary>
public class CanRollbackResponse
{
    public bool CanRollback { get; set; }
    public string Reason { get; set; } = "";
}
