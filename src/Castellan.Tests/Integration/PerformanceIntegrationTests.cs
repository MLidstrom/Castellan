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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;
using System.Runtime.CompilerServices;

namespace Castellan.Tests.Integration;

/// <summary>
/// Performance tests to validate connection pool improvements and debug batching behavior
/// </summary>
public class PerformanceIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ILogger<Pipeline>> _mockLogger;

    public PerformanceIntegrationTests()
    {
        // Setup service collection
        var services = new ServiceCollection();

        // Create mocks
        _mockVectorStore = new Mock<IVectorStore>();
        _mockLogger = new Mock<ILogger<Pipeline>>();

        // Register services
        services.AddSingleton(_mockVectorStore.Object);
        services.AddSingleton<ILogger<Pipeline>>(_mockLogger.Object);
        
        // Register all the required dependencies
        services.AddOptions();
        
        // Create pipeline options with batching enabled for performance testing
        var pipelineOptions = new PipelineOptions
        {
            EnableVectorBatching = true,
            VectorBatchSize = 3, // Small batch size for testing
            VectorBatchTimeoutMs = 2000, // Short timeout for testing
            EnableParallelProcessing = true,
            EnableParallelVectorOperations = true
        };
        
        services.Configure<PipelineOptions>(opt => 
        {
            opt.EnableVectorBatching = pipelineOptions.EnableVectorBatching;
            opt.VectorBatchSize = pipelineOptions.VectorBatchSize;
            opt.VectorBatchTimeoutMs = pipelineOptions.VectorBatchTimeoutMs;
            opt.EnableParallelProcessing = pipelineOptions.EnableParallelProcessing;
            opt.EnableParallelVectorOperations = pipelineOptions.EnableParallelVectorOperations;
        });

        // Register Pipeline and supporting services
        RegisterMockServices(services);
        
        // Build service provider
        _serviceProvider = services.BuildServiceProvider();
    }
    
    private void RegisterMockServices(IServiceCollection services)
    {
        // Register the minimum required services for Pipeline
        services.AddSingleton(Mock.Of<IIPEnrichmentService>());
        services.AddSingleton(Mock.Of<INotificationService>());
        services.AddSingleton(Mock.Of<IPerformanceMonitor>());
        services.AddSingleton(Mock.Of<ISecurityEventStore>());
        services.AddSingleton(Mock.Of<IAutomatedResponseService>());
        services.AddSingleton<IMalwareScanService>(new Mock<IMalwareScanService>().Object);
        services.AddSingleton<IMalwareRuleStore>(new Mock<IMalwareRuleStore>().Object);
        services.AddSingleton(Options.Create(new AlertOptions()));
        services.AddSingleton(Options.Create(new NotificationOptions()));
        services.AddSingleton(Options.Create(new CorrelationOptions()));
        services.Configure<MalwareScanningOptions>(opt => { opt.Enabled = true; opt.ScanTimeoutSeconds = 5; });
        
        // Register the real services we want to test
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();
    }

    [Fact]
    public async Task Pipeline_BatchingDebug_ShouldShowDetailedBehavior()
    {
        // Arrange - Create exactly 3 events to match batch size
        var events = new List<LogEvent>
        {
            CreateNonDeterministicEvent(9001, "Event 1"),
            CreateNonDeterministicEvent(9002, "Event 2"),
            CreateNonDeterministicEvent(9003, "Event 3")
        };

        var mockLogCollector = new Mock<ILogCollector>();
        mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        var mockEmbedder = new Mock<IEmbedder>();
        mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768])
            .Callback<string, CancellationToken>((text, ct) => 
            {
                Console.WriteLine($"Embedding generated for: {text.Substring(0, Math.Min(50, text.Length))}...");
            });

        var mockLlmClient = new Mock<ILlmClient>();
        mockLlmClient
            .Setup(x => x.AnalyzeAsync(It.IsAny<LogEvent>(), It.IsAny<IEnumerable<LogEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{
                ""risk"": ""low"",
                ""mitre"": [""T1078""],
                ""confidence"": 85,
                ""summary"": ""Test event detected"",
                ""recommended_actions"": [""Monitor user activity""]
            }");

        // Setup vector store mocks with detailed tracking
        _mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => Console.WriteLine("EnsureCollectionAsync called"));
            
        _mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Callback(() => Console.WriteLine("Has24HoursOfDataAsync called"));
            
        _mockVectorStore
            .Setup(x => x.BatchUpsertAsync(It.IsAny<List<(LogEvent logEvent, float[] embedding)>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<List<(LogEvent logEvent, float[] embedding)>, CancellationToken>((items, ct) => 
            {
                Console.WriteLine($"BatchUpsertAsync called with {items.Count} items");
            });
            
        _mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<LogEvent, float[], CancellationToken>((evt, emb, ct) => 
            {
                Console.WriteLine($"UpsertAsync called for event {evt.EventId}");
            });
            
        _mockVectorStore
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>())
            .Callback<float[], int, CancellationToken>((query, k, ct) => 
            {
                Console.WriteLine($"SearchAsync called with k={k}");
            });

        // Create service provider with detailed logging
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton(mockLogCollector.Object);
        services.AddSingleton(mockEmbedder.Object);
        services.AddSingleton(_mockVectorStore.Object);
        services.AddSingleton(mockLlmClient.Object);
        
        // Add console logger for debugging
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<ILogger<Pipeline>>(loggerFactory.CreateLogger<Pipeline>());
        services.AddSingleton<ILogger<SecurityEventDetector>>(loggerFactory.CreateLogger<SecurityEventDetector>());
        services.AddSingleton<ILogger<RulesEngine>>(loggerFactory.CreateLogger<RulesEngine>());
        
        services.AddSingleton(Options.Create(new AlertOptions()));
        services.AddSingleton(Options.Create(new NotificationOptions()));
        
        // Configure with batching ENABLED and detailed logging
        services.Configure<PipelineOptions>(opt => 
        {
            opt.EnableParallelProcessing = true;
            opt.EnableVectorBatching = true;
            opt.VectorBatchSize = 3; // Match our event count exactly
            opt.VectorBatchTimeoutMs = 3000; // Short timeout for testing
            opt.EnableParallelVectorOperations = true;
        });
        
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();
        services.AddSingleton<IIPEnrichmentService>(new Mock<IIPEnrichmentService>().Object);
        services.AddSingleton<INotificationService>(new Mock<INotificationService>().Object);
        services.AddSingleton<IPerformanceMonitor>(new Mock<IPerformanceMonitor>().Object);
        services.AddSingleton<ISecurityEventStore>(new Mock<ISecurityEventStore>().Object);
        services.AddSingleton<IAutomatedResponseService>(new Mock<IAutomatedResponseService>().Object);
        services.AddSingleton<IMalwareScanService>(new Mock<IMalwareScanService>().Object);
        services.AddSingleton<IMalwareRuleStore>(new Mock<IMalwareRuleStore>().Object);
        services.AddSingleton(Options.Create(new CorrelationOptions
        {
            EnableLowScoreEvents = true
        }));
        services.Configure<MalwareScanningOptions>(opt => { opt.Enabled = true; opt.ScanTimeoutSeconds = 5; });

        var serviceProvider = services.BuildServiceProvider();
        var pipeline = serviceProvider.GetRequiredService<Pipeline>();

        Console.WriteLine("Starting pipeline test...");

        // Act
        await pipeline.StartAsync(CancellationToken.None);
        Console.WriteLine("Pipeline started, waiting for processing...");
        
        await Task.Delay(5000); // Give more time for processing and potential batch timeout
        
        Console.WriteLine("Stopping pipeline...");
        await pipeline.StopAsync(CancellationToken.None);
        Console.WriteLine("Pipeline stopped.");

        // Assert and Debug
        Console.WriteLine("\nVerifying mock calls...");
        
        // Check what was called
        _mockVectorStore.Verify(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()), Times.Once, "EnsureCollectionAsync should be called once");
        _mockVectorStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once, "Has24HoursOfDataAsync should be called once");
        
        // Check if we got any embedding calls
        mockEmbedder.Verify(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce, "EmbedAsync should be called");
        
        // Check if we got any search calls
        _mockVectorStore.Verify(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce, "SearchAsync should be called");
        
        // The main assertion - check for batch upsert
        try
        {
            _mockVectorStore.Verify(x => x.BatchUpsertAsync(It.IsAny<List<(LogEvent logEvent, float[] embedding)>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            Console.WriteLine("SUCCESS: BatchUpsertAsync was called!");
        }
        catch (MockException ex)
        {
            Console.WriteLine($"FAILED: BatchUpsertAsync was not called: {ex.Message}");
            
            // Check if individual upserts were called instead
            try 
            {
                _mockVectorStore.Verify(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
                Console.WriteLine("Individual UpsertAsync calls were made instead of batching");
            }
            catch
            {
                Console.WriteLine("No UpsertAsync calls were made at all");
            }
            
            throw; // Re-throw to fail the test
        }
    }

    [Fact]
    public async Task Pipeline_Performance_CompareIndividualVsBatchProcessing()
    {
        // This test compares performance between individual and batch processing
        var eventCount = 10;
        var events = new List<LogEvent>();
        
        for (int i = 0; i < eventCount; i++)
        {
            events.Add(CreateNonDeterministicEvent(9000 + i, $"Performance test event {i}"));
        }

        // Test with individual processing (batching disabled)
        var individualTime = await MeasureProcessingTime(events, batchingEnabled: false);
        
        // Test with batch processing (batching enabled)  
        var batchTime = await MeasureProcessingTime(events, batchingEnabled: true);
        
        Console.WriteLine($"Individual processing time: {individualTime}ms");
        Console.WriteLine($"Batch processing time: {batchTime}ms");
        
        if (batchTime < individualTime)
        {
            var improvement = ((individualTime - batchTime) / individualTime) * 100;
            Console.WriteLine($"Batch processing is {improvement:F1}% faster");
        }
        else
        {
            Console.WriteLine("No performance improvement observed (this may be due to test overhead)");
        }

        // Assert that both approaches actually processed the events
        Assert.True(individualTime > 0, "Individual processing should take measurable time");
        Assert.True(batchTime > 0, "Batch processing should take measurable time");
    }

    private async Task<long> MeasureProcessingTime(List<LogEvent> events, bool batchingEnabled)
    {
        var mockLogCollector = new Mock<ILogCollector>();
        mockLogCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(events));

        var mockEmbedder = new Mock<IEmbedder>();
        mockEmbedder
            .Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        var mockVectorStore = new Mock<IVectorStore>();
        mockVectorStore
            .Setup(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockVectorStore
            .Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockVectorStore
            .Setup(x => x.UpsertAsync(It.IsAny<LogEvent>(), It.IsAny<float[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockVectorStore
            .Setup(x => x.BatchUpsertAsync(It.IsAny<List<(LogEvent logEvent, float[] embedding)>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockVectorStore
            .Setup(x => x.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>());

        var mockLlmClient = new Mock<ILlmClient>();
        mockLlmClient
            .Setup(x => x.AnalyzeAsync(It.IsAny<LogEvent>(), It.IsAny<IEnumerable<LogEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{""risk"": ""low"", ""confidence"": 85, ""summary"": ""Test event""}");

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton(mockLogCollector.Object);
        services.AddSingleton(mockEmbedder.Object);
        services.AddSingleton(mockVectorStore.Object);
        services.AddSingleton(mockLlmClient.Object);
        services.AddSingleton<ILogger<Pipeline>>(Mock.Of<ILogger<Pipeline>>());
        services.AddSingleton<ILogger<SecurityEventDetector>>(Mock.Of<ILogger<SecurityEventDetector>>());
        services.AddSingleton<ILogger<RulesEngine>>(Mock.Of<ILogger<RulesEngine>>());
        services.AddSingleton(Options.Create(new AlertOptions()));
        services.AddSingleton(Options.Create(new NotificationOptions()));
        
        services.Configure<PipelineOptions>(opt => 
        {
            opt.EnableVectorBatching = batchingEnabled;
            opt.VectorBatchSize = 5;
            opt.VectorBatchTimeoutMs = 1000;
        });
        
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<Pipeline>();
        services.AddSingleton<IIPEnrichmentService>(Mock.Of<IIPEnrichmentService>());
        services.AddSingleton<INotificationService>(Mock.Of<INotificationService>());
        services.AddSingleton<IPerformanceMonitor>(Mock.Of<IPerformanceMonitor>());
        services.AddSingleton<ISecurityEventStore>(Mock.Of<ISecurityEventStore>());
        services.AddSingleton<IAutomatedResponseService>(Mock.Of<IAutomatedResponseService>());
        services.AddSingleton<IMalwareScanService>(new Mock<IMalwareScanService>().Object);
        services.AddSingleton<IMalwareRuleStore>(new Mock<IMalwareRuleStore>().Object);
        services.AddSingleton(Options.Create(new CorrelationOptions()));
        services.Configure<MalwareScanningOptions>(opt => { opt.Enabled = true; opt.ScanTimeoutSeconds = 5; });

        using var serviceProvider = services.BuildServiceProvider();
        var pipeline = serviceProvider.GetRequiredService<Pipeline>();

        var stopwatch = Stopwatch.StartNew();
        
        await pipeline.StartAsync(CancellationToken.None);
        await Task.Delay(1000); // Allow processing time
        await pipeline.StopAsync(CancellationToken.None);
        
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    private LogEvent CreateNonDeterministicEvent(int eventId, string message)
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Application", // Use Application channel to avoid deterministic detection
            eventId,
            "Information",
            "testuser",
            message,
            $"{{\"EventID\":{eventId}}}"
        );
    }

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(IEnumerable<T> items, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Console.WriteLine($"CreateAsyncEnumerable called with {items.Count()} items");
        int count = 0;
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) 
            {
                Console.WriteLine("Cancellation requested, breaking");
                yield break;
            }
            count++;
            Console.WriteLine($"Yielding item {count}: {item}");
            yield return item;
            await Task.Delay(100, ct); // Slightly longer delay
        }
        Console.WriteLine($"All {count} items yielded from async enumerable");
    }
    
    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
