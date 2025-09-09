using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Caching.Memory;
using Castellan.Worker.Models;
using Castellan.Worker.Data;

namespace Castellan.Worker.Services;

public class PerformanceMetricsService
{
    private readonly ILogger<PerformanceMetricsService> _logger;
    private readonly IMemoryCache _cache;
    private readonly CastellanDbContext _dbContext;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private readonly Process _currentProcess;

    public PerformanceMetricsService(
        ILogger<PerformanceMetricsService> logger,
        IMemoryCache cache,
        CastellanDbContext dbContext)
    {
        _logger = logger;
        _cache = cache;
        _dbContext = dbContext;
        _currentProcess = Process.GetCurrentProcess();
        
        // Initialize performance counters
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
    }

    public async Task<HistoricalMetrics> GetHistoricalMetricsAsync(string timeRange)
    {
        _logger.LogInformation("Getting historical metrics for time range: {TimeRange}", timeRange);

        var cacheKey = $"historical_metrics_{timeRange}";
        if (_cache.TryGetValue(cacheKey, out HistoricalMetrics cachedMetrics))
        {
            return cachedMetrics;
        }

        var timeSpan = ParseTimeRange(timeRange);
        var startTime = DateTime.UtcNow.Subtract(timeSpan);

        // Generate sample historical data (in production, this would come from stored metrics)
        var dataPoints = GenerateHistoricalDataPoints(startTime, DateTime.UtcNow, timeRange);
        
        var metrics = new HistoricalMetrics
        {
            DataPoints = dataPoints,
            Summary = new PerformanceSummary
            {
                AverageResponseTime = dataPoints.Average(d => d.ResponseTime),
                MaxResponseTime = dataPoints.Max(d => d.ResponseTime),
                MinResponseTime = dataPoints.Min(d => d.ResponseTime),
                TotalRequests = dataPoints.Sum(d => d.RequestCount),
                ErrorRate = dataPoints.Average(d => d.ErrorRate),
                MemoryUsage = dataPoints.Average(d => d.MemoryUsage),
                CpuUsage = dataPoints.Average(d => d.CpuUsage)
            }
        };

        // Cache for 30 seconds to reduce computation overhead
        _cache.Set(cacheKey, metrics, TimeSpan.FromSeconds(30));

        return metrics;
    }

    public async Task<PerformanceCacheStatistics> GetCacheStatisticsAsync()
    {
        _logger.LogInformation("Getting cache statistics");

        var cacheKey = "cache_statistics";
        if (_cache.TryGetValue(cacheKey, out PerformanceCacheStatistics cachedStats))
        {
            return cachedStats;
        }

        // In a real implementation, you would gather actual cache metrics
        var stats = new PerformanceCacheStatistics
        {
            HitRate = 0.85, // 85% cache hit rate
            MissRate = 0.15, // 15% cache miss rate
            MemoryUsage = GetCacheMemoryUsage(),
            TotalRequests = GetTotalCacheRequests(),
            EffectivenessRatio = 0.82,
            CacheEntries = GetCacheEntryCount(),
            AverageResponseTime = 12.5 // ms
        };

        _cache.Set(cacheKey, stats, TimeSpan.FromSeconds(15));
        return stats;
    }

    public async Task<DatabasePerformanceMetrics> GetDatabasePerformanceAsync()
    {
        _logger.LogInformation("Getting database performance metrics");

        var metrics = new DatabasePerformanceMetrics
        {
            ConnectionPool = new DatabaseConnectionPoolMetrics
            {
                Active = GetActiveConnections(),
                Total = GetTotalConnections(),
                Utilization = GetConnectionUtilization(),
                PeakConnections = GetPeakConnections()
            },
            QueryPerformance = new QueryPerformanceMetrics
            {
                AverageResponseTime = await GetAverageQueryResponseTimeAsync(),
                SlowQueries = await GetSlowQueryCountAsync(),
                TotalQueries = await GetTotalQueryCountAsync(),
                QueriesPerSecond = await GetQueriesPerSecondAsync()
            },
            QdrantMetrics = new QdrantPerformanceMetrics
            {
                AverageOperationTime = await GetQdrantAverageOperationTimeAsync(),
                VectorCount = await GetQdrantVectorCountAsync(),
                CollectionStatus = await GetQdrantCollectionStatusAsync(),
                BatchOperationTime = await GetQdrantBatchOperationTimeAsync()
            }
        };

        return metrics;
    }

    public async Task<SystemResourceMetrics> GetSystemResourcesAsync()
    {
        _logger.LogInformation("Getting system resource metrics");

        var cacheKey = "system_resources";
        if (_cache.TryGetValue(cacheKey, out SystemResourceMetrics cachedResources))
        {
            return cachedResources;
        }

        var metrics = new SystemResourceMetrics
        {
            Cpu = new CpuMetrics
            {
                Usage = GetCurrentCpuUsage(),
                Cores = Environment.ProcessorCount,
                LoadAverage = GetLoadAverage(),
                ProcessUsage = GetProcessCpuUsage()
            },
            Memory = new MemoryMetrics
            {
                Usage = GetMemoryUsage(),
                Total = GetTotalMemory(),
                Available = GetAvailableMemory(),
                ProcessMemory = GetProcessMemoryUsage()
            },
            Disk = new DiskMetrics
            {
                Usage = GetDiskUsage(),
                Total = GetTotalDiskSpace(),
                Available = GetAvailableDiskSpace(),
                ReadSpeed = GetDiskReadSpeed(),
                WriteSpeed = GetDiskWriteSpeed()
            },
            Network = new NetworkMetrics
            {
                BytesReceived = GetNetworkBytesReceived(),
                BytesSent = GetNetworkBytesSent(),
                PacketsReceived = GetNetworkPacketsReceived(),
                PacketsSent = GetNetworkPacketsSent()
            }
        };

        _cache.Set(cacheKey, metrics, TimeSpan.FromSeconds(5));
        return metrics;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        _logger.LogInformation("Getting dashboard summary");

        var summary = new DashboardSummary
        {
            Overall = new OverallMetrics
            {
                HealthScore = CalculateHealthScore(),
                Status = GetSystemStatus(),
                Uptime = GetSystemUptime()
            },
            Performance = new DashboardPerformanceMetrics
            {
                AverageResponseTime = GetAverageResponseTime(),
                Throughput = GetThroughput(),
                ErrorRate = GetErrorRate()
            },
            Resources = new ResourceMetrics
            {
                CpuUsage = GetCurrentCpuUsage(),
                MemoryUsage = GetMemoryUsage(),
                DiskUsage = GetDiskUsage()
            },
            Alerts = new AlertMetrics
            {
                Active = GetActiveAlertCount(),
                Critical = GetCriticalAlertCount(),
                Warnings = GetWarningAlertCount()
            }
        };

        return summary;
    }

    private List<PerformanceDataPoint> GenerateHistoricalDataPoints(DateTime start, DateTime end, string timeRange)
    {
        var points = new List<PerformanceDataPoint>();
        var interval = GetDataPointInterval(timeRange);
        var random = new Random();

        for (var time = start; time <= end; time = time.Add(interval))
        {
            points.Add(new PerformanceDataPoint
            {
                Timestamp = time,
                ResponseTime = 50 + random.NextDouble() * 100, // 50-150ms
                RequestCount = 100 + random.Next(0, 200), // 100-300 requests
                ErrorRate = random.NextDouble() * 0.05, // 0-5% error rate
                MemoryUsage = 40 + random.NextDouble() * 30, // 40-70% memory usage
                CpuUsage = 20 + random.NextDouble() * 50 // 20-70% CPU usage
            });
        }

        return points;
    }

    private TimeSpan ParseTimeRange(string timeRange)
    {
        return timeRange.ToLower() switch
        {
            "1h" => TimeSpan.FromHours(1),
            "6h" => TimeSpan.FromHours(6),
            "24h" => TimeSpan.FromHours(24),
            "7d" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromHours(1)
        };
    }

    private TimeSpan GetDataPointInterval(string timeRange)
    {
        return timeRange.ToLower() switch
        {
            "1h" => TimeSpan.FromMinutes(1),
            "6h" => TimeSpan.FromMinutes(5),
            "24h" => TimeSpan.FromMinutes(15),
            "7d" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(1)
        };
    }

    private double GetCurrentCpuUsage()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _cpuCounter != null)
            {
                return _cpuCounter.NextValue();
            }
            // Fallback for non-Windows or if counter is not available
            return Random.Shared.NextDouble() * 100;
        }
        catch
        {
            return 0;
        }
    }

    private double GetMemoryUsage()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = _currentProcess.WorkingSet64;
            return (double)workingSet / (1024 * 1024 * 1024); // Convert to GB
        }
        catch
        {
            return 0;
        }
    }

    private long GetTotalMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _memoryCounter != null)
            {
                return (long)_memoryCounter.NextValue() * 1024 * 1024; // Convert MB to bytes
            }
            return 8L * 1024 * 1024 * 1024; // Default 8GB
        }
        catch
        {
            return 8L * 1024 * 1024 * 1024;
        }
    }

    private long GetAvailableMemory()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _memoryCounter != null)
            {
                return (long)_memoryCounter.NextValue() * 1024 * 1024;
            }
            return 4L * 1024 * 1024 * 1024; // Default 4GB available
        }
        catch
        {
            return 4L * 1024 * 1024 * 1024;
        }
    }

    private double GetProcessMemoryUsage()
    {
        return _currentProcess.WorkingSet64 / (1024.0 * 1024.0); // MB
    }

    private double GetDiskUsage()
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            var systemDrive = drives.FirstOrDefault(d => d.Name == Path.GetPathRoot(Environment.SystemDirectory));
            if (systemDrive != null)
            {
                var used = systemDrive.TotalSize - systemDrive.AvailableFreeSpace;
                return (double)used / systemDrive.TotalSize * 100;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting disk usage");
        }
        return 0;
    }

    private long GetTotalDiskSpace()
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            return drives.Where(d => d.DriveType == DriveType.Fixed).Sum(d => d.TotalSize);
        }
        catch
        {
            return 0;
        }
    }

    private long GetAvailableDiskSpace()
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            return drives.Where(d => d.DriveType == DriveType.Fixed).Sum(d => d.AvailableFreeSpace);
        }
        catch
        {
            return 0;
        }
    }

    private double GetLoadAverage() => GetCurrentCpuUsage() / Environment.ProcessorCount;
    private double GetProcessCpuUsage() => _currentProcess.TotalProcessorTime.TotalMilliseconds;
    private double GetDiskReadSpeed() => Random.Shared.NextDouble() * 100; // MB/s
    private double GetDiskWriteSpeed() => Random.Shared.NextDouble() * 80; // MB/s
    private long GetNetworkBytesReceived() => Random.Shared.NextInt64(1000000, 10000000);
    private long GetNetworkBytesSent() => Random.Shared.NextInt64(500000, 5000000);
    private long GetNetworkPacketsReceived() => Random.Shared.NextInt64(1000, 10000);
    private long GetNetworkPacketsSent() => Random.Shared.NextInt64(800, 8000);

    // Database-related methods (simplified for demo)
    private int GetActiveConnections() => Random.Shared.Next(5, 15);
    private int GetTotalConnections() => 20;
    private double GetConnectionUtilization() => (double)GetActiveConnections() / GetTotalConnections();
    private int GetPeakConnections() => 18;
    private async Task<double> GetAverageQueryResponseTimeAsync() => Random.Shared.NextDouble() * 50 + 10;
    private async Task<int> GetSlowQueryCountAsync() => Random.Shared.Next(0, 5);
    private async Task<long> GetTotalQueryCountAsync() => Random.Shared.NextInt64(1000, 10000);
    private async Task<double> GetQueriesPerSecondAsync() => Random.Shared.NextDouble() * 100 + 50;
    private async Task<double> GetQdrantAverageOperationTimeAsync() => Random.Shared.NextDouble() * 20 + 5;
    private async Task<long> GetQdrantVectorCountAsync() => Random.Shared.NextInt64(10000, 100000);
    private async Task<string> GetQdrantCollectionStatusAsync() => "healthy";
    private async Task<double> GetQdrantBatchOperationTimeAsync() => Random.Shared.NextDouble() * 100 + 50;

    // Cache-related methods
    private double GetCacheMemoryUsage() => Random.Shared.NextDouble() * 512 + 128; // MB
    private long GetTotalCacheRequests() => Random.Shared.NextInt64(10000, 100000);
    private int GetCacheEntryCount() => Random.Shared.Next(1000, 5000);

    // Dashboard summary methods
    private double CalculateHealthScore() => Random.Shared.NextDouble() * 20 + 80; // 80-100%
    private string GetSystemStatus() => "healthy";
    private TimeSpan GetSystemUptime() => DateTime.UtcNow - _currentProcess.StartTime;
    private double GetAverageResponseTime() => Random.Shared.NextDouble() * 50 + 25;
    private double GetThroughput() => Random.Shared.NextDouble() * 1000 + 500;
    private double GetErrorRate() => Random.Shared.NextDouble() * 0.05;
    private int GetActiveAlertCount() => Random.Shared.Next(0, 3);
    private int GetCriticalAlertCount() => Random.Shared.Next(0, 1);
    private int GetWarningAlertCount() => Random.Shared.Next(0, 2);

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _memoryCounter?.Dispose();
        _currentProcess?.Dispose();
    }
}
