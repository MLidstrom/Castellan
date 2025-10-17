using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Polly.CircuitBreaker;
using Xunit;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Llms;
using Castellan.Worker.Models;
using Castellan.Worker.Options;

namespace Castellan.Tests.Llms;

/// <summary>
/// Unit tests for ResilientLlmClient decorator.
/// Tests Polly resilience patterns: retry, circuit breaker, and timeout.
/// </summary>
public class ResilientLlmClientTests
{
    private readonly Mock<ILlmClient> _mockInnerClient;
    private readonly Mock<ILogger<ResilientLlmClient>> _mockLogger;
    private readonly ResilienceOptions _options;
    private readonly LogEvent _testEvent;

    public ResilientLlmClientTests()
    {
        _mockInnerClient = new Mock<ILlmClient>();
        _mockLogger = new Mock<ILogger<ResilientLlmClient>>();
        _options = new ResilienceOptions
        {
            LLM = new LlmResilienceOptions
            {
                Enabled = true,
                RetryCount = 3,
                RetryBaseDelayMs = 10, // Short for tests
                TimeoutSeconds = 30,
                CircuitBreakerThreshold = 2,
                CircuitBreakerDurationMinutes = 1
            }
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
    public async Task AnalyzeAsync_SuccessfulCall_ShouldReturnResultAndIncrementSuccessCount()
    {
        // Arrange
        var client = CreateClient();
        var testResponse = "{\"event_type\":\"UnauthorizedAccess\",\"risk\":\"high\"}";
        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().Be(testResponse);
        _mockInnerClient.Verify(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(1);
        stats.SuccessfulCalls.Should().Be(1);
        stats.FailedCalls.Should().Be(0);
        stats.SuccessRate.Should().Be(1.0f);
    }

    [Fact]
    public async Task AnalyzeAsync_TransientFailure_ShouldRetry()
    {
        // Arrange
        var client = CreateClient();
        var testResponse = "{\"event_type\":\"UnauthorizedAccess\"}";
        var callCount = 0;

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 2)
                    throw new HttpRequestException("Transient network error");
                return testResponse;
            });

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().Be(testResponse);
        callCount.Should().Be(2); // Failed once, then succeeded

        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(1);
        stats.SuccessfulCalls.Should().Be(1);
        stats.RetriedCalls.Should().Be(1); // One retry
    }

    [Fact]
    public async Task AnalyzeAsync_AllRetriesFail_ShouldReturnEmptyAndGracefullyDegrade()
    {
        // Arrange - use high circuit breaker threshold to allow all retries
        _options.LLM.CircuitBreakerThreshold = 10;
        var client = CreateClient();

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Persistent network error"));

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert - graceful degradation
        result.Should().BeEmpty();
        _mockInnerClient.Verify(
            x => x.AnalyzeAsync(
                It.IsAny<LogEvent>(),
                It.IsAny<IEnumerable<LogEvent>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(4)); // Original + 3 retries

        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(1);
        stats.SuccessfulCalls.Should().Be(0);
        stats.FailedCalls.Should().Be(1);
        stats.RetriedCalls.Should().Be(3); // 3 retries
        stats.SuccessRate.Should().Be(0.0f);
    }

    [Fact]
    public async Task AnalyzeAsync_WithResilienceDisabled_ShouldPassThrough()
    {
        // Arrange
        _options.LLM.Enabled = false;
        var client = CreateClient();
        var testResponse = "{\"event_type\":\"UnauthorizedAccess\"}";

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().Be(testResponse);
        _mockInnerClient.Verify(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsync_CircuitBreaker_ShouldOpenAfterThreshold()
    {
        // Arrange - circuit breaker threshold = 2
        var client = CreateClient();

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act - trigger failures to open circuit breaker
        var result1 = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);
        var result2 = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Wait for circuit breaker to evaluate state
        await Task.Delay(100);

        var result3 = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result1.Should().BeEmpty(); // Failed after retries
        result2.Should().BeEmpty(); // Failed after retries
        result3.Should().BeEmpty(); // Rejected by circuit breaker

        var stats = client.GetStatistics();
        stats.FailedCalls.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyResponse_ShouldRetry()
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
                if (callCount < 2)
                    return ""; // Empty result should trigger retry
                return "{\"event_type\":\"test\"}";
            });

        // Act
        var result = await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        callCount.Should().Be(2); // Retried once

        var stats = client.GetStatistics();
        stats.RetriedCalls.Should().Be(1);
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
        stats.SuccessfulCalls.Should().Be(0);
        stats.FailedCalls.Should().Be(0);
        stats.RetriedCalls.Should().Be(0);
        stats.CircuitBreakerOpens.Should().Be(0);
        stats.Timeouts.Should().Be(0);
        stats.SuccessRate.Should().Be(0.0f);
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleSuccessfulCalls_ShouldTrackStatistics()
    {
        // Arrange
        var client = CreateClient();
        var testResponse = "{\"event_type\":\"test\"}";
        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResponse);

        // Act
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);
        await client.AnalyzeAsync(_testEvent, Enumerable.Empty<LogEvent>(), CancellationToken.None);

        // Assert
        var stats = client.GetStatistics();
        stats.TotalCalls.Should().Be(3);
        stats.SuccessfulCalls.Should().Be(3);
        stats.FailedCalls.Should().Be(0);
        stats.SuccessRate.Should().Be(1.0f);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNeighborEvents_ShouldPassToInner()
    {
        // Arrange
        var client = CreateClient();
        var testResponse = "{\"event_type\":\"test\"}";
        var neighbor1 = new LogEvent(
            DateTimeOffset.UtcNow,
            "HOST1",
            "Security",
            4624,
            "Login success",
            "user1",
            "{}",
            Guid.NewGuid().ToString());
        var neighbor2 = new LogEvent(
            DateTimeOffset.UtcNow,
            "HOST2",
            "Security",
            4672,
            "Special privileges",
            "user2",
            "{}",
            Guid.NewGuid().ToString());
        var neighbors = new[] { neighbor1, neighbor2 };

        _mockInnerClient.Setup(x => x.AnalyzeAsync(
            It.IsAny<LogEvent>(),
            It.IsAny<IEnumerable<LogEvent>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResponse);

        // Act
        var result = await client.AnalyzeAsync(_testEvent, neighbors, CancellationToken.None);

        // Assert
        result.Should().Be(testResponse);
        _mockInnerClient.Verify(x => x.AnalyzeAsync(
            It.Is<LogEvent>(e => e.EventId == 4625),
            It.Is<IEnumerable<LogEvent>>(n => n.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private ResilientLlmClient CreateClient()
    {
        return new ResilientLlmClient(
            _mockInnerClient.Object,
            Options.Create(_options),
            _mockLogger.Object);
    }
}
