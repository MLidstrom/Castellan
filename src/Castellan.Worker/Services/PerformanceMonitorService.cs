using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Performance monitoring service implementation
/// </summary>
public sealed class PerformanceMonitorService : IPerformanceMonitor
{
    private readonly ILogger<PerformanceMonitorService> _logger;
    private readonly PerformanceMonitorOptions _options;
    private readonly DateTimeOffset _startTime;
    private readonly object _lockObject = new();

    // Metrics storage
    private readonly ConcurrentQueue<PipelineMetricsSnapshot> _pipelineMetrics = new();
    private readonly ConcurrentDictionary<string, EventCollectionSnapshot> _eventCollectionMetrics = new();
    private readonly ConcurrentQueue<VectorStoreSnapshot> _vectorStoreMetrics = new();
    private readonly ConcurrentQueue<SecurityDetectionSnapshot> _securityDetectionMetrics = new();
    private readonly ConcurrentQueue<LlmSnapshot> _llmMetrics = new();
    private readonly ConcurrentQueue<NotificationSnapshot> _notificationMetrics = new();

    // Aggregated counters
    private long _totalEventsProcessed;
    private long _totalProcessingErrors;
    private long _totalSecurityDetections;
    private long _totalLlmRequests;
    private long _totalNotificationsSent;
    private long _totalNotificationFailures;
    private string? _lastError;
    private DateTimeOffset? _lastErrorTime;

    // Performance counters
    private readonly Process _currentProcess;

    public PerformanceMonitorService(IOptions<PerformanceMonitorOptions> options, ILogger<PerformanceMonitorService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _startTime = DateTimeOffset.UtcNow;
        _currentProcess = Process.GetCurrentProcess();

        _logger.LogInformation("Performance monitoring service initialized");
    }

    public void RecordPipelineMetrics(double processingTimeMs, int eventsProcessed, int queueDepth, string? error = null)
    {
        var snapshot = new PipelineMetricsSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ProcessingTimeMs = processingTimeMs,
            EventsProcessed = eventsProcessed,
            QueueDepth = queueDepth,
            Error = error
        };

        _pipelineMetrics.Enqueue(snapshot);
        Interlocked.Add(ref _totalEventsProcessed, eventsProcessed);

        if (!string.IsNullOrEmpty(error))
        {
            Interlocked.Increment(ref _totalProcessingErrors);
            lock (_lockObject)
            {
                _lastError = error;
                _lastErrorTime = DateTimeOffset.UtcNow;
            }
        }

        CleanupOldMetrics();

        if (_options.LogMetrics && (_options.LogFrequencyMinutes == 0 || 
            DateTimeOffset.UtcNow.Minute % _options.LogFrequencyMinutes == 0))
        {
            _logger.LogInformation("Pipeline metrics: {EventsProcessed} events, {ProcessingTime}ms, queue depth: {QueueDepth}", 
                eventsProcessed, processingTimeMs, queueDepth);
        }
    }

    public void RecordEventCollectionMetrics(string channel, int eventsCollected, double collectionTimeMs, int duplicatesFiltered = 0, string? error = null)
    {
        _eventCollectionMetrics.AddOrUpdate(channel, 
            new EventCollectionSnapshot
            {
                Channel = channel,
                EventsCollected = eventsCollected,
                CollectionTimeMs = collectionTimeMs,
                DuplicatesFiltered = duplicatesFiltered,
                ErrorCount = string.IsNullOrEmpty(error) ? 0 : 1,
                LastCollectionTime = DateTimeOffset.UtcNow
            },
            (key, existing) => new EventCollectionSnapshot
            {
                Channel = channel,
                EventsCollected = existing.EventsCollected + eventsCollected,
                CollectionTimeMs = (existing.CollectionTimeMs + collectionTimeMs) / 2, // Average
                DuplicatesFiltered = existing.DuplicatesFiltered + duplicatesFiltered,
                ErrorCount = existing.ErrorCount + (string.IsNullOrEmpty(error) ? 0 : 1),
                LastCollectionTime = DateTimeOffset.UtcNow
            });

        if (_options.LogMetrics)
        {
            _logger.LogDebug("Collection metrics for {Channel}: {EventsCollected} events, {CollectionTime}ms, {Duplicates} duplicates filtered", 
                channel, eventsCollected, collectionTimeMs, duplicatesFiltered);
        }
    }

    public void RecordVectorStoreMetrics(double embeddingTimeMs = 0, double upsertTimeMs = 0, double searchTimeMs = 0, int vectorsProcessed = 0, string? error = null)
    {
        var snapshot = new VectorStoreSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            EmbeddingTimeMs = embeddingTimeMs,
            UpsertTimeMs = upsertTimeMs,
            SearchTimeMs = searchTimeMs,
            VectorsProcessed = vectorsProcessed,
            Error = error
        };

        _vectorStoreMetrics.Enqueue(snapshot);
        CleanupOldMetrics();

        if (_options.LogMetrics)
        {
            _logger.LogDebug("Vector store metrics: embedding {EmbeddingTime}ms, upsert {UpsertTime}ms, search {SearchTime}ms, {Vectors} vectors", 
                embeddingTimeMs, upsertTimeMs, searchTimeMs, vectorsProcessed);
        }
    }

    public void RecordSecurityDetection(string eventType, string riskLevel, int confidence, bool isDeterministic, bool isCorrelationBased = false)
    {
        var snapshot = new SecurityDetectionSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = eventType,
            RiskLevel = riskLevel,
            Confidence = confidence,
            IsDeterministic = isDeterministic,
            IsCorrelationBased = isCorrelationBased
        };

        _securityDetectionMetrics.Enqueue(snapshot);
        Interlocked.Increment(ref _totalSecurityDetections);
        CleanupOldMetrics();

        if (_options.LogMetrics)
        {
            _logger.LogDebug("Security detection: {EventType} ({RiskLevel}, {Confidence}% confidence, {DetectionType})", 
                eventType, riskLevel, confidence, isDeterministic ? "deterministic" : "LLM");
        }
    }

    public void RecordLlmMetrics(string provider, string model, double responseTimeMs, int tokens = 0, bool success = true)
    {
        var snapshot = new LlmSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            Provider = provider,
            Model = model,
            ResponseTimeMs = responseTimeMs,
            Tokens = tokens,
            Success = success
        };

        _llmMetrics.Enqueue(snapshot);
        Interlocked.Increment(ref _totalLlmRequests);
        CleanupOldMetrics();

        if (_options.LogMetrics)
        {
            _logger.LogDebug("LLM metrics: {Provider}/{Model} - {ResponseTime}ms, {Tokens} tokens, success: {Success}", 
                provider, model, responseTimeMs, tokens, success);
        }
    }

    public void RecordNotificationMetrics(string notificationType, string riskLevel, double deliveryTimeMs, bool success = true)
    {
        var snapshot = new NotificationSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            NotificationType = notificationType,
            RiskLevel = riskLevel,
            DeliveryTimeMs = deliveryTimeMs,
            Success = success
        };

        _notificationMetrics.Enqueue(snapshot);
        
        if (success)
        {
            Interlocked.Increment(ref _totalNotificationsSent);
        }
        else
        {
            Interlocked.Increment(ref _totalNotificationFailures);
        }

        CleanupOldMetrics();

        if (_options.LogMetrics)
        {
            _logger.LogDebug("Notification metrics: {NotificationType} ({RiskLevel}) - {DeliveryTime}ms, success: {Success}", 
                notificationType, riskLevel, deliveryTimeMs, success);
        }
    }

    public void RecordVectorCleanup(int vectorsRemoved)
    {
        if (_options.LogMetrics)
        {
            _logger.LogInformation("Vector cleanup completed: {VectorsRemoved} vectors removed", vectorsRemoved);
        }
    }

    public PerformanceMetrics GetCurrentMetrics()
    {
        var now = DateTimeOffset.UtcNow;
        var uptimeSeconds = (now - _startTime).TotalSeconds;

        // Collect recent metrics
        var recentPipelineMetrics = GetRecentMetrics(_pipelineMetrics, TimeSpan.FromMinutes(5));
        var recentVectorMetrics = GetRecentMetrics(_vectorStoreMetrics, TimeSpan.FromMinutes(5));
        var recentSecurityMetrics = GetRecentMetrics(_securityDetectionMetrics, TimeSpan.FromMinutes(5));
        var recentLlmMetrics = GetRecentMetrics(_llmMetrics, TimeSpan.FromMinutes(5));
        var recentNotificationMetrics = GetRecentMetrics(_notificationMetrics, TimeSpan.FromMinutes(5));

        return new PerformanceMetrics
        {
            Timestamp = now,
            Pipeline = new PipelineMetrics
            {
                TotalEventsProcessed = _totalEventsProcessed,
                EventsPerMinute = CalculateEventsPerMinute(recentPipelineMetrics),
                AverageProcessingTimeMs = CalculateAverageProcessingTime(recentPipelineMetrics),
                QueueDepth = recentPipelineMetrics.LastOrDefault()?.QueueDepth ?? 0,
                UptimeSeconds = uptimeSeconds,
                ProcessingErrors = _totalProcessingErrors,
                LastError = _lastError,
                LastErrorTime = _lastErrorTime
            },
            EventCollection = new EventCollectionMetrics
            {
                EventsPerChannel = _eventCollectionMetrics.ToDictionary(kvp => kvp.Key, kvp => (long)kvp.Value.EventsCollected),
                CollectionTimePerChannelMs = _eventCollectionMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.CollectionTimeMs),
                ErrorsPerChannel = _eventCollectionMetrics.ToDictionary(kvp => kvp.Key, kvp => (long)kvp.Value.ErrorCount),
                DuplicateEventsFiltered = _eventCollectionMetrics.Values.Sum(v => v.DuplicatesFiltered),
                LastCollectionTime = _eventCollectionMetrics.Values.Max(v => v.LastCollectionTime)
            },
            VectorStore = CalculateVectorStoreMetrics(recentVectorMetrics),
            SecurityDetection = CalculateSecurityDetectionMetrics(recentSecurityMetrics),
            System = GetSystemMetrics(),
            Llm = CalculateLlmMetrics(recentLlmMetrics),
            Notifications = CalculateNotificationMetrics(recentNotificationMetrics)
        };
    }

    public async Task<PerformanceMetrics> GetMetricsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        // For now, return current metrics. In a full implementation, this would query historical data
        await Task.CompletedTask;
        return GetCurrentMetrics();
    }

    public void ResetMetrics()
    {
        lock (_lockObject)
        {
            while (_pipelineMetrics.TryDequeue(out _)) { }
            _eventCollectionMetrics.Clear();
            while (_vectorStoreMetrics.TryDequeue(out _)) { }
            while (_securityDetectionMetrics.TryDequeue(out _)) { }
            while (_llmMetrics.TryDequeue(out _)) { }
            while (_notificationMetrics.TryDequeue(out _)) { }

            Interlocked.Exchange(ref _totalEventsProcessed, 0);
            Interlocked.Exchange(ref _totalProcessingErrors, 0);
            Interlocked.Exchange(ref _totalSecurityDetections, 0);
            Interlocked.Exchange(ref _totalLlmRequests, 0);
            Interlocked.Exchange(ref _totalNotificationsSent, 0);
            Interlocked.Exchange(ref _totalNotificationFailures, 0);

            _lastError = null;
            _lastErrorTime = null;
        }

        _logger.LogInformation("Performance metrics reset");
    }

    public async Task ExportMetricsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var metrics = GetCurrentMetrics();
        var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        _logger.LogInformation("Performance metrics exported to {FilePath}", filePath);
    }

    private void CleanupOldMetrics()
    {
        if (!_options.EnableCleanup) return;

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-_options.RetentionMinutes);
        
        CleanupQueue(_pipelineMetrics, cutoff);
        CleanupQueue(_vectorStoreMetrics, cutoff);
        CleanupQueue(_securityDetectionMetrics, cutoff);
        CleanupQueue(_llmMetrics, cutoff);
        CleanupQueue(_notificationMetrics, cutoff);
    }

    private static void CleanupQueue<T>(ConcurrentQueue<T> queue, DateTimeOffset cutoff) where T : ITimestamped
    {
        while (queue.TryPeek(out var item) && item.Timestamp < cutoff)
        {
            queue.TryDequeue(out _);
        }
    }

    private static List<T> GetRecentMetrics<T>(ConcurrentQueue<T> queue, TimeSpan timeSpan) where T : ITimestamped
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(timeSpan);
        return queue.Where(m => m.Timestamp >= cutoff).ToList();
    }

    private static double CalculateEventsPerMinute(List<PipelineMetricsSnapshot> metrics)
    {
        if (!metrics.Any()) return 0;
        
        var totalEvents = metrics.Sum(m => m.EventsProcessed);
        var timeSpan = metrics.Max(m => m.Timestamp) - metrics.Min(m => m.Timestamp);
        var minutes = Math.Max(1, timeSpan.TotalMinutes);
        
        return totalEvents / minutes;
    }

    private static double CalculateAverageProcessingTime(List<PipelineMetricsSnapshot> metrics)
    {
        return metrics.Any() ? metrics.Average(m => m.ProcessingTimeMs) : 0;
    }

    private VectorStoreMetrics CalculateVectorStoreMetrics(List<VectorStoreSnapshot> metrics)
    {
        var embeddingTimes = metrics.Where(m => m.EmbeddingTimeMs > 0);
        var upsertTimes = metrics.Where(m => m.UpsertTimeMs > 0);
        var searchTimes = metrics.Where(m => m.SearchTimeMs > 0);
        
        return new VectorStoreMetrics
        {
            TotalVectors = metrics.Sum(m => m.VectorsProcessed),
            AverageEmbeddingTimeMs = embeddingTimes.Any() ? embeddingTimes.Average(m => m.EmbeddingTimeMs) : 0,
            AverageUpsertTimeMs = upsertTimes.Any() ? upsertTimes.Average(m => m.UpsertTimeMs) : 0,
            AverageSearchTimeMs = searchTimes.Any() ? searchTimes.Average(m => m.SearchTimeMs) : 0,
            EmbeddingErrors = metrics.Count(m => !string.IsNullOrEmpty(m.Error) && m.Error.Contains("embedding", StringComparison.OrdinalIgnoreCase)),
            VectorStoreErrors = metrics.Count(m => !string.IsNullOrEmpty(m.Error))
        };
    }

    private SecurityDetectionMetrics CalculateSecurityDetectionMetrics(List<SecurityDetectionSnapshot> metrics)
    {
        return new SecurityDetectionMetrics
        {
            EventsByType = metrics.GroupBy(m => m.EventType).ToDictionary(g => g.Key, g => (long)g.Count()),
            EventsByRiskLevel = metrics.GroupBy(m => m.RiskLevel).ToDictionary(g => g.Key, g => (long)g.Count()),
            DeterministicDetections = metrics.Count(m => m.IsDeterministic),
            LlmDetections = metrics.Count(m => !m.IsDeterministic),
            CorrelationDetections = metrics.Count(m => m.IsCorrelationBased),
            AverageConfidence = metrics.Any() ? metrics.Average(m => m.Confidence) : 0,
            DetectionRate = _totalEventsProcessed > 0 ? (double)_totalSecurityDetections / _totalEventsProcessed : 0,
            HighRiskDetections = metrics.Count(m => m.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase)),
            CriticalDetections = metrics.Count(m => m.RiskLevel.Equals("critical", StringComparison.OrdinalIgnoreCase))
        };
    }

    private SystemMetrics GetSystemMetrics()
    {
        try
        {
            _currentProcess.Refresh();
            
            return new SystemMetrics
            {
                MemoryUsageMB = _currentProcess.WorkingSet64 / (1024.0 * 1024.0),
                CpuUsagePercent = 0, // Would need performance counter for accurate CPU usage
                ThreadCount = _currentProcess.Threads.Count,
                GcMemoryPressure = GC.GetTotalMemory(false),
                GcCollections = new Dictionary<int, int>
                {
                    [0] = GC.CollectionCount(0),
                    [1] = GC.CollectionCount(1),
                    [2] = GC.CollectionCount(2)
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect system metrics");
            return new SystemMetrics();
        }
    }

    private LlmMetrics CalculateLlmMetrics(List<LlmSnapshot> metrics)
    {
        var latestMetric = metrics.LastOrDefault();
        
        return new LlmMetrics
        {
            TotalRequests = _totalLlmRequests,
            AverageResponseTimeMs = metrics.Any() ? metrics.Average(m => m.ResponseTimeMs) : 0,
            Provider = latestMetric?.Provider ?? string.Empty,
            Model = latestMetric?.Model ?? string.Empty,
            SuccessfulResponses = metrics.Count(m => m.Success),
            FailedResponses = metrics.Count(m => !m.Success),
            AverageTokensPerRequest = metrics.Where(m => m.Tokens > 0).Any() ? metrics.Where(m => m.Tokens > 0).Average(m => m.Tokens) : 0,
            LastResponseTime = latestMetric?.Timestamp
        };
    }

    private NotificationMetrics CalculateNotificationMetrics(List<NotificationSnapshot> metrics)
    {
        return new NotificationMetrics
        {
            DesktopNotificationsSent = metrics.Count(m => m.NotificationType.Equals("Desktop", StringComparison.OrdinalIgnoreCase) && m.Success),
            NotificationFailures = _totalNotificationFailures,
            AverageDeliveryTimeMs = metrics.Any() ? metrics.Average(m => m.DeliveryTimeMs) : 0,
            NotificationsByRiskLevel = metrics.GroupBy(m => m.RiskLevel).ToDictionary(g => g.Key, g => (long)g.Count()),
            LastNotificationTime = metrics.LastOrDefault()?.Timestamp
        };
    }
}

// Snapshot classes for internal metrics storage
internal interface ITimestamped
{
    DateTimeOffset Timestamp { get; }
}

internal record PipelineMetricsSnapshot : ITimestamped
{
    public DateTimeOffset Timestamp { get; init; }
    public double ProcessingTimeMs { get; init; }
    public int EventsProcessed { get; init; }
    public int QueueDepth { get; init; }
    public string? Error { get; init; }
}

internal record EventCollectionSnapshot
{
    public string Channel { get; init; } = string.Empty;
    public int EventsCollected { get; init; }
    public double CollectionTimeMs { get; init; }
    public int DuplicatesFiltered { get; init; }
    public int ErrorCount { get; init; }
    public DateTimeOffset? LastCollectionTime { get; init; }
}

internal record VectorStoreSnapshot : ITimestamped
{
    public DateTimeOffset Timestamp { get; init; }
    public double EmbeddingTimeMs { get; init; }
    public double UpsertTimeMs { get; init; }
    public double SearchTimeMs { get; init; }
    public int VectorsProcessed { get; init; }
    public string? Error { get; init; }
}

internal record SecurityDetectionSnapshot : ITimestamped
{
    public DateTimeOffset Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public int Confidence { get; init; }
    public bool IsDeterministic { get; init; }
    public bool IsCorrelationBased { get; init; }
}

internal record LlmSnapshot : ITimestamped
{
    public DateTimeOffset Timestamp { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public double ResponseTimeMs { get; init; }
    public int Tokens { get; init; }
    public bool Success { get; init; }
}

internal record NotificationSnapshot : ITimestamped
{
    public DateTimeOffset Timestamp { get; init; }
    public string NotificationType { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public double DeliveryTimeMs { get; init; }
    public bool Success { get; init; }
}