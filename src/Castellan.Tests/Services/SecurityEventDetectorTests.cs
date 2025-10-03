using FluentAssertions;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Castellan.Tests.Services;

public class SecurityEventDetectorTests
{
    private readonly Mock<ILogger<SecurityEventDetector>> _mockLogger;
    private readonly Mock<ICorrelationEngine> _mockCorrelationEngine;
    private readonly Mock<ISecurityEventRuleStore> _mockRuleStore;
    private readonly SecurityEventDetector _detector;

    public SecurityEventDetectorTests()
    {
        _mockLogger = new Mock<ILogger<SecurityEventDetector>>();
        _mockCorrelationEngine = new Mock<ICorrelationEngine>();
        _mockRuleStore = new Mock<ISecurityEventRuleStore>();

        // Setup mock rule store to return empty list (will use legacy rules as fallback)
        _mockRuleStore.Setup(x => x.GetAllEnabledRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEventRuleEntity>());

        _detector = new SecurityEventDetector(_mockLogger.Object, _mockCorrelationEngine.Object, _mockRuleStore.Object);
    }

    [Fact]
    public void DetectSecurityEvent_WithAuthenticationSuccess_ShouldReturnValidEvent()
    {
        // Arrange - Use a time within business hours (10 AM UTC) to avoid off-hours detection
        var logEvent = new LogEvent(
            new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero),
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        result.RiskLevel.Should().Be("medium");
        result.Confidence.Should().BeGreaterThanOrEqualTo(85);
        result.Summary.Should().Contain("Successful logon");
        result.MitreTechniques.Should().Contain("T1078");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithAuthenticationFailure_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4625,
            "Warning",
            "testuser",
            "An account failed to log on"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.AuthenticationFailure);
        result.RiskLevel.Should().Be("high");
        result.Confidence.Should().BeGreaterThan(85);
        result.Summary.Should().Contain("Failed logon");
        result.MitreTechniques.Should().Contain("T1110");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithPrivilegeEscalation_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4672,
            "Information",
            "adminuser",
            "Special privileges assigned to new logon"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.PrivilegeEscalation);
        result.RiskLevel.Should().Be("medium");
        result.Confidence.Should().BeGreaterThan(70);
        result.Summary.Should().Contain("privilege");
        result.MitreTechniques.Should().Contain("T1068");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithAccountCreation_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4720,
            "Information",
            "adminuser",
            "A user account was created"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.AccountManagement);
        result.RiskLevel.Should().Be("high");
        result.Confidence.Should().BeGreaterThan(85);
        result.Summary.Should().Contain("Account created");
        result.MitreTechniques.Should().Contain("T1136");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithProcessCreation_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4688,
            "Information",
            "testuser",
            "A new process has been created"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.ProcessCreation);
        result.RiskLevel.Should().Be("medium");
        result.Confidence.Should().BeGreaterThan(75);
        result.Summary.Should().Contain("Process creation");
        result.MitreTechniques.Should().Contain("T1055");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithServiceInstallation_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            7045,
            "Information",
            "SYSTEM",
            "A service was installed in the system"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.ServiceInstallation);
        result.RiskLevel.Should().Be("high");
        result.Confidence.Should().BeGreaterThanOrEqualTo(85);
        result.Summary.Should().Contain("Service installed");
        result.MitreTechniques.Should().Contain("T1543");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithScheduledTask_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4698,
            "Information",
            "adminuser",
            "A scheduled task was created"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.ScheduledTask);
        result.RiskLevel.Should().Be("medium");
        result.Confidence.Should().BeGreaterThan(70);
        result.Summary.Should().Contain("Scheduled task created");
        result.MitreTechniques.Should().Contain("T1053");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithSecurityPolicyChange_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4719,
            "Information",
            "adminuser",
            "System audit policy was changed"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.SecurityPolicyChange);
        result.RiskLevel.Should().Be("high");
        result.Confidence.Should().BeGreaterThanOrEqualTo(85);
        result.Summary.Should().Contain("audit policy");
        result.MitreTechniques.Should().Contain("T1562");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithUnknownEvent_ShouldReturnNull()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Application",
            1000,
            "Information",
            "testuser",
            "Application started successfully"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void DetectSecurityEvent_WithSystemStartup_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            6005,
            "Information",
            "SYSTEM",
            "The Event log service was started"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.SystemStartup);
        result.RiskLevel.Should().Be("low");
        result.Confidence.Should().BeGreaterThan(50);
        result.Summary.Should().Contain("Event log service was started");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithSystemShutdown_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            6006,
            "Information",
            "SYSTEM",
            "The Event log service was stopped"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.SystemShutdown);
        result.RiskLevel.Should().Be("low");
        result.Confidence.Should().BeGreaterThan(50);
        result.Summary.Should().Contain("Event log service was stopped");
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithLogClearing_ShouldReturnValidEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            1102,
            "Information",
            "adminuser",
            "The audit log was cleared"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.OriginalEvent.Should().Be(logEvent);
        result.EventType.Should().Be(SecurityEventType.SecurityPolicyChange);
        result.RiskLevel.Should().Be("critical");
        result.Confidence.Should().BeGreaterThan(85);
        result.Summary.Should().Contain("Audit log was cleared");
        result.MitreTechniques.Should().Contain("T1562");
        result.IsDeterministic.Should().BeTrue();
    }

    [Theory]
    [InlineData(4624, SecurityEventType.AuthenticationSuccess)]
    [InlineData(4625, SecurityEventType.AuthenticationFailure)]
    [InlineData(4672, SecurityEventType.PrivilegeEscalation)]
    [InlineData(4720, SecurityEventType.AccountManagement)]
    [InlineData(4722, SecurityEventType.AccountManagement)]
    [InlineData(4724, SecurityEventType.AccountManagement)]
    [InlineData(4728, SecurityEventType.PrivilegeEscalation)]
    [InlineData(4732, SecurityEventType.PrivilegeEscalation)]
    [InlineData(4688, SecurityEventType.ProcessCreation)]
    [InlineData(7045, SecurityEventType.ServiceInstallation)]
    [InlineData(4697, SecurityEventType.ServiceInstallation)]
    [InlineData(4698, SecurityEventType.ScheduledTask)]
    [InlineData(4700, SecurityEventType.ScheduledTask)]
    [InlineData(4719, SecurityEventType.SecurityPolicyChange)]
    [InlineData(4902, SecurityEventType.SecurityPolicyChange)]
    [InlineData(4904, SecurityEventType.SecurityPolicyChange)]
    [InlineData(4905, SecurityEventType.SecurityPolicyChange)]
    [InlineData(4907, SecurityEventType.SecurityPolicyChange)]
    [InlineData(4908, SecurityEventType.SecurityPolicyChange)]
    [InlineData(6005, SecurityEventType.SystemStartup)]
    [InlineData(6006, SecurityEventType.SystemShutdown)]
    [InlineData(1102, SecurityEventType.SecurityPolicyChange)]
    public void DetectSecurityEvent_WithVariousEventIds_ShouldReturnCorrectEventType(int eventId, SecurityEventType expectedEventType)
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            eventId,
            "Information",
            "testuser",
            $"Test event {eventId}"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(expectedEventType);
        result.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DetectSecurityEvent_WithAdminUser_ShouldHaveHigherRiskLevel()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "administrator",
            "An account was successfully logged on"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.RiskLevel.Should().Be("high"); // Admin logons are higher risk
        result.Confidence.Should().BeGreaterThan(85);
    }

    [Fact]
    public void DetectSecurityEvent_WithOffHoursActivity_ShouldHaveHigherRiskLevel()
    {
        // Arrange - Create event at 2 AM
        var offHoursTime = DateTimeOffset.UtcNow.Date.AddHours(2);
        var logEvent = new LogEvent(
            offHoursTime,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        // Act
        var result = _detector.DetectSecurityEvent(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.RiskLevel.Should().Be("medium"); // Off-hours activity is higher risk
        result.Confidence.Should().BeGreaterThan(85);
    }

    [Fact]
    public async Task DetectAndCorrelateSecurityEventAsync_WithNoCorrelation_ShouldReturnBaseEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        _mockCorrelationEngine.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
            .ReturnsAsync(new CorrelationResult
            {
                HasCorrelation = false,
                ConfidenceScore = 0.0,
                Explanation = "No correlations found"
            });

        // Act
        var result = await _detector.DetectAndCorrelateSecurityEventAsync(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        result.CorrelationIds.Should().BeNull();
        result.CorrelationContext.Should().BeNull();
        result.IsCorrelationBased.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAndCorrelateSecurityEventAsync_WithAttackChainCorrelation_ShouldEnhanceEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var correlation = new EventCorrelation
        {
            Id = Guid.NewGuid().ToString(),
            CorrelationType = "AttackChain",
            ConfidenceScore = 0.9,
            Pattern = "Brute Force -> Privilege Escalation",
            RiskLevel = "high",
            EventIds = new List<string> { "event1", "event2" },
            TimeWindow = TimeSpan.FromMinutes(15),
            AttackChainStage = "Initial Access",
            MitreTechniques = new List<string> { "T1078", "T1110" }
        };

        _mockCorrelationEngine.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
            .ReturnsAsync(new CorrelationResult
            {
                HasCorrelation = true,
                ConfidenceScore = 0.9,
                Correlation = correlation,
                Explanation = "Attack chain detected"
            });

        // Act
        var result = await _detector.DetectAndCorrelateSecurityEventAsync(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        result.IsCorrelationBased.Should().BeTrue();
        result.IsEnhanced.Should().BeTrue();
        result.CorrelationIds.Should().NotBeNull();
        result.CorrelationIds!.Should().Contain(correlation.Id);
        result.CorrelationContext.Should().NotBeNullOrEmpty();
        result.CorrelationContext.Should().Contain("attackchain pattern");
        result.CorrelationScore.Should().Be(0.9);

        // Risk level should be upgraded due to attack chain
        result.RiskLevel.Should().Be("critical"); // Upgraded from medium to critical
        result.Confidence.Should().BeGreaterThan(95); // Enhanced confidence
        result.RecommendedActions.Should().Contain("Investigate entire attack sequence");
    }

    [Fact]
    public async Task DetectAndCorrelateSecurityEventAsync_WithLateralMovementCorrelation_ShouldUpgradeRisk()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var correlation = new EventCorrelation
        {
            Id = Guid.NewGuid().ToString(),
            CorrelationType = "LateralMovement",
            ConfidenceScore = 0.85,
            Pattern = "Cross-host authentication",
            RiskLevel = "high",
            EventIds = new List<string> { "event1", "event2", "event3" },
            TimeWindow = TimeSpan.FromMinutes(30),
            MitreTechniques = new List<string> { "T1021" }
        };

        _mockCorrelationEngine.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
            .ReturnsAsync(new CorrelationResult
            {
                HasCorrelation = true,
                ConfidenceScore = 0.85,
                Correlation = correlation,
                Explanation = "Lateral movement detected"
            });

        // Act
        var result = await _detector.DetectAndCorrelateSecurityEventAsync(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.IsCorrelationBased.Should().BeTrue();
        result.RiskLevel.Should().Be("high"); // Upgraded from medium to high
        result.CorrelationContext.Should().Contain("lateralmovement pattern");
        result.RecommendedActions.Should().Contain("Investigate lateral movement across systems");
    }

    [Fact]
    public async Task DetectAndCorrelateSecurityEventAsync_WithMLDetectedCorrelation_ShouldAddMLActions()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4688,
            "Information",
            "testuser",
            "A new process has been created"
        );

        var correlation = new EventCorrelation
        {
            Id = Guid.NewGuid().ToString(),
            CorrelationType = "MLDetected",
            ConfidenceScore = 0.75,
            Pattern = "ML Anomaly Detection",
            RiskLevel = "medium",
            EventIds = new List<string> { "event1" },
            TimeWindow = TimeSpan.FromMinutes(5)
        };

        _mockCorrelationEngine.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
            .ReturnsAsync(new CorrelationResult
            {
                HasCorrelation = true,
                ConfidenceScore = 0.75,
                Correlation = correlation,
                Explanation = "ML anomaly detected"
            });

        // Act
        var result = await _detector.DetectAndCorrelateSecurityEventAsync(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.IsCorrelationBased.Should().BeTrue();
        result.CorrelationContext.Should().Contain("mldetected pattern");
        result.RecommendedActions.Should().Contain("Review ML-detected anomaly pattern");
        result.RecommendedActions.Should().Contain("Consider updating correlation rules");
    }

    [Fact]
    public async Task DetectAndCorrelateSecurityEventAsync_WithCorrelationEngineError_ShouldReturnBaseEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        _mockCorrelationEngine.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
            .ThrowsAsync(new Exception("Correlation engine error"));

        // Act
        var result = await _detector.DetectAndCorrelateSecurityEventAsync(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be(SecurityEventType.AuthenticationSuccess);
        result.IsCorrelationBased.Should().BeFalse();
        result.CorrelationIds.Should().BeNull();
        // Should return base event when correlation fails
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("medium", "medium")]
    [InlineData("high", "high")]
    [InlineData("critical", "critical")]
    public async Task DetectAndCorrelateSecurityEventAsync_WithTemporalBurst_ShouldMaintainRiskLevel(string baseRisk, string expectedRisk)
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4688,
            "Information",
            "testuser",
            "A new process has been created"
        );

        var correlation = new EventCorrelation
        {
            Id = Guid.NewGuid().ToString(),
            CorrelationType = "TemporalBurst",
            ConfidenceScore = 0.70,
            Pattern = "Rapid process creation",
            RiskLevel = baseRisk,
            EventIds = new List<string> { "event1", "event2", "event3" },
            TimeWindow = TimeSpan.FromMinutes(2)
        };

        _mockCorrelationEngine.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
            .ReturnsAsync(new CorrelationResult
            {
                HasCorrelation = true,
                ConfidenceScore = 0.70,
                Correlation = correlation,
                Explanation = "Temporal burst detected"
            });

        // Act
        var result = await _detector.DetectAndCorrelateSecurityEventAsync(logEvent);

        // Assert
        result.Should().NotBeNull();
        result!.IsCorrelationBased.Should().BeTrue();
        result.RiskLevel.Should().Be(expectedRisk); // Temporal burst doesn't upgrade risk level
        result.RecommendedActions.Should().Contain("Investigate burst pattern for automation");
    }
}

