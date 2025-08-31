using System.Text.Json.Serialization;

namespace Castellan.Worker.Models;

/// <summary>
/// Performance metrics collected by Castellan
/// </summary>
public sealed class PerformanceMetrics
{
    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Pipeline performance metrics
    /// </summary>
    public PipelineMetrics Pipeline { get; init; } = new();

    /// <summary>
    /// Event collection metrics
    /// </summary>
    public EventCollectionMetrics EventCollection { get; init; } = new();

    /// <summary>
    /// Vector store metrics
    /// </summary>
    public VectorStoreMetrics VectorStore { get; init; } = new();

    /// <summary>
    /// Security detection metrics
    /// </summary>
    public SecurityDetectionMetrics SecurityDetection { get; init; } = new();

    /// <summary>
    /// System resource metrics
    /// </summary>
    public SystemMetrics System { get; init; } = new();

    /// <summary>
    /// LLM analysis metrics
    /// </summary>
    public LlmMetrics Llm { get; init; } = new();

    /// <summary>
    /// Notification metrics
    /// </summary>
    public NotificationMetrics Notifications { get; init; } = new();
}

/// <summary>
/// Pipeline-specific performance metrics
/// </summary>
public sealed class PipelineMetrics
{
    /// <summary>
    /// Total events processed since startup
    /// </summary>
    public long TotalEventsProcessed { get; set; }

    /// <summary>
    /// Events processed per minute (average)
    /// </summary>
    public double EventsPerMinute { get; set; }

    /// <summary>
    /// Average event processing time in milliseconds
    /// </summary>
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>
    /// Number of events currently in processing queue
    /// </summary>
    public int QueueDepth { get; set; }

    /// <summary>
    /// Pipeline uptime in seconds
    /// </summary>
    public double UptimeSeconds { get; set; }

    /// <summary>
    /// Number of processing errors
    /// </summary>
    public long ProcessingErrors { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Time of last error
    /// </summary>
    public DateTimeOffset? LastErrorTime { get; set; }
}

/// <summary>
/// Event collection performance metrics
/// </summary>
public sealed class EventCollectionMetrics
{
    /// <summary>
    /// Events collected per channel
    /// </summary>
    public Dictionary<string, long> EventsPerChannel { get; set; } = new();

    /// <summary>
    /// Average collection time per channel in milliseconds
    /// </summary>
    public Dictionary<string, double> CollectionTimePerChannelMs { get; set; } = new();

    /// <summary>
    /// Collection errors per channel
    /// </summary>
    public Dictionary<string, long> ErrorsPerChannel { get; set; } = new();

    /// <summary>
    /// Duplicate events filtered
    /// </summary>
    public long DuplicateEventsFiltered { get; set; }

    /// <summary>
    /// Last collection time
    /// </summary>
    public DateTimeOffset? LastCollectionTime { get; set; }
}

/// <summary>
/// Vector store performance metrics
/// </summary>
public sealed class VectorStoreMetrics
{
    /// <summary>
    /// Total vectors stored
    /// </summary>
    public long TotalVectors { get; set; }

    /// <summary>
    /// Average embedding time in milliseconds
    /// </summary>
    public double AverageEmbeddingTimeMs { get; set; }

    /// <summary>
    /// Average upsert time in milliseconds
    /// </summary>
    public double AverageUpsertTimeMs { get; set; }

    /// <summary>
    /// Average search time in milliseconds
    /// </summary>
    public double AverageSearchTimeMs { get; set; }

    /// <summary>
    /// Vector store size in MB (if available)
    /// </summary>
    public double StorageSizeMB { get; set; }

    /// <summary>
    /// Vectors cleaned up in last maintenance
    /// </summary>
    public long LastCleanupVectorCount { get; set; }

    /// <summary>
    /// Last cleanup time
    /// </summary>
    public DateTimeOffset? LastCleanupTime { get; set; }

    /// <summary>
    /// Embedding errors
    /// </summary>
    public long EmbeddingErrors { get; set; }

    /// <summary>
    /// Vector store errors
    /// </summary>
    public long VectorStoreErrors { get; set; }
}

/// <summary>
/// Security detection performance metrics
/// </summary>
public sealed class SecurityDetectionMetrics
{
    /// <summary>
    /// Security events detected by type
    /// </summary>
    public Dictionary<string, long> EventsByType { get; set; } = new();

    /// <summary>
    /// Security events by risk level
    /// </summary>
    public Dictionary<string, long> EventsByRiskLevel { get; set; } = new();

    /// <summary>
    /// Deterministic vs LLM detections
    /// </summary>
    public long DeterministicDetections { get; set; }
    public long LlmDetections { get; set; }

    /// <summary>
    /// Correlation-based detections
    /// </summary>
    public long CorrelationDetections { get; set; }

    /// <summary>
    /// Average confidence score
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    /// Detection rate (detections per total events)
    /// </summary>
    public double DetectionRate { get; set; }

    /// <summary>
    /// High-risk detections
    /// </summary>
    public long HighRiskDetections { get; set; }

    /// <summary>
    /// Critical detections
    /// </summary>
    public long CriticalDetections { get; set; }
}

/// <summary>
/// System resource metrics
/// </summary>
public sealed class SystemMetrics
{
    /// <summary>
    /// Memory usage in MB
    /// </summary>
    public double MemoryUsageMB { get; set; }

    /// <summary>
    /// CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Disk usage percentage
    /// </summary>
    public double DiskUsagePercent { get; set; }

    /// <summary>
    /// Thread count
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// GC memory pressure
    /// </summary>
    public long GcMemoryPressure { get; set; }

    /// <summary>
    /// Number of GC collections (Gen 0, 1, 2)
    /// </summary>
    public Dictionary<int, int> GcCollections { get; set; } = new();
}

/// <summary>
/// LLM analysis performance metrics
/// </summary>
public sealed class LlmMetrics
{
    /// <summary>
    /// Total LLM requests
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Average LLM response time in milliseconds
    /// </summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// LLM provider (Ollama, OpenAI, etc.)
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Model name
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Successful LLM responses
    /// </summary>
    public long SuccessfulResponses { get; set; }

    /// <summary>
    /// Failed LLM responses
    /// </summary>
    public long FailedResponses { get; set; }

    /// <summary>
    /// Average tokens per request
    /// </summary>
    public double AverageTokensPerRequest { get; set; }

    /// <summary>
    /// Last response time
    /// </summary>
    public DateTimeOffset? LastResponseTime { get; set; }
}

/// <summary>
/// Notification performance metrics
/// </summary>
public sealed class NotificationMetrics
{
    /// <summary>
    /// Desktop notifications sent
    /// </summary>
    public long DesktopNotificationsSent { get; set; }


    /// <summary>
    /// Notification failures
    /// </summary>
    public long NotificationFailures { get; set; }

    /// <summary>
    /// Average notification delivery time in milliseconds
    /// </summary>
    public double AverageDeliveryTimeMs { get; set; }

    /// <summary>
    /// Notifications by risk level
    /// </summary>
    public Dictionary<string, long> NotificationsByRiskLevel { get; set; } = new();

    /// <summary>
    /// Last notification time
    /// </summary>
    public DateTimeOffset? LastNotificationTime { get; set; }
}