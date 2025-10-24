using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Chat;
using Castellan.Worker.Services.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Castellan.Tests.Services.Actions;

public class ActionRollbackServiceTests : IDisposable
{
    private readonly CastellanDbContext _context;
    private readonly Mock<IDbContextFactory<CastellanDbContext>> _mockContextFactory;
    private readonly Mock<IActionHandler> _mockHandler;
    private readonly Mock<ILogger<ActionRollbackService>> _mockLogger;
    private readonly ActionRollbackOptions _options;
    private readonly ActionRollbackService _service;

    public ActionRollbackServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<CastellanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CastellanDbContext(options);

        // Mock context factory
        _mockContextFactory = new Mock<IDbContextFactory<CastellanDbContext>>();
        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_context);

        // Mock action handler
        _mockHandler = new Mock<IActionHandler>();
        _mockHandler.Setup(h => h.ActionType).Returns(ActionType.BlockIP);

        _mockLogger = new Mock<ILogger<ActionRollbackService>>();

        _options = new ActionRollbackOptions
        {
            MaxPendingActionsPerConversation = 10,
            AutoExpirePendingActions = true,
            PendingActionExpirationHours = 24,
            ActionTypeSettings = new Dictionary<string, ActionTypeSettings>
            {
                ["BlockIP"] = new ActionTypeSettings
                {
                    AllowUndo = true,
                    UndoWindowHours = 72
                }
            }
        };

        var handlers = new List<IActionHandler> { _mockHandler.Object };

        _service = new ActionRollbackService(
            _mockContextFactory.Object,
            handlers,
            Options.Create(_options),
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task SuggestActionAsync_WithValidData_CreatesActionExecution()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var messageId = "test-msg-1";
        var actionData = new { ipAddress = "192.168.1.100" };

        _mockHandler.Setup(h => h.ValidateAsync(It.IsAny<object>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        // Act
        var result = await _service.SuggestActionAsync(
            conversationId,
            messageId,
            ActionType.BlockIP,
            actionData);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(conversationId, result.ConversationId);
        Assert.Equal(messageId, result.ChatMessageId);
        Assert.Equal(ActionType.BlockIP, result.Type);
        Assert.Equal(ActionStatus.Pending, result.Status);
        Assert.NotNull(result.SuggestedAt);
    }

    [Fact]
    public async Task SuggestActionAsync_WithInvalidData_ThrowsArgumentException()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var messageId = "test-msg-1";
        var actionData = new { ipAddress = "invalid-ip" };

        _mockHandler.Setup(h => h.ValidateAsync(It.IsAny<object>()))
            .ReturnsAsync(new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid IP address"
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SuggestActionAsync(
                conversationId,
                messageId,
                ActionType.BlockIP,
                actionData));

        Assert.Contains("Invalid action data", exception.Message);
    }

    [Fact]
    public async Task SuggestActionAsync_WhenMaxPendingActionsReached_ThrowsInvalidOperationException()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var messageId = "test-msg-1";
        var actionData = new { ipAddress = "192.168.1.100" };

        // Create max number of pending actions
        for (int i = 0; i < _options.MaxPendingActionsPerConversation; i++)
        {
            _context.ActionExecutions.Add(new ActionExecution
            {
                ConversationId = conversationId,
                ChatMessageId = $"msg-{i}",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            });
        }
        await _context.SaveChangesAsync();

        _mockHandler.Setup(h => h.ValidateAsync(It.IsAny<object>()))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SuggestActionAsync(
                conversationId,
                messageId,
                ActionType.BlockIP,
                actionData));

        Assert.Contains("Maximum pending actions", exception.Message);
    }

    [Fact]
    public async Task SuggestActionAsync_WithUnknownActionType_ThrowsInvalidOperationException()
    {
        // Arrange
        var conversationId = "test-conv-1";
        var messageId = "test-msg-1";
        var actionData = new { test = "data" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SuggestActionAsync(
                conversationId,
                messageId,
                ActionType.IsolateHost,  // No handler registered
                actionData));
    }

    [Fact]
    public async Task ExecuteActionAsync_WithPendingAction_ExecutesSuccessfully()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{\"ipAddress\":\"192.168.1.100\"}",
            Status = ActionStatus.Pending,
            SuggestedAt = DateTime.UtcNow,
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        _mockHandler.Setup(h => h.CaptureBeforeStateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"before\":\"state\"}");

        _mockHandler.Setup(h => h.ExecuteAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecutionResult
            {
                Success = true,
                Message = "IP blocked successfully",
                AfterState = "{\"after\":\"state\"}",
                Logs = new List<string> { "Firewall rule created" }
            });

        // Act
        var result = await _service.ExecuteActionAsync(action.Id, "admin");

        // Assert
        Assert.Equal(ActionStatus.Executed, result.Status);
        Assert.NotNull(result.ExecutedAt);
        Assert.Equal("admin", result.ExecutedBy);
        Assert.NotNull(result.BeforeState);
        Assert.NotNull(result.AfterState);
    }

    [Fact]
    public async Task ExecuteActionAsync_WithNonPendingAction_ThrowsInvalidOperationException()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{}",
            Status = ActionStatus.Executed,  // Already executed
            SuggestedAt = DateTime.UtcNow,
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ExecuteActionAsync(action.Id, "admin"));

        Assert.Contains("not in Pending status", exception.Message);
    }

    [Fact]
    public async Task ExecuteActionAsync_WithExpiredAction_ThrowsInvalidOperationException()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{}",
            Status = ActionStatus.Pending,
            SuggestedAt = DateTime.UtcNow.AddHours(-(_options.PendingActionExpirationHours + 1)),
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ExecuteActionAsync(action.Id, "admin"));

        Assert.Contains("expired", exception.Message);

        // Verify action status was updated to Expired
        var updatedAction = await _context.ActionExecutions.FindAsync(action.Id);
        Assert.Equal(ActionStatus.Expired, updatedAction!.Status);
    }

    [Fact]
    public async Task ExecuteActionAsync_WhenHandlerFails_MarksActionAsFailed()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{\"ipAddress\":\"192.168.1.100\"}",
            Status = ActionStatus.Pending,
            SuggestedAt = DateTime.UtcNow,
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        _mockHandler.Setup(h => h.CaptureBeforeStateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"before\":\"state\"}");

        _mockHandler.Setup(h => h.ExecuteAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecutionResult
            {
                Success = false,
                Message = "Firewall rule creation failed",
                ErrorDetails = "Permission denied"
            });

        // Act
        var result = await _service.ExecuteActionAsync(action.Id, "admin");

        // Assert
        Assert.Equal(ActionStatus.Failed, result.Status);
        Assert.Null(result.ExecutedAt);
    }

    [Fact]
    public async Task RollbackActionAsync_WithExecutedAction_RollsBackSuccessfully()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{\"ipAddress\":\"192.168.1.100\"}",
            Status = ActionStatus.Executed,
            SuggestedAt = DateTime.UtcNow.AddHours(-1),
            ExecutedAt = DateTime.UtcNow,
            ExecutedBy = "admin",
            BeforeState = "{\"before\":\"state\"}",
            AfterState = "{\"after\":\"state\"}",
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        _mockHandler.Setup(h => h.RollbackAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecutionResult
            {
                Success = true,
                Message = "IP unblocked successfully",
                Logs = new List<string> { "Firewall rule removed" }
            });

        // Act
        var result = await _service.RollbackActionAsync(
            action.Id,
            "admin",
            "False positive");

        // Assert
        Assert.Equal(ActionStatus.RolledBack, result.Status);
        Assert.NotNull(result.RolledBackAt);
        Assert.Equal("admin", result.RolledBackBy);
        Assert.Equal("False positive", result.RollbackReason);
    }

    [Fact]
    public async Task RollbackActionAsync_WithNonExecutedAction_ThrowsInvalidOperationException()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{}",
            Status = ActionStatus.Pending,  // Not executed yet
            SuggestedAt = DateTime.UtcNow,
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RollbackActionAsync(action.Id, "admin", "Test reason"));

        Assert.Contains("not in Executed status", exception.Message);
    }

    [Fact]
    public async Task RollbackActionAsync_OutsideUndoWindow_ThrowsInvalidOperationException()
    {
        // Arrange
        var undoWindowHours = _options.ActionTypeSettings["BlockIP"].UndoWindowHours;

        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{}",
            Status = ActionStatus.Executed,
            SuggestedAt = DateTime.UtcNow.AddHours(-(undoWindowHours + 1)),
            ExecutedAt = DateTime.UtcNow.AddHours(-(undoWindowHours + 1)),
            ExecutedBy = "admin",
            BeforeState = "{}",
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RollbackActionAsync(action.Id, "admin", "Test reason"));

        Assert.Contains("outside the undo window", exception.Message);
    }

    [Fact]
    public async Task GetPendingActionsAsync_ReturnsOnlyPendingActions()
    {
        // Arrange
        var conversationId = "test-conv-1";

        _context.ActionExecutions.AddRange(
            new ActionExecution
            {
                ConversationId = conversationId,
                ChatMessageId = "msg-1",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            },
            new ActionExecution
            {
                ConversationId = conversationId,
                ChatMessageId = "msg-2",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Executed,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            },
            new ActionExecution
            {
                ConversationId = conversationId,
                ChatMessageId = "msg-3",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetPendingActionsAsync(conversationId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(ActionStatus.Pending, a.Status));
    }

    [Fact]
    public async Task GetActionHistoryAsync_ReturnsAllActionsForConversation()
    {
        // Arrange
        var conversationId = "test-conv-1";

        _context.ActionExecutions.AddRange(
            new ActionExecution
            {
                ConversationId = conversationId,
                ChatMessageId = "msg-1",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            },
            new ActionExecution
            {
                ConversationId = conversationId,
                ChatMessageId = "msg-2",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Executed,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            },
            new ActionExecution
            {
                ConversationId = "other-conv",
                ChatMessageId = "msg-3",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActionHistoryAsync(conversationId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(conversationId, a.ConversationId));
    }

    [Fact]
    public async Task CanRollbackAsync_WithEligibleAction_ReturnsTrue()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{}",
            Status = ActionStatus.Executed,
            SuggestedAt = DateTime.UtcNow.AddHours(-1),
            ExecutedAt = DateTime.UtcNow,
            ExecutedBy = "admin",
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CanRollbackAsync(action.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanRollbackAsync_WithNonExecutedAction_ReturnsFalse()
    {
        // Arrange
        var action = new ActionExecution
        {
            ConversationId = "test-conv-1",
            ChatMessageId = "test-msg-1",
            Type = ActionType.BlockIP,
            ActionData = "{}",
            Status = ActionStatus.Pending,
            SuggestedAt = DateTime.UtcNow,
            ExecutionLog = "[]"
        };

        _context.ActionExecutions.Add(action);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CanRollbackAsync(action.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetActionStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        _context.ActionExecutions.AddRange(
            new ActionExecution
            {
                ConversationId = "conv-1",
                ChatMessageId = "msg-1",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Pending,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            },
            new ActionExecution
            {
                ConversationId = "conv-1",
                ChatMessageId = "msg-2",
                Type = ActionType.BlockIP,
                ActionData = "{}",
                Status = ActionStatus.Executed,
                SuggestedAt = DateTime.UtcNow.AddMinutes(-10),
                ExecutedAt = DateTime.UtcNow,
                ExecutedBy = "admin",
                ExecutionLog = "[]"
            },
            new ActionExecution
            {
                ConversationId = "conv-1",
                ChatMessageId = "msg-3",
                Type = ActionType.IsolateHost,
                ActionData = "{}",
                Status = ActionStatus.Failed,
                SuggestedAt = DateTime.UtcNow,
                ExecutionLog = "[]"
            }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActionStatisticsAsync();

        // Assert
        Assert.Equal(3, result.TotalSuggested);
        Assert.Equal(1, result.TotalExecuted);
        Assert.Equal(0, result.TotalRolledBack);
        Assert.Equal(1, result.TotalFailed);
        Assert.Equal(1, result.PendingActions);
        Assert.Equal(ActionType.BlockIP, result.MostCommonAction);
        Assert.True(result.AverageExecutionDelayMinutes > 0);
    }

    [Fact]
    public void GetUndoWindow_ReturnsCorrectTimeSpan()
    {
        // Act
        var result = _service.GetUndoWindow(ActionType.BlockIP);

        // Assert
        Assert.Equal(TimeSpan.FromHours(72), result);
    }
}
