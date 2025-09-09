namespace Castellan.Worker.Models;

// Main data transfer models
public class HistoricalMetrics
{
    public List<PerformanceDataPoint> DataPoints { get; set; } = new();
    public PerformanceSummary Summary { get; set; } = new();
}

public class PerformanceDataPoint
{
    public DateTime Timestamp { get; set; }
    public double ResponseTime { get; set; } // milliseconds
    public int RequestCount { get; set; }
    public double ErrorRate { get; set; } // percentage (0.0 - 1.0)
    public double MemoryUsage { get; set; } // percentage
    public double CpuUsage { get; set; } // percentage
}

public class PerformanceSummary
{
    public double AverageResponseTime { get; set; }
    public double MaxResponseTime { get; set; }
    public double MinResponseTime { get; set; }
    public long TotalRequests { get; set; }
    public double ErrorRate { get; set; }
    public double MemoryUsage { get; set; }
    public double CpuUsage { get; set; }
}

// Cache statistics models
public class PerformanceCacheStatistics
{
    public double HitRate { get; set; } // percentage (0.0 - 1.0)
    public double MissRate { get; set; } // percentage (0.0 - 1.0)
    public double MemoryUsage { get; set; } // MB
    public long TotalRequests { get; set; }
    public double EffectivenessRatio { get; set; } // effectiveness score
    public int CacheEntries { get; set; }
    public double AverageResponseTime { get; set; } // milliseconds
}

// Database performance models
public class DatabasePerformanceMetrics
{
    public DatabaseConnectionPoolMetrics ConnectionPool { get; set; } = new();
    public QueryPerformanceMetrics QueryPerformance { get; set; } = new();
    public QdrantPerformanceMetrics QdrantMetrics { get; set; } = new();
}

public class DatabaseConnectionPoolMetrics
{
    public int Active { get; set; }
    public int Total { get; set; }
    public double Utilization { get; set; } // percentage (0.0 - 1.0)
    public int PeakConnections { get; set; }
}

public class QueryPerformanceMetrics
{
    public double AverageResponseTime { get; set; } // milliseconds
    public int SlowQueries { get; set; } // count of slow queries
    public long TotalQueries { get; set; }
    public double QueriesPerSecond { get; set; }
}

public class QdrantPerformanceMetrics
{
    public double AverageOperationTime { get; set; } // milliseconds
    public long VectorCount { get; set; }
    public string CollectionStatus { get; set; } = "healthy";
    public double BatchOperationTime { get; set; } // milliseconds
}

// System resource models
public class SystemResourceMetrics
{
    public CpuMetrics Cpu { get; set; } = new();
    public MemoryMetrics Memory { get; set; } = new();
    public DiskMetrics Disk { get; set; } = new();
    public NetworkMetrics Network { get; set; } = new();
}

public class CpuMetrics
{
    public double Usage { get; set; } // percentage
    public int Cores { get; set; }
    public double LoadAverage { get; set; }
    public double ProcessUsage { get; set; } // milliseconds
}

public class MemoryMetrics
{
    public double Usage { get; set; } // GB
    public long Total { get; set; } // bytes
    public long Available { get; set; } // bytes
    public double ProcessMemory { get; set; } // MB
}

public class DiskMetrics
{
    public double Usage { get; set; } // percentage
    public long Total { get; set; } // bytes
    public long Available { get; set; } // bytes
    public double ReadSpeed { get; set; } // MB/s
    public double WriteSpeed { get; set; } // MB/s
}

public class NetworkMetrics
{
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public long PacketsReceived { get; set; }
    public long PacketsSent { get; set; }
}

// Dashboard summary models
public class DashboardSummary
{
    public OverallMetrics Overall { get; set; } = new();
    public DashboardPerformanceMetrics Performance { get; set; } = new();
    public ResourceMetrics Resources { get; set; } = new();
    public AlertMetrics Alerts { get; set; } = new();
}

public class OverallMetrics
{
    public double HealthScore { get; set; } // 0-100 percentage
    public string Status { get; set; } = "healthy";
    public TimeSpan Uptime { get; set; }
}

public class DashboardPerformanceMetrics
{
    public double AverageResponseTime { get; set; } // milliseconds
    public double Throughput { get; set; } // requests per second
    public double ErrorRate { get; set; } // percentage (0.0 - 1.0)
}

public class ResourceMetrics
{
    public double CpuUsage { get; set; } // percentage
    public double MemoryUsage { get; set; } // percentage or GB
    public double DiskUsage { get; set; } // percentage
}

public class AlertMetrics
{
    public int Active { get; set; }
    public int Critical { get; set; }
    public int Warnings { get; set; }
}

// Alert system models
public class PerformanceAlert
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // CPU, Memory, Disk, etc.
    public string Severity { get; set; } = string.Empty; // warning, critical
    public string Message { get; set; } = string.Empty;
    public double Threshold { get; set; }
    public double CurrentValue { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "active"; // active, resolved, acknowledged
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Notes { get; set; }
}

public class AlertResponse
{
    public List<PerformanceAlert> Active { get; set; } = new();
    public List<PerformanceAlert> History { get; set; } = new();
}

// Threshold configuration models
public class PerformanceThresholds
{
    public ThresholdConfig CpuThreshold { get; set; } = new();
    public ThresholdConfig MemoryThreshold { get; set; } = new();
    public ThresholdConfig DiskThreshold { get; set; } = new();
    public ThresholdConfig ResponseTimeThreshold { get; set; } = new();
    public ThresholdConfig ErrorRateThreshold { get; set; } = new();
    public ThresholdConfig CacheHitRateThreshold { get; set; } = new();
    public ThresholdConfig DatabaseConnectionThreshold { get; set; } = new();
}

public class ThresholdConfig
{
    public double Warning { get; set; }
    public double Critical { get; set; }
}

// API response wrapper models
public class PerformanceApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}

// Time series models for historical data
public class TimeSeriesData<T>
{
    public DateTime Timestamp { get; set; }
    public T Value { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class TimeSeriesResponse<T>
{
    public string TimeRange { get; set; } = string.Empty;
    public List<TimeSeriesData<T>> DataPoints { get; set; } = new();
    public TimeSeriesMetadata Metadata { get; set; } = new();
}

public class TimeSeriesMetadata
{
    public int TotalPoints { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Interval { get; set; }
    public string AggregationMethod { get; set; } = "average";
}

// Health check models
public class PerformanceHealthCheckResult
{
    public string Component { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // healthy, degraded, unhealthy
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public TimeSpan ResponseTime { get; set; }
    public DateTime LastChecked { get; set; }
}

public class SystemHealthSummary
{
    public string OverallStatus { get; set; } = "healthy";
    public double HealthScore { get; set; } // 0-100
    public List<PerformanceHealthCheckResult> Components { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public TimeSpan TotalResponseTime { get; set; }
}

// Performance trend analysis models
public class PerformanceTrend
{
    public string MetricName { get; set; } = string.Empty;
    public string TrendDirection { get; set; } = string.Empty; // improving, declining, stable
    public double TrendStrength { get; set; } // 0.0 - 1.0
    public double ChangePercentage { get; set; }
    public DateTime AnalysisPeriod { get; set; }
    public List<double> DataPoints { get; set; } = new();
}

public class PerformanceTrendSummary
{
    public List<PerformanceTrend> Trends { get; set; } = new();
    public string OverallTrend { get; set; } = "stable";
    public DateTime AnalysisDate { get; set; }
    public string TimeRange { get; set; } = string.Empty;
}
