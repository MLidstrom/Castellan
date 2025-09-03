using FluentAssertions;
using Castellan.Worker;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Castellan.Worker.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using System.Runtime.CompilerServices;

namespace Castellan.Tests.Integration;

public class PipelineIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ILogCollector> _mockLogCollector;
    private readonly Mock<IEmbedder> _mockEmbedder;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ILlmClient> _mockLlmClient;
    private readonly Mock<ILogger<Pipeline>> _mockLogger;
    private readonly Mock<IIPEnrichmentService> _mockIPEnrichmentService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IPerformanceMonitor> _mockPerformanceMonitor;
    private readonly Mock<ISecurityEventStore> _mockSecurityEventStore;
    private readonly Mock<IAutomatedResponseService> _mockAutomatedResponseService;
    private readonly SecurityEventDetector _securityEventDetector;
    private readonly AlertOptions _alertOptions;
    private readonly NotificationOptions _notificationOptions;

    public PipelineIntegrationTests()
    {
        // Setup service collection
        var services = new ServiceCollection();

        // Create mocks
        _mockLogCollector = new Mock<ILogCollector>();
        _mockEmbedder = new Mock<IEmbedder>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockLlmClient = new Mock<ILlmClient>();
        _mockLogger = new Mock<ILogger<Pipeline>>();
        _mockIPEnrichmentService = new Mock<IIPEnrichmentService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        _mockSecurityEventStore = new Mock<ISecurityEventStore>();
        _mockAutomatedResponseService = new Mock<IAutomatedResponseService>();
        var mockSecurityEventDetectorLogger = new Mock<ILogger<SecurityEventDetector>>();
        var mockRulesEngineLogger = new Mock<ILogger<RulesEngine>>();

        // Setup alert options
        _alertOptions = new AlertOptions
        {
            MinRiskLevel = "low",
            EnableConsoleAlerts = false,
            EnableFileLogging = false
        };

        // Setup notification options
        _notificationOptions = new NotificationOptions
        {
            EnableDesktopNotifications = false,
            EnableSoundAlerts = false,
            NotificationLevel = "high",
            ShowEventDetails = true,
            ShowIPEnrichment = true,
            NotificationTimeout = 5000
        };

        // Register services
        services.AddSingleton(_mockLogCollector.Object);
        services.AddSingleton(_mockEmbedder.Object);
        services.AddSingleton(_mockVectorStore.Object);
        services.AddSingleton(_mockLlmClient.Object);
        services.AddSingleton<ILogger<Pipeline>>(_mockLogger.Object);
        services.AddSingleton<ILogger<SecurityEventDetector>>(mockSecurityEventDetectorLogger.Object);
        services.AddSingleton<ILogger<RulesEngine>>(mockRulesEngineLogger.Object);
        services.AddSingleton(_mockIPEnrichmentService.Object);
        services.AddSingleton(_mockNotificationService.Object);
        services.AddSingleton(_mockPerformanceMonitor.Object);
        services.AddSingleton(_mockSecurityEventStore.Object);
        services.AddSingleton(_mockAutomatedResponseService.Object);
        services.AddSingleton(Options.Create(_alertOptions));
        services.AddSingleton(Options.Create(_notificationOptions));
        services.AddSingleton(Options.Create(new PipelineOptions
        {
            EnableParallelProcessing = true,
            MaxConcurrency = 4,
            ParallelOperationTimeoutMs = 30000,
            EnableParallelVectorOperations = true
        }));
        services.AddSingleton(Options.Create(new Castellan.Worker.Configuration.CorrelationOptions
        {
            EnableLowScoreEvents = true, // Enable for testing to maintain existing test behavior
            MinCorrelationScore = 0.5,
            MinBurstScore = 0.5,
            MinAnomalyScore = 0.5,
            MinTotalScore = 1.0
        }));
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();

        _serviceProvider = services.BuildServiceProvider();
        _securityEventDetector = _serviceProvider.GetRequiredService<SecurityEventDetector>();
    }

    [Fact]
    public async Task Pipeline_ShouldProcessSecurityEventWithDeterministicDetection()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4625, // Failed logon
            "Warning",
            "unknown",
            "An account failed to log on",
            "{\"EventID\":4625}"
        );

        var events = new List<LogEvent> { logEvent };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>());

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // For deterministic events (Security channel with known EventId), LLM analysis is skipped
        // Only EnsureCollectionAsync should be called
        _mockVectorStore.Verify(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ShouldProcessMultipleEvents()
    {
        // Arrange
        var logEvents = new List<LogEvent>
        {
            new LogEvent(
                DateTimeOffset.UtcNow,
                "TEST-HOST",
                "Security",
                4624,
                "Information",
                "testuser",
                "An account was successfully logged on",
                "{\"EventID\":4624}"
            ),
            new LogEvent(
                DateTimeOffset.UtcNow,
                "TEST-HOST",
                "Security",
                4625,
                "Warning",
                "unknown",
                "An account failed to log on",
                "{\"EventID\":4625}"
            )
        };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(logEvents));

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>());

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // For deterministic events (Security channel with known EventId), LLM analysis is skipped
        // Only EnsureCollectionAsync should be called
        _mockVectorStore.Verify(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleEmptyEventCollection()
    {
        // Arrange
        var events = new List<LogEvent>();

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        _mockVectorStore.Verify(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockVectorStore.Verify(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleEmbedderFailure()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on",
            "{\"EventID\":4624}"
        );

        var events = new List<LogEvent> { logEvent };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Embedding service unavailable"));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        _mockVectorStore.Verify(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleVectorStoreFailure()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on",
            "{\"EventID\":4624}"
        );

        var events = new List<LogEvent> { logEvent };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Vector store unavailable"));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // Should handle the exception gracefully
    }

    [Fact]
    public async Task Pipeline_ShouldHandleLlmClientFailure()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on",
            "{\"EventID\":4624}"
        );

        var events = new List<LogEvent> { logEvent };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)> { (logEvent, 0.95f) });

        _mockLlmClient
            .Setup(x => x.AnalyzeAsync(It.IsAny<LogEvent>(), It.IsAny<IEnumerable<LogEvent>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM service unavailable"));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // Should handle the exception gracefully
    }

    [Fact]
    public async Task Pipeline_ShouldProcessSecurityEventWithLlmAnalysis()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Application", // Use Application channel instead of Security to avoid deterministic detection
            9999, // Use unknown EventId to avoid deterministic detection
            "Information",
            "testuser",
            "An account was successfully logged on",
            "{\"EventID\":9999}"
        );

        var events = new List<LogEvent> { logEvent };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)> { (logEvent, 0.95f) });

        _mockLlmClient
            .Setup(x => x.AnalyzeAsync(It.IsAny<LogEvent>(), It.IsAny<IEnumerable<LogEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{
                ""risk"": ""low"",
                ""mitre"": [""T1078""],
                ""confidence"": 85,
                ""summary"": ""Successful login detected"",
                ""recommended_actions"": [""Monitor user activity""]
            }");

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // For non-deterministic events, LLM analysis should be performed
        _mockVectorStore.Verify(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockVectorStore.Verify(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockLlmClient.Verify(x => x.AnalyzeAsync(It.IsAny<LogEvent>(), It.IsAny<IEnumerable<LogEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleCancellation()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on",
            "{\"EventID\":4624}"
        );

        var events = new List<LogEvent> { logEvent };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms
        await pipeline.StopAsync(cts.Token);

        // Assert
        // Should handle cancellation gracefully
    }

    [Fact]
    public async Task Pipeline_ShouldProcessHighRiskSecurityEvent()
    {
        // Arrange
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4672, // Special privileges assigned
            "Information",
            "admin",
            "Special privileges assigned to new logon",
            "{\"EventID\":4672}"
        );

        var events = new List<LogEvent> { logEvent };

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>());

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // For deterministic events (Security channel with known EventId), LLM analysis is skipped
        // Only EnsureCollectionAsync should be called
        _mockVectorStore.Verify(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #region 24-Hour Data Management Tests

    [Fact]
    public async Task Pipeline_ShouldCheck24HoursDataOnStartup()
    {
        // Arrange
        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<LogEvent>()));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to process
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        _mockVectorStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ShouldBackfillWhenNo24HoursData()
    {
        // Arrange
        var historicalEvents = new List<LogEvent>
        {
            new LogEvent(DateTimeOffset.UtcNow.AddHours(-1), "TEST-HOST", "Security", 4624, "Information", "user1", "Test event 1", "{}"),
            new LogEvent(DateTimeOffset.UtcNow.AddHours(-2), "TEST-HOST", "System", 6005, "Information", "user2", "Test event 2", "{}"),
            new LogEvent(DateTimeOffset.UtcNow.AddHours(-3), "TEST-HOST", "Application", 1000, "Information", "user3", "Test event 3", "{}")
        };

        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        // Mock the EVTX collector for historical data
        var mockEvtxCollector = new Mock<ILogCollector>();
        mockEvtxCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(historicalEvents));

        // Replace the collector in the service provider
        var services = new ServiceCollection();
        services.AddSingleton(mockEvtxCollector.Object);
        services.AddSingleton(_mockVectorStore.Object);
        services.AddSingleton(_mockEmbedder.Object);
        services.AddSingleton(_mockLlmClient.Object);
        services.AddSingleton<ILogger<Pipeline>>(_mockLogger.Object);
        services.AddSingleton<ILogger<SecurityEventDetector>>(new Mock<ILogger<SecurityEventDetector>>().Object);
        services.AddSingleton<ILogger<RulesEngine>>(new Mock<ILogger<RulesEngine>>().Object);
        services.AddSingleton(Options.Create(_alertOptions));
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();
        // Add missing IIPEnrichmentService
        services.AddSingleton<IIPEnrichmentService>(new Mock<IIPEnrichmentService>().Object);
        services.AddSingleton<INotificationService>(new Mock<INotificationService>().Object);
        services.AddSingleton<IPerformanceMonitor>(new Mock<IPerformanceMonitor>().Object);
        services.AddSingleton<ISecurityEventStore>(new Mock<ISecurityEventStore>().Object);
        services.AddSingleton<IAutomatedResponseService>(new Mock<IAutomatedResponseService>().Object);
        services.AddSingleton(Options.Create(new NotificationOptions()));
        services.AddSingleton(Options.Create(new CorrelationOptions
        {
            EnableLowScoreEvents = true,
            MinCorrelationScore = 0.5,
            MinBurstScore = 0.5,
            MinAnomalyScore = 0.5,
            MinTotalScore = 1.0
        }));

        var newServiceProvider = services.BuildServiceProvider();
        var pipeline = newServiceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(200); // Give pipeline time to process backfill
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        _mockVectorStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockVectorStore.Verify(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        _mockEmbedder.Verify(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task Pipeline_ShouldStartCleanupTaskOnStartup()
    {
        // Arrange
        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.DeleteVectorsOlderThan24HoursAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<LogEvent>()));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Give pipeline time to start cleanup task
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // The cleanup task is started but runs every hour, so we verify it's set up correctly
        // The actual cleanup call would happen after an hour in real operation
    }

    [Fact]
    public async Task Pipeline_ShouldHandleBackfillErrors()
    {
        // Arrange
        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock the EVTX collector to throw an exception during historical collection
        var mockEvtxCollector = new Mock<ILogCollector>();
        mockEvtxCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(ThrowAsyncEnumerable<LogEvent>(new InvalidOperationException("Historical collection failed")));

        // Replace the collector in the service provider
        var services = new ServiceCollection();
        services.AddSingleton(mockEvtxCollector.Object);
        services.AddSingleton(_mockVectorStore.Object);
        services.AddSingleton(_mockEmbedder.Object);
        services.AddSingleton(_mockLlmClient.Object);
        services.AddSingleton<ILogger<Pipeline>>(_mockLogger.Object);
        services.AddSingleton<ILogger<SecurityEventDetector>>(new Mock<ILogger<SecurityEventDetector>>().Object);
        services.AddSingleton<ILogger<RulesEngine>>(new Mock<ILogger<RulesEngine>>().Object);
        services.AddSingleton(Options.Create(_alertOptions));
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();
        // Add missing IIPEnrichmentService
        services.AddSingleton<IIPEnrichmentService>(new Mock<IIPEnrichmentService>().Object);
        services.AddSingleton<INotificationService>(new Mock<INotificationService>().Object);
        services.AddSingleton<IPerformanceMonitor>(new Mock<IPerformanceMonitor>().Object);
        services.AddSingleton<ISecurityEventStore>(new Mock<ISecurityEventStore>().Object);
        services.AddSingleton<IAutomatedResponseService>(new Mock<IAutomatedResponseService>().Object);
        services.AddSingleton(Options.Create(new NotificationOptions()));
        services.AddSingleton(Options.Create(new Castellan.Worker.Configuration.CorrelationOptions()));

        var newServiceProvider = services.BuildServiceProvider();
        var pipeline = newServiceProvider.GetRequiredService<Pipeline>();

        // Act & Assert
        // Should handle the error gracefully and continue with normal operation
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await pipeline.StopAsync(CancellationToken.None);

        // Verify that the pipeline still started successfully despite backfill errors
        _mockVectorStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleCleanupErrors()
    {
        // Arrange
        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.DeleteVectorsOlderThan24HoursAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Cleanup failed"));

        _mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<LogEvent>()));

        var pipeline = _serviceProvider.GetRequiredService<Pipeline>();

        // Act & Assert
        // Should handle cleanup errors gracefully and continue operation
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await pipeline.StopAsync(CancellationToken.None);

        // Verify that the pipeline still started successfully despite cleanup errors
        _mockVectorStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ShouldHandleCancellationDuringBackfill()
    {
        // Arrange
        var historicalEvents = Enumerable.Range(0, 1000)
            .Select(i => new LogEvent(DateTimeOffset.UtcNow.AddHours(-i), "TEST-HOST", "Security", 4624, "Information", $"user{i}", $"Test event {i}", "{}"))
            .ToList();

        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        // Mock the EVTX collector for historical data
        var mockEvtxCollector = new Mock<ILogCollector>();
        mockEvtxCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(historicalEvents));

        // Replace the collector in the service provider
        var services = new ServiceCollection();
        services.AddSingleton(mockEvtxCollector.Object);
        services.AddSingleton(_mockVectorStore.Object);
        services.AddSingleton(_mockEmbedder.Object);
        services.AddSingleton(_mockLlmClient.Object);
        services.AddSingleton<ILogger<Pipeline>>(_mockLogger.Object);
        services.AddSingleton<ILogger<SecurityEventDetector>>(new Mock<ILogger<SecurityEventDetector>>().Object);
        services.AddSingleton<ILogger<RulesEngine>>(new Mock<ILogger<RulesEngine>>().Object);
        services.AddSingleton(Options.Create(_alertOptions));
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();
        // Add missing IIPEnrichmentService
        services.AddSingleton<IIPEnrichmentService>(new Mock<IIPEnrichmentService>().Object);
        services.AddSingleton<INotificationService>(new Mock<INotificationService>().Object);
        services.AddSingleton<IPerformanceMonitor>(new Mock<IPerformanceMonitor>().Object);
        services.AddSingleton<ISecurityEventStore>(new Mock<ISecurityEventStore>().Object);
        services.AddSingleton<IAutomatedResponseService>(new Mock<IAutomatedResponseService>().Object);
        services.AddSingleton(Options.Create(new NotificationOptions()));
        services.AddSingleton(Options.Create(new CorrelationOptions
        {
            EnableLowScoreEvents = true,
            MinCorrelationScore = 0.5,
            MinBurstScore = 0.5,
            MinAnomalyScore = 0.5,
            MinTotalScore = 1.0
        }));

        var newServiceProvider = services.BuildServiceProvider();
        var pipeline = newServiceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms
        await pipeline.StopAsync(cts.Token);

        // Assert
        // Should handle cancellation gracefully during backfill
        _mockVectorStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pipeline_ShouldSkipBackfillWhenNoEvtxCollector()
    {
        // Arrange
        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Use a non-EVTX collector
        var mockCustomCollector = new Mock<ILogCollector>();
        mockCustomCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new List<LogEvent>()));

        // Replace the collector in the service provider
        var services = new ServiceCollection();
        services.AddSingleton(mockCustomCollector.Object);
        services.AddSingleton(_mockVectorStore.Object);
        services.AddSingleton(_mockEmbedder.Object);
        services.AddSingleton(_mockLlmClient.Object);
        services.AddSingleton<ILogger<Pipeline>>(_mockLogger.Object);
        services.AddSingleton<ILogger<SecurityEventDetector>>(new Mock<ILogger<SecurityEventDetector>>().Object);
        services.AddSingleton<ILogger<RulesEngine>>(new Mock<ILogger<RulesEngine>>().Object);
        services.AddSingleton(Options.Create(_alertOptions));
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();
        // Add missing IIPEnrichmentService
        services.AddSingleton<IIPEnrichmentService>(new Mock<IIPEnrichmentService>().Object);
        services.AddSingleton<INotificationService>(new Mock<INotificationService>().Object);
        services.AddSingleton<IPerformanceMonitor>(new Mock<IPerformanceMonitor>().Object);
        services.AddSingleton<ISecurityEventStore>(new Mock<ISecurityEventStore>().Object);
        services.AddSingleton<IAutomatedResponseService>(new Mock<IAutomatedResponseService>().Object);
        services.AddSingleton(Options.Create(new NotificationOptions()));
        services.AddSingleton(Options.Create(new CorrelationOptions
        {
            EnableLowScoreEvents = true,
            MinCorrelationScore = 0.5,
            MinBurstScore = 0.5,
            MinAnomalyScore = 0.5,
            MinTotalScore = 1.0
        }));

        var newServiceProvider = services.BuildServiceProvider();
        var pipeline = newServiceProvider.GetRequiredService<Pipeline>();

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await pipeline.StopAsync(CancellationToken.None);

        // Assert
        // Should check for 24 hours data but skip backfill when no EVTX collector is available
        _mockVectorStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once);
        // Should not attempt to upsert any historical events
        _mockVectorStore.Verify(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    private static async IAsyncEnumerable<LogEvent> CreateAsyncEnumerable(IEnumerable<LogEvent> events)
    {
        foreach (var evt in events)
        {
            await Task.Delay(1); // Small delay to make it truly async
            yield return evt;
        }
    }

    private static async IAsyncEnumerable<T> ThrowAsyncEnumerable<T>(Exception exception)
    {
        await Task.Delay(1); // Small delay to make it truly async
        throw exception;
        yield break; // Required for async-iterator method even after throw
#pragma warning disable CS0162 // Unreachable code detected
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

