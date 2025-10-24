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

public class ActionsControllerTests
{
    private readonly Mock<IActionRollbackService> _mockActionService;
    private readonly Mock<ILogger<ActionsController>> _mockLogger;
    private readonly ActionsController _controller;
    private readonly string _userId = "test-user-123";

    public ActionsControllerTests()
    {
        _mockActionService = new Mock<IActionRollbackService>();
        _mockLogger = new Mock<ILogger<ActionsController>>();

        _controller = new ActionsController(
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
    public async Task SuggestAction_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new SuggestActionRequest
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = new { ipAddress = "192.168.1.100" }
        };

        var actionExecution = new ActionExecution
        {
            Id = 1,
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            Status = ActionStatus.Pending,
            SuggestedAt = DateTime.UtcNow
        };

        _mockActionService.Setup(x => x.SuggestActionAsync(
                request.ConversationId,
                request.ChatMessageId,
                request.Type,
                request.ActionData,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionExecution);

        // Act
        var result = await _controller.SuggestAction(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedAction = Assert.IsType<ActionExecution>(okResult.Value);
        Assert.Equal(1, returnedAction.Id);
        Assert.Equal(ActionStatus.Pending, returnedAction.Status);
    }

    [Fact]
    public async Task SuggestAction_WithMissingConversationId_ReturnsBadRequest()
    {
        // Arrange
        var request = new SuggestActionRequest
        {
            ConversationId = "",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = new { }
        };

        // Act
        var result = await _controller.SuggestAction(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        _mockActionService.Verify(x => x.SuggestActionAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<ActionType>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestAction_WithMissingChatMessageId_ReturnsBadRequest()
    {
        // Arrange
        var request = new SuggestActionRequest
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "",
            Type = ActionType.BlockIP,
            ActionData = new { }
        };

        // Act
        var result = await _controller.SuggestAction(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ExecuteAction_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var actionId = 1;
        var executedAction = new ActionExecution
        {
            Id = actionId,
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            Status = ActionStatus.Executed,
            ExecutedAt = DateTime.UtcNow,
            ExecutedBy = _userId
        };

        _mockActionService.Setup(x => x.ExecuteActionAsync(actionId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(executedAction);

        // Act
        var result = await _controller.ExecuteAction(actionId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedAction = Assert.IsType<ActionExecution>(okResult.Value);
        Assert.Equal(ActionStatus.Executed, returnedAction.Status);
        Assert.Equal(_userId, returnedAction.ExecutedBy);
    }

    [Fact]
    public async Task ExecuteAction_WithInvalidState_ReturnsBadRequest()
    {
        // Arrange
        var actionId = 1;

        _mockActionService.Setup(x => x.ExecuteActionAsync(actionId, _userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Action already executed"));

        // Act
        var result = await _controller.ExecuteAction(actionId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ExecuteAction_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var actionId = 999;

        _mockActionService.Setup(x => x.ExecuteActionAsync(actionId, _userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.ExecuteAction(actionId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task RollbackAction_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var actionId = 1;
        var rollbackRequest = new RollbackActionRequest
        {
            Reason = "False positive detection"
        };

        var rolledBackAction = new ActionExecution
        {
            Id = actionId,
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            Status = ActionStatus.RolledBack,
            ExecutedAt = DateTime.UtcNow.AddMinutes(-5),
            RolledBackAt = DateTime.UtcNow,
            RolledBackBy = _userId,
            RollbackReason = "False positive detection"
        };

        _mockActionService.Setup(x => x.RollbackActionAsync(
                actionId,
                _userId,
                "False positive detection",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rolledBackAction);

        // Act
        var result = await _controller.RollbackAction(actionId, rollbackRequest, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedAction = Assert.IsType<ActionExecution>(okResult.Value);
        Assert.Equal(ActionStatus.RolledBack, returnedAction.Status);
        Assert.Equal("False positive detection", returnedAction.RollbackReason);
    }

    [Fact]
    public async Task RollbackAction_WithoutReason_UsesDefaultReason()
    {
        // Arrange
        var actionId = 1;
        var rollbackRequest = new RollbackActionRequest
        {
            Reason = null
        };

        var rolledBackAction = new ActionExecution
        {
            Id = actionId,
            Status = ActionStatus.RolledBack
        };

        _mockActionService.Setup(x => x.RollbackActionAsync(
                actionId,
                _userId,
                "User requested rollback",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rolledBackAction);

        // Act
        var result = await _controller.RollbackAction(actionId, rollbackRequest, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        _mockActionService.Verify(x => x.RollbackActionAsync(
            actionId,
            _userId,
            "User requested rollback",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RollbackAction_WithExpiredUndoWindow_ReturnsBadRequest()
    {
        // Arrange
        var actionId = 1;
        var rollbackRequest = new RollbackActionRequest
        {
            Reason = "Test rollback"
        };

        _mockActionService.Setup(x => x.RollbackActionAsync(
                actionId,
                _userId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Undo window expired"));

        // Act
        var result = await _controller.RollbackAction(actionId, rollbackRequest, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPendingActions_WithValidConversationId_ReturnsActions()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var pendingActions = new List<ActionExecution>
        {
            new ActionExecution
            {
                Id = 1,
                ConversationId = conversationId,
                Type = ActionType.BlockIP,
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow
            },
            new ActionExecution
            {
                Id = 2,
                ConversationId = conversationId,
                Type = ActionType.IsolateHost,
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow
            }
        };

        _mockActionService.Setup(x => x.GetPendingActionsAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingActions);

        // Act
        var result = await _controller.GetPendingActions(conversationId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedActions = Assert.IsType<List<ActionExecution>>(okResult.Value);
        Assert.Equal(2, returnedActions.Count);
        Assert.All(returnedActions, a => Assert.Equal(ActionStatus.Pending, a.Status));
    }

    [Fact]
    public async Task GetPendingActions_WithMissingConversationId_ReturnsBadRequest()
    {
        // Arrange
        var conversationId = "";

        // Act
        var result = await _controller.GetPendingActions(conversationId, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetActionHistory_WithValidConversationId_ReturnsAllActions()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var actionHistory = new List<ActionExecution>
        {
            new ActionExecution
            {
                Id = 1,
                ConversationId = conversationId,
                Type = ActionType.BlockIP,
                Status = ActionStatus.Executed,
                ExecutedAt = DateTime.UtcNow.AddMinutes(-30)
            },
            new ActionExecution
            {
                Id = 2,
                ConversationId = conversationId,
                Type = ActionType.IsolateHost,
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow.AddMinutes(-15)
            },
            new ActionExecution
            {
                Id = 3,
                ConversationId = conversationId,
                Type = ActionType.QuarantineFile,
                Status = ActionStatus.RolledBack,
                RolledBackAt = DateTime.UtcNow
            }
        };

        _mockActionService.Setup(x => x.GetActionHistoryAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionHistory);

        // Act
        var result = await _controller.GetActionHistory(conversationId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedActions = Assert.IsType<List<ActionExecution>>(okResult.Value);
        Assert.Equal(3, returnedActions.Count);
    }

    [Fact]
    public async Task GetAction_WithValidId_ReturnsAction()
    {
        // Arrange
        var actionId = 1;
        var action = new ActionExecution
        {
            Id = actionId,
            ConversationId = "test-conv-1",
            Type = ActionType.BlockIP,
            Status = ActionStatus.Executed
        };

        _mockActionService.Setup(x => x.GetActionByIdAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(action);

        // Act
        var result = await _controller.GetAction(actionId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedAction = Assert.IsType<ActionExecution>(okResult.Value);
        Assert.Equal(actionId, returnedAction.Id);
    }

    [Fact]
    public async Task GetAction_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var actionId = 999;

        _mockActionService.Setup(x => x.GetActionByIdAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionExecution?)null);

        // Act
        var result = await _controller.GetAction(actionId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CanRollback_WithRollbackEligibleAction_ReturnsTrue()
    {
        // Arrange
        var actionId = 1;
        var action = new ActionExecution
        {
            Id = actionId,
            Type = ActionType.BlockIP,
            Status = ActionStatus.Executed,
            ExecutedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        _mockActionService.Setup(x => x.CanRollbackAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockActionService.Setup(x => x.GetActionByIdAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(action);

        _mockActionService.Setup(x => x.GetUndoWindow(ActionType.BlockIP))
            .Returns(TimeSpan.FromHours(1));

        // Act
        var result = await _controller.CanRollback(actionId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CanRollbackResponse>(okResult.Value);
        Assert.True(response.CanRollback);
        Assert.Equal("Action can be rolled back", response.Reason);
    }

    [Fact]
    public async Task CanRollback_WithExpiredUndoWindow_ReturnsFalse()
    {
        // Arrange
        var actionId = 1;
        var action = new ActionExecution
        {
            Id = actionId,
            Type = ActionType.BlockIP,
            Status = ActionStatus.Executed,
            ExecutedAt = DateTime.UtcNow.AddHours(-2)
        };

        _mockActionService.Setup(x => x.CanRollbackAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockActionService.Setup(x => x.GetActionByIdAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(action);

        _mockActionService.Setup(x => x.GetUndoWindow(ActionType.BlockIP))
            .Returns(TimeSpan.FromHours(1));

        // Act
        var result = await _controller.CanRollback(actionId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CanRollbackResponse>(okResult.Value);
        Assert.False(response.CanRollback);
        Assert.Contains("Undo window expired", response.Reason);
    }

    [Fact]
    public async Task CanRollback_WithNonExistentAction_ReturnsNotFound()
    {
        // Arrange
        var actionId = 999;

        _mockActionService.Setup(x => x.GetActionByIdAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActionExecution?)null);

        // Act
        var result = await _controller.CanRollback(actionId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetStatistics_ReturnsStatistics()
    {
        // Arrange
        var statistics = new ActionStatistics
        {
            TotalActions = 100,
            PendingActions = 5,
            ExecutedActions = 80,
            RolledBackActions = 10,
            ExpiredActions = 5,
            SuccessRate = 0.95f,
            ActionsByType = new Dictionary<ActionType, int>
            {
                [ActionType.BlockIP] = 40,
                [ActionType.IsolateHost] = 30,
                [ActionType.QuarantineFile] = 20,
                [ActionType.AddToWatchlist] = 5,
                [ActionType.CreateTicket] = 5
            },
            Last24Hours = new ActionStatistics
            {
                TotalActions = 20,
                ExecutedActions = 15,
                RolledBackActions = 2
            }
        };

        _mockActionService.Setup(x => x.GetActionStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);

        // Act
        var result = await _controller.GetStatistics(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedStats = Assert.IsType<ActionStatistics>(okResult.Value);
        Assert.Equal(100, returnedStats.TotalActions);
        Assert.Equal(5, returnedStats.PendingActions);
        Assert.Equal(0.95f, returnedStats.SuccessRate);
    }

    [Fact]
    public async Task SuggestAction_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        var request = new SuggestActionRequest
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = new { }
        };

        _mockActionService.Setup(x => x.SuggestActionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ActionType>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.SuggestAction(request, CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetPendingActions_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        var conversationId = "test-conv-1";

        _mockActionService.Setup(x => x.GetPendingActionsAsync(conversationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetPendingActions(conversationId, CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusCodeResult.StatusCode);
    }
}
