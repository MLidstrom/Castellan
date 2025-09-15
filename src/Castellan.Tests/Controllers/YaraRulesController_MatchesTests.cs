using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using Castellan.Worker.Controllers;
using Castellan.Worker.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Castellan.Worker.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests.Controllers;

/// <summary>
/// Tests for YARA match-related endpoints in YaraRulesController
/// </summary>
[Collection("TestEnvironment")]
public class YaraRulesController_MatchesTests : IDisposable
{
    private readonly Mock<ILogger<YaraRulesController>> _mockLogger;
    private readonly Mock<IYaraRuleStore> _mockRuleStore;
    private readonly YaraRulesController _controller;

    public YaraRulesController_MatchesTests()
    {
        _mockLogger = new Mock<ILogger<YaraRulesController>>();
        _mockRuleStore = new Mock<IYaraRuleStore>();
        _controller = new YaraRulesController(_mockLogger.Object, _mockRuleStore.Object);
    }

    public void Dispose()
    {
        // Controllers don't implement IDisposable
    }

    #region GetMatches Tests

    [Fact]
    public async Task GetMatches_WithoutSecurityEventId_ReturnsRecentMatches()
    {
        // Arrange
        var recentMatches = new List<YaraMatch>
        {
            TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1"),
            TestDataFactory.CreateTestYaraMatch("rule2", "TestRule2"),
            TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1")
        };

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ReturnsAsync(recentMatches);

        // Act
        var result = await _controller.GetMatches();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var totalProperty = response.GetType().GetProperty("total");
        var returnedMatches = dataProperty!.GetValue(response) as IEnumerable<YaraMatch>;
        var total = (int)totalProperty!.GetValue(response)!;
        
        returnedMatches.Should().NotBeNull();
        returnedMatches.Should().HaveCount(3);
        total.Should().Be(3);
        returnedMatches.Should().BeEquivalentTo(recentMatches);

        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(100), Times.Once);
        _mockRuleStore.Verify(x => x.GetMatchesBySecurityEventAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetMatches_WithSecurityEventId_ReturnsMatchesForEvent()
    {
        // Arrange
        var securityEventId = "security-event-123";
        var match1 = TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1");
        match1.SecurityEventId = securityEventId;
        var match2 = TestDataFactory.CreateTestYaraMatch("rule2", "TestRule2");
        match2.SecurityEventId = securityEventId;
        
        var eventMatches = new List<YaraMatch> { match1, match2 };

        _mockRuleStore.Setup(x => x.GetMatchesBySecurityEventAsync(securityEventId))
                     .ReturnsAsync(eventMatches);

        // Act
        var result = await _controller.GetMatches(securityEventId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var totalProperty = response.GetType().GetProperty("total");
        var returnedMatches = dataProperty!.GetValue(response) as IEnumerable<YaraMatch>;
        var total = (int)totalProperty!.GetValue(response)!;
        
        returnedMatches.Should().NotBeNull();
        returnedMatches.Should().HaveCount(2);
        total.Should().Be(2);
        returnedMatches.Should().BeEquivalentTo(eventMatches);

        _mockRuleStore.Verify(x => x.GetMatchesBySecurityEventAsync(securityEventId), Times.Once);
        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetMatches_WithCustomLimit_UsesSpecifiedLimit()
    {
        // Arrange
        var limit = 50;
        var recentMatches = new List<YaraMatch>
        {
            TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1"),
            TestDataFactory.CreateTestYaraMatch("rule2", "TestRule2")
        };

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(limit))
                     .ReturnsAsync(recentMatches);

        // Act
        var result = await _controller.GetMatches(count: limit);
        
        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var totalProperty = response.GetType().GetProperty("total");
        var returnedMatches = dataProperty!.GetValue(response) as IEnumerable<YaraMatch>;
        var total = (int)totalProperty!.GetValue(response)!;
        
        returnedMatches.Should().NotBeNull();
        returnedMatches.Should().HaveCount(2);
        total.Should().Be(2);
        
        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(limit), Times.Once);
    }

    [Fact]
    public async Task GetMatches_EmptyResults_ReturnsEmptyCollection()
    {
        // Arrange
        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ReturnsAsync(new List<YaraMatch>());

        // Act
        var result = await _controller.GetMatches();
        
        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var totalProperty = response.GetType().GetProperty("total");
        var returnedMatches = dataProperty!.GetValue(response) as IEnumerable<YaraMatch>;
        var total = (int)totalProperty!.GetValue(response)!;
        
        returnedMatches.Should().NotBeNull();
        returnedMatches.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetMatches_StoreThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await _controller.GetMatches();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().BeEquivalentTo(new { message = "Internal server error" });

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error getting YARA matches")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMatches_WithSecurityEventIdNotFound_ReturnsEmptyCollection()
    {
        // Arrange
        var securityEventId = "non-existent-event-id";
        _mockRuleStore.Setup(x => x.GetMatchesBySecurityEventAsync(securityEventId))
                     .ReturnsAsync(new List<YaraMatch>());

        // Act
        var result = await _controller.GetMatches(securityEventId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var totalProperty = response.GetType().GetProperty("total");
        var returnedMatches = dataProperty!.GetValue(response) as IEnumerable<YaraMatch>;
        var total = (int)totalProperty!.GetValue(response)!;
        
        returnedMatches.Should().NotBeNull();
        returnedMatches.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetMatches_WithInvalidLimit_PassesThroughLimit(int invalidLimit)
    {
        // Arrange
        var recentMatches = new List<YaraMatch>
        {
            TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1")
        };

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(invalidLimit))
                     .ReturnsAsync(recentMatches);

        // Act
        var result = await _controller.GetMatches(count: invalidLimit);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        // The controller currently passes through all limits, including invalid ones
        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(invalidLimit), Times.Once);
    }

    [Fact]
    public async Task GetMatches_WithLargeLimit_UsesSpecifiedLimit()
    {
        // Arrange
        var largeLimit = 1000;
        var recentMatches = new List<YaraMatch>();

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(largeLimit))
                     .ReturnsAsync(recentMatches);

        // Act
        var result = await _controller.GetMatches(count: largeLimit);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(largeLimit), Times.Once);
    }

    [Fact]
    public async Task GetMatches_MatchesWithAllProperties_ReturnsCompleteData()
    {
        // Arrange
        var completeMatch = new YaraMatch
        {
            Id = "complete-match-id",
            RuleName = "CompleteRule",
            TargetFile = "/path/to/suspicious/file.exe",
            MatchedStrings = new List<YaraMatchString>
            {
                new() { Identifier = "$string1", Value = "malware_pattern", Offset = 1024 },
                new() { Identifier = "$string2", Value = "suspicious_behavior", Offset = 2048 }
            },
            SecurityEventId = "security-event-456",
            MatchTime = new DateTime(2025, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            TargetHash = "sha256_hash_value",
            ExecutionTimeMs = 15.5,
            Metadata = new Dictionary<string, string>
            {
                { "process_id", "1234" },
                { "process_name", "suspicious_process.exe" }
            }
        };

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ReturnsAsync(new List<YaraMatch> { completeMatch });

        // Act
        var result = await _controller.GetMatches();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var returnedMatches = dataProperty!.GetValue(response) as IEnumerable<YaraMatch>;
        var firstMatch = returnedMatches!.First();

        firstMatch.Should().BeEquivalentTo(completeMatch);
        firstMatch.MatchedStrings.Should().HaveCount(2);
        firstMatch.MatchedStrings.Should().Contain(ms => ms.Identifier == "$string1" && ms.Value == "malware_pattern");
        firstMatch.MatchedStrings.Should().Contain(ms => ms.Identifier == "$string2" && ms.Value == "suspicious_behavior");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetMatches_NullSecurityEventId_TreatsAsRecentMatches()
    {
        // Arrange
        var recentMatches = new List<YaraMatch>
        {
            TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1")
        };

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ReturnsAsync(recentMatches);

        // Act
        var result = await _controller.GetMatches(securityEventId: null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(100), Times.Once);
        _mockRuleStore.Verify(x => x.GetMatchesBySecurityEventAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetMatches_EmptyStringSecurityEventId_TreatsAsRecentMatches()
    {
        // Arrange
        var recentMatches = new List<YaraMatch>
        {
            TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1")
        };

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ReturnsAsync(recentMatches);

        // Act
        var result = await _controller.GetMatches(securityEventId: "");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(100), Times.Once);
        _mockRuleStore.Verify(x => x.GetMatchesBySecurityEventAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetMatches_WhitespaceSecurityEventId_TreatsAsRecentMatches()
    {
        // Arrange
        var recentMatches = new List<YaraMatch>
        {
            TestDataFactory.CreateTestYaraMatch("rule1", "TestRule1")
        };

        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ReturnsAsync(recentMatches);

        // Act
        var result = await _controller.GetMatches(securityEventId: "   ");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockRuleStore.Verify(x => x.GetRecentMatchesAsync(100), Times.Once);
        _mockRuleStore.Verify(x => x.GetMatchesBySecurityEventAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetMatches_StoreReturnsNull_HandlesGracefully()
    {
        // Arrange
        _mockRuleStore.Setup(x => x.GetRecentMatchesAsync(It.IsAny<int>()))
                     .ReturnsAsync((List<YaraMatch>)null!);

        // Act
        var result = await _controller.GetMatches();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var response = okResult!.Value;
        var dataProperty = response!.GetType().GetProperty("data");
        var totalProperty = response.GetType().GetProperty("total");
        var returnedMatches = dataProperty!.GetValue(response) as IEnumerable<YaraMatch>;
        var total = (int)totalProperty!.GetValue(response)!;
        
        returnedMatches.Should().NotBeNull();
        returnedMatches.Should().BeEmpty();
        total.Should().Be(0);
    }

    #endregion
}
