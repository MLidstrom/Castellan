using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Castellan.Worker.Models;
using Castellan.Worker.Services;

namespace Castellan.Worker.Controllers;

/// <summary>
/// REST API controller for consolidated dashboard data
/// Provides fallback access to dashboard data when SignalR is unavailable
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardDataController : ControllerBase
{
    private readonly IDashboardDataConsolidationService _dashboardDataService;
    private readonly DashboardDataBroadcastService _broadcastService;
    private readonly ILogger<DashboardDataController> _logger;

    public DashboardDataController(
        IDashboardDataConsolidationService dashboardDataService,
        DashboardDataBroadcastService broadcastService,
        ILogger<DashboardDataController> logger)
    {
        _dashboardDataService = dashboardDataService;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    /// <summary>
    /// Get consolidated dashboard data for all widgets in a single call
    /// This replaces the need for multiple API calls: /security-events, /system-status, /threat-scanner
    /// </summary>
    /// <param name="timeRange">Time range for dashboard data (1h, 24h, 7d, 30d)</param>
    /// <returns>Consolidated dashboard data</returns>
    [HttpGet("consolidated")]
    public async Task<ActionResult<ConsolidatedDashboardData>> GetConsolidatedDashboardData([FromQuery] string timeRange = "24h")
    {
        try
        {
            _logger.LogInformation("REST API request for consolidated dashboard data, time range: {TimeRange}", timeRange);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var data = await _dashboardDataService.GetConsolidatedDashboardDataAsync(timeRange);
            stopwatch.Stop();

            _logger.LogInformation("Returned consolidated dashboard data via REST API in {ElapsedMs}ms. " +
                                 "Events: {EventCount}, Components: {ComponentCount}, Scans: {ScanCount}, YARA: {YaraRules}",
                stopwatch.ElapsedMilliseconds, data.SecurityEvents.TotalEvents,
                data.SystemStatus.TotalComponents, data.ThreatScanner.TotalScans, data.Yara.EnabledRules);

            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consolidated dashboard data via REST API");
            return StatusCode(500, new { error = "Failed to retrieve dashboard data", message = ex.Message });
        }
    }

    /// <summary>
    /// Get security events summary only
    /// </summary>
    [HttpGet("security-events")]
    public async Task<ActionResult<SecurityEventsSummary>> GetSecurityEventsSummary([FromQuery] string timeRange = "24h")
    {
        try
        {
            _logger.LogInformation("REST API request for security events summary, time range: {TimeRange}", timeRange);
            var data = await _dashboardDataService.GetSecurityEventsSummaryAsync(timeRange);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security events summary");
            return StatusCode(500, new { error = "Failed to retrieve security events summary", message = ex.Message });
        }
    }

    /// <summary>
    /// Get system status summary only
    /// </summary>
    [HttpGet("system-status")]
    public async Task<ActionResult<SystemStatusSummary>> GetSystemStatusSummary()
    {
        try
        {
            var data = await _dashboardDataService.GetSystemStatusSummaryAsync();
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system status summary");
            return StatusCode(500, new { error = "Failed to retrieve system status summary", message = ex.Message });
        }
    }

    /// <summary>
    /// Get threat scanner summary only
    /// </summary>
    [HttpGet("threat-scanner")]
    public async Task<ActionResult<ThreatScannerSummary>> GetThreatScannerSummary()
    {
        try
        {
            var data = await _dashboardDataService.GetThreatScannerSummaryAsync();
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving threat scanner summary");
            return StatusCode(500, new { error = "Failed to retrieve threat scanner summary", message = ex.Message });
        }
    }

    /// <summary>
    /// Invalidate dashboard data cache and trigger immediate refresh
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshDashboardData()
    {
        try
        {
            _logger.LogInformation("Manual dashboard data refresh requested via REST API");

            // Trigger immediate broadcast with cache invalidation
            await _broadcastService.TriggerImmediateBroadcastWithCacheInvalidation();

            return Ok(new { message = "Dashboard data refresh triggered", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering dashboard data refresh");
            return StatusCode(500, new { error = "Failed to refresh dashboard data", message = ex.Message });
        }
    }

    /// <summary>
    /// Get dashboard data cache status and statistics
    /// </summary>
    [HttpGet("cache-status")]
    public Task<IActionResult> GetCacheStatus()
    {
        try
        {
            // This would show cache hit rates, last update times, etc.
            var status = new
            {
                cacheEnabled = true,
                cacheDurationSeconds = 30,
                lastUpdate = DateTime.UtcNow,
                message = "Cache status information - implement cache metrics collection for detailed stats"
            };

            return Task.FromResult<IActionResult>(Ok(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache status");
            return Task.FromResult<IActionResult>(StatusCode(500, new { error = "Failed to retrieve cache status", message = ex.Message }));
        }
    }
}
