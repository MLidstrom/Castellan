using Castellan.Worker.Models.Chat;
using Castellan.Worker.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Castellan.Worker.Controllers;

/// <summary>
/// Controller for chat interface operations.
/// Provides REST API endpoints for conversational AI interactions.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IConversationManager _conversationManager;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        IConversationManager conversationManager,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _conversationManager = conversationManager;
        _logger = logger;
        _logger.LogInformation("ChatController instantiated successfully with ChatService: {ChatServiceType}", chatService?.GetType().Name ?? "NULL");
    }

    /// <summary>
    /// Sends a message to the chat assistant and receives a response.
    /// </summary>
    /// <param name="request">Chat request containing the user's message</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Chat response with assistant message and context</returns>
    [HttpPost("message")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> SendMessage(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { Error = "Message cannot be empty" });
            }

            // Set user ID from authenticated user
            request.UserId = GetUserId();

            var response = await _chatService.ProcessMessageAsync(request, ct);

            if (!response.Success)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process chat message");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to process message" });
        }
    }

    /// <summary>
    /// Gets all conversations for the authenticated user.
    /// </summary>
    /// <param name="includeArchived">Whether to include archived conversations</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of conversations</returns>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(List<Conversation>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Conversation>>> GetConversations(
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        try
        {
            var userId = GetUserId();
            var conversations = await _conversationManager.GetConversationsAsync(
                userId,
                includeArchived,
                ct);

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversations");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to get conversations" });
        }
    }

    /// <summary>
    /// Gets a specific conversation by ID.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The conversation with all messages</returns>
    [HttpGet("conversations/{conversationId}")]
    [ProducesResponseType(typeof(Conversation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Conversation>> GetConversation(
        string conversationId,
        CancellationToken ct)
    {
        try
        {
            var conversation = await _conversationManager.GetConversationAsync(conversationId, ct);

            if (conversation == null)
            {
                return NotFound(new { Error = "Conversation not found" });
            }

            // Verify user owns this conversation
            var userId = GetUserId();
            if (conversation.UserId != userId)
            {
                return Forbid();
            }

            return Ok(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to get conversation" });
        }
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created conversation</returns>
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(Conversation), StatusCodes.Status201Created)]
    public async Task<ActionResult<Conversation>> CreateConversation(CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var conversation = await _conversationManager.CreateConversationAsync(userId, ct);

            return CreatedAtAction(
                nameof(GetConversation),
                new { conversationId = conversation.Id },
                conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to create conversation" });
        }
    }

    /// <summary>
    /// Updates a conversation's metadata.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="update">Update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPatch("conversations/{conversationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConversation(
        string conversationId,
        [FromBody] ConversationUpdateRequest update,
        CancellationToken ct)
    {
        try
        {
            // Verify conversation exists and user owns it
            var conversation = await _conversationManager.GetConversationAsync(conversationId, ct);
            if (conversation == null)
            {
                return NotFound(new { Error = "Conversation not found" });
            }

            var userId = GetUserId();
            if (conversation.UserId != userId)
            {
                return Forbid();
            }

            await _conversationManager.UpdateConversationAsync(
                conversationId,
                update.Title,
                update.Tags,
                ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to update conversation" });
        }
    }

    /// <summary>
    /// Archives a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("conversations/{conversationId}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveConversation(
        string conversationId,
        CancellationToken ct)
    {
        try
        {
            // Verify conversation exists and user owns it
            var conversation = await _conversationManager.GetConversationAsync(conversationId, ct);
            if (conversation == null)
            {
                return NotFound(new { Error = "Conversation not found" });
            }

            var userId = GetUserId();
            if (conversation.UserId != userId)
            {
                return Forbid();
            }

            await _conversationManager.ArchiveConversationAsync(conversationId, ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to archive conversation" });
        }
    }

    /// <summary>
    /// Deletes a conversation and all its messages.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("conversations/{conversationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConversation(
        string conversationId,
        CancellationToken ct)
    {
        try
        {
            // Verify conversation exists and user owns it
            var conversation = await _conversationManager.GetConversationAsync(conversationId, ct);
            if (conversation == null)
            {
                return NotFound(new { Error = "Conversation not found" });
            }

            var userId = GetUserId();
            if (conversation.UserId != userId)
            {
                return Forbid();
            }

            await _conversationManager.DeleteConversationAsync(conversationId, ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to delete conversation" });
        }
    }

    /// <summary>
    /// Records user feedback (rating and comment) for a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="feedback">Feedback request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("conversations/{conversationId}/feedback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordFeedback(
        string conversationId,
        [FromBody] ConversationFeedbackRequest feedback,
        CancellationToken ct)
    {
        try
        {
            if (feedback.Rating < 1 || feedback.Rating > 5)
            {
                return BadRequest(new { Error = "Rating must be between 1 and 5" });
            }

            // Verify conversation exists and user owns it
            var conversation = await _conversationManager.GetConversationAsync(conversationId, ct);
            if (conversation == null)
            {
                return NotFound(new { Error = "Conversation not found" });
            }

            var userId = GetUserId();
            if (conversation.UserId != userId)
            {
                return Forbid();
            }

            await _conversationManager.RecordFeedbackAsync(
                conversationId,
                feedback.Rating,
                feedback.Comment,
                ct);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record feedback for conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to record feedback" });
        }
    }

    /// <summary>
    /// Generates suggested follow-up questions for a conversation.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of suggested follow-up questions</returns>
    [HttpGet("conversations/{conversationId}/suggested-followups")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetSuggestedFollowUps(
        string conversationId,
        CancellationToken ct)
    {
        try
        {
            var followUps = await _chatService.GenerateSuggestedFollowUpsAsync(conversationId, ct);
            return Ok(followUps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate suggested follow-ups for conversation {ConversationId}", conversationId);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { Error = "Failed to generate suggested follow-ups" });
        }
    }

    /// <summary>
    /// Gets the authenticated user's ID from the JWT claims.
    /// </summary>
    /// <returns>User ID</returns>
    private string GetUserId()
    {
        // Check if user is authenticated
        if (User?.Identity?.IsAuthenticated == true)
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? "authenticated";
        }
        
        // Return default user ID for unauthenticated requests (debugging mode)
        return "anonymous";
    }
}

/// <summary>
/// Request for updating a conversation.
/// </summary>
public class ConversationUpdateRequest
{
    public string? Title { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Request for recording feedback on a conversation.
/// </summary>
public class ConversationFeedbackRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
