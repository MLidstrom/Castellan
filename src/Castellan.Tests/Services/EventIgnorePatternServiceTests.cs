using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Castellan.Worker.Services;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;

namespace Castellan.Tests.Services;

public class EventIgnorePatternServiceTests
{
    private readonly Mock<ILogger<EventIgnorePatternService>> _mockLogger;
    private readonly Mock<IOptionsMonitor<IgnorePatternOptions>> _mockOptions;

    public EventIgnorePatternServiceTests()
    {
        _mockLogger = new Mock<ILogger<EventIgnorePatternService>>();
        _mockOptions = new Mock<IOptionsMonitor<IgnorePatternOptions>>();
    }

    [Fact]
    public void ShouldIgnoreEvent_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new IgnorePatternOptions { Enabled = false };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);
        var securityEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" });

        // Act
        var result = service.ShouldIgnoreEvent(securityEvent);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIgnoreEvent_FilterAllLocalEvents_FiltersLocalMachineEvents()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            FilterAllLocalEvents = true,
            LocalMachines = new List<string> { "TEST-MACHINE", "127.0.0.1" }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);
        var securityEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" }, "TEST-MACHINE");

        // Act
        var result = service.ShouldIgnoreEvent(securityEvent);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIgnoreEvent_FilterAllLocalEvents_DoesNotFilterNonLocalEvents()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            FilterAllLocalEvents = true,
            LocalMachines = new List<string> { "TEST-MACHINE", "127.0.0.1" }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);
        var securityEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" }, "REMOTE-MACHINE");

        // Act
        var result = service.ShouldIgnoreEvent(securityEvent);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldIgnoreEvent_SequentialPattern_MatchesFullSequence()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            SequenceTimeWindowSeconds = 30,
            MaxRecentEvents = 100,
            Patterns = new List<SequentialIgnorePattern>
            {
                new SequentialIgnorePattern
                {
                    Sequence = new List<EventStep>
                    {
                        new EventStep
                        {
                            EventType = "AuthenticationSuccess",
                            MitreTechniques = new List<string> { "T1078" },
                            SourceMachines = new List<string> { "TEST-MACHINE" }
                        },
                        new EventStep
                        {
                            EventType = "PrivilegeEscalation",
                            MitreTechniques = new List<string> { "T1548", "T1055" },
                            SourceMachines = new List<string> { "TEST-MACHINE" }
                        }
                    },
                    Reason = "Test pattern",
                    IgnoreAllEventsInSequence = false
                }
            }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);

        // Act - First event (should not be ignored)
        var firstEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" }, "TEST-MACHINE");
        var firstResult = service.ShouldIgnoreEvent(firstEvent);

        // Wait a moment to ensure different timestamps
        System.Threading.Thread.Sleep(100);

        // Second event (should complete pattern and be ignored)
        var secondEvent = CreateTestSecurityEvent("PrivilegeEscalation", new[] { "T1548" }, "TEST-MACHINE");
        var secondResult = service.ShouldIgnoreEvent(secondEvent);

        // Assert
        Assert.False(firstResult); // First event not ignored
        Assert.True(secondResult);  // Second event completes pattern and is ignored
    }

    [Fact]
    public void ShouldIgnoreEvent_SequentialPattern_IgnoreAllEventsInSequence()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            SequenceTimeWindowSeconds = 30,
            MaxRecentEvents = 100,
            Patterns = new List<SequentialIgnorePattern>
            {
                new SequentialIgnorePattern
                {
                    Sequence = new List<EventStep>
                    {
                        new EventStep
                        {
                            EventType = "AuthenticationSuccess",
                            MitreTechniques = new List<string> { "T1078" },
                            SourceMachines = new List<string> { "TEST-MACHINE" }
                        },
                        new EventStep
                        {
                            EventType = "PrivilegeEscalation",
                            MitreTechniques = new List<string> { "T1548" },
                            SourceMachines = new List<string> { "TEST-MACHINE" }
                        }
                    },
                    Reason = "Test pattern",
                    IgnoreAllEventsInSequence = true
                }
            }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);

        // Act - First event
        var firstEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" }, "TEST-MACHINE");
        var firstResult = service.ShouldIgnoreEvent(firstEvent);

        System.Threading.Thread.Sleep(100);

        // Second event
        var secondEvent = CreateTestSecurityEvent("PrivilegeEscalation", new[] { "T1548" }, "TEST-MACHINE");
        var secondResult = service.ShouldIgnoreEvent(secondEvent);

        // Assert
        Assert.True(firstResult);  // First event ignored when IgnoreAllEventsInSequence = true
        Assert.True(secondResult); // Second event also ignored
    }

    [Fact]
    public void ShouldIgnoreEvent_StandalonePattern_MatchesSingleEvent()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            Patterns = new List<SequentialIgnorePattern>
            {
                new SequentialIgnorePattern
                {
                    Sequence = new List<EventStep>
                    {
                        new EventStep
                        {
                            EventType = "PrivilegeEscalation",
                            MitreTechniques = new List<string> { "T1078" },
                            SourceMachines = new List<string> { "TEST-MACHINE" }
                        }
                    },
                    Reason = "Standalone pattern",
                    IgnoreAllEventsInSequence = true
                }
            }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);
        var securityEvent = CreateTestSecurityEvent("PrivilegeEscalation", new[] { "T1078" }, "TEST-MACHINE");

        // Act
        var result = service.ShouldIgnoreEvent(securityEvent);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIgnoreEvent_PatternWithAccountName_MatchesAccountName()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            Patterns = new List<SequentialIgnorePattern>
            {
                new SequentialIgnorePattern
                {
                    Sequence = new List<EventStep>
                    {
                        new EventStep
                        {
                            EventType = "AuthenticationSuccess",
                            AccountNames = new List<string> { "SYSTEM" },
                            SourceMachines = new List<string> { "TEST-MACHINE" }
                        }
                    },
                    IgnoreAllEventsInSequence = true
                }
            }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);
        var message = "New Logon:\r\n\tAccount Name:\t\tSYSTEM";
        var securityEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" }, "TEST-MACHINE", message);

        // Act
        var result = service.ShouldIgnoreEvent(securityEvent);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIgnoreEvent_PatternWithLogonType_MatchesLogonType()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            Patterns = new List<SequentialIgnorePattern>
            {
                new SequentialIgnorePattern
                {
                    Sequence = new List<EventStep>
                    {
                        new EventStep
                        {
                            EventType = "AuthenticationSuccess",
                            LogonTypes = new List<int> { 5 },
                            SourceMachines = new List<string> { "TEST-MACHINE" }
                        }
                    },
                    IgnoreAllEventsInSequence = true
                }
            }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);
        var message = "Logon Type:\t\t5";
        var securityEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" }, "TEST-MACHINE", message);

        // Act
        var result = service.ShouldIgnoreEvent(securityEvent);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldIgnoreEvent_TimeWindowExpired_DoesNotMatch()
    {
        // Arrange
        var options = new IgnorePatternOptions
        {
            Enabled = true,
            SequenceTimeWindowSeconds = 1, // Very short window
            MaxRecentEvents = 100,
            Patterns = new List<SequentialIgnorePattern>
            {
                new SequentialIgnorePattern
                {
                    Sequence = new List<EventStep>
                    {
                        new EventStep { EventType = "AuthenticationSuccess", MitreTechniques = new List<string> { "T1078" } },
                        new EventStep { EventType = "PrivilegeEscalation", MitreTechniques = new List<string> { "T1548" } }
                    },
                    IgnoreAllEventsInSequence = false
                }
            }
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        var service = new EventIgnorePatternService(_mockOptions.Object, _mockLogger.Object);

        // Act - First event
        var firstEvent = CreateTestSecurityEvent("AuthenticationSuccess", new[] { "T1078" });
        service.ShouldIgnoreEvent(firstEvent);

        // Wait longer than time window
        System.Threading.Thread.Sleep(1500);

        // Second event (should not match because time window expired)
        var secondEvent = CreateTestSecurityEvent("PrivilegeEscalation", new[] { "T1548" });
        var secondResult = service.ShouldIgnoreEvent(secondEvent);

        // Assert
        Assert.False(secondResult); // Pattern expired, should not be ignored
    }

    private SecurityEvent CreateTestSecurityEvent(
        string eventType,
        string[] mitreTechniques,
        string machine = "TEST-MACHINE",
        string message = "Test message")
    {
        return new SecurityEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = Enum.Parse<SecurityEventType>(eventType),
            MitreTechniques = mitreTechniques,
            OriginalEvent = new LogEvent(
                Time: DateTimeOffset.UtcNow,
                Host: machine,
                Channel: "Security",
                EventId: 4624,
                Level: "Information",
                User: "TestUser",
                Message: message,
                RawJson: "{}",
                UniqueId: Guid.NewGuid().ToString()
            ),
            RiskLevel = "Medium",
            Confidence = 75,
            Summary = "Test event"
        };
    }
}
