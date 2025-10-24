using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Chat;
using Castellan.Worker.Services.Chat;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Castellan.Tests.Services.Chat;

public class ContextRetrieverTests
{
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ISecurityEventStore> _mockEventStore;
    private readonly Mock<ICorrelationEngine> _mockCorrelationEngine;
    private readonly Mock<ILogger<ContextRetriever>> _mockLogger;
    private readonly ContextRetriever _retriever;

    public ContextRetrieverTests()
    {
        _mockVectorStore = new Mock<IVectorStore>();
        _mockEventStore = new Mock<ISecurityEventStore>();
        _mockCorrelationEngine = new Mock<ICorrelationEngine>();
        _mockLogger = new Mock<ILogger<ContextRetriever>>();

        _retriever = new ContextRetriever(
            _mockVectorStore.Object,
            _mockEventStore.Object,
            _mockCorrelationEngine.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithBasicIntent_ReturnsContext()
    {
        // Arrange
        var message = "Show me critical events";
        var intent = new ChatIntent
        {
            Type = IntentType.Query,
            Confidence = 0.9f
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(intent, result.Intent);
        Assert.Equal(TimeRange.Last24Hours, result.TimeRange);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithKeywordMatches_ReturnsSimilarEvents()
    {
        // Arrange
        var message = "Show me failed login events";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };

        var mockEvents = new List<SecurityEvent>
        {
            new SecurityEvent
            {
                EventId = 1,
                EventType = EventType.FailedLogon,
                Summary = "Failed login attempt detected",
                RiskLevel = RiskLevel.High,
                OriginalEvent = new LogEvent
                {
                    Time = DateTime.UtcNow,
                    Message = "User login failed"
                }
            },
            new SecurityEvent
            {
                EventId = 2,
                EventType = EventType.ProcessCreation,
                Summary = "Process created",
                RiskLevel = RiskLevel.Low,
                OriginalEvent = new LogEvent
                {
                    Time = DateTime.UtcNow,
                    Message = "notepad.exe started"
                }
            }
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(1, 100, null))
            .Returns(mockEvents);
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent);

        // Assert
        Assert.NotNull(result.SimilarEvents);
        Assert.NotEmpty(result.SimilarEvents);
        Assert.Contains(result.SimilarEvents, e => e.EventType == EventType.FailedLogon);
    }

    [Fact]
    public async Task RetrieveContextAsync_FiltersByTimeRange()
    {
        // Arrange
        var message = "Show me recent events";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };

        var oldEvent = new SecurityEvent
        {
            EventId = 1,
            EventType = EventType.FailedLogon,
            Summary = "Old event",
            RiskLevel = RiskLevel.High,
            OriginalEvent = new LogEvent
            {
                Time = DateTime.UtcNow.AddDays(-2),
                Message = "Old event"
            }
        };

        var recentEvent = new SecurityEvent
        {
            EventId = 2,
            EventType = EventType.FailedLogon,
            Summary = "Recent event",
            RiskLevel = RiskLevel.High,
            OriginalEvent = new LogEvent
            {
                Time = DateTime.UtcNow.AddHours(-1),
                Message = "Recent event"
            }
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(1, 100, null))
            .Returns(new List<SecurityEvent> { oldEvent, recentEvent });
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent);

        // Assert
        Assert.NotNull(result.SimilarEvents);
        Assert.DoesNotContain(result.SimilarEvents, e => e.EventId == 1); // Old event filtered out
    }

    [Fact]
    public async Task RetrieveContextAsync_RetrievesCriticalEvents()
    {
        // Arrange
        var message = "What happened?";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };

        var criticalEvents = new List<SecurityEvent>
        {
            new SecurityEvent
            {
                EventId = 1,
                EventType = EventType.MalwareDetected,
                Summary = "Malware found",
                RiskLevel = RiskLevel.Critical,
                OriginalEvent = new LogEvent
                {
                    Time = DateTime.UtcNow,
                    Message = "Critical threat"
                }
            }
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(1, 100, null))
            .Returns(new List<SecurityEvent>());
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>()))
            .Returns(criticalEvents);

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent);

        // Assert
        Assert.NotNull(result.RecentCriticalEvents);
        Assert.NotEmpty(result.RecentCriticalEvents);
        Assert.All(result.RecentCriticalEvents, e => Assert.True(e.RiskLevel is RiskLevel.Critical or RiskLevel.High));
    }

    [Fact]
    public async Task RetrieveContextAsync_WithIncludeCorrelationPatterns_RetrievesPatterns()
    {
        // Arrange
        var message = "Show me attack patterns";
        var intent = new ChatIntent { Type = IntentType.Hunt, Confidence = 0.95f };
        var options = new ContextOptions { IncludeCorrelationPatterns = true };

        var stats = new CorrelationStatistics
        {
            CorrelationsDetected = 5,
            EventsCorrelated = 15
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());
        _mockCorrelationEngine.Setup(x => x.GetStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(stats);

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent, options);

        // Assert
        Assert.NotNull(result.ActivePatterns);
        Assert.NotEmpty(result.ActivePatterns);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithIncludeSystemMetrics_RetrievesMetrics()
    {
        // Arrange
        var message = "What's the system status?";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };
        var options = new ContextOptions { IncludeSystemMetrics = true };

        var events = new List<SecurityEvent>
        {
            new SecurityEvent
            {
                EventId = 1,
                EventType = EventType.FailedLogon,
                Summary = "Event 1",
                RiskLevel = RiskLevel.High,
                OriginalEvent = new LogEvent { Time = DateTime.UtcNow }
            },
            new SecurityEvent
            {
                EventId = 2,
                EventType = EventType.ProcessCreation,
                Summary = "Event 2",
                RiskLevel = RiskLevel.Critical,
                OriginalEvent = new LogEvent { Time = DateTime.UtcNow }
            }
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(events);
        _mockEventStore.Setup(x => x.GetTotalCount()).Returns(100);
        _mockEventStore.Setup(x => x.GetRiskLevelCounts())
            .Returns(new Dictionary<string, int>
            {
                ["Critical"] = 5,
                ["High"] = 10,
                ["Medium"] = 20,
                ["Low"] = 65
            });

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent, options);

        // Assert
        Assert.NotNull(result.CurrentMetrics);
        Assert.Equal(2, result.CurrentMetrics.TotalEvents24h);
        Assert.Equal(5, result.CurrentMetrics.CriticalEvents);
        Assert.Equal(10, result.CurrentMetrics.HighRiskEvents);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithNoKeywords_ReturnsEmptySimilarEvents()
    {
        // Arrange
        var message = "Hi";  // Too short, no keywords
        var intent = new ChatIntent { Type = IntentType.Conversational, Confidence = 0.95f };

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent);

        // Assert
        Assert.Empty(result.SimilarEvents);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithException_ReturnsPartialContext()
    {
        // Arrange
        var message = "Show me events";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Throws(new Exception("Database error"));

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent);

        // Assert - Should return partial context instead of throwing
        Assert.NotNull(result);
        Assert.Equal(intent, result.Intent);
    }

    [Fact]
    public async Task RetrieveContextAsync_ScoresEventsByKeywordRelevance()
    {
        // Arrange
        var message = "Show me failed login events from security log";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };

        var events = new List<SecurityEvent>
        {
            new SecurityEvent
            {
                EventId = 1,
                EventType = EventType.FailedLogon,  // Matches "failed" and "login"
                Summary = "Failed login attempt",
                RiskLevel = RiskLevel.High,
                OriginalEvent = new LogEvent { Time = DateTime.UtcNow, Message = "Security event" }
            },
            new SecurityEvent
            {
                EventId = 2,
                EventType = EventType.ProcessCreation,  // No keyword match
                Summary = "Process started",
                RiskLevel = RiskLevel.Low,
                OriginalEvent = new LogEvent { Time = DateTime.UtcNow, Message = "System event" }
            },
            new SecurityEvent
            {
                EventId = 3,
                EventType = EventType.SuccessfulLogon,  // Matches "login"
                Summary = "User logged in",
                RiskLevel = RiskLevel.Low,
                OriginalEvent = new LogEvent { Time = DateTime.UtcNow, Message = "Login successful" }
            }
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(1, 100, null))
            .Returns(events);
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());

        var options = new ContextOptions { MaxSimilarEvents = 2 };

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent, options);

        // Assert
        Assert.Equal(2, result.SimilarEvents.Count);
        // FailedLogon should score highest due to matching both EventType and keywords
        Assert.Equal(EventType.FailedLogon, result.SimilarEvents[0].EventType);
    }

    [Fact]
    public async Task RetrieveContextAsync_LimitsResultsByOptions()
    {
        // Arrange
        var message = "Show me events";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };

        var manyEvents = Enumerable.Range(1, 50)
            .Select(i => new SecurityEvent
            {
                EventId = i,
                EventType = EventType.ProcessCreation,
                Summary = $"Event {i}",
                RiskLevel = RiskLevel.Low,
                OriginalEvent = new LogEvent { Time = DateTime.UtcNow, Message = $"Event {i}" }
            })
            .ToList();

        _mockEventStore.Setup(x => x.GetSecurityEvents(1, 100, null))
            .Returns(manyEvents);
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());

        var options = new ContextOptions
        {
            MaxSimilarEvents = 5,
            MaxRecentCriticalEvents = 3
        };

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent, options);

        // Assert
        Assert.True(result.SimilarEvents.Count <= options.MaxSimilarEvents);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithCustomTimeRange_UsesSpecifiedRange()
    {
        // Arrange
        var message = "Show me events";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };
        var customTimeRange = new TimeRange
        {
            Start = DateTime.UtcNow.AddHours(-6),
            End = DateTime.UtcNow
        };
        var options = new ContextOptions { TimeRange = customTimeRange };

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent, options);

        // Assert
        Assert.Equal(customTimeRange, result.TimeRange);
    }

    [Fact]
    public async Task RetrieveContextAsync_RunsParallelRetrieval()
    {
        // Arrange
        var message = "Show me everything";
        var intent = new ChatIntent { Type = IntentType.Query, Confidence = 0.9f };
        var options = new ContextOptions
        {
            IncludeCorrelationPatterns = true,
            IncludeSystemMetrics = true
        };

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>?>()))
            .Returns(new List<SecurityEvent>());
        _mockEventStore.Setup(x => x.GetTotalCount()).Returns(0);
        _mockEventStore.Setup(x => x.GetRiskLevelCounts())
            .Returns(new Dictionary<string, int>());
        _mockCorrelationEngine.Setup(x => x.GetStatisticsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new CorrelationStatistics { CorrelationsDetected = 0 });

        // Act
        var result = await _retriever.RetrieveContextAsync(message, intent, options);

        // Assert - All async operations should complete
        Assert.NotNull(result.SimilarEvents);
        Assert.NotNull(result.RecentCriticalEvents);
        Assert.NotNull(result.ActivePatterns);
        Assert.NotNull(result.CurrentMetrics);
    }
}
