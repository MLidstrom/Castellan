using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Castellan.Worker.Hubs;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service that periodically broadcasts consolidated dashboard data via SignalR
/// This replaces the need for frontend polling and provides real-time dashboard updates
/// </summary>
public class DashboardDataBroadcastService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScanProgressHub> _hubContext;
    private readonly ILogger<DashboardDataBroadcastService> _logger;

    // Broadcast every 30 seconds - matches current dashboard cache duration
    // This provides a good balance between responsiveness and server load
    private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(30);

    public DashboardDataBroadcastService(
        IServiceScopeFactory scopeFactory,
        IHubContext<ScanProgressHub> hubContext,
        ILogger<DashboardDataBroadcastService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DashboardDataBroadcastService starting - broadcasting consolidated dashboard data every {Interval} seconds",
            _broadcastInterval.TotalSeconds);

        // Wait a bit for the application to fully start and other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastDashboardData();
                await Task.Delay(_broadcastInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during dashboard data broadcast cycle");

                // Wait a shorter period before retrying to avoid hammering in case of persistent errors
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("DashboardDataBroadcastService stopped");
    }

    /// <summary>
    /// Broadcast consolidated dashboard data to all connected dashboard clients
    /// </summary>
    private async Task BroadcastDashboardData()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dashboardDataService = scope.ServiceProvider.GetRequiredService<IDashboardDataConsolidationService>();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Get consolidated dashboard data - this is much more efficient than 4+ separate API calls
            var dashboardData = await dashboardDataService.GetConsolidatedDashboardDataAsync();

            stopwatch.Stop();

            // Broadcast to all clients subscribed to dashboard updates
            await _hubContext.Clients.Group("DashboardUpdates")
                .SendAsync("DashboardUpdate", dashboardData);

            _logger.LogDebug("Broadcasted consolidated dashboard data in {ElapsedMs}ms at {Timestamp}. " +
                           "Events: {EventCount}, Components: {ComponentCount}, Scans: {ScanCount}, Threats: {ThreatCount}, YARA: {MalwareRules}, Activity: {ActivityCount}",
                stopwatch.ElapsedMilliseconds,
                DateTime.UtcNow,
                dashboardData.SecurityEvents.TotalEvents,
                dashboardData.SystemStatus.TotalComponents,
                dashboardData.ThreatScanner.TotalScans,
                dashboardData.ThreatScanner.ThreatsFound,
                dashboardData.Yara.EnabledRules,
                dashboardData.RecentActivity.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting consolidated dashboard data");
        }
    }

    /// <summary>
    /// Trigger an immediate dashboard data broadcast outside of the regular schedule
    /// This can be called when dashboard data changes significantly (e.g., after a scan completes)
    /// </summary>
    public async Task TriggerImmediateBroadcast()
    {
        _logger.LogInformation("Immediate dashboard data broadcast requested");
        await BroadcastDashboardData();
    }

    /// <summary>
    /// Trigger immediate broadcast with cache invalidation
    /// Use when you know the data has changed and want to bypass cache
    /// </summary>
    public async Task TriggerImmediateBroadcastWithCacheInvalidation()
    {
        _logger.LogInformation("Immediate dashboard data broadcast with cache invalidation requested");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dashboardDataService = scope.ServiceProvider.GetRequiredService<IDashboardDataConsolidationService>();

            // Invalidate cache first to ensure fresh data
            await dashboardDataService.InvalidateCache();

            // Then broadcast
            await BroadcastDashboardData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during immediate broadcast with cache invalidation");
        }
    }
}