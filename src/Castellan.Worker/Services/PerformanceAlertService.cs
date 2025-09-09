using Microsoft.Extensions.Caching.Memory;
using Castellan.Worker.Models;
using Castellan.Worker.Data;

namespace Castellan.Worker.Services;

public class PerformanceAlertService
{
    private readonly ILogger<PerformanceAlertService> _logger;
    private readonly IMemoryCache _cache;
    private readonly CastellanDbContext _dbContext;
    private readonly PerformanceMetricsService _performanceMetricsService;

    // Default thresholds
    private static readonly PerformanceThresholds DefaultThresholds = new()
    {
        CpuThreshold = new ThresholdConfig { Warning = 70, Critical = 90 },
        MemoryThreshold = new ThresholdConfig { Warning = 80, Critical = 95 },
        DiskThreshold = new ThresholdConfig { Warning = 85, Critical = 95 },
        ResponseTimeThreshold = new ThresholdConfig { Warning = 1000, Critical = 5000 }, // ms
        ErrorRateThreshold = new ThresholdConfig { Warning = 0.05, Critical = 0.10 }, // 5% warning, 10% critical
        CacheHitRateThreshold = new ThresholdConfig { Warning = 0.70, Critical = 0.50 }, // Below 70% warning, below 50% critical
        DatabaseConnectionThreshold = new ThresholdConfig { Warning = 0.80, Critical = 0.95 } // 80% of pool warning, 95% critical
    };

    public PerformanceAlertService(
        ILogger<PerformanceAlertService> logger,
        IMemoryCache cache,
        CastellanDbContext dbContext,
        PerformanceMetricsService performanceMetricsService)
    {
        _logger = logger;
        _cache = cache;
        _dbContext = dbContext;
        _performanceMetricsService = performanceMetricsService;
    }

    public async Task<AlertResponse> GetAlertsAsync()
    {
        _logger.LogInformation("Getting performance alerts");

        var cacheKey = "performance_alerts";
        if (_cache.TryGetValue(cacheKey, out AlertResponse cachedAlerts))
        {
            return cachedAlerts;
        }

        var activeAlerts = new List<PerformanceAlert>();
        var alertHistory = new List<PerformanceAlert>();

        // Generate current alerts based on system metrics
        var currentAlerts = await GenerateCurrentAlertsAsync();
        activeAlerts.AddRange(currentAlerts);

        // Get alert history (in production, this would come from database)
        var historicalAlerts = GenerateAlertHistory();
        alertHistory.AddRange(historicalAlerts);

        var response = new AlertResponse
        {
            Active = activeAlerts,
            History = alertHistory.Take(50).ToList() // Last 50 alerts
        };

        // Cache for 10 seconds
        _cache.Set(cacheKey, response, TimeSpan.FromSeconds(10));

        return response;
    }

    public async Task UpdateThresholdsAsync(PerformanceThresholds thresholds)
    {
        _logger.LogInformation("Updating performance alert thresholds");

        // Validate thresholds
        ValidateThresholds(thresholds);

        // In production, save to database
        var cacheKey = "performance_thresholds";
        _cache.Set(cacheKey, thresholds, TimeSpan.FromHours(24));

        _logger.LogInformation("Performance alert thresholds updated successfully");
    }

    public async Task<PerformanceThresholds> GetThresholdsAsync()
    {
        var cacheKey = "performance_thresholds";
        if (_cache.TryGetValue(cacheKey, out PerformanceThresholds thresholds))
        {
            return thresholds;
        }

        // Return default thresholds if none are set
        return DefaultThresholds;
    }

    private async Task<List<PerformanceAlert>> GenerateCurrentAlertsAsync()
    {
        var alerts = new List<PerformanceAlert>();
        var thresholds = await GetThresholdsAsync();

        // Get current system metrics
        var systemResources = await _performanceMetricsService.GetSystemResourcesAsync();
        var cacheStats = await _performanceMetricsService.GetCacheStatisticsAsync();
        var dbMetrics = await _performanceMetricsService.GetDatabasePerformanceAsync();
        var dashboardSummary = await _performanceMetricsService.GetDashboardSummaryAsync();

        // Check CPU usage
        if (systemResources.Cpu.Usage >= thresholds.CpuThreshold.Critical)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "CPU",
                Severity = "critical",
                Message = $"Critical CPU usage: {systemResources.Cpu.Usage:F1}%",
                Threshold = thresholds.CpuThreshold.Critical,
                CurrentValue = systemResources.Cpu.Usage,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }
        else if (systemResources.Cpu.Usage >= thresholds.CpuThreshold.Warning)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "CPU",
                Severity = "warning",
                Message = $"High CPU usage: {systemResources.Cpu.Usage:F1}%",
                Threshold = thresholds.CpuThreshold.Warning,
                CurrentValue = systemResources.Cpu.Usage,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }

        // Check Memory usage
        var memoryUsagePercent = (systemResources.Memory.Usage / systemResources.Memory.Total) * 100;
        if (memoryUsagePercent >= thresholds.MemoryThreshold.Critical)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "Memory",
                Severity = "critical",
                Message = $"Critical memory usage: {memoryUsagePercent:F1}%",
                Threshold = thresholds.MemoryThreshold.Critical,
                CurrentValue = memoryUsagePercent,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }
        else if (memoryUsagePercent >= thresholds.MemoryThreshold.Warning)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "Memory",
                Severity = "warning",
                Message = $"High memory usage: {memoryUsagePercent:F1}%",
                Threshold = thresholds.MemoryThreshold.Warning,
                CurrentValue = memoryUsagePercent,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }

        // Check Disk usage
        if (systemResources.Disk.Usage >= thresholds.DiskThreshold.Critical)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "Disk",
                Severity = "critical",
                Message = $"Critical disk usage: {systemResources.Disk.Usage:F1}%",
                Threshold = thresholds.DiskThreshold.Critical,
                CurrentValue = systemResources.Disk.Usage,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }
        else if (systemResources.Disk.Usage >= thresholds.DiskThreshold.Warning)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "Disk",
                Severity = "warning",
                Message = $"High disk usage: {systemResources.Disk.Usage:F1}%",
                Threshold = thresholds.DiskThreshold.Warning,
                CurrentValue = systemResources.Disk.Usage,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }

        // Check Response time
        if (dashboardSummary.Performance.AverageResponseTime >= thresholds.ResponseTimeThreshold.Critical)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "ResponseTime",
                Severity = "critical",
                Message = $"Critical response time: {dashboardSummary.Performance.AverageResponseTime:F1}ms",
                Threshold = thresholds.ResponseTimeThreshold.Critical,
                CurrentValue = dashboardSummary.Performance.AverageResponseTime,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }
        else if (dashboardSummary.Performance.AverageResponseTime >= thresholds.ResponseTimeThreshold.Warning)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "ResponseTime",
                Severity = "warning",
                Message = $"High response time: {dashboardSummary.Performance.AverageResponseTime:F1}ms",
                Threshold = thresholds.ResponseTimeThreshold.Warning,
                CurrentValue = dashboardSummary.Performance.AverageResponseTime,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }

        // Check Error rate
        if (dashboardSummary.Performance.ErrorRate >= thresholds.ErrorRateThreshold.Critical)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "ErrorRate",
                Severity = "critical",
                Message = $"Critical error rate: {dashboardSummary.Performance.ErrorRate * 100:F2}%",
                Threshold = thresholds.ErrorRateThreshold.Critical,
                CurrentValue = dashboardSummary.Performance.ErrorRate,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }
        else if (dashboardSummary.Performance.ErrorRate >= thresholds.ErrorRateThreshold.Warning)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "ErrorRate",
                Severity = "warning",
                Message = $"High error rate: {dashboardSummary.Performance.ErrorRate * 100:F2}%",
                Threshold = thresholds.ErrorRateThreshold.Warning,
                CurrentValue = dashboardSummary.Performance.ErrorRate,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }

        // Check Cache hit rate (inverse check - alert when below threshold)
        if (cacheStats.HitRate <= thresholds.CacheHitRateThreshold.Critical)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "CacheHitRate",
                Severity = "critical",
                Message = $"Critical cache hit rate: {cacheStats.HitRate * 100:F1}%",
                Threshold = thresholds.CacheHitRateThreshold.Critical,
                CurrentValue = cacheStats.HitRate,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }
        else if (cacheStats.HitRate <= thresholds.CacheHitRateThreshold.Warning)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "CacheHitRate",
                Severity = "warning",
                Message = $"Low cache hit rate: {cacheStats.HitRate * 100:F1}%",
                Threshold = thresholds.CacheHitRateThreshold.Warning,
                CurrentValue = cacheStats.HitRate,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }

        // Check Database connection utilization
        if (dbMetrics.ConnectionPool.Utilization >= thresholds.DatabaseConnectionThreshold.Critical)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "DatabaseConnections",
                Severity = "critical",
                Message = $"Critical database connection usage: {dbMetrics.ConnectionPool.Utilization * 100:F1}%",
                Threshold = thresholds.DatabaseConnectionThreshold.Critical,
                CurrentValue = dbMetrics.ConnectionPool.Utilization,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }
        else if (dbMetrics.ConnectionPool.Utilization >= thresholds.DatabaseConnectionThreshold.Warning)
        {
            alerts.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = "DatabaseConnections",
                Severity = "warning",
                Message = $"High database connection usage: {dbMetrics.ConnectionPool.Utilization * 100:F1}%",
                Threshold = thresholds.DatabaseConnectionThreshold.Warning,
                CurrentValue = dbMetrics.ConnectionPool.Utilization,
                Timestamp = DateTime.UtcNow,
                Status = "active"
            });
        }

        return alerts;
    }

    private List<PerformanceAlert> GenerateAlertHistory()
    {
        // Generate sample alert history (in production, this would come from database)
        var history = new List<PerformanceAlert>();
        var random = new Random();
        var alertTypes = new[] { "CPU", "Memory", "Disk", "ResponseTime", "ErrorRate", "CacheHitRate", "DatabaseConnections" };
        var severities = new[] { "warning", "critical" };
        var statuses = new[] { "resolved", "acknowledged" };

        for (int i = 0; i < 20; i++)
        {
            var alertType = alertTypes[random.Next(alertTypes.Length)];
            var severity = severities[random.Next(severities.Length)];
            var status = statuses[random.Next(statuses.Length)];
            var timestamp = DateTime.UtcNow.AddHours(-random.Next(1, 168)); // Last week

            history.Add(new PerformanceAlert
            {
                Id = Guid.NewGuid(),
                Type = alertType,
                Severity = severity,
                Message = GenerateAlertMessage(alertType, severity),
                Threshold = random.NextDouble() * 100,
                CurrentValue = random.NextDouble() * 100,
                Timestamp = timestamp,
                Status = status,
                ResolvedAt = status == "resolved" ? timestamp.AddMinutes(random.Next(5, 60)) : null
            });
        }

        return history.OrderByDescending(a => a.Timestamp).ToList();
    }

    private string GenerateAlertMessage(string alertType, string severity)
    {
        var random = new Random();
        return alertType switch
        {
            "CPU" => $"{(severity == "critical" ? "Critical" : "High")} CPU usage: {random.Next(70, 100)}%",
            "Memory" => $"{(severity == "critical" ? "Critical" : "High")} memory usage: {random.Next(80, 100)}%",
            "Disk" => $"{(severity == "critical" ? "Critical" : "High")} disk usage: {random.Next(85, 100)}%",
            "ResponseTime" => $"{(severity == "critical" ? "Critical" : "High")} response time: {random.Next(1000, 5000)}ms",
            "ErrorRate" => $"{(severity == "critical" ? "Critical" : "High")} error rate: {random.NextDouble() * 0.10:F2}%",
            "CacheHitRate" => $"Low cache hit rate: {random.Next(50, 70)}%",
            "DatabaseConnections" => $"{(severity == "critical" ? "Critical" : "High")} database connection usage: {random.Next(80, 100)}%",
            _ => $"{severity} alert for {alertType}"
        };
    }

    private void ValidateThresholds(PerformanceThresholds thresholds)
    {
        if (thresholds == null)
            throw new ArgumentNullException(nameof(thresholds));

        ValidateThresholdConfig(thresholds.CpuThreshold, "CPU");
        ValidateThresholdConfig(thresholds.MemoryThreshold, "Memory");
        ValidateThresholdConfig(thresholds.DiskThreshold, "Disk");
        ValidateThresholdConfig(thresholds.ResponseTimeThreshold, "ResponseTime");
        ValidateThresholdConfig(thresholds.ErrorRateThreshold, "ErrorRate");
        ValidateThresholdConfig(thresholds.CacheHitRateThreshold, "CacheHitRate");
        ValidateThresholdConfig(thresholds.DatabaseConnectionThreshold, "DatabaseConnection");
    }

    private void ValidateThresholdConfig(ThresholdConfig config, string name)
    {
        if (config == null)
            throw new ArgumentException($"{name} threshold configuration is required");

        if (config.Warning <= 0 || config.Critical <= 0)
            throw new ArgumentException($"{name} thresholds must be positive values");

        if (config.Warning >= config.Critical && name != "CacheHitRate")
            throw new ArgumentException($"{name} warning threshold must be less than critical threshold");

        if (name == "CacheHitRate" && config.Critical >= config.Warning)
            throw new ArgumentException($"{name} critical threshold must be less than warning threshold");
    }
}
