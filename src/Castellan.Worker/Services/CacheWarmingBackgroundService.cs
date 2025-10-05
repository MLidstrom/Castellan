using Microsoft.Extensions.Caching.Memory;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service that proactively warms up cache for expensive queries.
/// Ensures instant page loads by pre-computing and caching data before users request it.
/// </summary>
public class CacheWarmingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheWarmingBackgroundService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(2); // Refresh every 2 minutes

    public CacheWarmingBackgroundService(
        IServiceProvider serviceProvider,
        IMemoryCache cache,
        ILogger<CacheWarmingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache Warming Service started - will refresh cache every {Interval} minutes", _refreshInterval.TotalMinutes);

        // Initial warm-up after 10 seconds
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await WarmCacheAsync(stoppingToken);

        // Then refresh periodically
        using var timer = new PeriodicTimer(_refreshInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await WarmCacheAsync(stoppingToken);
        }
    }

    private async Task WarmCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔥 Starting cache warm-up cycle...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            using var scope = _serviceProvider.CreateScope();
            var timelineService = scope.ServiceProvider.GetRequiredService<ITimelineService>();

            // Warm up Timeline data (7 days, daily granularity)
            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7);

            var timelineRequest = new Controllers.TimelineRequest
            {
                Granularity = Controllers.TimelineGranularity.Day,
                StartTime = sevenDaysAgo,
                EndTime = now
            };

            // Pre-compute and cache Timeline data
            var timelineData = await timelineService.GetTimelineDataAsync(timelineRequest);
            var timelineDataCacheKey = $"timeline_data_{timelineRequest.Granularity}_{sevenDaysAgo:yyyy-MM-dd}_{now:yyyy-MM-dd}";
            _cache.Set(timelineDataCacheKey, timelineData, TimeSpan.FromMinutes(5));
            _logger.LogInformation("✅ Cached timeline data: {Count} data points", timelineData.DataPoints.Count);

            // Pre-compute and cache Timeline stats
            var statsRequest = new Controllers.TimelineStatsRequest
            {
                StartTime = sevenDaysAgo,
                EndTime = now
            };

            var timelineStats = await timelineService.GetTimelineStatsAsync(statsRequest);
            var statsCacheKey = $"timeline_stats_{sevenDaysAgo:yyyy-MM-dd}_{now:yyyy-MM-dd}";
            _cache.Set(statsCacheKey, timelineStats, TimeSpan.FromMinutes(5));
            _logger.LogInformation("✅ Cached timeline stats: {TotalEvents} total events", timelineStats.TotalEvents);

            // Warm up Dashboard data (if dashboard service exists)
            try
            {
                var dashboardDataUrl = "dashboarddata/consolidated?timeRange=24h";
                var dashboardCacheKey = "dashboard_consolidated_24h";
                // Dashboard data would be fetched via custom endpoint or service
                _logger.LogInformation("✅ Dashboard cache warming skipped (implement if needed)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not warm dashboard cache");
            }

            stopwatch.Stop();
            _logger.LogInformation("🔥 Cache warm-up completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache warm-up cycle");
        }
    }
}
