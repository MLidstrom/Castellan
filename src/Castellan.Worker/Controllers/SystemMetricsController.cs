using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services;

namespace Castellan.Worker.Controllers;

/// <summary>
/// Controller providing enhanced system metrics, monitoring data, and performance statistics
/// </summary>
[ApiController]
[Route("api/system-metrics")]
[Authorize]
public class SystemMetricsController : ControllerBase
{
    private readonly ILogger<SystemMetricsController> _logger;
    private readonly IEnhancedProgressTrackingService _progressTracker;

    public SystemMetricsController(
        ILogger<SystemMetricsController> logger,
        IEnhancedProgressTrackingService progressTracker)
    {
        _logger = logger;
        _progressTracker = progressTracker;
    }

    /// <summary>
    /// Get comprehensive system status including health, performance, and threat intelligence metrics
    /// </summary>
    /// <returns>Enhanced system status with detailed metrics</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetEnhancedStatus()
    {
        try
        {
            _logger.LogInformation("Getting enhanced system status");
            var status = await _progressTracker.GetSystemStatusAsync();
            
            return Ok(new { 
                data = status,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting enhanced system status");
            return StatusCode(500, new { message = "Error getting system status", error = ex.Message });
        }
    }

    /// <summary>
    /// Get threat intelligence service status and health metrics
    /// </summary>
    /// <returns>Threat intelligence service status with health data</returns>
    [HttpGet("threat-intelligence")]
    public async Task<IActionResult> GetThreatIntelligenceStatus()
    {
        try
        {
            _logger.LogInformation("Getting threat intelligence service status");
            var status = await _progressTracker.GetThreatIntelligenceStatusAsync();
            
            return Ok(new { 
                data = status,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat intelligence status");
            return StatusCode(500, new { message = "Error getting threat intelligence status", error = ex.Message });
        }
    }

    /// <summary>
    /// Get cache statistics and performance metrics
    /// </summary>
    /// <returns>Cache statistics including hit rates and memory usage</returns>
    [HttpGet("cache")]
    public async Task<IActionResult> GetCacheStatistics()
    {
        try
        {
            _logger.LogInformation("Getting cache statistics");
            var stats = await _progressTracker.GetCacheStatisticsAsync();
            
            return Ok(new { 
                data = stats,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache statistics");
            return StatusCode(500, new { message = "Error getting cache statistics", error = ex.Message });
        }
    }

    /// <summary>
    /// Get system performance metrics including CPU, memory, and processing statistics
    /// </summary>
    /// <returns>System performance metrics</returns>
    [HttpGet("performance")]
    public async Task<IActionResult> GetPerformanceMetrics()
    {
        try
        {
            _logger.LogInformation("Getting performance metrics");
            var metrics = await _progressTracker.GetPerformanceMetricsAsync();
            
            return Ok(new { 
                data = metrics,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics");
            return StatusCode(500, new { message = "Error getting performance metrics", error = ex.Message });
        }
    }

    /// <summary>
    /// Trigger an immediate system status broadcast to all connected clients
    /// </summary>
    /// <returns>Broadcast confirmation</returns>
    [HttpPost("broadcast")]
    public async Task<IActionResult> BroadcastSystemUpdate()
    {
        try
        {
            _logger.LogInformation("Broadcasting system update via API request");
            await _progressTracker.BroadcastSystemUpdate();
            
            return Ok(new { 
                message = "System update broadcasted successfully",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting system update");
            return StatusCode(500, new { message = "Error broadcasting system update", error = ex.Message });
        }
    }

    /// <summary>
    /// Get system health summary with component status overview
    /// </summary>
    /// <returns>System health summary</returns>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthSummary()
    {
        try
        {
            _logger.LogInformation("Getting system health summary");
            var status = await _progressTracker.GetSystemStatusAsync();
            
            return Ok(new { 
                data = new {
                    isHealthy = status.Health.IsHealthy,
                    totalComponents = status.Health.TotalComponents,
                    healthyComponents = status.Health.HealthyComponents,
                    systemUptime = status.Health.SystemUptime,
                    components = status.Health.Components.Select(c => new {
                        name = c.Key,
                        isHealthy = c.Value.IsHealthy,
                        status = c.Value.Status,
                        responseTime = c.Value.ResponseTimeMs,
                        lastCheck = c.Value.LastCheck
                    })
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health summary");
            return StatusCode(500, new { message = "Error getting health summary", error = ex.Message });
        }
    }

    /// <summary>
    /// Get active scan status and recent scan history
    /// </summary>
    /// <returns>Active scan information and history</returns>
    [HttpGet("scans")]
    public async Task<IActionResult> GetScanStatus()
    {
        try
        {
            _logger.LogInformation("Getting scan status");
            var status = await _progressTracker.GetSystemStatusAsync();
            
            return Ok(new { 
                data = status.ActiveScans,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan status");
            return StatusCode(500, new { message = "Error getting scan status", error = ex.Message });
        }
    }

    /// <summary>
    /// Get system dashboard data combining key metrics for overview display
    /// </summary>
    /// <returns>Dashboard overview data</returns>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardData()
    {
        try
        {
            _logger.LogInformation("Getting dashboard data");
            var status = await _progressTracker.GetSystemStatusAsync();
            
            // Create a condensed dashboard view
            var dashboard = new
            {
                systemHealth = new
                {
                    isHealthy = status.Health.IsHealthy,
                    healthyComponents = status.Health.HealthyComponents,
                    totalComponents = status.Health.TotalComponents,
                    uptime = status.Health.SystemUptime
                },
                threatIntelligence = new
                {
                    isEnabled = status.ThreatIntelligence.IsEnabled,
                    servicesHealthy = status.ThreatIntelligence.Services.Count(s => s.Value.IsHealthy),
                    totalServices = status.ThreatIntelligence.Services.Count,
                    cacheHitRate = status.ThreatIntelligence.CacheHitRate
                },
                performance = new
                {
                    memoryUsageMB = status.Performance.MemoryUsageMB,
                    threadCount = status.Performance.ThreadCount,
                    eventsPerSecond = status.Performance.EventProcessing.EventsPerSecond,
                    queuedEvents = status.Performance.EventProcessing.QueuedEvents
                },
                activeScans = new
                {
                    hasActiveScan = status.ActiveScans.HasActiveScan,
                    queuedScans = status.ActiveScans.QueuedScans,
                    recentScansCount = status.ActiveScans.RecentScans.Count
                },
                cache = new
                {
                    totalMemoryMB = status.Cache.General.TotalMemoryUsageMB,
                    activeCaches = status.Cache.General.ActiveCaches,
                    embeddingHitRate = status.Cache.Embedding.HitRate
                }
            };
            
            return Ok(new { 
                data = dashboard,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data");
            return StatusCode(500, new { message = "Error getting dashboard data", error = ex.Message });
        }
    }
}
