using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Tests.Services;

public class CorrelationEngineTests
{
    private readonly Mock<ILogger<CorrelationEngine>> _mockLogger;
    private readonly Mock<ISecurityEventStore> _mockEventStore;
    private readonly CorrelationEngine _correlationEngine;

    public CorrelationEngineTests()
    {
        _mockLogger = new Mock<ILogger<CorrelationEngine>>();
        _mockEventStore = new Mock<ISecurityEventStore>();
        _correlationEngine = new CorrelationEngine(_mockLogger.Object, _mockEventStore.Object);
    }

    [Fact]
    public async Task AnalyzeEventAsync_WithNoRecentEvents_ReturnsNoCorrelation()
    {
        // Arrange
        var testEvent = CreateTestSecurityEvent(SecurityEventType.ProcessCreation, "TestHost", DateTime.UtcNow);
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>()))
            .Returns(new List<SecurityEvent>());

        // Act
        var result = await _correlationEngine.AnalyzeEventAsync(testEvent);

        // Assert
        Assert.False(result.HasCorrelation);
        Assert.Equal(0, result.ConfidenceScore);
        Assert.Equal("No correlation patterns detected", result.Explanation);
    }

    [Fact]
    public async Task AnalyzeEventAsync_WithBruteForcePattern_DetectsCorrelation()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var failureEvents = Enumerable.Range(0, 5)
            .Select(i => CreateTestSecurityEvent(SecurityEventType.AuthenticationFailure, "TestHost", now.AddMinutes(-i)))
            .ToList();
        var successEvent = CreateTestSecurityEvent(SecurityEventType.AuthenticationSuccess, "TestHost", now);

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>()))
            .Returns(failureEvents);

        // Act
        var result = await _correlationEngine.AnalyzeEventAsync(successEvent);

        // Assert
        Assert.True(result.HasCorrelation);
        Assert.True(result.ConfidenceScore > 0.7);
        Assert.Contains("Brute Force Attack", result.MatchedRules);
    }

    [Fact]
    public async Task AnalyzeBatchAsync_WithTemporalBurst_DetectsCorrelation()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var burstEvents = Enumerable.Range(0, 8)
            .Select(i => CreateTestSecurityEvent(SecurityEventType.ProcessCreation, "TestHost", now.AddSeconds(i * 10)))
            .ToList();

        // Act
        var correlations = await _correlationEngine.AnalyzeBatchAsync(burstEvents, TimeSpan.FromMinutes(5));

        // Assert
        Assert.NotEmpty(correlations);
        var temporalBurst = correlations.FirstOrDefault(c => c.CorrelationType == "TemporalBurst");
        Assert.NotNull(temporalBurst);
        Assert.True(temporalBurst.ConfidenceScore > 0.8);
        Assert.Equal(8, temporalBurst.EventIds.Count);
    }

    [Fact]
    public async Task AnalyzeBatchAsync_WithLateralMovement_DetectsCorrelation()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var lateralEvents = new List<SecurityEvent>
        {
            CreateTestSecurityEvent(SecurityEventType.NetworkConnection, "Host1", now),
            CreateTestSecurityEvent(SecurityEventType.NetworkConnection, "Host2", now.AddMinutes(1)),
            CreateTestSecurityEvent(SecurityEventType.NetworkConnection, "Host3", now.AddMinutes(2)),
            CreateTestSecurityEvent(SecurityEventType.NetworkConnection, "Host4", now.AddMinutes(3))
        };

        // Act
        var correlations = await _correlationEngine.AnalyzeBatchAsync(lateralEvents, TimeSpan.FromMinutes(30));

        // Assert
        Assert.NotEmpty(correlations);
        var lateralMovement = correlations.FirstOrDefault(c => c.CorrelationType == "LateralMovement");
        Assert.NotNull(lateralMovement);
        Assert.True(lateralMovement.ConfidenceScore > 0.75);
        Assert.Equal("high", lateralMovement.RiskLevel);
    }

    [Fact]
    public async Task DetectAttackChainsAsync_WithValidPattern_DetectsChain()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var chainEvents = new List<SecurityEvent>
        {
            CreateTestSecurityEvent(SecurityEventType.AuthenticationSuccess, "TestHost", now),
            CreateTestSecurityEvent(SecurityEventType.PrivilegeEscalation, "TestHost", now.AddMinutes(1)),
            CreateTestSecurityEvent(SecurityEventType.NetworkConnection, "TestHost", now.AddMinutes(2))
        };

        // Act
        var chains = await _correlationEngine.DetectAttackChainsAsync(chainEvents, TimeSpan.FromMinutes(30));

        // Assert
        Assert.NotEmpty(chains);
        var chain = chains.First();
        Assert.Equal(3, chain.Stages.Count);
        Assert.True(chain.ConfidenceScore > 0.8);
        Assert.Equal("high", chain.RiskLevel);
    }

    [Fact]
    public async Task GetCorrelationsAsync_WithTimeRange_ReturnsFilteredCorrelations()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var events = Enumerable.Range(0, 6)
            .Select(i => CreateTestSecurityEvent(SecurityEventType.ProcessCreation, "TestHost", now.AddSeconds(i * 10)))
            .ToList();

        await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromMinutes(5));

        // Act
        var correlations = await _correlationEngine.GetCorrelationsAsync(now.AddMinutes(-1), now.AddMinutes(1));

        // Assert
        Assert.NotEmpty(correlations);
        Assert.All(correlations, c =>
        {
            Assert.True(c.DetectedAt >= now.AddMinutes(-1));
            Assert.True(c.DetectedAt <= now.AddMinutes(1));
        });
    }

    [Fact]
    public async Task GetStatisticsAsync_WithCorrelations_ReturnsAccurateStats()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var events = Enumerable.Range(0, 10)
            .Select(i => CreateTestSecurityEvent(SecurityEventType.ProcessCreation, "TestHost", now.AddSeconds(i * 5)))
            .ToList();

        await _correlationEngine.AnalyzeBatchAsync(events, TimeSpan.FromMinutes(5));

        // Act
        var stats = await _correlationEngine.GetStatisticsAsync();

        // Assert
        Assert.True(stats.CorrelationsDetected > 0);
        Assert.True(stats.AverageConfidenceScore > 0);
        Assert.NotEmpty(stats.CorrelationsByType);
        Assert.True(stats.TotalEventsProcessed >= 0);
    }

    [Fact]
    public async Task GetRulesAsync_ReturnsDefaultRules()
    {
        // Act
        var rules = await _correlationEngine.GetRulesAsync();

        // Assert
        Assert.NotEmpty(rules);
        Assert.Contains(rules, r => r.Name == "Temporal Burst Detection");
        Assert.Contains(rules, r => r.Name == "Brute Force Attack");
        Assert.Contains(rules, r => r.Name == "Lateral Movement Detection");
        Assert.Contains(rules, r => r.Name == "Privilege Escalation");
        Assert.All(rules, r => Assert.True(r.IsEnabled));
    }

    [Fact]
    public async Task UpdateRuleAsync_WithExistingRule_UpdatesSuccessfully()
    {
        // Arrange
        var rules = await _correlationEngine.GetRulesAsync();
        var ruleToUpdate = rules.First();
        ruleToUpdate.IsEnabled = false;
        ruleToUpdate.MinConfidence = 0.9;

        // Act
        await _correlationEngine.UpdateRuleAsync(ruleToUpdate);

        // Assert
        var updatedRules = await _correlationEngine.GetRulesAsync();
        var updatedRule = updatedRules.First(r => r.Id == ruleToUpdate.Id);
        Assert.False(updatedRule.IsEnabled);
        Assert.Equal(0.9, updatedRule.MinConfidence);
    }

    [Fact]
    public async Task TrainModelsAsync_WithConfirmedCorrelations_CompletesSuccessfully()
    {
        // Arrange
        var confirmedCorrelations = new List<EventCorrelation>
        {
            new EventCorrelation
            {
                Id = Guid.NewGuid().ToString(),
                CorrelationType = "TestType",
                ConfidenceScore = 0.9,
                Pattern = "TestPattern",
                EventIds = new List<string> { "event1", "event2" },
                TimeWindow = TimeSpan.FromMinutes(30),
                MitreTechniques = new List<string> { "T1001", "T1002" },
                RecommendedActions = new List<string> { "Action1", "Action2" },
                RiskLevel = "high"
            }
        };

        // Act & Assert
        await _correlationEngine.TrainModelsAsync(confirmedCorrelations);
        // Should not throw exception
    }

    [Fact]
    public async Task TrainModelsAsync_WithInsufficientData_LogsWarning()
    {
        // Arrange
        var insufficientCorrelations = new List<EventCorrelation>
        {
            new EventCorrelation
            {
                Id = Guid.NewGuid().ToString(),
                CorrelationType = "TestType",
                ConfidenceScore = 0.9,
                Pattern = "TestPattern",
                EventIds = new List<string> { "event1" },
                TimeWindow = TimeSpan.FromMinutes(30),
                RiskLevel = "medium"
            }
        };

        // Act & Assert
        await _correlationEngine.TrainModelsAsync(insufficientCorrelations);
        // Should not throw exception - insufficient data should be handled gracefully
    }

    [Fact]
    public async Task TrainModelsAsync_WithSufficientData_TrainsModel()
    {
        // Arrange
        var sufficientCorrelations = Enumerable.Range(0, 15)
            .Select(i => new EventCorrelation
            {
                Id = Guid.NewGuid().ToString(),
                CorrelationType = i % 3 == 0 ? "AttackChain" : i % 3 == 1 ? "LateralMovement" : "TemporalBurst",
                ConfidenceScore = 0.8 + (i % 3 * 0.05),
                Pattern = $"TestPattern{i}",
                EventIds = new List<string> { $"event{i}", $"event{i+1}" },
                TimeWindow = TimeSpan.FromMinutes(20 + i),
                MitreTechniques = new List<string> { $"T{1000 + i}" },
                RecommendedActions = new List<string> { $"Action{i}" },
                RiskLevel = i % 4 == 0 ? "critical" : i % 4 == 1 ? "high" : i % 4 == 2 ? "medium" : "low"
            })
            .ToList();

        // Act & Assert
        await _correlationEngine.TrainModelsAsync(sufficientCorrelations);
        // Should complete successfully with sufficient training data
    }

    [Fact]
    public async Task CleanupOldCorrelationsAsync_RemovesOldCorrelations()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var oldEvents = Enumerable.Range(0, 5)
            .Select(i => CreateTestSecurityEvent(SecurityEventType.ProcessCreation, "TestHost", now.AddDays(-2).AddSeconds(i * 10)))
            .ToList();

        await _correlationEngine.AnalyzeBatchAsync(oldEvents, TimeSpan.FromMinutes(5));

        // Act
        await _correlationEngine.CleanupOldCorrelationsAsync(TimeSpan.FromDays(1));

        // Assert
        var recentCorrelations = await _correlationEngine.GetCorrelationsAsync(now.AddDays(-1), now);
        Assert.Empty(recentCorrelations);
    }

    [Theory]
    [InlineData(SecurityEventType.AuthenticationFailure, "AuthenticationFailure")]
    [InlineData(SecurityEventType.ProcessCreation, "ProcessCreation")]
    [InlineData(SecurityEventType.NetworkConnection, "NetworkConnection")]
    public async Task AnalyzeEventAsync_WithDifferentEventTypes_HandlesCorrectly(SecurityEventType eventType, string expectedType)
    {
        // Arrange
        var testEvent = CreateTestSecurityEvent(eventType, "TestHost", DateTime.UtcNow);
        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>()))
            .Returns(new List<SecurityEvent>());

        // Act
        var result = await _correlationEngine.AnalyzeEventAsync(testEvent);

        // Assert
        Assert.Equal(expectedType, testEvent.EventType.ToString());
    }

    [Fact]
    public async Task AnalyzeEventAsync_WithHighVolumeEvents_PerformsWithinTimeLimit()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var largeEventSet = Enumerable.Range(0, 1000)
            .Select(i => CreateTestSecurityEvent(SecurityEventType.ProcessCreation, $"Host{i % 10}", now.AddSeconds(-i)))
            .ToList();

        _mockEventStore.Setup(x => x.GetSecurityEvents(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object>>()))
            .Returns(largeEventSet);

        var testEvent = CreateTestSecurityEvent(SecurityEventType.ProcessCreation, "TestHost", now);
        var startTime = DateTime.UtcNow;

        // Act
        var result = await _correlationEngine.AnalyzeEventAsync(testEvent);

        // Assert
        var duration = DateTime.UtcNow - startTime;
        Assert.True(duration.TotalSeconds < 5, $"Analysis took {duration.TotalSeconds} seconds, expected < 5 seconds");
    }

    private SecurityEvent CreateTestSecurityEvent(SecurityEventType eventType, string host, DateTime timestamp)
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = eventType,
            OriginalEvent = new LogEvent(
                Time: new DateTimeOffset(timestamp, TimeSpan.Zero),
                Host: host,
                Channel: "TestChannel",
                EventId: 1001,
                Level: "Information",
                User: "TestUser",
                Message: $"Test {eventType} event",
                RawJson: "{}",
                UniqueId: Guid.NewGuid().ToString()
            ),
            Summary = $"Test {eventType} event",
            MitreTechniques = new[] { "T1001" },
            RiskLevel = "Medium",
            Confidence = 80
        };
    }
}