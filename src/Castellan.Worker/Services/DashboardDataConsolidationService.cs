using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Models;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services;

/// <summary>
/// Service interface for consolidating all dashboard data into a single optimized payload
/// </summary>
public interface IDashboardDataConsolidationService
{
    Task<ConsolidatedDashboardData> GetConsolidatedDashboardDataAsync(string timeRange = "24h");
    Task<SecurityEventsSummary> GetSecurityEventsSummaryAsync(string timeRange = "24h");
    Task<SystemStatusSummary> GetSystemStatusSummaryAsync();
    Task<ThreatScannerSummary> GetThreatScannerSummaryAsync();
    Task InvalidateCache();
}

/// <summary>
/// Service that consolidates data from multiple sources into a single dashboard payload
/// This replaces the need for 4+ separate API calls on dashboard load
/// </summary>
public class DashboardDataConsolidationService : IDashboardDataConsolidationService
{
    private readonly ISecurityEventStore _securityEventStore;
    private readonly SystemHealthService _systemHealthService;
    private readonly ILogger<DashboardDataConsolidationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IThreatScanHistoryRepository _threatScanRepository;

    private const string CACHE_KEY_PREFIX = "consolidated_dashboard_data";
    private const int CACHE_DURATION_SECONDS = 30; // Same as current dashboard cache

    public DashboardDataConsolidationService(
        ISecurityEventStore securityEventStore,
        SystemHealthService systemHealthService,
        IThreatScanHistoryRepository threatScanRepository,
        IMemoryCache cache,
        ILogger<DashboardDataConsolidationService> logger)
    {
        _securityEventStore = securityEventStore;
        _systemHealthService = systemHealthService;
        _threatScanRepository = threatScanRepository;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get all dashboard data consolidated into a single payload
    /// </summary>
    public async Task<ConsolidatedDashboardData> GetConsolidatedDashboardDataAsync(string timeRange = "24h")
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}_{timeRange}";

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out ConsolidatedDashboardData? cachedData) && cachedData != null)
        {
            _logger.LogDebug("Returning cached consolidated dashboard data for time range: {TimeRange}", timeRange);
            return cachedData;
        }

        _logger.LogInformation("Fetching fresh consolidated dashboard data for time range: {TimeRange}", timeRange);

        try
        {
            // Fetch all dashboard data in parallel for optimal performance
            // This is the key optimization - single parallel fetch instead of 4+ sequential API calls
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var securityEventsTask = GetSecurityEventsSummaryAsync(timeRange);
            var systemStatusTask = GetSystemStatusSummaryAsync();
            var threatScannerTask = GetThreatScannerSummaryAsync();

            await Task.WhenAll(securityEventsTask, systemStatusTask, threatScannerTask);

            var securityEvents = securityEventsTask.Result;
            var systemStatus = systemStatusTask.Result;
            var threatScanner = threatScannerTask.Result;

            stopwatch.Stop();

            var consolidatedData = new ConsolidatedDashboardData
            {
                SecurityEvents = securityEvents,
                SystemStatus = systemStatus,
                ThreatScanner = threatScanner,
                TimeRange = timeRange,
                LastUpdated = DateTime.UtcNow
            };

            // Cache the consolidated data with explicit size for memory management
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CACHE_DURATION_SECONDS),
                Size = 1 // Each consolidated data entry counts as 1 unit
            };
            _cache.Set(cacheKey, consolidatedData, cacheOptions);

            _logger.LogInformation("Successfully consolidated dashboard data in {ElapsedMs}ms. Security Events: {EventCount}, Components: {ComponentCount}, Scans: {ScanCount}",
                stopwatch.ElapsedMilliseconds, securityEvents.TotalEvents, systemStatus.TotalComponents, threatScanner.TotalScans);

            return consolidatedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consolidating dashboard data");
            throw;
        }
    }

    /// <summary>
    /// Get security events summary optimized for dashboard display
    /// </summary>
    public async Task<SecurityEventsSummary> GetSecurityEventsSummaryAsync(string timeRange = "24h")
    {
        try
        {
            var filters = BuildTimeRangeFilters(timeRange);
            DateTime? startDate = null;
            DateTime? endDate = null;

            if (filters != null)
            {
                if (filters.TryGetValue("startDate", out var startObj) && startObj is DateTime start)
                {
                    startDate = start;
                }

                if (filters.TryGetValue("endDate", out var endObj) && endObj is DateTime end)
                {
                    endDate = end;
                }
            }

            _logger.LogDebug("Dashboard summary filters: Start={Start} End={End}", startDate, endDate);

            List<SecurityEvent> recentEvents;
            int totalEvents;
            Dictionary<string, int> riskLevelCounts;

            if (filters != null && filters.Count > 0)
            {
                totalEvents = _securityEventStore.GetTotalCount(filters);
                riskLevelCounts = _securityEventStore.GetRiskLevelCounts(filters);
                recentEvents = _securityEventStore.GetSecurityEvents(1, 25, filters).ToList();
            }
            else
            {
                totalEvents = _securityEventStore.GetTotalCount();
                riskLevelCounts = _securityEventStore.GetRiskLevelCounts();
                recentEvents = _securityEventStore.GetSecurityEvents(1, 25).ToList();
            }

            _logger.LogDebug("Dashboard summary count for {TimeRange}: {Count}", timeRange, totalEvents);

            var basicEvents = recentEvents
                .OrderByDescending(e => e.OriginalEvent.Time)
                .Take(10)
                .Select(e => new SecurityEventBasic
                {
                    Id = e.Id,
                    EventType = e.EventType.ToString(),
                    Timestamp = e.OriginalEvent.Time.DateTime,
                    RiskLevel = e.RiskLevel,
                    Source = e.OriginalEvent.Channel,
                    Machine = e.OriginalEvent.Host ?? string.Empty
                })
                .ToList();

            return new SecurityEventsSummary
            {
                TotalEvents = totalEvents,
                RiskLevelCounts = riskLevelCounts,
                RecentEvents = basicEvents,
                LastEventTime = basicEvents.FirstOrDefault()?.Timestamp ?? DateTime.MinValue
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security events summary");
            return new SecurityEventsSummary();
        }
    }
    /// <summary>
    /// Get system status summary optimized for dashboard display
    /// </summary>
    public async Task<SystemStatusSummary> GetSystemStatusSummaryAsync()
    {
        try
        {
            var systemStatus = await _systemHealthService.GetSystemStatusAsync();

            var basicComponents = systemStatus.Select(s => new ComponentHealthBasic
            {
                Component = s.Component,
                Status = s.Status,
                ResponseTime = s.ResponseTime,
                LastCheck = s.LastCheck
            }).ToList();

            var componentStatuses = systemStatus.ToDictionary(s => s.Component, s => s.Status);

            return new SystemStatusSummary
            {
                TotalComponents = systemStatus.Count(),
                HealthyComponents = systemStatus.Count(s => s.Status == "Healthy"),
                Components = basicComponents,
                ComponentStatuses = componentStatuses
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system status summary");
            return new SystemStatusSummary();
        }
    }

    /// <summary>
    /// Get threat scanner summary optimized for dashboard display
    /// </summary>
    public async Task<ThreatScannerSummary> GetThreatScannerSummaryAsync()
    {
        try
        {
            var recentScans = await _threatScanRepository.GetScanHistoryAsync(1, 50);

            // Only return essential fields for dashboard
            var basicScans = recentScans.Take(10).Select(s => new ThreatScanBasic
            {
                Id = s.Id,
                ScanType = s.ScanType.ToString(),
                Timestamp = s.StartTime,
                Status = s.Status.ToString(),
                FilesScanned = s.FilesScanned,
                ThreatsFound = s.ThreatsFound
            }).ToList();

            return new ThreatScannerSummary
            {
                TotalScans = recentScans.Count(),
                ActiveScans = recentScans.Count(s => s.Status == ThreatScanStatus.Running),
                CompletedScans = recentScans.Count(s => s.Status == ThreatScanStatus.Completed),
                ThreatsFound = recentScans.Sum(s => s.ThreatsFound),
                LastScanTime = recentScans.FirstOrDefault()?.StartTime ?? DateTime.MinValue,
                RecentScans = basicScans
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat scanner summary");
            return new ThreatScannerSummary();
        }
    }

    private Dictionary<string, object>? BuildTimeRangeFilters(string timeRange)
    {
        if (string.IsNullOrWhiteSpace(timeRange))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        DateTime? start = timeRange.ToLowerInvariant() switch
        {
            "1h" => now.AddHours(-1),
            "24h" => now.AddHours(-24),
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            _ when timeRange.EndsWith("h", StringComparison.OrdinalIgnoreCase) && int.TryParse(timeRange[..^1], out var hours)
                => now.AddHours(-hours),
            _ when timeRange.EndsWith("d", StringComparison.OrdinalIgnoreCase) && int.TryParse(timeRange[..^1], out var days)
                => now.AddDays(-days),
            _ => null
        };

        if (!start.HasValue)
        {
            return null;
        }

        return new Dictionary<string, object>
        {
            ["startDate"] = start.Value,
            ["endDate"] = now
        };
    }
    /// <summary>
    /// Invalidate cached dashboard data to force fresh fetch
    /// </summary>
    public async Task InvalidateCache()
    {
        // Remove all cached dashboard data for all time ranges
        var cacheKeys = new[] { "24h", "7d", "30d", "1h" };
        foreach (var timeRange in cacheKeys)
        {
            _cache.Remove($"{CACHE_KEY_PREFIX}_{timeRange}");
        }

        _logger.LogInformation("Dashboard data cache invalidated");
        await Task.CompletedTask;
    }
}




















