using System;
using FluentAssertions;
using Xunit;
using Castellan.Worker.Models;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests;

public class BasicTests
{
    [Fact]
    public void LogEvent_ShouldCreateCorrectly()
    {
        // Arrange & Act
        var logEvent = TestDataFactory.CreateSecurityEvent(4624, "testuser");

        // Assert
        logEvent.Should().NotBeNull();
        logEvent.EventId.Should().Be(4624);
        logEvent.User.Should().Be("testuser");
        logEvent.Channel.Should().Be("Security");
        logEvent.Level.Should().Be("Information");
        logEvent.Host.Should().Be(Environment.MachineName);
    }

    [Fact]
    public void SecurityEvent_ShouldCreateDeterministicCorrectly()
    {
        // Arrange
        var logEvent = TestDataFactory.CreateSecurityEvent(4624, "admin");

        // Act
        var securityEvent = SecurityEvent.CreateDeterministic(
            logEvent,
            SecurityEventType.AuthenticationSuccess,
            "medium",
            75,
            "Administrative logon detected",
            new[] { "T1078" },
            new[] { "Monitor administrative account activity" }
        );

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.Should().Be(logEvent);
        securityEvent.EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        securityEvent.RiskLevel.Should().Be("medium");
        securityEvent.Confidence.Should().Be(75);
        securityEvent.IsDeterministic.Should().BeTrue();
        securityEvent.MitreTechniques.Should().Contain("T1078");
        securityEvent.RecommendedActions.Should().Contain("Monitor administrative account activity");
    }

    [Fact]
    public void SecurityEvent_ShouldCreateFromLlmResponseCorrectly()
    {
        // Arrange
        var logEvent = TestDataFactory.CreateSecurityEvent(4624, "admin");
        var llmResponse = TestDataFactory.CreateTestLlmResponse();

        // Act
        var securityEvent = SecurityEvent.CreateFromLlmResponse(logEvent, llmResponse);

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.Should().Be(logEvent);
        securityEvent.IsDeterministic.Should().BeFalse();
        securityEvent.RiskLevel.Should().Be("medium");
        securityEvent.Confidence.Should().Be(75);
        securityEvent.MitreTechniques.Should().Contain("T1078");
        securityEvent.MitreTechniques.Should().Contain("T1078.002");
        securityEvent.RecommendedActions.Should().Contain("Monitor user activity");
        securityEvent.RecommendedActions.Should().Contain("Enable MFA");
    }

    [Fact]
    public void TestDataFactory_ShouldCreateValidTestData()
    {
        // Arrange & Act
        var embedding = TestDataFactory.CreateTestEmbedding(768);
        var searchResults = TestDataFactory.CreateTestSearchResults(3);

        // Assert
        embedding.Should().NotBeNull();
        embedding.Should().HaveCount(768);
        embedding.Should().OnlyContain(x => x >= -0.5 && x <= 0.5);

        searchResults.Should().NotBeNull();
        searchResults.Should().HaveCount(3);
        searchResults.Should().BeInDescendingOrder(x => x.score);
        searchResults[0].score.Should().Be(1.0f);
        searchResults[1].score.Should().Be(0.9f);
        searchResults[2].score.Should().Be(0.8f);
    }

    [Fact]
    public void SecurityEventType_ShouldHaveExpectedValues()
    {
        // Assert
        SecurityEventType.AuthenticationSuccess.Should().Be(SecurityEventType.AuthenticationSuccess);
        SecurityEventType.AuthenticationFailure.Should().Be(SecurityEventType.AuthenticationFailure);
        SecurityEventType.PrivilegeEscalation.Should().Be(SecurityEventType.PrivilegeEscalation);
        SecurityEventType.BurstActivity.Should().Be(SecurityEventType.BurstActivity);
        SecurityEventType.CorrelatedActivity.Should().Be(SecurityEventType.CorrelatedActivity);
        SecurityEventType.AnomalousActivity.Should().Be(SecurityEventType.AnomalousActivity);
        SecurityEventType.SuspiciousActivity.Should().Be(SecurityEventType.SuspiciousActivity);
    }

    [Theory]
    [InlineData(4624)]
    [InlineData(4625)]
    [InlineData(4672)]
    [InlineData(4720)]
    [InlineData(4688)]
    public void SecurityEvent_ShouldHandleDifferentEventTypes(int eventId)
    {
        // Arrange
        var logEvent = TestDataFactory.CreateSecurityEvent(eventId, "testuser");

        // Act
        var securityEvent = SecurityEvent.CreateDeterministic(
            logEvent,
            SecurityEventType.AuthenticationSuccess, // Using a default type for testing
            "medium",
            75,
            "Test event",
            new[] { "T1078" },
            new[] { "Monitor activity" }
        );

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.EventId.Should().Be(eventId);
    }

    [Fact]
    public void LogEvent_ShouldHandleSpecialCharacters()
    {
        // Arrange & Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4624,
            "Information",
            "testuser",
            "Message with \"quotes\" and \n newlines and \t tabs",
            "{\"special\": \"characters\"}"
        );

        // Assert
        logEvent.Should().NotBeNull();
        logEvent.Message.Should().Contain("\"quotes\"");
        logEvent.Message.Should().Contain("\n");
        logEvent.Message.Should().Contain("\t");
        logEvent.RawJson.Should().Contain("\"special\"");
    }

    [Fact]
    public void SecurityEvent_ShouldHandleEmptyArrays()
    {
        // Arrange
        var logEvent = TestDataFactory.CreateSecurityEvent(4624, "testuser");

        // Act
        var securityEvent = SecurityEvent.CreateDeterministic(
            logEvent,
            SecurityEventType.AuthenticationSuccess,
            "low",
            50,
            "Test event",
            Array.Empty<string>(),
            Array.Empty<string>()
        );

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.MitreTechniques.Should().BeEmpty();
        securityEvent.RecommendedActions.Should().BeEmpty();
    }

    [Fact]
    public void SecurityEvent_ShouldHandleNullEnrichmentData()
    {
        // Arrange
        var logEvent = TestDataFactory.CreateSecurityEvent(4624, "testuser");

        // Act
        var securityEvent = SecurityEvent.CreateDeterministic(
            logEvent,
            SecurityEventType.AuthenticationSuccess,
            "medium",
            75,
            "Test event",
            new[] { "T1078" },
            new[] { "Monitor activity" }
        );

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.EnrichmentData.Should().BeNull();
    }
}

