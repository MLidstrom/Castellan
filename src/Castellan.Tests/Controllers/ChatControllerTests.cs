using Castellan.Worker.Abstractions;
using Castellan.Worker.Controllers;
using Castellan.Worker.Models.Chat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Castellan.Tests.Controllers;

public class ChatControllerTests
{
    private readonly Mock<IChatService> _mockChatService;
    private readonly Mock<IConversationManager> _mockConversationManager;
    private readonly Mock<IActionRollbackService> _mockActionService;
    private readonly Mock<ILogger<ChatController>> _mockLogger;
    private readonly ChatController _controller;
    private readonly string _userId = "test-user-123";

    public ChatControllerTests()
    {
        _mockChatService = new Mock<IChatService>();
        _mockConversationManager = new Mock<IConversationManager>();
        _mockActionService = new Mock<IActionRollbackService>();
        _mockLogger = new Mock<ILogger<ChatController>>();

        _controller = new ChatController(
            _mockChatService.Object,
            _mockConversationManager.Object,
            _mockActionService.Object,
            _mockLogger.Object);

        // Set up authenticated user context
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task SendMessage_WithValidMessage_ReturnsOkResult()
    {
        // Arrange
        var request = new ChatRequest
        {
            ConversationId = "test-conv-1",
            Message = "What are the recent security events?"
        };

        var response = new ChatResponse
        {
            Success = true,
            ConversationId = "test-conv-1",
            Message = new ChatMessage
            {
                Id = "msg-1",
                Role = "assistant",
                Content = "Here are the recent security events...",
                Timestamp = DateTime.UtcNow
            }
        };

        _mockChatService.Setup(x => x.ProcessMessageAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.True(returnedResponse.Success);
        Assert.Equal("test-conv-1", returnedResponse.ConversationId);
    }

    [Fact]
    public async Task SendMessage_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new ChatRequest
        {
            ConversationId = "test-conv-1",
            Message = ""
        };

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockChatService.Verify(x => x.ProcessMessageAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessage_WithSuggestedActions_PersistsActions()
    {
        // Arrange
        var request = new ChatRequest
        {
            ConversationId = "test-conv-1",
            Message = "Block suspicious IP 192.168.1.100"
        };

        var response = new ChatResponse
        {
            Success = true,
            ConversationId = "test-conv-1",
            Message = new ChatMessage
            {
                Id = "msg-1",
                Role = "assistant",
                Content = "I'll help you block that IP address.",
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Type = "block_ip",
                        Title = "Block IP",
                        Description = "Block 192.168.1.100",
                        Parameters = new Dictionary<string, object>
                        {
                            ["ipAddress"] = "192.168.1.100"
                        }
                    }
                }
            }
        };

        _mockChatService.Setup(x => x.ProcessMessageAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _mockActionService.Setup(x => x.SuggestActionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ActionType>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecution
            {
                Id = 1,
                ConversationId = "test-conv-1",
                ChatMessageId = "msg-1",
                Type = ActionType.BlockIP,
                Status = ActionStatus.Pending
            });

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.NotEmpty(returnedResponse.Message!.SuggestedActions!);
        Assert.Equal(1, returnedResponse.Message.SuggestedActions.First().ExecutionId);

        _mockActionService.Verify(x => x.SuggestActionAsync(
            "test-conv-1",
            "msg-1",
            ActionType.BlockIP,
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_WhenChatServiceFails_Returns500()
    {
        // Arrange
        var request = new ChatRequest
        {
            ConversationId = "test-conv-1",
            Message = "Test message"
        };

        var response = new ChatResponse
        {
            Success = false,
            Error = "LLM service unavailable"
        };

        _mockChatService.Setup(x => x.ProcessMessageAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetConversations_ReturnsUserConversations()
    {
        // Arrange
        var conversations = new List<Conversation>
        {
            new Conversation
            {
                Id = "conv-1",
                UserId = _userId,
                Title = "Security Analysis",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Conversation
            {
                Id = "conv-2",
                UserId = _userId,
                Title = "Threat Investigation",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockConversationManager.Setup(x => x.GetConversationsAsync(_userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversations);

        // Act
        var result = await _controller.GetConversations(includeArchived: false);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedConversations = Assert.IsType<List<Conversation>>(okResult.Value);
        Assert.Equal(2, returnedConversations.Count);
    }

    [Fact]
    public async Task GetConversation_WithValidId_ReturnsConversation()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var conversation = new Conversation
        {
            Id = conversationId,
            UserId = _userId,
            Title = "Security Analysis",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockConversationManager.Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        // Act
        var result = await _controller.GetConversation(conversationId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedConversation = Assert.IsType<Conversation>(okResult.Value);
        Assert.Equal(conversationId, returnedConversation.Id);
        Assert.Equal(_userId, returnedConversation.UserId);
    }

    [Fact]
    public async Task GetConversation_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var conversationId = "non-existent";

        _mockConversationManager.Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        var result = await _controller.GetConversation(conversationId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetConversation_WithUnauthorizedUser_ReturnsForbid()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var conversation = new Conversation
        {
            Id = conversationId,
            UserId = "different-user",
            Title = "Security Analysis",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockConversationManager.Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        // Act
        var result = await _controller.GetConversation(conversationId, CancellationToken.None);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task CreateConversation_ReturnsCreatedConversation()
    {
        // Arrange
        var conversation = new Conversation
        {
            Id = "new-conv-1",
            UserId = _userId,
            Title = "New Conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockConversationManager.Setup(x => x.CreateConversationAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        // Act
        var result = await _controller.CreateConversation(CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedConversation = Assert.IsType<Conversation>(createdResult.Value);
        Assert.Equal("new-conv-1", returnedConversation.Id);
        Assert.Equal(_userId, returnedConversation.UserId);
    }

    [Fact]
    public async Task UpdateConversation_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var conversation = new Conversation
        {
            Id = conversationId,
            UserId = _userId,
            Title = "Old Title",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updateRequest = new ConversationUpdateRequest
        {
            Title = "New Title",
            Tags = new List<string> { "security", "analysis" }
        };

        _mockConversationManager.Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockConversationManager.Setup(x => x.UpdateConversationAsync(
                conversationId,
                updateRequest.Title,
                updateRequest.Tags,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.UpdateConversation(conversationId, updateRequest, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _mockConversationManager.Verify(x => x.UpdateConversationAsync(
            conversationId,
            "New Title",
            updateRequest.Tags,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveConversation_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var conversation = new Conversation
        {
            Id = conversationId,
            UserId = _userId,
            Title = "Test Conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockConversationManager.Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockConversationManager.Setup(x => x.ArchiveConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ArchiveConversation(conversationId, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _mockConversationManager.Verify(x => x.ArchiveConversationAsync(conversationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteConversation_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var conversation = new Conversation
        {
            Id = conversationId,
            UserId = _userId,
            Title = "Test Conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockConversationManager.Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockConversationManager.Setup(x => x.DeleteConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteConversation(conversationId, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _mockConversationManager.Verify(x => x.DeleteConversationAsync(conversationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordFeedback_WithValidRating_ReturnsNoContent()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var conversation = new Conversation
        {
            Id = conversationId,
            UserId = _userId,
            Title = "Test Conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var feedbackRequest = new ConversationFeedbackRequest
        {
            Rating = 4,
            Comment = "Very helpful analysis"
        };

        _mockConversationManager.Setup(x => x.GetConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockConversationManager.Setup(x => x.RecordFeedbackAsync(
                conversationId,
                feedbackRequest.Rating,
                feedbackRequest.Comment,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RecordFeedback(conversationId, feedbackRequest, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        _mockConversationManager.Verify(x => x.RecordFeedbackAsync(
            conversationId,
            4,
            "Very helpful analysis",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task RecordFeedback_WithInvalidRating_ReturnsBadRequest(int invalidRating)
    {
        // Arrange
        var conversationId = "test-conv-1";
        var feedbackRequest = new ConversationFeedbackRequest
        {
            Rating = invalidRating,
            Comment = "Test comment"
        };

        // Act
        var result = await _controller.RecordFeedback(conversationId, feedbackRequest, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockConversationManager.Verify(x => x.RecordFeedbackAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSuggestedFollowUps_ReturnsFollowUpQuestions()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var followUps = new List<string>
        {
            "What MITRE techniques were involved?",
            "Show me related security events",
            "What actions do you recommend?"
        };

        _mockChatService.Setup(x => x.GenerateSuggestedFollowUpsAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(followUps);

        // Act
        var result = await _controller.GetSuggestedFollowUps(conversationId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedFollowUps = Assert.IsType<List<string>>(okResult.Value);
        Assert.Equal(3, returnedFollowUps.Count);
        Assert.Contains("What MITRE techniques were involved?", returnedFollowUps);
    }

    [Fact]
    public async Task SendMessage_WhenActionPersistenceFails_StillReturnsResponse()
    {
        // Arrange
        var request = new ChatRequest
        {
            ConversationId = "test-conv-1",
            Message = "Block suspicious IP"
        };

        var response = new ChatResponse
        {
            Success = true,
            ConversationId = "test-conv-1",
            Message = new ChatMessage
            {
                Id = "msg-1",
                Role = "assistant",
                Content = "I'll help you block that IP.",
                SuggestedActions = new List<SuggestedAction>
                {
                    new SuggestedAction
                    {
                        Type = "block_ip",
                        Title = "Block IP",
                        Parameters = new Dictionary<string, object>()
                    }
                }
            }
        };

        _mockChatService.Setup(x => x.ProcessMessageAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        _mockActionService.Setup(x => x.SuggestActionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ActionType>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert - Should still return OK, action persistence failure is logged but not critical
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<ChatResponse>(okResult.Value);
        Assert.True(returnedResponse.Success);
    }
}
