using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Configuration;
using Castellan.Worker.Services;
using Moq;
using Xunit;

namespace Castellan.Tests.Services;

public class PerformanceMonitorServiceTests
{
    private readonly Mock<ILogger<PerformanceMonitorService>> _mockLogger;
    private readonly PerformanceMonitorOptions _options;
    private readonly PerformanceMonitorService _service;

    public PerformanceMonitorServiceTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceMonitorService>>();
        
        _options = new PerformanceMonitorOptions
        {
            Enabled = true,
            LogMetrics = false, // Disable logging for tests
            RetentionMinutes = 60,
            EnableCleanup = true
        };

        var optionsWrapper = Options.Create(_options);
        _service = new PerformanceMonitorService(optionsWrapper, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldCreateService()
    {
        // Act & Assert
        _service.Should().NotBeNull();
    }

    [Fact]
    public void RecordPipelineMetrics_WithValidData_ShouldRecordMetrics()
    {
        // Arrange
        var processingTime = 150.5;
        var eventsProcessed = 5;
        var queueDepth = 10;

        // Act
        _service.RecordPipelineMetrics(processingTime, eventsProcessed, queueDepth);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.Pipeline.TotalEventsProcessed.Should().Be(5);
        metrics.Pipeline.QueueDepth.Should().Be(10);
        metrics.Pipeline.ProcessingErrors.Should().Be(0);
    }

    [Fact]
    public void RecordPipelineMetrics_WithError_ShouldRecordError()
    {
        // Arrange
        var processingTime = 150.5;
        var eventsProcessed = 0;
        var queueDepth = 5;
        var error = "Test error message";

        // Act
        _service.RecordPipelineMetrics(processingTime, eventsProcessed, queueDepth, error);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.Pipeline.ProcessingErrors.Should().Be(1);
        metrics.Pipeline.LastError.Should().Be(error);
        metrics.Pipeline.LastErrorTime.Should().NotBeNull();
    }

    [Fact]
    public void RecordEventCollectionMetrics_WithValidData_ShouldRecordMetrics()
    {
        // Arrange
        var channel = "Security";
        var eventsCollected = 10;
        var collectionTime = 250.0;
        var duplicatesFiltered = 2;

        // Act
        _service.RecordEventCollectionMetrics(channel, eventsCollected, collectionTime, duplicatesFiltered);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.EventCollection.EventsPerChannel.Should().ContainKey(channel);
        metrics.EventCollection.EventsPerChannel[channel].Should().Be(10);
        metrics.EventCollection.DuplicateEventsFiltered.Should().Be(2);
    }

    [Fact]
    public void RecordVectorStoreMetrics_WithValidData_ShouldRecordMetrics()
    {
        // Arrange
        var embeddingTime = 100.0;
        var upsertTime = 50.0;
        var searchTime = 75.0;
        var vectorsProcessed = 1;

        // Act
        _service.RecordVectorStoreMetrics(embeddingTime, upsertTime, searchTime, vectorsProcessed);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.VectorStore.TotalVectors.Should().Be(1);
        metrics.VectorStore.AverageEmbeddingTimeMs.Should().Be(100.0);
        metrics.VectorStore.AverageUpsertTimeMs.Should().Be(50.0);
        metrics.VectorStore.AverageSearchTimeMs.Should().Be(75.0);
    }

    [Fact]
    public void RecordSecurityDetection_WithValidData_ShouldRecordMetrics()
    {
        // Arrange
        var eventType = "AuthenticationFailure";
        var riskLevel = "high";
        var confidence = 90;
        var isDeterministic = true;
        var isCorrelationBased = false;

        // Act
        _service.RecordSecurityDetection(eventType, riskLevel, confidence, isDeterministic, isCorrelationBased);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.SecurityDetection.DeterministicDetections.Should().Be(1);
        metrics.SecurityDetection.LlmDetections.Should().Be(0);
        metrics.SecurityDetection.HighRiskDetections.Should().Be(1);
        metrics.SecurityDetection.EventsByType.Should().ContainKey(eventType);
        metrics.SecurityDetection.EventsByRiskLevel.Should().ContainKey(riskLevel);
    }

    [Fact]
    public void RecordLlmMetrics_WithValidData_ShouldRecordMetrics()
    {
        // Arrange
        var provider = "Ollama";
        var model = "llama3.1";
        var responseTime = 2500.0;
        var tokens = 150;
        var success = true;

        // Act
        _service.RecordLlmMetrics(provider, model, responseTime, tokens, success);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.Llm.TotalRequests.Should().Be(1);
        metrics.Llm.Provider.Should().Be(provider);
        metrics.Llm.Model.Should().Be(model);
        metrics.Llm.SuccessfulResponses.Should().Be(1);
        metrics.Llm.FailedResponses.Should().Be(0);
        metrics.Llm.AverageResponseTimeMs.Should().Be(2500.0);
    }

    [Fact]
    public void RecordNotificationMetrics_WithValidData_ShouldRecordMetrics()
    {
        // Arrange
        var notificationType = "Desktop";
        var riskLevel = "high";
        var deliveryTime = 100.0;
        var success = true;

        // Act
        _service.RecordNotificationMetrics(notificationType, riskLevel, deliveryTime, success);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.Notifications.DesktopNotificationsSent.Should().Be(1);
        metrics.Notifications.NotificationFailures.Should().Be(0);
        metrics.Notifications.AverageDeliveryTimeMs.Should().Be(100.0);
        metrics.Notifications.NotificationsByRiskLevel.Should().ContainKey(riskLevel);
    }

    [Fact]
    public void RecordNotificationMetrics_WithFailure_ShouldRecordFailure()
    {
        // Arrange
        var notificationType = "Desktop";
        var riskLevel = "critical";
        var deliveryTime = 150.0;
        var success = false;

        // Act
        _service.RecordNotificationMetrics(notificationType, riskLevel, deliveryTime, success);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.Notifications.DesktopNotificationsSent.Should().Be(0);
        metrics.Notifications.NotificationFailures.Should().Be(1);
    }

    [Fact]
    public void GetCurrentMetrics_ShouldReturnValidMetrics()
    {
        // Arrange
        _service.RecordPipelineMetrics(100.0, 5, 2);
        _service.RecordSecurityDetection("PowerShellExecution", "medium", 75, true, false);

        // Act
        var metrics = _service.GetCurrentMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        metrics.Pipeline.Should().NotBeNull();
        metrics.SecurityDetection.Should().NotBeNull();
        metrics.System.Should().NotBeNull();
    }

    [Fact]
    public void ResetMetrics_ShouldClearAllMetrics()
    {
        // Arrange
        _service.RecordPipelineMetrics(100.0, 5, 2);
        _service.RecordSecurityDetection("PowerShellExecution", "medium", 75, true, false);

        // Act
        _service.ResetMetrics();

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.Pipeline.TotalEventsProcessed.Should().Be(0);
        metrics.Pipeline.ProcessingErrors.Should().Be(0);
        metrics.Pipeline.LastError.Should().BeNull();
    }

    [Fact]
    public async Task ExportMetricsAsync_ShouldCreateFile()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        _service.RecordPipelineMetrics(100.0, 5, 2);

        try
        {
            // Act
            await _service.ExportMetricsAsync(tempFile);

            // Assert
            File.Exists(tempFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(tempFile);
            content.Should().Contain("Pipeline");
            content.Should().Contain("TotalEventsProcessed");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void RecordSecurityDetection_PowerShellEvent_ShouldRecordCorrectly()
    {
        // Arrange
        var eventType = "PowerShellExecution";
        var riskLevel = "high";
        var confidence = 85;
        var isDeterministic = true;
        var isCorrelationBased = false;

        // Act
        _service.RecordSecurityDetection(eventType, riskLevel, confidence, isDeterministic, isCorrelationBased);

        // Assert
        var metrics = _service.GetCurrentMetrics();
        metrics.SecurityDetection.EventsByType.Should().ContainKey(eventType);
        metrics.SecurityDetection.EventsByType[eventType].Should().Be(1);
        metrics.SecurityDetection.HighRiskDetections.Should().Be(1);
        metrics.SecurityDetection.DeterministicDetections.Should().Be(1);
    }
}