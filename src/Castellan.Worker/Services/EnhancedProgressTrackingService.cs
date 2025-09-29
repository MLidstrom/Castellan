using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Timer = System.Threading.Timer;
using Castellan.Worker.Models;
using Castellan.Worker.Models.ThreatIntelligence;
using Castellan.Worker.Services.Interfaces;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Hubs;

namespace Castellan.Worker.Services;

/// <summary>
/// Enhanced progress tracking service that provides comprehensive system monitoring,
/// scan progress tracking, and real-time updates via SignalR
/// </summary>
public interface IEnhancedProgressTrackingService
{
    Task<EnhancedSystemStatus> GetSystemStatusAsync();
    Task<ThreatIntelligenceServiceStatus> GetThreatIntelligenceStatusAsync();
    Task<EnhancedCacheStatistics> GetCacheStatisticsAsync();
    Task<EnhancedPerformanceMetrics> GetPerformanceMetricsAsync();
    Task BroadcastSystemUpdate();
    Task StartPeriodicUpdates(CancellationToken cancellationToken);
}

/// <summary>
/// Enhanced system status with additional metrics and monitoring data
/// </summary>
public class EnhancedSystemStatus
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public SystemHealthSummary Health { get; set; } = new();
    public ThreatIntelligenceServiceStatus ThreatIntelligence { get; set; } = new();
    public EnhancedCacheStatistics Cache { get; set; } = new();
    public EnhancedPerformanceMetrics Performance { get; set; } = new();
    public ScanStatus ActiveScans { get; set; } = new();
}

/// <summary>
/// System health summary with component status
/// </summary>
public class SystemHealthSummary
{
    public bool IsHealthy { get; set; }
    public Dictionary<string, ComponentHealth> Components { get; set; } = new();
    public int TotalComponents { get; set; }
    public int HealthyComponents { get; set; }
    public TimeSpan SystemUptime { get; set; }
}

/// <summary>
/// Individual component health status
/// </summary>
public class ComponentHealth
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastCheck { get; set; }
    public int ResponseTimeMs { get; set; }
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Threat intelligence service status aggregation
/// </summary>
public class ThreatIntelligenceServiceStatus
{
    public bool IsEnabled { get; set; }
    public Dictionary<string, ServiceHealth> Services { get; set; } = new();
    public int TotalQueries { get; set; }
    public int CacheHits { get; set; }
    public double CacheHitRate { get; set; }
    public DateTime LastQuery { get; set; }
}

/// <summary>
/// Individual threat intelligence service health
/// </summary>
public class ServiceHealth
{
    public bool IsEnabled { get; set; }
    public bool IsHealthy { get; set; }
    public int ApiCallsToday { get; set; }
    public int RateLimit { get; set; }
    public int RemainingQuota { get; set; }
    public DateTime LastSuccessfulQuery { get; set; }
    public string LastError { get; set; } = string.Empty;
}

/// <summary>
/// Enhanced cache statistics and performance metrics
/// </summary>
public class EnhancedCacheStatistics
{
    public EmbeddingCacheStats Embedding { get; set; } = new();
    public ThreatIntelligenceCache ThreatIntelligence { get; set; } = new();
    public GeneralCacheStats General { get; set; } = new();
}

/// <summary>
/// Embedding cache statistics
/// </summary>
public class EmbeddingCacheStats
{
    public int TotalEntries { get; set; }
    public int Hits { get; set; }
    public int Misses { get; set; }
    public double HitRate { get; set; }
    public long MemoryUsageMB { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
}

/// <summary>
/// Threat intelligence cache statistics
/// </summary>
public class ThreatIntelligenceCache
{
    public int TotalHashes { get; set; }
    public int CachedResults { get; set; }
    public double CacheUtilization { get; set; }
    public DateTime OldestEntry { get; set; }
    public int ExpiredEntries { get; set; }
}

/// <summary>
/// General system cache statistics
/// </summary>
public class GeneralCacheStats
{
    public long TotalMemoryUsageMB { get; set; }
    public int ActiveCaches { get; set; }
    public double MemoryPressure { get; set; }
    public int EvictedEntries { get; set; }
}

/// <summary>
/// Enhanced system performance metrics
/// </summary>
public class EnhancedPerformanceMetrics
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public EventProcessingStats EventProcessing { get; set; } = new();
    public VectorOperationStats VectorOperations { get; set; } = new();
}

/// <summary>
/// Event processing performance statistics
/// </summary>
public class EventProcessingStats
{
    public int EventsPerSecond { get; set; }
    public int TotalEventsProcessed { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
    public int QueuedEvents { get; set; }
    public int FailedEvents { get; set; }
}

/// <summary>
/// Vector operation performance statistics
/// </summary>
public class VectorOperationStats
{
    public int VectorsPerSecond { get; set; }
    public TimeSpan AverageEmbeddingTime { get; set; }
    public TimeSpan AverageUpsertTime { get; set; }
    public TimeSpan AverageSearchTime { get; set; }
    public int BatchOperations { get; set; }
}

/// <summary>
/// Active scan status information
/// </summary>
public class ScanStatus
{
    public bool HasActiveScan { get; set; }
    public ThreatScanProgress? CurrentScan { get; set; }
    public int QueuedScans { get; set; }
    public List<ScanSummary> RecentScans { get; set; } = new();
}

/// <summary>
/// Summary of scan information
/// </summary>
public class ScanSummary
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public ThreatScanStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int FilesScanned { get; set; }
    public int ThreatsFound { get; set; }
}

/// <summary>
/// Implementation of enhanced progress tracking service
/// </summary>
public class EnhancedProgressTrackingService : IEnhancedProgressTrackingService
{
    private readonly ILogger<EnhancedProgressTrackingService> _logger;
    private readonly IScanProgressBroadcaster _broadcaster;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly SystemHealthService _systemHealth;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IVirusTotalService _virusTotalService;
    private readonly IMalwareBazaarService _malwareBazaarService;
    private readonly IOtxService _otxService;
    private readonly IMemoryCache _memoryCache;
    private readonly Process _currentProcess;
    private Timer? _updateTimer;

    public EnhancedProgressTrackingService(
        ILogger<EnhancedProgressTrackingService> logger,
        IScanProgressBroadcaster broadcaster,
        IServiceScopeFactory serviceScopeFactory,
        SystemHealthService systemHealth,
        IPerformanceMonitor performanceMonitor,
        IVirusTotalService virusTotalService,
        IMalwareBazaarService malwareBazaarService,
        IOtxService otxService,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _broadcaster = broadcaster;
        _serviceScopeFactory = serviceScopeFactory;
        _systemHealth = systemHealth;
        _performanceMonitor = performanceMonitor;
        _virusTotalService = virusTotalService;
        _malwareBazaarService = malwareBazaarService;
        _otxService = otxService;
        _memoryCache = memoryCache;
        _currentProcess = Process.GetCurrentProcess();
    }

    /// <summary>
    /// Get comprehensive system status including all monitored components
    /// </summary>
    public async Task<EnhancedSystemStatus> GetSystemStatusAsync()
    {
        try
        {
            var status = new EnhancedSystemStatus
            {
                Health = await GetSystemHealthSummaryAsync(),
                ThreatIntelligence = await GetThreatIntelligenceStatusAsync(),
                Cache = await GetCacheStatisticsAsync(),
                Performance = await GetPerformanceMetricsAsync(),
                ActiveScans = await GetActiveScanStatusAsync()
            };

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting enhanced system status");
            throw;
        }
    }

    /// <summary>
    /// Get threat intelligence service status
    /// </summary>
    public async Task<ThreatIntelligenceServiceStatus> GetThreatIntelligenceStatusAsync()
    {
        try
        {
            var status = new ThreatIntelligenceServiceStatus
            {
                IsEnabled = true, // Based on configuration
                Services = new Dictionary<string, ServiceHealth>()
            };

            // Check VirusTotal service
            var virusTotalHealth = await GetServiceHealthAsync("VirusTotal", _virusTotalService.IsHealthyAsync);
            status.Services["VirusTotal"] = virusTotalHealth;

            // Check MalwareBazaar service  
            var malwareBazaarHealth = await GetServiceHealthAsync("MalwareBazaar", _malwareBazaarService.IsHealthyAsync);
            status.Services["MalwareBazaar"] = malwareBazaarHealth;

            // Check AlienVault OTX service
            var otxHealth = await GetServiceHealthAsync("AlienVault OTX", _otxService.IsHealthyAsync);
            status.Services["OTX"] = otxHealth;

            // Get cache statistics for threat intelligence
            // This would be expanded based on actual cache implementation
            status.CacheHits = 0; // Placeholder - implement cache hit tracking
            status.TotalQueries = 0; // Placeholder - implement query tracking

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat intelligence status");
            throw;
        }
    }

    /// <summary>
    /// Get cache statistics (simplified after cache removal)
    /// </summary>
    public async Task<EnhancedCacheStatistics> GetCacheStatisticsAsync()
    {
        try
        {
            var stats = new EnhancedCacheStatistics();

            // Embedding cache disabled - return empty stats
            stats.Embedding = new EmbeddingCacheStats
            {
                TotalEntries = 0,
                HitRate = 0.0,
                MemoryUsageMB = 0
            };

            // Only basic memory statistics available
            stats.General = new GeneralCacheStats
            {
                TotalMemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024),
                ActiveCaches = 1, // Only system memory cache remains
                MemoryPressure = 0.0 
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache statistics");
            throw;
        }
    }

    /// <summary>
    /// Get system performance metrics
    /// </summary>
    public async Task<EnhancedPerformanceMetrics> GetPerformanceMetricsAsync()
    {
        try
        {
            _currentProcess.Refresh();

            // Get real performance metrics from the performance monitor
            var realMetrics = _performanceMonitor.GetCurrentMetrics();

            var metrics = new EnhancedPerformanceMetrics
            {
                CpuUsagePercent = realMetrics.System.CpuUsagePercent,
                MemoryUsageMB = _currentProcess.WorkingSet64 / (1024 * 1024),
                ThreadCount = _currentProcess.Threads.Count,
                HandleCount = _currentProcess.HandleCount,

                // Map real event processing metrics from performance monitor
                EventProcessing = new EventProcessingStats
                {
                    EventsPerSecond = (int)(realMetrics.Pipeline.EventsPerMinute / 60.0), // Convert per minute to per second
                    TotalEventsProcessed = (int)realMetrics.Pipeline.TotalEventsProcessed,
                    QueuedEvents = realMetrics.Pipeline.QueueDepth,
                    FailedEvents = (int)realMetrics.Pipeline.ProcessingErrors,
                    AverageProcessingTime = TimeSpan.FromMilliseconds(realMetrics.Pipeline.AverageProcessingTimeMs)
                },

                // Map real vector operation metrics from performance monitor
                VectorOperations = new VectorOperationStats
                {
                    VectorsPerSecond = (int)(realMetrics.VectorStore.TotalVectors > 0 ?
                        Math.Max(1, realMetrics.VectorStore.TotalVectors / Math.Max(1, realMetrics.Pipeline.UptimeSeconds)) : 0),
                    AverageEmbeddingTime = TimeSpan.FromMilliseconds(realMetrics.VectorStore.AverageEmbeddingTimeMs),
                    AverageUpsertTime = TimeSpan.FromMilliseconds(realMetrics.VectorStore.AverageUpsertTimeMs),
                    AverageSearchTime = TimeSpan.FromMilliseconds(realMetrics.VectorStore.AverageSearchTimeMs),
                    BatchOperations = (int)realMetrics.VectorStore.LastCleanupVectorCount
                }
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics");
            throw;
        }
    }

    /// <summary>
    /// Broadcast system updates to connected clients
    /// </summary>
    public async Task BroadcastSystemUpdate()
    {
        try
        {
            var status = await GetSystemStatusAsync();
            await _broadcaster.BroadcastSystemMetrics(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting system update");
        }
    }

    /// <summary>
    /// Start periodic system updates to connected clients
    /// </summary>
    public async Task StartPeriodicUpdates(CancellationToken cancellationToken)
    {
        _updateTimer = new Timer(async _ => await BroadcastSystemUpdate(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        _logger.LogInformation("Started periodic system updates every 30 seconds");
        
        // Keep the task running until cancellation
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }

    // Private helper methods

    private async Task<SystemHealthSummary> GetSystemHealthSummaryAsync()
    {
        var components = await _systemHealth.GetSystemStatusAsync();
        
        return new SystemHealthSummary
        {
            IsHealthy = components.All(c => c.Status == "Healthy"),
            Components = components.ToDictionary(
                c => c.Component,
                c => new ComponentHealth
                {
                    IsHealthy = c.Status == "Healthy",
                    Status = c.Status,
                    LastCheck = c.LastCheck,
                    ResponseTimeMs = c.ResponseTime,
                    Details = c.Details
                }
            ),
            TotalComponents = components.Count(),
            HealthyComponents = components.Count(c => c.Status == "Healthy"),
            SystemUptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime
        };
    }

    private async Task<ServiceHealth> GetServiceHealthAsync(string serviceName, Func<CancellationToken, Task<bool>> healthCheck)
    {
        try
        {
            var isHealthy = await healthCheck(CancellationToken.None);
            
            return new ServiceHealth
            {
                IsEnabled = true,
                IsHealthy = isHealthy,
                ApiCallsToday = 0, // Placeholder - would track actual calls
                RateLimit = 1000, // Placeholder - would come from service configuration
                RemainingQuota = 1000, // Placeholder - would track actual quota
                LastSuccessfulQuery = DateTime.UtcNow, // Placeholder
                LastError = string.Empty
            };
        }
        catch (Exception ex)
        {
            return new ServiceHealth
            {
                IsEnabled = true,
                IsHealthy = false,
                LastError = ex.Message
            };
        }
    }

    private async Task<ScanStatus> GetActiveScanStatusAsync()
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var threatScanner = scope.ServiceProvider.GetRequiredService<IThreatScanner>();
            var currentProgress = await threatScanner.GetScanProgressAsync();
            var recentScans = await threatScanner.GetScanHistoryAsync(5);

            return new ScanStatus
            {
                HasActiveScan = currentProgress != null,
                CurrentScan = currentProgress,
                QueuedScans = 0, // Placeholder - would track queued scans
                RecentScans = recentScans.Select(scan => new ScanSummary
                {
                    Id = scan.Id,
                    Type = scan.ScanType.ToString(),
                    Status = scan.Status,
                    StartTime = scan.StartTime,
                    Duration = scan.Duration,
                    FilesScanned = scan.FilesScanned,
                    ThreatsFound = scan.ThreatsFound
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active scan status");
            return new ScanStatus();
        }
    }

    public void Dispose()
    {
        _updateTimer?.Dispose();
        _currentProcess?.Dispose();
    }
}
