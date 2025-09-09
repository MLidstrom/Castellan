using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services;
using Castellan.Worker.Models;
using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/performance")]
[Authorize]
public class PerformanceController : ControllerBase
{
    private readonly ILogger<PerformanceController> _logger;
    private readonly PerformanceMetricsService _performanceMetricsService;
    private readonly PerformanceAlertService _performanceAlertService;

    public PerformanceController(
        ILogger<PerformanceController> logger,
        PerformanceMetricsService performanceMetricsService,
        PerformanceAlertService performanceAlertService)
    {
        _logger = logger;
        _performanceMetricsService = performanceMetricsService;
        _performanceAlertService = performanceAlertService;
    }

    /// <summary>
    /// Get historical performance metrics for specified time range
    /// </summary>
    /// <param name="timeRange">Time range: 1h, 6h, 24h, 7d</param>
    /// <returns>Historical performance data with time series</returns>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetPerformanceMetrics(
        [FromQuery][Required] string timeRange = "1h")
    {
        try
        {
            _logger.LogInformation("Getting performance metrics for time range: {TimeRange}", timeRange);

            // Validate time range parameter
            if (!IsValidTimeRange(timeRange))
            {
                return BadRequest(new { message = "Invalid time range. Valid values: 1h, 6h, 24h, 7d" });
            }

            var metrics = await _performanceMetricsService.GetHistoricalMetricsAsync(timeRange);
            
            return Ok(new
            {
                timeRange,
                dataPoints = metrics.DataPoints,
                summary = metrics.Summary,
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics for time range: {TimeRange}", timeRange);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get current performance alerts and alert history
    /// </summary>
    /// <returns>Active alerts and alert history</returns>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetPerformanceAlerts()
    {
        try
        {
            _logger.LogInformation("Getting performance alerts");

            var alerts = await _performanceAlertService.GetAlertsAsync();
            
            return Ok(new
            {
                active = alerts.Active,
                history = alerts.History,
                summary = new
                {
                    totalActive = alerts.Active.Count,
                    criticalCount = alerts.Active.Count(a => a.Severity == "critical"),
                    warningCount = alerts.Active.Count(a => a.Severity == "warning"),
                    lastCheck = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance alerts");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get cache performance statistics
    /// </summary>
    /// <returns>Cache hit rates, memory usage, and effectiveness metrics</returns>
    [HttpGet("cache-stats")]
    public async Task<IActionResult> GetCacheStatistics()
    {
        try
        {
            _logger.LogInformation("Getting cache statistics");

            var cacheStats = await _performanceMetricsService.GetCacheStatisticsAsync();
            
            return Ok(new
            {
                hitRate = cacheStats.HitRate,
                missRate = cacheStats.MissRate,
                memoryUsage = cacheStats.MemoryUsage,
                totalRequests = cacheStats.TotalRequests,
                effectivenessRatio = cacheStats.EffectivenessRatio,
                cacheEntries = cacheStats.CacheEntries,
                averageResponseTime = cacheStats.AverageResponseTime,
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache statistics");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get database performance metrics
    /// </summary>
    /// <returns>Connection pool status, query performance, and database metrics</returns>
    [HttpGet("database")]
    public async Task<IActionResult> GetDatabasePerformance()
    {
        try
        {
            _logger.LogInformation("Getting database performance metrics");

            var dbMetrics = await _performanceMetricsService.GetDatabasePerformanceAsync();
            
            return Ok(new
            {
                connectionPool = new
                {
                    active = dbMetrics.ConnectionPool.Active,
                    total = dbMetrics.ConnectionPool.Total,
                    utilization = dbMetrics.ConnectionPool.Utilization,
                    peakConnections = dbMetrics.ConnectionPool.PeakConnections
                },
                queryPerformance = new
                {
                    avgResponseTime = dbMetrics.QueryPerformance.AverageResponseTime,
                    slowQueries = dbMetrics.QueryPerformance.SlowQueries,
                    totalQueries = dbMetrics.QueryPerformance.TotalQueries,
                    queriesPerSecond = dbMetrics.QueryPerformance.QueriesPerSecond
                },
                qdrantMetrics = new
                {
                    avgOperationTime = dbMetrics.QdrantMetrics.AverageOperationTime,
                    vectorCount = dbMetrics.QdrantMetrics.VectorCount,
                    collectionStatus = dbMetrics.QdrantMetrics.CollectionStatus,
                    batchOperationTime = dbMetrics.QdrantMetrics.BatchOperationTime
                },
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database performance metrics");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get system resource metrics (CPU, memory, disk, network)
    /// </summary>
    /// <returns>Detailed system resource utilization metrics</returns>
    [HttpGet("system-resources")]
    public async Task<IActionResult> GetSystemResources()
    {
        try
        {
            _logger.LogInformation("Getting system resource metrics");

            var systemResources = await _performanceMetricsService.GetSystemResourcesAsync();
            
            return Ok(new
            {
                cpu = new
                {
                    usage = systemResources.Cpu.Usage,
                    cores = systemResources.Cpu.Cores,
                    loadAverage = systemResources.Cpu.LoadAverage,
                    processUsage = systemResources.Cpu.ProcessUsage
                },
                memory = new
                {
                    usage = systemResources.Memory.Usage,
                    total = systemResources.Memory.Total,
                    available = systemResources.Memory.Available,
                    processMemory = systemResources.Memory.ProcessMemory
                },
                disk = new
                {
                    usage = systemResources.Disk.Usage,
                    total = systemResources.Disk.Total,
                    available = systemResources.Disk.Available,
                    readSpeed = systemResources.Disk.ReadSpeed,
                    writeSpeed = systemResources.Disk.WriteSpeed
                },
                network = new
                {
                    bytesReceived = systemResources.Network.BytesReceived,
                    bytesSent = systemResources.Network.BytesSent,
                    packetsReceived = systemResources.Network.PacketsReceived,
                    packetsSent = systemResources.Network.PacketsSent
                },
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system resource metrics");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get performance dashboard summary data
    /// </summary>
    /// <returns>Consolidated performance summary for dashboard</returns>
    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> GetDashboardSummary()
    {
        try
        {
            _logger.LogInformation("Getting performance dashboard summary");

            var summary = await _performanceMetricsService.GetDashboardSummaryAsync();
            
            return Ok(new
            {
                overall = new
                {
                    healthScore = summary.Overall.HealthScore,
                    status = summary.Overall.Status,
                    uptime = summary.Overall.Uptime
                },
                performance = new
                {
                    avgResponseTime = summary.Performance.AverageResponseTime,
                    throughput = summary.Performance.Throughput,
                    errorRate = summary.Performance.ErrorRate
                },
                resources = new
                {
                    cpuUsage = summary.Resources.CpuUsage,
                    memoryUsage = summary.Resources.MemoryUsage,
                    diskUsage = summary.Resources.DiskUsage
                },
                alerts = new
                {
                    active = summary.Alerts.Active,
                    critical = summary.Alerts.Critical,
                    warnings = summary.Alerts.Warnings
                },
                lastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance dashboard summary");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update performance alert thresholds
    /// </summary>
    /// <param name="thresholds">Alert threshold configuration</param>
    /// <returns>Success status</returns>
    [HttpPost("alert-thresholds")]
    public async Task<IActionResult> UpdateAlertThresholds([FromBody] PerformanceThresholds thresholds)
    {
        try
        {
            _logger.LogInformation("Updating performance alert thresholds");

            await _performanceAlertService.UpdateThresholdsAsync(thresholds);
            
            return Ok(new
            {
                message = "Alert thresholds updated successfully",
                thresholds,
                updatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance alert thresholds");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private static bool IsValidTimeRange(string timeRange)
    {
        return timeRange?.ToLower() switch
        {
            "1h" or "6h" or "24h" or "7d" => true,
            _ => false
        };
    }
}
