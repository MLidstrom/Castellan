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
    Task<YaraSummary> GetYaraSummaryAsync();
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
    private readonly IYaraRuleStore _yaraRuleStore;

    private const string CACHE_KEY_PREFIX = "consolidated_dashboard_data";
    private const int CACHE_DURATION_SECONDS = 30; // Same as current dashboard cache

    public DashboardDataConsolidationService(
        ISecurityEventStore securityEventStore,
        SystemHealthService systemHealthService,
        IThreatScanHistoryRepository threatScanRepository,
        IYaraRuleStore yaraRuleStore,
        IMemoryCache cache,
        ILogger<DashboardDataConsolidationService> logger)
    {
        _securityEventStore = securityEventStore;
        _systemHealthService = systemHealthService;
        _threatScanRepository = threatScanRepository;
        _yaraRuleStore = yaraRuleStore;
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
            // This is the key optimization - single parallel fetch instead of 5+ sequential API calls
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var securityEventsTask = GetSecurityEventsSummaryAsync(timeRange);
            var systemStatusTask = GetSystemStatusSummaryAsync();
            var threatScannerTask = GetThreatScannerSummaryAsync();
            var yaraTask = GetYaraSummaryAsync();

            await Task.WhenAll(securityEventsTask, systemStatusTask, threatScannerTask, yaraTask);

            var securityEvents = securityEventsTask.Result;
            var systemStatus = systemStatusTask.Result;
            var threatScanner = threatScannerTask.Result;
            var yara = yaraTask.Result;

            stopwatch.Stop();

            // Get recent activity for the activity feed (top 8 most recent events)
            var recentActivityEvents = _securityEventStore
                .GetSecurityEvents(1, 8)
                .OrderByDescending(e => e.OriginalEvent.Time)
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

            var consolidatedData = new ConsolidatedDashboardData
            {
                SecurityEvents = securityEvents,
                SystemStatus = systemStatus,
                ThreatScanner = threatScanner,
                Yara = yara,
                RecentActivity = recentActivityEvents,
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

            _logger.LogInformation("Successfully consolidated dashboard data in {ElapsedMs}ms. Security Events: {EventCount}, Components: {ComponentCount}, Scans: {ScanCount}, YARA Rules: {YaraRules}, Recent Activity: {ActivityCount}",
                stopwatch.ElapsedMilliseconds, securityEvents.TotalEvents, systemStatus.TotalComponents, threatScanner.TotalScans, yara.EnabledRules, recentActivityEvents.Count);

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

            // Total Events and Risk Level Counts respect the time range filter (24h scope)
            // This provides an accurate view of events within the specified time window
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
            var lastScan = recentScans.FirstOrDefault();

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

            // Determine last scan result and status for dashboard card
            string lastScanResult = "N/A";
            string lastScanStatus = "unknown";
            string scanType = string.Empty;

            if (lastScan != null)
            {
                scanType = lastScan.ScanType.ToString();

                // Map status to result text
                lastScanResult = lastScan.Status switch
                {
                    ThreatScanStatus.Completed => "Clean",
                    ThreatScanStatus.CompletedWithThreats => "Findings Detected",
                    ThreatScanStatus.Running => "In Progress",
                    ThreatScanStatus.Failed => "Failed",
                    ThreatScanStatus.Cancelled => "Cancelled",
                    _ => "Unknown"
                };

                // Map to status for color coding
                lastScanStatus = lastScan.Status switch
                {
                    ThreatScanStatus.Completed => "clean",
                    ThreatScanStatus.CompletedWithThreats => "threat",
                    ThreatScanStatus.Running => "running",
                    ThreatScanStatus.Failed => "error",
                    _ => "unknown"
                };
            }

            return new ThreatScannerSummary
            {
                TotalScans = recentScans.Count(),
                ActiveScans = recentScans.Count(s => s.Status == ThreatScanStatus.Running),
                CompletedScans = recentScans.Count(s => s.Status == ThreatScanStatus.Completed),
                ThreatsFound = recentScans.Sum(s => s.ThreatsFound),
                LastScanTime = lastScan?.StartTime ?? DateTime.MinValue,
                LastScanResult = lastScanResult,
                LastScanStatus = lastScanStatus,
                ScanType = scanType,
                RecentScans = basicScans
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat scanner summary");
            return new ThreatScannerSummary();
        }
    }

    /// <summary>
    /// Get YARA rules summary optimized for dashboard display
    /// </summary>
    public async Task<YaraSummary> GetYaraSummaryAsync()
    {
        try
        {
            var allRules = (await _yaraRuleStore.GetAllRulesAsync()).ToList();
            var recentMatches = (await _yaraRuleStore.GetRecentMatchesAsync(10)).ToList();

            var basicMatches = recentMatches
                .OrderByDescending(m => m.MatchTime)
                .Take(10)
                .Select(m => new YaraMatchBasic
                {
                    Id = m.Id,
                    RuleName = m.RuleName,
                    MatchTime = m.MatchTime,
                    SecurityEventId = m.SecurityEventId ?? string.Empty
                })
                .ToList();

            return new YaraSummary
            {
                TotalRules = allRules.Count,
                EnabledRules = allRules.Count(r => r.IsEnabled),
                DisabledRules = allRules.Count(r => !r.IsEnabled),
                RecentMatches = recentMatches.Count,
                LastMatchTime = basicMatches.FirstOrDefault()?.MatchTime ?? DateTime.MinValue,
                RecentMatchList = basicMatches
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YARA summary");
            return new YaraSummary();
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
            // Removed: 7d, 30d - Castellan scope limited to 24 hours for AI pattern detection
            _ when timeRange.EndsWith("h", StringComparison.OrdinalIgnoreCase) && int.TryParse(timeRange[..^1], out var hours) && hours <= 24
                => now.AddHours(-hours),
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
        // Remove all cached dashboard data for supported time ranges (24h scope only)
        var cacheKeys = new[] { "1h", "24h" };
        foreach (var timeRange in cacheKeys)
        {
            _cache.Remove($"{CACHE_KEY_PREFIX}_{timeRange}");
        }

        _logger.LogInformation("Dashboard data cache invalidated");
        await Task.CompletedTask;
    }
}




















