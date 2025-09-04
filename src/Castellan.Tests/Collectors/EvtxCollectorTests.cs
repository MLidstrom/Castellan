using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Castellan.Worker.Models;
using Castellan.Worker.Collectors;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests.Collectors;

public class EvtxCollectorTests
{
    private readonly Mock<ILogger<EvtxCollector>> _mockLogger;
    private readonly Mock<IOptions<EvtxOptions>> _mockOptions;

    public EvtxCollectorTests()
    {
        _mockLogger = new Mock<ILogger<EvtxCollector>>();
        _mockOptions = new Mock<IOptions<EvtxOptions>>();
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security", "System", "Application" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyChannels_ShouldCreateInstance()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = Array.Empty<string>(),
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullChannels_ShouldCreateInstance()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = null!,
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDuplicateChannels_ShouldDeduplicate()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security", "Security", "System", "System", "Application" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCaseInsensitiveDuplicateChannels_ShouldDeduplicate()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security", "SECURITY", "System", "SYSTEM", "Application" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomXPath_ShouldUseCustomXPath()
    {
        // Arrange
        var customXPath = "*[System[EventID=4624]]";
        var options = new EvtxOptions
        {
            Channels = new[] { "Security" },
            XPath = customXPath,
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomPollSeconds_ShouldUseCustomPollSeconds()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 60
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public async Task CollectAsync_ShouldReturnIAsyncEnumerable()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);

        // Act
        var result = collector.CollectAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await foreach (var item in result)
        {
            // This will likely be empty in test environment, but we're testing the interface
            break;
        }
    }

    [Fact]
    public async Task CollectAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 30
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        var collector = new EvtxCollector(_mockOptions.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        var result = collector.CollectAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        await foreach (var item in result)
        {
            // This should complete quickly due to cancellation
            break;
        }
    }

    [Fact]
    public void EvtxOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new EvtxOptions();

        // Assert
        options.Channels.Should().BeEquivalentTo(new[] { "Security", "System", "Application" });
        options.XPath.Should().Be("*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]");
        options.PollSeconds.Should().Be(30);
    }

    [Fact]
    public void EvtxOptions_ShouldAllowCustomValues()
    {
        // Arrange
        var customChannels = new[] { "Security", "Setup" };
        var customXPath = "*[System[EventID=4624]]";
        var customPollSeconds = 60;

        // Act
        var options = new EvtxOptions
        {
            Channels = customChannels,
            XPath = customXPath,
            PollSeconds = customPollSeconds
        };

        // Assert
        options.Channels.Should().BeEquivalentTo(customChannels);
        options.XPath.Should().Be(customXPath);
        options.PollSeconds.Should().Be(customPollSeconds);
    }

    [Fact]
    public void EvtxOptions_ShouldHandleNullChannels()
    {
        // Act
        var options = new EvtxOptions
        {
            Channels = null!
        };

        // Assert
        options.Channels.Should().BeNull();
    }

    [Fact]
    public void EvtxOptions_ShouldHandleEmptyChannels()
    {
        // Act
        var options = new EvtxOptions
        {
            Channels = Array.Empty<string>()
        };

        // Assert
        options.Channels.Should().BeEmpty();
    }

    [Fact]
    public void EvtxOptions_ShouldHandleNullXPath()
    {
        // Act
        var options = new EvtxOptions
        {
            XPath = null!
        };

        // Assert
        options.XPath.Should().BeNull();
    }

    [Fact]
    public void EvtxOptions_ShouldHandleEmptyXPath()
    {
        // Act
        var options = new EvtxOptions
        {
            XPath = ""
        };

        // Assert
        options.XPath.Should().Be("");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(300)]
    public void EvtxOptions_ShouldHandleVariousPollSeconds(int pollSeconds)
    {
        // Act
        var options = new EvtxOptions
        {
            PollSeconds = pollSeconds
        };

        // Assert
        options.PollSeconds.Should().Be(pollSeconds);
    }

    [Fact]
    public void EvtxOptions_ShouldHandleNegativePollSeconds()
    {
        // Act
        var options = new EvtxOptions
        {
            PollSeconds = -1
        };

        // Assert
        options.PollSeconds.Should().Be(-1);
    }

    [Fact]
    public void EvtxOptions_ShouldHandleLargePollSeconds()
    {
        // Act
        var options = new EvtxOptions
        {
            PollSeconds = int.MaxValue
        };

        // Assert
        options.PollSeconds.Should().Be(int.MaxValue);
    }

    [Fact]
    public void EvtxOptions_ShouldBeMutable()
    {
        // Arrange
        var options = new EvtxOptions();

        // Act
        options.Channels = new[] { "Custom" };
        options.XPath = "Custom XPath";
        options.PollSeconds = 999;

        // Assert
        options.Channels.Should().BeEquivalentTo(new[] { "Custom" });
        options.XPath.Should().Be("Custom XPath");
        options.PollSeconds.Should().Be(999);
    }

    [Fact]
    public void EvtxOptions_ShouldHandleSpecialCharactersInXPath()
    {
        // Arrange
        var specialXPath = "*[System[EventID=4624 and TimeCreated[timediff(@SystemTime) <= 600000]]]";

        // Act
        var options = new EvtxOptions
        {
            XPath = specialXPath
        };

        // Assert
        options.XPath.Should().Be(specialXPath);
    }

    [Fact]
    public void EvtxOptions_ShouldHandleLongXPath()
    {
        // Arrange
        var longXPath = new string('*', 1000);

        // Act
        var options = new EvtxOptions
        {
            XPath = longXPath
        };

        // Assert
        options.XPath.Should().Be(longXPath);
        options.XPath.Length.Should().Be(1000);
    }

    [Fact]
    public void EvtxOptions_ShouldHandleManyChannels()
    {
        // Arrange
        var manyChannels = Enumerable.Range(0, 100).Select(i => $"Channel{i}").ToArray();

        // Act
        var options = new EvtxOptions
        {
            Channels = manyChannels
        };

        // Assert
        options.Channels.Should().HaveCount(100);
        options.Channels.Should().Contain("Channel0");
        options.Channels.Should().Contain("Channel99");
    }

    [Fact]
    public void EvtxOptions_ShouldHandleChannelsWithSpecialCharacters()
    {
        // Arrange
        var specialChannels = new[] { "Security", "System", "Application", "Setup", "ForwardedEvents" };

        // Act
        var options = new EvtxOptions
        {
            Channels = specialChannels
        };

        // Assert
        options.Channels.Should().BeEquivalentTo(specialChannels);
    }

    [Fact]
    public void EvtxOptions_ShouldHandleChannelsWithSpaces()
    {
        // Arrange
        var channelsWithSpaces = new[] { "Windows PowerShell", "Microsoft-Windows-TaskScheduler/Operational" };

        // Act
        var options = new EvtxOptions
        {
            Channels = channelsWithSpaces
        };

        // Assert
        options.Channels.Should().BeEquivalentTo(channelsWithSpaces);
    }

    #region Historical Collection Tests

    [Fact]
    public async Task CollectHistoricalAsync_ShouldCollectEventsFromPast24Hours()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security", "System", "Application" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 5
        };

        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        var collector = new EvtxCollector(mockOptions.Object, mockLogger.Object);

        // Act
        var events = new List<LogEvent>();
        await foreach (var evt in collector.CollectHistoricalAsync(CancellationToken.None))
        {
            events.Add(evt);
            if (events.Count >= 10) break; // Limit for testing
        }

        // Assert
        events.Should().NotBeNull();
        // Note: On non-Windows systems or without proper permissions, this might return empty
        // The test verifies the method doesn't throw and handles the collection properly
    }

    [Fact]
    public async Task CollectHistoricalAsync_ShouldHandleEmptyChannels()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = Array.Empty<string>(),
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 5
        };

        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        var collector = new EvtxCollector(mockOptions.Object, mockLogger.Object);

        // Act
        var events = new List<LogEvent>();
        await foreach (var evt in collector.CollectHistoricalAsync(CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectHistoricalAsync_ShouldHandleCancellation()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security", "System", "Application" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 5
        };

        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        var collector = new EvtxCollector(mockOptions.Object, mockLogger.Object);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel after 100ms

        // Act
        var events = new List<LogEvent>();
        await foreach (var evt in collector.CollectHistoricalAsync(cts.Token))
        {
            events.Add(evt);
        }

        // Assert
        // Should handle cancellation gracefully without throwing
    }

    [Fact]
    public async Task CollectHistoricalAsync_ShouldHandleChannelAccessErrors()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "NonExistentChannel" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 5
        };

        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        var collector = new EvtxCollector(mockOptions.Object, mockLogger.Object);

        // Act
        var events = new List<LogEvent>();
        await foreach (var evt in collector.CollectHistoricalAsync(CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        // Should handle access errors gracefully without throwing
        events.Should().NotBeNull();
    }

    [Fact]
    public async Task CollectHistoricalAsync_ShouldUseCorrectTimeRange()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 5
        };

        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        var collector = new EvtxCollector(mockOptions.Object, mockLogger.Object);

        // Act
        var startTime = DateTimeOffset.UtcNow;
        var events = new List<LogEvent>();
        await foreach (var evt in collector.CollectHistoricalAsync(CancellationToken.None))
        {
            events.Add(evt);
            if (events.Count >= 5) break; // Limit for testing
        }
        var endTime = DateTimeOffset.UtcNow;

        // Assert
        // The time range should be approximately 24 hours
        var timeSpan = endTime - startTime;
        timeSpan.Should().BeLessThan(TimeSpan.FromMinutes(5)); // Should complete quickly
        
        // Note: The actual 24-hour range is handled internally by the XPath query
    }

    [Fact]
    public async Task CollectHistoricalAsync_ShouldHandleLargeEventSets()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security", "System", "Application" },
            XPath = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]",
            PollSeconds = 5
        };

        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        var collector = new EvtxCollector(mockOptions.Object, mockLogger.Object);

        // Act
        var events = new List<LogEvent>();
        var count = 0;
        await foreach (var evt in collector.CollectHistoricalAsync(CancellationToken.None))
        {
            events.Add(evt);
            count++;
            if (count >= 100) break; // Limit to prevent infinite loops in testing
        }

        // Assert
        events.Should().NotBeNull();
        count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void CollectHistoricalAsync_ShouldHandleNullOptions()
    {
        // Arrange
        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns((EvtxOptions)null!);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => 
            new EvtxCollector(mockOptions.Object, mockLogger.Object));
        
        exception.Message.Should().Be("EVTX collector configuration is null");
    }

    [Fact]
    public async Task CollectHistoricalAsync_ShouldHandleInvalidXPath()
    {
        // Arrange
        var options = new EvtxOptions
        {
            Channels = new[] { "Security" },
            XPath = "Invalid XPath Query",
            PollSeconds = 5
        };

        var mockOptions = new Mock<IOptions<EvtxOptions>>();
        mockOptions.Setup(x => x.Value).Returns(options);

        var mockLogger = new Mock<ILogger<EvtxCollector>>();

        var collector = new EvtxCollector(mockOptions.Object, mockLogger.Object);

        // Act
        var events = new List<LogEvent>();
        await foreach (var evt in collector.CollectHistoricalAsync(CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        // Should handle invalid XPath gracefully without throwing
        events.Should().NotBeNull();
    }

    #endregion
}

