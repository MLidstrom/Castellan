namespace Castellan.Worker.Configuration;

/// <summary>
/// Configuration options for performance monitoring
/// </summary>
public class PerformanceMonitorOptions
{
    public const string SectionName = "PerformanceMonitoring";

    /// <summary>
    /// Enable performance monitoring
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Log performance metrics to console/file
    /// </summary>
    public bool LogMetrics { get; set; } = true;

    /// <summary>
    /// Frequency of metric logging in minutes (0 = log all metrics)
    /// </summary>
    public int LogFrequencyMinutes { get; set; } = 5;

    /// <summary>
    /// Enable automatic cleanup of old metrics
    /// </summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>
    /// How long to retain metrics in memory (minutes)
    /// </summary>
    public int RetentionMinutes { get; set; } = 60;

    /// <summary>
    /// Export metrics to file periodically
    /// </summary>
    public bool EnablePeriodicExport { get; set; } = false;

    /// <summary>
    /// Export frequency in minutes
    /// </summary>
    public int ExportFrequencyMinutes { get; set; } = 30;

    /// <summary>
    /// Directory to export metrics files
    /// </summary>
    public string ExportDirectory { get; set; } = "metrics";

    /// <summary>
    /// Enable system resource monitoring (CPU, memory, etc.)
    /// </summary>
    public bool MonitorSystemResources { get; set; } = true;

    /// <summary>
    /// Enable detailed LLM metrics
    /// </summary>
    public bool MonitorLlmMetrics { get; set; } = true;

    /// <summary>
    /// Enable detailed vector store metrics
    /// </summary>
    public bool MonitorVectorStoreMetrics { get; set; } = true;

    /// <summary>
    /// Enable detailed notification metrics
    /// </summary>
    public bool MonitorNotificationMetrics { get; set; } = true;

    /// <summary>
    /// Minimum event processing time to log (milliseconds)
    /// </summary>
    public double MinLogProcessingTimeMs { get; set; } = 100;

    /// <summary>
    /// Alert threshold for high memory usage (MB)
    /// </summary>
    public double HighMemoryThresholdMB { get; set; } = 1000;

    /// <summary>
    /// Alert threshold for high processing time (milliseconds)
    /// </summary>
    public double HighProcessingTimeThresholdMs { get; set; } = 5000;

    /// <summary>
    /// Alert threshold for high queue depth
    /// </summary>
    public int HighQueueDepthThreshold { get; set; } = 100;
}