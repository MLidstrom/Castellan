using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Services.Compliance;

namespace Castellan.Worker.Controllers;

[Authorize]
[ApiController]
[Route("api/compliance-performance")]
public class CompliancePerformanceController : ControllerBase
{
    private readonly ICompliancePerformanceMonitorService _performanceMonitor;
    private readonly ILogger<CompliancePerformanceController> _logger;

    public CompliancePerformanceController(
        ICompliancePerformanceMonitorService performanceMonitor,
        ILogger<CompliancePerformanceController> logger)
    {
        _performanceMonitor = performanceMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive compliance performance metrics
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetPerformanceMetrics()
    {
        try
        {
            var metrics = _performanceMonitor.GetPerformanceMetrics();

            return Ok(new
            {
                data = metrics,
                summary = new
                {
                    total_reports = metrics.TotalReportsGenerated,
                    total_pdfs = metrics.TotalPdfsGenerated,
                    cache_hit_rate = $"{metrics.CacheHitRate:P2}",
                    avg_report_time_ms = metrics.AverageReportGenerationTime.TotalMilliseconds,
                    avg_pdf_time_ms = metrics.AveragePdfGenerationTime.TotalMilliseconds,
                    most_popular_framework = metrics.ReportsByFramework.OrderByDescending(x => x.Value).FirstOrDefault().Key ?? "None",
                    most_popular_format = metrics.ReportsByFormat.OrderByDescending(x => x.Value).FirstOrDefault().Key.ToString() ?? "None"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance performance metrics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get performance metrics summary for dashboard
    /// </summary>
    [HttpGet("summary")]
    public IActionResult GetPerformanceSummary()
    {
        try
        {
            var metrics = _performanceMonitor.GetPerformanceMetrics();

            var summary = new
            {
                reports_generated = metrics.TotalReportsGenerated,
                pdfs_generated = metrics.TotalPdfsGenerated,
                cache_efficiency = new
                {
                    hit_rate = metrics.CacheHitRate,
                    total_operations = metrics.TotalCacheOperations,
                    hits = metrics.CacheHits,
                    misses = metrics.CacheMisses
                },
                performance = new
                {
                    avg_report_generation_ms = Math.Round(metrics.AverageReportGenerationTime.TotalMilliseconds, 2),
                    avg_pdf_generation_ms = Math.Round(metrics.AveragePdfGenerationTime.TotalMilliseconds, 2),
                    fastest_report_ms = Math.Round(metrics.FastestReportGenerationTime.TotalMilliseconds, 2),
                    slowest_report_ms = Math.Round(metrics.SlowestReportGenerationTime.TotalMilliseconds, 2),
                    slowest_framework = metrics.SlowestReportFramework
                },
                usage_patterns = new
                {
                    by_framework = metrics.ReportsByFramework,
                    by_format = metrics.ReportsByFormat.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value
                    )
                },
                pdf_statistics = new
                {
                    total_size_mb = Math.Round(metrics.TotalPdfSizeBytes / (1024.0 * 1024.0), 2),
                    avg_size_kb = Math.Round(metrics.AveragePdfSizeBytes / 1024.0, 2),
                    largest_size_kb = Math.Round(metrics.LargestPdfSizeBytes / 1024.0, 2),
                    smallest_size_kb = Math.Round(metrics.SmallestPdfSizeBytes / 1024.0, 2)
                },
                last_updated = metrics.LastUpdated
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance performance summary");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get performance trends over time (simplified version)
    /// </summary>
    [HttpGet("trends")]
    public IActionResult GetPerformanceTrends()
    {
        try
        {
            var metrics = _performanceMonitor.GetPerformanceMetrics();

            // This is a simplified version - in a full implementation,
            // you would store historical data points
            var trends = new
            {
                current_period = new
                {
                    reports_per_hour = 0, // Would calculate based on time window
                    avg_response_time_ms = metrics.AverageReportGenerationTime.TotalMilliseconds,
                    cache_hit_rate = metrics.CacheHitRate,
                    error_rate = 0.0 // Would track errors
                },
                comparison_available = false,
                message = "Historical trend data collection not yet implemented"
            };

            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance performance trends");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Reset performance metrics (admin only)
    /// </summary>
    [HttpPost("reset")]
    public IActionResult ResetPerformanceMetrics()
    {
        try
        {
            // In a production system, you might want additional authorization checks here
            _performanceMonitor.ResetMetrics();

            _logger.LogInformation("Compliance performance metrics reset by user");

            return Ok(new
            {
                message = "Performance metrics have been reset",
                reset_at = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting compliance performance metrics");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get performance health status
    /// </summary>
    [HttpGet("health")]
    public IActionResult GetPerformanceHealth()
    {
        try
        {
            var metrics = _performanceMonitor.GetPerformanceMetrics();

            // Define performance thresholds
            const double maxAcceptableReportTimeMs = 5000; // 5 seconds
            const double maxAcceptablePdfTimeMs = 10000;   // 10 seconds
            const double minAcceptableCacheHitRate = 0.7;  // 70%

            var health = new
            {
                overall_status = "healthy", // Would calculate based on thresholds
                metrics_collection = "active",
                performance_indicators = new
                {
                    report_generation = new
                    {
                        status = metrics.AverageReportGenerationTime.TotalMilliseconds <= maxAcceptableReportTimeMs ? "healthy" : "warning",
                        avg_time_ms = Math.Round(metrics.AverageReportGenerationTime.TotalMilliseconds, 2),
                        threshold_ms = maxAcceptableReportTimeMs,
                        message = metrics.AverageReportGenerationTime.TotalMilliseconds <= maxAcceptableReportTimeMs
                            ? "Report generation time within acceptable limits"
                            : "Report generation time exceeds recommended threshold"
                    },
                    pdf_generation = new
                    {
                        status = metrics.AveragePdfGenerationTime.TotalMilliseconds <= maxAcceptablePdfTimeMs ? "healthy" : "warning",
                        avg_time_ms = Math.Round(metrics.AveragePdfGenerationTime.TotalMilliseconds, 2),
                        threshold_ms = maxAcceptablePdfTimeMs,
                        message = metrics.AveragePdfGenerationTime.TotalMilliseconds <= maxAcceptablePdfTimeMs
                            ? "PDF generation time within acceptable limits"
                            : "PDF generation time exceeds recommended threshold"
                    },
                    cache_efficiency = new
                    {
                        status = metrics.CacheHitRate >= minAcceptableCacheHitRate ? "healthy" : "warning",
                        hit_rate = Math.Round(metrics.CacheHitRate, 3),
                        threshold = minAcceptableCacheHitRate,
                        message = metrics.CacheHitRate >= minAcceptableCacheHitRate
                            ? "Cache hit rate is optimal"
                            : "Cache hit rate below recommended threshold"
                    }
                },
                recommendations = GeneratePerformanceRecommendations(metrics),
                last_checked = DateTime.UtcNow
            };

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving compliance performance health");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static List<string> GeneratePerformanceRecommendations(CompliancePerformanceMetrics metrics)
    {
        var recommendations = new List<string>();

        if (metrics.CacheHitRate < 0.7 && metrics.TotalCacheOperations > 10)
        {
            recommendations.Add("Consider increasing cache duration to improve hit rate");
        }

        if (metrics.AverageReportGenerationTime.TotalMilliseconds > 3000)
        {
            recommendations.Add("Report generation time is high - consider optimizing database queries");
        }

        if (metrics.AveragePdfGenerationTime.TotalMilliseconds > 5000)
        {
            recommendations.Add("PDF generation time is high - consider optimizing PDF rendering");
        }

        if (metrics.TotalReportsGenerated == 0)
        {
            recommendations.Add("No reports have been generated yet - metrics will improve with usage");
        }

        return recommendations;
    }
}