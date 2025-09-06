using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

public sealed class PipelineOptions
{
    /// <summary>
    /// Enable parallel processing for independent operations in the pipeline
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent operations in parallel processing
    /// </summary>
    [Range(1, 32, ErrorMessage = "MaxConcurrency must be between 1 and 32")]
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Timeout in milliseconds for parallel operations
    /// </summary>
    [Range(1000, 120000, ErrorMessage = "ParallelOperationTimeoutMs must be between 1000ms and 120000ms (2 minutes)")]
    public int ParallelOperationTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Enable parallel vector operations (upsert and search concurrently)
    /// </summary>
    public bool EnableParallelVectorOperations { get; set; } = true;

    /// <summary>
    /// Batch size for processing operations
    /// </summary>
    [Range(1, 10000, ErrorMessage = "BatchSize must be between 1 and 10000")]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Processing interval in milliseconds
    /// </summary>
    [Range(100, 300000, ErrorMessage = "ProcessingIntervalMs must be between 100ms and 300000ms (5 minutes)")]
    public int ProcessingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Number of retry attempts for failed operations
    /// </summary>
    [Range(0, 10, ErrorMessage = "RetryAttempts must be between 0 and 10")]
    public int RetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay in milliseconds between retry attempts
    /// </summary>
    [Range(100, 60000, ErrorMessage = "RetryDelayMs must be between 100ms and 60000ms (1 minute)")]
    public int RetryDelayMs { get; set; } = 1000;

    // --- NEW THROTTLING & CONCURRENCY SETTINGS ---

    /// <summary>
    /// Enable semaphore-based throttling to enforce MaxConcurrency limits
    /// </summary>
    public bool EnableSemaphoreThrottling { get; set; } = true;

    /// <summary>
    /// Maximum concurrent tasks allowed in the pipeline (enforced with semaphore)
    /// </summary>
    [Range(1, 100, ErrorMessage = "MaxConcurrentTasks must be between 1 and 100")]
    public int MaxConcurrentTasks { get; set; } = 8;

    /// <summary>
    /// Timeout in milliseconds for acquiring semaphore permission
    /// </summary>
    [Range(1000, 60000, ErrorMessage = "SemaphoreTimeoutMs must be between 1000ms and 60000ms (1 minute)")]
    public int SemaphoreTimeoutMs { get; set; } = 15000;

    /// <summary>
    /// Skip processing when semaphore timeout is exceeded (instead of waiting)
    /// </summary>
    public bool SkipOnThrottleTimeout { get; set; } = false;

    /// <summary>
    /// Enable adaptive throttling based on system load
    /// </summary>
    public bool EnableAdaptiveThrottling { get; set; } = false;

    /// <summary>
    /// CPU threshold percentage for adaptive throttling (0-100)
    /// </summary>
    [Range(10, 95, ErrorMessage = "CpuThrottleThreshold must be between 10% and 95%")]
    public int CpuThrottleThreshold { get; set; } = 80;

    // --- NEW MEMORY MANAGEMENT & RETENTION SETTINGS ---

    /// <summary>
    /// Event history retention time in minutes for RulesEngine correlation
    /// </summary>
    [Range(1, 1440, ErrorMessage = "EventHistoryRetentionMinutes must be between 1 minute and 1440 minutes (24 hours)")]
    public int EventHistoryRetentionMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of events to retain per correlation key
    /// </summary>
    [Range(1, 10000, ErrorMessage = "MaxEventsPerCorrelationKey must be between 1 and 10000")]
    public int MaxEventsPerCorrelationKey { get; set; } = 1000;

    /// <summary>
    /// Memory high water mark in MB - trigger cleanup when exceeded
    /// </summary>
    [Range(100, 8192, ErrorMessage = "MemoryHighWaterMarkMB must be between 100MB and 8192MB (8GB)")]
    public int MemoryHighWaterMarkMB { get; set; } = 1024;

    /// <summary>
    /// Memory cleanup interval in minutes
    /// </summary>
    [Range(1, 60, ErrorMessage = "MemoryCleanupIntervalMinutes must be between 1 and 60 minutes")]
    public int MemoryCleanupIntervalMinutes { get; set; } = 10;

    /// <summary>
    /// Force garbage collection when memory threshold is reached
    /// </summary>
    public bool EnableAggressiveGarbageCollection { get; set; } = false;

    /// <summary>
    /// Enable memory pressure monitoring and automatic cleanup
    /// </summary>
    public bool EnableMemoryPressureMonitoring { get; set; } = true;

    // --- NEW QUEUE MANAGEMENT SETTINGS ---

    /// <summary>
    /// Maximum queue depth before applying back-pressure
    /// </summary>
    [Range(10, 100000, ErrorMessage = "MaxQueueDepth must be between 10 and 100000")]
    public int MaxQueueDepth { get; set; } = 1000;

    /// <summary>
    /// Enable queue depth monitoring and back-pressure
    /// </summary>
    public bool EnableQueueBackPressure { get; set; } = true;

    /// <summary>
    /// Drop oldest events when queue is full instead of blocking
    /// </summary>
    public bool DropOldestOnQueueFull { get; set; } = false;

    // --- NEW PERFORMANCE MONITORING SETTINGS ---

    /// <summary>
    /// Enable detailed pipeline performance metrics
    /// </summary>
    public bool EnableDetailedMetrics { get; set; } = true;

    /// <summary>
    /// Metrics collection interval in milliseconds
    /// </summary>
    [Range(1000, 300000, ErrorMessage = "MetricsIntervalMs must be between 1000ms and 300000ms (5 minutes)")]
    public int MetricsIntervalMs { get; set; } = 30000;

    /// <summary>
    /// Enable performance alerts when thresholds are exceeded
    /// </summary>
    public bool EnablePerformanceAlerts { get; set; } = true;

    // --- VECTOR BATCH PROCESSING SETTINGS ---

    /// <summary>
    /// Enable batch processing for vector operations (upsert)
    /// </summary>
    public bool EnableVectorBatching { get; set; } = true;

    /// <summary>
    /// Maximum number of vectors to batch together before flushing to vector store
    /// </summary>
    [Range(1, 1000, ErrorMessage = "VectorBatchSize must be between 1 and 1000")]
    public int VectorBatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum time in milliseconds to wait before flushing a partial batch
    /// </summary>
    [Range(100, 60000, ErrorMessage = "VectorBatchTimeoutMs must be between 100ms and 60000ms (1 minute)")]
    public int VectorBatchTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Maximum time in milliseconds to wait for batch processing to complete
    /// </summary>
    [Range(1000, 300000, ErrorMessage = "VectorBatchProcessingTimeoutMs must be between 1000ms and 300000ms (5 minutes)")]
    public int VectorBatchProcessingTimeoutMs { get; set; } = 30000;
}
