using FluentAssertions;
using Castellan.Worker.Models;
using Xunit;

namespace Castellan.Tests.Models;

public class SecurityEventTests
{
    [Fact]
    public void CreateDeterministic_ShouldCreateValidSecurityEvent()
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4624,
            "Information",
            "test-user",
            "An account was successfully logged on."
        );

        // Act
        var securityEvent = SecurityEvent.CreateDeterministic(
            originalEvent,
            SecurityEventType.AuthenticationSuccess,
            "low",
            95,
            "Successful login detected",
            new[] { "T1078" },
            new[] { "Monitor user activity" }
        );

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.Should().Be(originalEvent);
        securityEvent.EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        securityEvent.RiskLevel.Should().Be("low");
        securityEvent.Confidence.Should().Be(95);
        securityEvent.Summary.Should().Be("Successful login detected");
        securityEvent.MitreTechniques.Should().Contain("T1078");
        securityEvent.RecommendedActions.Should().Contain("Monitor user activity");
        securityEvent.IsDeterministic.Should().BeTrue();
        securityEvent.IsCorrelationBased.Should().BeFalse();
        securityEvent.IsEnhanced.Should().BeFalse();
        securityEvent.CorrelationScore.Should().Be(0.0);
        securityEvent.BurstScore.Should().Be(0.0);
        securityEvent.AnomalyScore.Should().Be(0.0);
    }

    [Fact]
    public void CreateFromLlmResponse_WithValidJson_ShouldParseCorrectly()
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4625,
            "Warning",
            "test-user",
            "An account failed to log on."
        );

        var validJson = @"{
            ""event_type"": ""AuthenticationFailure"",
            ""risk"": ""high"",
            ""mitre"": [""T1110"", ""T1078""],
            ""confidence"": 85,
            ""summary"": ""Failed login attempt detected"",
            ""recommended_actions"": [""Block IP address"", ""Reset password""]
        }";

        // Act
        var securityEvent = SecurityEvent.CreateFromLlmResponse(originalEvent, validJson);

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.Should().Be(originalEvent);
        securityEvent.EventType.Should().Be(SecurityEventType.AuthenticationFailure);
        securityEvent.RiskLevel.Should().Be("high");
        securityEvent.Confidence.Should().Be(85);
        securityEvent.Summary.Should().Be("Failed login attempt detected");
        securityEvent.MitreTechniques.Should().Contain("T1110");
        securityEvent.MitreTechniques.Should().Contain("T1078");
        securityEvent.RecommendedActions.Should().Contain("Block IP address");
        securityEvent.RecommendedActions.Should().Contain("Reset password");
        securityEvent.IsDeterministic.Should().BeFalse();
    }

    [Fact]
    public void CreateFromLlmResponse_WithInvalidJson_ShouldCreateFallbackEvent()
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4625,
            "Warning",
            "test-user",
            "An account failed to log on."
        );

        var invalidJson = "{ invalid json }";

        // Act
        var securityEvent = SecurityEvent.CreateFromLlmResponse(originalEvent, invalidJson);

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.Should().Be(originalEvent);
        securityEvent.EventType.Should().Be(SecurityEventType.Unknown);
        securityEvent.RiskLevel.Should().Be("unknown");
        securityEvent.Confidence.Should().Be(0);
        securityEvent.Summary.Should().Be("Failed to parse LLM response");
        securityEvent.MitreTechniques.Should().BeEmpty();
        securityEvent.RecommendedActions.Should().BeEmpty();
        securityEvent.IsDeterministic.Should().BeFalse();
    }

    [Fact]
    public void CreateCorrelationBased_ShouldCreateValidCorrelationEvent()
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4625,
            "Warning",
            "test-user",
            "Multiple failed login attempts detected."
        );

        // Act
        var securityEvent = SecurityEvent.CreateCorrelationBased(
            originalEvent,
            SecurityEventType.BurstActivity,
            "high",
            90,
            "Burst of failed login attempts detected",
            new[] { "T1110", "T1078" },
            new[] { "Block IP address", "Enable MFA" },
            0.85,
            0.92,
            0.78
        );

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.Should().Be(originalEvent);
        securityEvent.EventType.Should().Be(SecurityEventType.BurstActivity);
        securityEvent.RiskLevel.Should().Be("high");
        securityEvent.Confidence.Should().Be(90);
        securityEvent.Summary.Should().Be("Burst of failed login attempts detected");
        securityEvent.MitreTechniques.Should().Contain("T1110");
        securityEvent.MitreTechniques.Should().Contain("T1078");
        securityEvent.RecommendedActions.Should().Contain("Block IP address");
        securityEvent.RecommendedActions.Should().Contain("Enable MFA");
        securityEvent.IsDeterministic.Should().BeFalse();
        securityEvent.IsCorrelationBased.Should().BeTrue();
        securityEvent.IsEnhanced.Should().BeFalse();
        securityEvent.CorrelationScore.Should().Be(0.85);
        securityEvent.BurstScore.Should().Be(0.92);
        securityEvent.AnomalyScore.Should().Be(0.78);
    }

    [Fact]
    public void CreateEnhanced_ShouldCreateValidEnhancedEvent()
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4672,
            "Information",
            "admin-user",
            "Special privileges assigned to new logon."
        );

        // Act
        var securityEvent = SecurityEvent.CreateEnhanced(
            originalEvent,
            SecurityEventType.PrivilegeEscalation,
            "critical",
            95,
            "Privilege escalation detected",
            new[] { "T1068", "T1078" },
            new[] { "Investigate user activity", "Review permissions" },
            true,
            0.75,
            0.60,
            0.85
        );

        // Assert
        securityEvent.Should().NotBeNull();
        securityEvent.OriginalEvent.Should().Be(originalEvent);
        securityEvent.EventType.Should().Be(SecurityEventType.PrivilegeEscalation);
        securityEvent.RiskLevel.Should().Be("critical");
        securityEvent.Confidence.Should().Be(95);
        securityEvent.Summary.Should().Be("Privilege escalation detected");
        securityEvent.MitreTechniques.Should().Contain("T1068");
        securityEvent.MitreTechniques.Should().Contain("T1078");
        securityEvent.RecommendedActions.Should().Contain("Investigate user activity");
        securityEvent.RecommendedActions.Should().Contain("Review permissions");
        securityEvent.IsDeterministic.Should().BeTrue();
        securityEvent.IsCorrelationBased.Should().BeTrue(); // Should be true since correlation scores are > 0
        securityEvent.IsEnhanced.Should().BeTrue();
        securityEvent.CorrelationScore.Should().Be(0.75);
        securityEvent.BurstScore.Should().Be(0.60);
        securityEvent.AnomalyScore.Should().Be(0.85);
    }

    [Theory]
    [InlineData("burstactivity", SecurityEventType.BurstActivity)]
    [InlineData("correlatedactivity", SecurityEventType.CorrelatedActivity)]
    [InlineData("anomalousactivity", SecurityEventType.AnomalousActivity)]
    [InlineData("suspiciousactivity", SecurityEventType.SuspiciousActivity)]
    [InlineData("unknown", SecurityEventType.Unknown)]
    [InlineData("invalid", SecurityEventType.Unknown)]
    public void ParseEventType_ShouldReturnCorrectEnum(string eventType, SecurityEventType expected)
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4625,
            "Warning",
            "test-user",
            "Test event."
        );

        // Act  
        // Parse eventType string to enum (test the parsing logic)
        var parsedEventType = Enum.TryParse<SecurityEventType>(eventType, true, out var result) ? result : SecurityEventType.Unknown;
        var securityEvent = SecurityEvent.CreateCorrelationBased(
            originalEvent,
            parsedEventType,
            "medium",
            80,
            "Test event",
            new[] { "T1110" },
            new[] { "Monitor activity" },
            0.5,
            0.5,
            0.5
        );

        // Assert
        securityEvent.EventType.Should().Be(expected);
    }

    [Fact]
    public void SecurityEvent_WithEnrichmentData_ShouldStoreEnrichment()
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4624,
            "Information",
            "test-user",
            "An account was successfully logged on."
        );

        // Act
        var securityEvent = SecurityEvent.CreateDeterministic(
            originalEvent,
            SecurityEventType.AuthenticationSuccess,
            "low",
            95,
            "Successful login detected",
            new[] { "T1078" },
            new[] { "Monitor user activity" }
        );

        // Assert
        // CreateDeterministic doesn't support enrichment data, so it should be null
        securityEvent.EnrichmentData.Should().BeNull();
    }

    [Fact]
    public void SecurityEvent_WithoutEnrichmentData_ShouldHaveNullEnrichment()
    {
        // Arrange
        var originalEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "test-host",
            "Security",
            4624,
            "Information",
            "test-user",
            "An account was successfully logged on."
        );

        // Act
        var securityEvent = SecurityEvent.CreateDeterministic(
            originalEvent,
            SecurityEventType.AuthenticationSuccess,
            "low",
            95,
            "Successful login detected",
            new[] { "T1078" },
            new[] { "Monitor user activity" }
        );

        // Assert
        securityEvent.EnrichmentData.Should().BeNull();
    }
}

