using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Castellan.Worker.Services.Compliance;

public interface ICompliancePerformanceMonitorService
{
    void RecordReportGeneration(string framework, ReportFormat format, TimeSpan duration, bool fromCache);
    void RecordPdfGeneration(string framework, TimeSpan duration, int sizeBytes);
    void RecordCacheOperation(string operation, TimeSpan duration, bool hit);
    CompliancePerformanceMetrics GetPerformanceMetrics();
    void ResetMetrics();
}

public class CompliancePerformanceMonitorService : ICompliancePerformanceMonitorService
{
    private readonly ILogger<CompliancePerformanceMonitorService> _logger;
    private readonly ConcurrentDictionary<string, PerformanceCounter> _counters = new();
    private readonly object _lockObject = new();
    private CompliancePerformanceMetrics _metrics = new();

    public CompliancePerformanceMonitorService(ILogger<CompliancePerformanceMonitorService> logger)
    {
        _logger = logger;
        InitializeCounters();
    }

    public void RecordReportGeneration(string framework, ReportFormat format, TimeSpan duration, bool fromCache)
    {
        lock (_lockObject)
        {
            _metrics.TotalReportsGenerated++;
            _metrics.TotalReportGenerationTime += duration;

            if (fromCache)
            {
                _metrics.CacheHits++;
            }
            else
            {
                _metrics.CacheMisses++;
            }

            // Track by framework
            if (!_metrics.ReportsByFramework.ContainsKey(framework))
                _metrics.ReportsByFramework[framework] = 0;
            _metrics.ReportsByFramework[framework]++;

            // Track by format
            if (!_metrics.ReportsByFormat.ContainsKey(format))
                _metrics.ReportsByFormat[format] = 0;
            _metrics.ReportsByFormat[format]++;

            // Update performance counters
            UpdateCounter($"reports_generated_{framework.ToLowerInvariant()}", 1);
            UpdateCounter($"reports_format_{format.ToString().ToLowerInvariant()}", 1);

            if (duration.TotalMilliseconds > _metrics.SlowestReportGenerationTime.TotalMilliseconds)
            {
                _metrics.SlowestReportGenerationTime = duration;
                _metrics.SlowestReportFramework = framework;
            }

            if (_metrics.FastestReportGenerationTime == TimeSpan.Zero ||
                duration.TotalMilliseconds < _metrics.FastestReportGenerationTime.TotalMilliseconds)
            {
                _metrics.FastestReportGenerationTime = duration;
            }
        }

        _logger.LogDebug("Report generation recorded: {Framework} ({Format}) - {Duration}ms (Cache: {FromCache})",
            framework, format, duration.TotalMilliseconds, fromCache);
    }

    public void RecordPdfGeneration(string framework, TimeSpan duration, int sizeBytes)
    {
        lock (_lockObject)
        {
            _metrics.TotalPdfsGenerated++;
            _metrics.TotalPdfGenerationTime += duration;
            _metrics.TotalPdfSizeBytes += sizeBytes;

            if (duration.TotalMilliseconds > _metrics.SlowestPdfGenerationTime.TotalMilliseconds)
            {
                _metrics.SlowestPdfGenerationTime = duration;
            }

            if (_metrics.FastestPdfGenerationTime == TimeSpan.Zero ||
                duration.TotalMilliseconds < _metrics.FastestPdfGenerationTime.TotalMilliseconds)
            {
                _metrics.FastestPdfGenerationTime = duration;
            }

            if (sizeBytes > _metrics.LargestPdfSizeBytes)
            {
                _metrics.LargestPdfSizeBytes = sizeBytes;
            }

            if (_metrics.SmallestPdfSizeBytes == 0 || sizeBytes < _metrics.SmallestPdfSizeBytes)
            {
                _metrics.SmallestPdfSizeBytes = sizeBytes;
            }

            UpdateCounter("pdfs_generated", 1);
            UpdateCounter("pdf_size_bytes", sizeBytes);
        }

        _logger.LogDebug("PDF generation recorded: {Framework} - {Duration}ms, {Size} bytes",
            framework, duration.TotalMilliseconds, sizeBytes);
    }

    public void RecordCacheOperation(string operation, TimeSpan duration, bool hit)
    {
        lock (_lockObject)
        {
            _metrics.TotalCacheOperations++;
            _metrics.TotalCacheOperationTime += duration;

            if (hit)
            {
                _metrics.CacheHits++;
            }
            else
            {
                _metrics.CacheMisses++;
            }

            UpdateCounter($"cache_{operation.ToLowerInvariant()}", 1);
            UpdateCounter($"cache_{(hit ? "hit" : "miss")}", 1);
        }

        _logger.LogDebug("Cache operation recorded: {Operation} - {Duration}ms (Hit: {Hit})",
            operation, duration.TotalMilliseconds, hit);
    }

    public CompliancePerformanceMetrics GetPerformanceMetrics()
    {
        lock (_lockObject)
        {
            // Calculate derived metrics
            _metrics.AverageReportGenerationTime = _metrics.TotalReportsGenerated > 0
                ? TimeSpan.FromMilliseconds(_metrics.TotalReportGenerationTime.TotalMilliseconds / _metrics.TotalReportsGenerated)
                : TimeSpan.Zero;

            _metrics.AveragePdfGenerationTime = _metrics.TotalPdfsGenerated > 0
                ? TimeSpan.FromMilliseconds(_metrics.TotalPdfGenerationTime.TotalMilliseconds / _metrics.TotalPdfsGenerated)
                : TimeSpan.Zero;

            _metrics.AveragePdfSizeBytes = _metrics.TotalPdfsGenerated > 0
                ? _metrics.TotalPdfSizeBytes / _metrics.TotalPdfsGenerated
                : 0;

            _metrics.CacheHitRate = _metrics.TotalCacheOperations > 0
                ? (double)_metrics.CacheHits / _metrics.TotalCacheOperations
                : 0.0;

            _metrics.LastUpdated = DateTime.UtcNow;

            // Create a copy to avoid modification while reading
            return new CompliancePerformanceMetrics
            {
                TotalReportsGenerated = _metrics.TotalReportsGenerated,
                TotalReportGenerationTime = _metrics.TotalReportGenerationTime,
                AverageReportGenerationTime = _metrics.AverageReportGenerationTime,
                FastestReportGenerationTime = _metrics.FastestReportGenerationTime,
                SlowestReportGenerationTime = _metrics.SlowestReportGenerationTime,
                SlowestReportFramework = _metrics.SlowestReportFramework,

                TotalPdfsGenerated = _metrics.TotalPdfsGenerated,
                TotalPdfGenerationTime = _metrics.TotalPdfGenerationTime,
                AveragePdfGenerationTime = _metrics.AveragePdfGenerationTime,
                FastestPdfGenerationTime = _metrics.FastestPdfGenerationTime,
                SlowestPdfGenerationTime = _metrics.SlowestPdfGenerationTime,
                TotalPdfSizeBytes = _metrics.TotalPdfSizeBytes,
                AveragePdfSizeBytes = _metrics.AveragePdfSizeBytes,
                LargestPdfSizeBytes = _metrics.LargestPdfSizeBytes,
                SmallestPdfSizeBytes = _metrics.SmallestPdfSizeBytes,

                TotalCacheOperations = _metrics.TotalCacheOperations,
                TotalCacheOperationTime = _metrics.TotalCacheOperationTime,
                CacheHits = _metrics.CacheHits,
                CacheMisses = _metrics.CacheMisses,
                CacheHitRate = _metrics.CacheHitRate,

                ReportsByFramework = new Dictionary<string, int>(_metrics.ReportsByFramework),
                ReportsByFormat = new Dictionary<ReportFormat, int>(_metrics.ReportsByFormat),
                LastUpdated = _metrics.LastUpdated
            };
        }
    }

    public void ResetMetrics()
    {
        lock (_lockObject)
        {
            _metrics = new CompliancePerformanceMetrics();
            _counters.Clear();
            InitializeCounters();
        }

        _logger.LogInformation("Compliance performance metrics reset");
    }

    private void InitializeCounters()
    {
        var baseCounters = new[]
        {
            "reports_generated_total",
            "pdfs_generated",
            "cache_hit",
            "cache_miss",
            "cache_get",
            "cache_set",
            "cache_invalidate"
        };

        foreach (var counter in baseCounters)
        {
            _counters[counter] = new PerformanceCounter();
        }
    }

    private void UpdateCounter(string name, long value)
    {
        if (!_counters.ContainsKey(name))
        {
            _counters[name] = new PerformanceCounter();
        }

        _counters[name].Increment(value);
    }
}

public class CompliancePerformanceMetrics
{
    // Report Generation Metrics
    public int TotalReportsGenerated { get; set; }
    public TimeSpan TotalReportGenerationTime { get; set; }
    public TimeSpan AverageReportGenerationTime { get; set; }
    public TimeSpan FastestReportGenerationTime { get; set; }
    public TimeSpan SlowestReportGenerationTime { get; set; }
    public string SlowestReportFramework { get; set; } = string.Empty;

    // PDF Generation Metrics
    public int TotalPdfsGenerated { get; set; }
    public TimeSpan TotalPdfGenerationTime { get; set; }
    public TimeSpan AveragePdfGenerationTime { get; set; }
    public TimeSpan FastestPdfGenerationTime { get; set; }
    public TimeSpan SlowestPdfGenerationTime { get; set; }
    public long TotalPdfSizeBytes { get; set; }
    public long AveragePdfSizeBytes { get; set; }
    public int LargestPdfSizeBytes { get; set; }
    public int SmallestPdfSizeBytes { get; set; }

    // Cache Metrics
    public int TotalCacheOperations { get; set; }
    public TimeSpan TotalCacheOperationTime { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double CacheHitRate { get; set; }

    // Breakdown Metrics
    public Dictionary<string, int> ReportsByFramework { get; set; } = new();
    public Dictionary<ReportFormat, int> ReportsByFormat { get; set; } = new();

    // Metadata
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class PerformanceCounter
{
    private long _value;
    private readonly object _lock = new();

    public long Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
    }

    public void Increment(long amount = 1)
    {
        lock (_lock)
        {
            _value += amount;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _value = 0;
        }
    }
}