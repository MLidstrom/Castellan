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
using Castellan.Worker.Abstractions;
using Castellan.Worker.Llms;
using Castellan.Worker.Models;
using Castellan.Worker.Options;

namespace Castellan.Tests.Llms;

/// <summary>
/// Unit tests for StrictJsonLlmClient decorator.
/// Tests JSON extraction, validation, retry logic, and fallback generation.
/// </summary>
public class StrictJsonLlmClientTests
{
    private readonly Mock<ILlmClient> _mockInnerClient;
    private readonly Mock<ILogger<StrictJsonLlmClient>> _mockLogger;
    private readonly StrictJsonOptions _options;
    private readonly LogEvent _testEvent;

    public StrictJsonLlmClientTests()
    {
        _mockInnerClient = new Mock<ILlmClient>();
        _mockLogger = new Mock<ILogger<StrictJsonLlmClient>>();
        _options = new StrictJsonOptions
        {
            Enabled = true,
            EnableRetryOnFailure = true,
            EnableFallbackGeneration = true,
            MaxRetryAttempts = 1,
            MinConfidenceThreshold = 0
        };

        _testEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4625,
            "Test failed login",
            "testuser",
            "{}",
            Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task AnalyzeAsync_ValidJsonResponse_ShouldReturnJsonDirectly()
    {
        // Arrange
        var client = CreateClient();
        var validJson = "{\"risk\":\"high\",\"confidence\":85,\"summary\":\"Failed login attempt\",\"mitre\":[\"T1110\"]}";
        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validJson);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().Be(validJson);
        _mockInnerClient.Verify(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(1);
        stats.SuccessfulParses.Should().Be(1);
        stats.FailedParses.Should().Be(0);
        stats.ParseSuccessRate.Should().Be(1.0f);
    }

    [Fact]
    public async Task AnalyzeAsync_JsonInMarkdownCodeBlock_ShouldExtractJson()
    {
        // Arrange
        var client = CreateClient();
        var markdownResponse = @"Here's the analysis:
```json
{""risk"":""medium"",""confidence"":70,""summary"":""Suspicious activity detected""}
```
Hope this helps!";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(markdownResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().Contain("\"risk\":\"medium\"");
        result.Should().Contain("\"summary\":\"Suspicious activity detected\"");
        result.Should().NotContain("Here's the analysis");
        result.Should().NotContain("```");

        var stats = client.GetStatistics();
        stats.SuccessfulParses.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_JsonWithoutCodeBlock_ShouldExtractJson()
    {
        // Arrange
        var client = CreateClient();
        var mixedResponse = @"Analysis complete. The event shows: {""risk"":""low"",""confidence"":50,""summary"":""Normal activity""} and no further action needed.";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mixedResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().Contain("\"risk\":\"low\"");
        result.Should().Contain("\"summary\":\"Normal activity\"");
        result.Should().NotContain("Analysis complete");

        var stats = client.GetStatistics();
        stats.SuccessfulParses.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidJson_ShouldRetryAndUseFallback()
    {
        // Arrange
        var client = CreateClient();
        var invalidResponse = "This is not valid JSON at all!";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert - should use fallback
        result.Should().Contain("\"risk\":\"low\""); // Fallback risk
        result.Should().Contain("\"confidence\":25"); // Low confidence for fallback
        result.Should().Contain("\"summary\":");

        // Should have called inner client twice (original + retry)
        _mockInnerClient.Verify(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(1);
        stats.FailedParses.Should().Be(1);
        stats.RetriedCalls.Should().Be(1);
        stats.FallbackUsed.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_MissingRequiredFields_ShouldRetryAndUseFallback()
    {
        // Arrange
        var client = CreateClient();
        var incompleteJson = "{\"confidence\":90}"; // Missing 'risk' and 'summary'

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(incompleteJson);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert - should use fallback because required fields missing
        result.Should().Contain("\"risk\":");
        result.Should().Contain("\"summary\":");

        var stats = client.GetStatistics();
        stats.RetriedCalls.Should().Be(1);
        stats.FallbackUsed.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyResponse_ShouldUseFallback()
    {
        // Arrange
        var client = CreateClient();

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().Contain("\"risk\":");
        result.Should().Contain("\"summary\":");

        var stats = client.GetStatistics();
        stats.FallbackUsed.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_WithRetryDisabled_ShouldNotRetry()
    {
        // Arrange
        _options.EnableRetryOnFailure = false;
        var client = CreateClient();
        var invalidResponse = "Not JSON";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert - should use fallback without retry
        _mockInnerClient.Verify(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()), Times.Once); // No retry

        var stats = client.GetStatistics();
        stats.RetriedCalls.Should().Be(0); // Retry disabled
        stats.FallbackUsed.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_WithStrictJsonDisabled_ShouldPassThrough()
    {
        // Arrange
        _options.Enabled = false;
        var client = CreateClient();
        var anyResponse = "Any response format";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(anyResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert - should return exactly what inner client returned
        result.Should().Be(anyResponse);

        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(1);
        // No parsing statistics tracked when disabled
    }

    [Fact]
    public async Task AnalyzeAsync_PartialJsonWithSummary_ShouldExtractSummaryForFallback()
    {
        // Arrange
        var client = CreateClient();
        var partialResponse = "Here's what I found: \"summary\": \"Critical security breach detected\" but the JSON is incomplete";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(partialResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert - fallback should extract summary from partial response
        result.Should().Contain("Critical security breach detected");

        var stats = client.GetStatistics();
        stats.FallbackUsed.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleSuccessfulParses_ShouldTrackStatistics()
    {
        // Arrange
        var client = CreateClient();
        var validJson = "{\"risk\":\"low\",\"confidence\":60,\"summary\":\"Test event\"}";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(validJson);

        // Act
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(3);
        stats.SuccessfulParses.Should().Be(3);
        stats.FailedParses.Should().Be(0);
        stats.ParseSuccessRate.Should().Be(1.0f);
    }

    [Fact]
    public async Task AnalyzeAsync_MixedSuccessAndFailure_ShouldCalculateCorrectSuccessRate()
    {
        // Arrange
        var client = CreateClient();
        var callCount = 0;

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First and third calls return valid JSON, second returns invalid
                if (callCount == 1 || callCount >= 5) // Account for retry on call 2
                    return "{\"risk\":\"low\",\"summary\":\"Valid\"}";
                else
                    return "Invalid";
            });

        // Act
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None); // Success
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None); // Fail (with retry)
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None); // Success

        // Assert
        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(3);
        stats.SuccessfulParses.Should().Be(2); // Calls 1 and 3
        stats.FailedParses.Should().Be(1); // Call 2
        stats.ParseSuccessRate.Should().BeApproximately(0.67f, 0.01f); // 2/3
    }

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeroValues()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var stats = client.GetStatistics();

        // Assert
        stats.TotalCalls.Should().Be(0);
        stats.SuccessfulParses.Should().Be(0);
        stats.FailedParses.Should().Be(0);
        stats.RetriedCalls.Should().Be(0);
        stats.FallbackUsed.Should().Be(0);
        stats.ParseSuccessRate.Should().Be(0.0f);
    }

    private StrictJsonLlmClient CreateClient()
    {
        return new StrictJsonLlmClient(
            _mockInnerClient.Object,
            Options.Create(_options),
            _mockLogger.Object);
    }
}
