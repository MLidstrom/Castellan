using FluentAssertions;
using Castellan.Worker.Models;
using Xunit;

namespace Castellan.Tests.Models;

public class LogEventTests
{
    [Fact]
    public void LogEvent_ShouldCreateInstanceWithValidData()
    {
        // Arrange
        var time = DateTimeOffset.UtcNow;
        var host = "TEST-HOST";
        var channel = "Security";
        var eventId = 4624;
        var level = "Information";
        var user = "testuser";
        var message = "An account was successfully logged on";
        var rawJson = "{\"EventID\": 4624}";

        // Act
        var logEvent = new LogEvent(time, host, channel, eventId, level, user, message, rawJson);

        // Assert
        logEvent.Should().NotBeNull();
        logEvent.Time.Should().Be(time);
        logEvent.Host.Should().Be(host);
        logEvent.Channel.Should().Be(channel);
        logEvent.EventId.Should().Be(eventId);
        logEvent.Level.Should().Be(level);
        logEvent.User.Should().Be(user);
        logEvent.Message.Should().Be(message);
        logEvent.RawJson.Should().Be(rawJson);
    }

    [Fact]
    public void LogEvent_ShouldCreateInstanceWithDefaultRawJson()
    {
        // Arrange
        var time = DateTimeOffset.UtcNow;
        var host = "TEST-HOST";
        var channel = "Security";
        var eventId = 4624;
        var level = "Information";
        var user = "testuser";
        var message = "An account was successfully logged on";

        // Act
        var logEvent = new LogEvent(time, host, channel, eventId, level, user, message);

        // Assert
        logEvent.Should().NotBeNull();
        logEvent.Time.Should().Be(time);
        logEvent.Host.Should().Be(host);
        logEvent.Channel.Should().Be(channel);
        logEvent.EventId.Should().Be(eventId);
        logEvent.Level.Should().Be(level);
        logEvent.User.Should().Be(user);
        logEvent.Message.Should().Be(message);
        logEvent.RawJson.Should().Be("");
    }

    [Theory]
    [InlineData("Security")]
    [InlineData("System")]
    [InlineData("Application")]
    [InlineData("Setup")]
    [InlineData("ForwardedEvents")]
    public void LogEvent_ShouldHandleVariousChannels(string channel)
    {
        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            channel,
            4624,
            "Information",
            "testuser",
            "Test event"
        );

        // Assert
        logEvent.Channel.Should().Be(channel);
    }

    [Theory]
    [InlineData(4624)]
    [InlineData(4625)]
    [InlineData(4672)]
    [InlineData(4728)]
    [InlineData(4732)]
    [InlineData(4719)]
    [InlineData(1102)]
    public void LogEvent_ShouldHandleVariousEventIds(int eventId)
    {
        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            eventId,
            "Information",
            "testuser",
            "Test event"
        );

        // Assert
        logEvent.EventId.Should().Be(eventId);
    }

    [Theory]
    [InlineData("Information")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Critical")]
    public void LogEvent_ShouldHandleVariousLevels(string level)
    {
        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            level,
            "testuser",
            "Test event"
        );

        // Assert
        logEvent.Level.Should().Be(level);
    }

    [Fact]
    public void LogEvent_ShouldHandleEmptyUser()
    {
        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "",
            "Test event"
        );

        // Assert
        logEvent.User.Should().Be("");
    }

    [Fact]
    public void LogEvent_ShouldHandleLongMessage()
    {
        // Arrange
        var longMessage = new string('A', 1000);

        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            longMessage
        );

        // Assert
        logEvent.Message.Should().Be(longMessage);
        logEvent.Message.Length.Should().Be(1000);
    }

    [Fact]
    public void LogEvent_ShouldHandleSpecialCharactersInMessage()
    {
        // Arrange
        var messageWithSpecialChars = "Failed logon attempt from IP 192.168.1.100:12345. User: testuser@domain.com. Process: C:\\Windows\\System32\\winlogon.exe";

        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            messageWithSpecialChars
        );

        // Assert
        logEvent.Message.Should().Be(messageWithSpecialChars);
        logEvent.Message.Should().Contain("\\");
        logEvent.Message.Should().Contain(":");
        logEvent.Message.Should().Contain("@");
    }

    [Fact]
    public void LogEvent_ShouldHandleJsonRawJson()
    {
        // Arrange
        var jsonRawJson = @"{
            ""EventID"": 4624,
            ""TimeCreated"": ""2024-01-01T00:00:00.000Z"",
            ""Computer"": ""TEST-HOST"",
            ""Channel"": ""Security"",
            ""Level"": ""Information"",
            ""User"": ""testuser"",
            ""Message"": ""An account was successfully logged on""
        }";

        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on",
            jsonRawJson
        );

        // Assert
        logEvent.RawJson.Should().Be(jsonRawJson);
        logEvent.RawJson.Should().Contain("EventID");
        logEvent.RawJson.Should().Contain("4624");
    }

    [Fact]
    public void LogEvent_ShouldHandleNullValuesInRawJson()
    {
        // Arrange
        var rawJsonWithNulls = "{\"EventID\": 4624, \"User\": null, \"Message\": null}";

        // Act
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "Test event",
            rawJsonWithNulls
        );

        // Assert
        logEvent.RawJson.Should().Be(rawJsonWithNulls);
        logEvent.RawJson.Should().Contain("null");
    }

    [Fact]
    public void LogEvent_ShouldHandleDifferentTimeZones()
    {
        // Arrange
        var utcTime = DateTimeOffset.UtcNow;
        var localTime = DateTimeOffset.Now;
        var specificTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(5));

        // Act
        var logEvent1 = new LogEvent(utcTime, "TEST-HOST", "Security", 4624, "Information", "testuser", "Test event");
        var logEvent2 = new LogEvent(localTime, "TEST-HOST", "Security", 4624, "Information", "testuser", "Test event");
        var logEvent3 = new LogEvent(specificTime, "TEST-HOST", "Security", 4624, "Information", "testuser", "Test event");

        // Assert
        logEvent1.Time.Should().Be(utcTime);
        logEvent2.Time.Should().Be(localTime);
        logEvent3.Time.Should().Be(specificTime);
    }

    [Fact]
    public void LogEvent_ShouldHandleRecordEquality()
    {
        // Arrange
        var time = DateTimeOffset.UtcNow;
        var logEvent1 = new LogEvent(time, "TEST-HOST", "Security", 4624, "Information", "testuser", "Test event");
        var logEvent2 = new LogEvent(time, "TEST-HOST", "Security", 4624, "Information", "testuser", "Test event");
        var logEvent3 = new LogEvent(time, "TEST-HOST", "System", 4624, "Information", "testuser", "Test event");

        // Assert
        logEvent1.Should().Be(logEvent2);
        logEvent1.Should().NotBe(logEvent3);
    }

    [Fact]
    public void LogEvent_ShouldHandleRecordDeconstruction()
    {
        // Arrange
        var time = DateTimeOffset.UtcNow;
        var host = "TEST-HOST";
        var channel = "Security";
        var eventId = 4624;
        var level = "Information";
        var user = "testuser";
        var message = "Test event";
        var rawJson = "{\"EventID\": 4624}";

        var logEvent = new LogEvent(time, host, channel, eventId, level, user, message, rawJson);

        // Act
        var (deconstructedTime, deconstructedHost, deconstructedChannel, deconstructedEventId, 
             deconstructedLevel, deconstructedUser, deconstructedMessage, deconstructedRawJson, deconstructedUniqueId) = logEvent;

        // Assert
        deconstructedTime.Should().Be(time);
        deconstructedHost.Should().Be(host);
        deconstructedChannel.Should().Be(channel);
        deconstructedEventId.Should().Be(eventId);
        deconstructedLevel.Should().Be(level);
        deconstructedUser.Should().Be(user);
        deconstructedMessage.Should().Be(message);
        deconstructedRawJson.Should().Be(rawJson);
    }

    [Fact]
    public void LogEvent_ShouldHandleToString()
    {
        // Arrange
        var time = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var logEvent = new LogEvent(time, "TEST-HOST", "Security", 4624, "Information", "testuser", "Test event");

        // Act
        var result = logEvent.ToString();

        // Assert
        result.Should().Contain("TEST-HOST");
        result.Should().Contain("Security");
        result.Should().Contain("4624");
        result.Should().Contain("testuser");
        result.Should().Contain("Test event");
    }
}

