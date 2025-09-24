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
    Task<SecurityEventsSummary> GetSecurityEventsSummaryAsync();
    Task<SystemStatusSummary> GetSystemStatusSummaryAsync();
    Task<ComplianceReportsSummary> GetComplianceReportsSummaryAsync();
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

    // Inject existing services that provide the data
    private readonly ApplicationService _applicationService; // For compliance reports
    private readonly IThreatScanHistoryRepository _threatScanRepository;

    private const string CACHE_KEY_PREFIX = "consolidated_dashboard_data";
    private const int CACHE_DURATION_SECONDS = 30; // Same as current dashboard cache

    public DashboardDataConsolidationService(
        ISecurityEventStore securityEventStore,
        SystemHealthService systemHealthService,
        ApplicationService applicationService,
        IThreatScanHistoryRepository threatScanRepository,
        IMemoryCache cache,
        ILogger<DashboardDataConsolidationService> logger)
    {
        _securityEventStore = securityEventStore;
        _systemHealthService = systemHealthService;
        _applicationService = applicationService;
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

            var securityEventsTask = GetSecurityEventsSummaryAsync();
            var systemStatusTask = GetSystemStatusSummaryAsync();
            var complianceTask = GetComplianceReportsSummaryAsync();
            var threatScannerTask = GetThreatScannerSummaryAsync();

            await Task.WhenAll(securityEventsTask, systemStatusTask, complianceTask, threatScannerTask);

            var securityEvents = securityEventsTask.Result;
            var systemStatus = systemStatusTask.Result;
            var compliance = complianceTask.Result;
            var threatScanner = threatScannerTask.Result;

            stopwatch.Stop();

            var consolidatedData = new ConsolidatedDashboardData
            {
                SecurityEvents = securityEvents,
                SystemStatus = systemStatus,
                Compliance = compliance,
                ThreatScanner = threatScanner,
                TimeRange = timeRange,
                LastUpdated = DateTime.UtcNow
            };

            // Cache the consolidated data
            _cache.Set(cacheKey, consolidatedData, TimeSpan.FromSeconds(CACHE_DURATION_SECONDS));

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
    public async Task<SecurityEventsSummary> GetSecurityEventsSummaryAsync()
    {
        try
        {
            // Get total count of all events in the store
            var totalEvents = _securityEventStore.GetTotalCount();
            var totalEventsWithEmptyFilters = _securityEventStore.GetTotalCount(new Dictionary<string, object>());
            _logger.LogInformation("🔍 Dashboard: GetTotalCount() returned {TotalEvents}, GetTotalCount(empty filters) returned {TotalEventsWithEmptyFilters}", totalEvents, totalEventsWithEmptyFilters);

            // Get recent events for dashboard display (limit to 100 for risk level analysis)
            var recentEvents = _securityEventStore.GetSecurityEvents(1, 100);

            var riskLevelCounts = recentEvents
                .GroupBy(e => e.RiskLevel)
                .ToDictionary(g => g.Key.ToLower(), g => g.Count());

            // Only return essential fields for dashboard - reduces payload size (just 10 most recent)
            var basicEvents = recentEvents.Take(10).Select(e => new SecurityEventBasic
            {
                Id = e.Id,
                EventType = e.EventType.ToString(),
                Timestamp = e.OriginalEvent.Time.DateTime,
                RiskLevel = e.RiskLevel,
                Source = e.OriginalEvent.Channel,
                Machine = e.OriginalEvent.Host
            }).ToList();

            return new SecurityEventsSummary
            {
                TotalEvents = totalEvents, // Show actual total count from store
                RiskLevelCounts = riskLevelCounts,
                RecentEvents = basicEvents,
                LastEventTime = recentEvents.FirstOrDefault()?.OriginalEvent.Time.DateTime ?? DateTime.MinValue
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
    /// Get compliance reports summary optimized for dashboard display
    /// </summary>
    public async Task<ComplianceReportsSummary> GetComplianceReportsSummaryAsync()
    {
        try
        {
            // This would need to be implemented based on your compliance report storage
            // For now, return mock data matching the current API response structure
            // TODO: Implement actual compliance report retrieval once the storage mechanism is clarified
            return new ComplianceReportsSummary
            {
                TotalReports = 5,
                AverageScore = 85.4,
                PassingReports = 4,
                FailingReports = 1,
                RecentReports = new List<ComplianceReportBasic>
                {
                    new ComplianceReportBasic
                    {
                        Id = "1",
                        Title = "Windows Security Baseline",
                        Score = 92.5,
                        Generated = DateTime.UtcNow.AddHours(-2),
                        Status = "Passed"
                    },
                    new ComplianceReportBasic
                    {
                        Id = "2",
                        Title = "NIST Cybersecurity Framework",
                        Score = 78.3,
                        Generated = DateTime.UtcNow.AddHours(-4),
                        Status = "Failed"
                    },
                    new ComplianceReportBasic
                    {
                        Id = "3",
                        Title = "ISO 27001 Controls",
                        Score = 88.7,
                        Generated = DateTime.UtcNow.AddHours(-6),
                        Status = "Passed"
                    },
                    new ComplianceReportBasic
                    {
                        Id = "4",
                        Title = "CIS Controls v8",
                        Score = 82.1,
                        Generated = DateTime.UtcNow.AddHours(-12),
                        Status = "Passed"
                    },
                    new ComplianceReportBasic
                    {
                        Id = "5",
                        Title = "SOC 2 Type II",
                        Score = 90.0,
                        Generated = DateTime.UtcNow.AddDays(-1),
                        Status = "Passed"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance reports summary");
            return new ComplianceReportsSummary();
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