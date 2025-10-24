using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Chat;
using Castellan.Worker.Services.Chat;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Castellan.Tests.Services.Chat;

public class IntentClassifierTests
{
    private readonly Mock<ILlmClient> _mockLlm;
    private readonly Mock<ILogger<IntentClassifier>> _mockLogger;
    private readonly IntentClassifier _classifier;

    public IntentClassifierTests()
    {
        _mockLlm = new Mock<ILlmClient>();
        _mockLogger = new Mock<ILogger<IntentClassifier>>();
        _classifier = new IntentClassifier(_mockLlm.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithQueryIntent_ReturnsQueryIntentType()
    {
        // Arrange
        var message = "How many critical events today?";
        var conversationHistory = new List<ChatMessage>();

        var llmResponse = new
        {
            intentType = "Query",
            confidence = 0.95f,
            entities = new Dictionary<string, string>
            {
                ["timeRange"] = "today",
                ["riskLevel"] = "critical"
            },
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = new Dictionary<string, object>()
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.Equal(IntentType.Query, result.Type);
        Assert.Equal(0.95f, result.Confidence);
        Assert.False(result.RequiresAction);
        Assert.Equal("today", result.Entities["timeRange"]);
        Assert.Equal("critical", result.Entities["riskLevel"]);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithInvestigateIntent_ReturnsInvestigateIntentType()
    {
        // Arrange
        var message = "Show me details about event 4625";
        var conversationHistory = new List<ChatMessage>();

        var llmResponse = new
        {
            intentType = "Investigate",
            confidence = 0.90f,
            entities = new Dictionary<string, string>
            {
                ["eventId"] = "4625"
            },
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = new Dictionary<string, object>()
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.Equal(IntentType.Investigate, result.Type);
        Assert.Equal(0.90f, result.Confidence);
        Assert.Equal("4625", result.Entities["eventId"]);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithActionIntent_ReturnsActionTypeAndSuggestedAction()
    {
        // Arrange
        var message = "Block IP address 192.168.1.100";
        var conversationHistory = new List<ChatMessage>();

        var llmResponse = new
        {
            intentType = "Action",
            confidence = 0.98f,
            entities = new Dictionary<string, string>
            {
                ["ipAddress"] = "192.168.1.100"
            },
            requiresAction = true,
            suggestedAction = "BlockIP",
            actionParameters = new Dictionary<string, object>
            {
                ["ipAddress"] = "192.168.1.100"
            }
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.Equal(IntentType.Action, result.Type);
        Assert.Equal(0.98f, result.Confidence);
        Assert.True(result.RequiresAction);
        Assert.Equal("BlockIP", result.SuggestedAction);
        Assert.Equal("192.168.1.100", result.ActionParameters["ipAddress"]);
    }

    [Theory]
    [InlineData("hunt", IntentType.Hunt)]
    [InlineData("Hunt", IntentType.Hunt)]
    [InlineData("HUNT", IntentType.Hunt)]
    [InlineData("compliance", IntentType.Compliance)]
    [InlineData("explain", IntentType.Explain)]
    [InlineData("conversational", IntentType.Conversational)]
    public async Task ClassifyIntentAsync_WithVariousIntentTypes_ParsesCorrectly(
        string intentTypeString,
        IntentType expectedType)
    {
        // Arrange
        var message = "Test message";
        var conversationHistory = new List<ChatMessage>();

        var llmResponse = new
        {
            intentType = intentTypeString,
            confidence = 0.85f,
            entities = new Dictionary<string, string>(),
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = new Dictionary<string, object>()
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.Equal(expectedType, result.Type);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithInvalidIntentType_DefaultsToConversational()
    {
        // Arrange
        var message = "Test message";
        var conversationHistory = new List<ChatMessage>();

        var llmResponse = new
        {
            intentType = "UnknownIntent",
            confidence = 0.50f,
            entities = new Dictionary<string, string>(),
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = new Dictionary<string, object>()
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.Equal(IntentType.Conversational, result.Type);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithLlmException_ReturnsDefaultConversationalIntent()
    {
        // Arrange
        var message = "Test message";
        var conversationHistory = new List<ChatMessage>();

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM failure"));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.Equal(IntentType.Conversational, result.Type);
        Assert.Equal(0.5f, result.Confidence);
        Assert.False(result.RequiresAction);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithConversationHistory_IncludesContextInPrompt()
    {
        // Arrange
        var message = "What about now?";
        var conversationHistory = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "user",
                Content = "How many critical events today?"
            },
            new ChatMessage
            {
                Role = "assistant",
                Content = "There are 5 critical events today."
            }
        };

        var llmResponse = new
        {
            intentType = "Query",
            confidence = 0.90f,
            entities = new Dictionary<string, string>(),
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = new Dictionary<string, object>()
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Conversation history")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.Equal(IntentType.Query, result.Type);
        _mockLlm.Verify(x => x.GenerateAsync(
            It.IsAny<string>(),
            It.Is<string>(p => p.Contains("user: How many critical events today?")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClassifyIntentAsync_IncludesOnlyLast3MessagesInHistory()
    {
        // Arrange
        var message = "Current question";
        var conversationHistory = new List<ChatMessage>();

        // Add 5 messages to history
        for (int i = 0; i < 5; i++)
        {
            conversationHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = $"Message {i}"
            });
        }

        var llmResponse = new
        {
            intentType = "Query",
            confidence = 0.85f,
            entities = new Dictionary<string, string>(),
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = new Dictionary<string, object>()
        };

        string? capturedPrompt = null;
        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((s, u, c) => capturedPrompt = u)
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.NotNull(capturedPrompt);
        Assert.Contains("Message 2", capturedPrompt);
        Assert.Contains("Message 3", capturedPrompt);
        Assert.Contains("Message 4", capturedPrompt);
        Assert.DoesNotContain("Message 0", capturedPrompt);
        Assert.DoesNotContain("Message 1", capturedPrompt);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithEmptyEntities_ReturnsEmptyDictionary()
    {
        // Arrange
        var message = "Hello";
        var conversationHistory = new List<ChatMessage>();

        var llmResponse = new
        {
            intentType = "Conversational",
            confidence = 0.95f,
            entities = (Dictionary<string, string>?)null,
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = (Dictionary<string, object>?)null
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        Assert.NotNull(result.Entities);
        Assert.Empty(result.Entities);
        Assert.NotNull(result.ActionParameters);
        Assert.Empty(result.ActionParameters);
    }

    [Fact]
    public async Task ClassifyIntentAsync_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var message = "Test message";
        var conversationHistory = new List<ChatMessage>();

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Invalid JSON {{{");

        // Act
        var result = await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert - Should return default conversational intent due to exception handling
        Assert.Equal(IntentType.Conversational, result.Type);
        Assert.Equal(0.5f, result.Confidence);
    }

    [Fact]
    public async Task ClassifyIntentAsync_CallsLlmWithSystemAndUserPrompts()
    {
        // Arrange
        var message = "Test message";
        var conversationHistory = new List<ChatMessage>();

        var llmResponse = new
        {
            intentType = "Query",
            confidence = 0.80f,
            entities = new Dictionary<string, string>(),
            requiresAction = false,
            suggestedAction = (string?)null,
            actionParameters = new Dictionary<string, object>()
        };

        _mockLlm.Setup(x => x.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(llmResponse));

        // Act
        await _classifier.ClassifyIntentAsync(message, conversationHistory);

        // Assert
        _mockLlm.Verify(x => x.GenerateAsync(
            It.Is<string>(s => s.Contains("intent classifier")),
            It.Is<string>(u => u.Contains("Test message")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
