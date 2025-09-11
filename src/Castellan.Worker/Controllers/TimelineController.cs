using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services;
using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TimelineController : ControllerBase
{
    private readonly ITimelineService _timelineService;
    private readonly ILogger<TimelineController> _logger;

    public TimelineController(ITimelineService timelineService, ILogger<TimelineController> logger)
    {
        _timelineService = timelineService;
        _logger = logger;
    }

    /// <summary>
    /// Gets timeline data for the specified time range and granularity (main endpoint)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> GetTimeline(
        [FromQuery] string granularity = "day",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string[]? eventTypes = null,
        [FromQuery] string[]? riskLevels = null)
    {
        try
        {
            // Parse granularity string to enum
            var granularityEnum = granularity.ToLowerInvariant() switch
            {
                "minute" => TimelineGranularity.Minute,
                "hour" => TimelineGranularity.Hour,
                "day" => TimelineGranularity.Day,
                "week" => TimelineGranularity.Week,
                "month" => TimelineGranularity.Month,
                _ => TimelineGranularity.Day
            };

            var request = new TimelineRequest
            {
                StartTime = from ?? DateTime.UtcNow.AddDays(-7),
                EndTime = to ?? DateTime.UtcNow,
                Granularity = granularityEnum,
                RiskLevels = riskLevels?.ToList() ?? new List<string>(),
                EventTypes = eventTypes?.ToList() ?? new List<string>()
            };

            var result = await _timelineService.GetTimelineDataAsync(request);
            
            // Transform to format expected by frontend
            var timelineData = result.DataPoints.Select(dp => new
            {
                timestamp = dp.Timestamp.ToString("O"), // ISO 8601
                count = dp.Count
            }).ToList();

            return Ok(new { data = timelineData, total = timelineData.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving timeline data");
            return StatusCode(500, new { message = "Internal server error while retrieving timeline data" });
        }
    }

    /// <summary>
    /// Gets timeline data for the specified time range and granularity (legacy endpoint)
    /// </summary>
    [HttpGet("events")]
    public async Task<ActionResult<TimelineResponse>> GetTimelineEvents(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] TimelineGranularity granularity = TimelineGranularity.Hour,
        [FromQuery] string[]? riskLevels = null,
        [FromQuery] string[]? eventTypes = null,
        [FromQuery] string? search = null)
    {
        try
        {
            var request = new TimelineRequest
            {
                StartTime = startTime ?? DateTime.UtcNow.AddDays(-7),
                EndTime = endTime ?? DateTime.UtcNow,
                Granularity = granularity,
                RiskLevels = riskLevels?.ToList() ?? new List<string>(),
                EventTypes = eventTypes?.ToList() ?? new List<string>(),
                Search = search
            };

            var result = await _timelineService.GetTimelineDataAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving timeline events");
            return StatusCode(500, new { message = "Internal server error while retrieving timeline data" });
        }
    }

    /// <summary>
    /// Gets timeline statistics summary
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<TimelineStatsResponse>> GetTimelineStats(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        try
        {
            var request = new TimelineStatsRequest
            {
                StartTime = startTime ?? DateTime.UtcNow.AddDays(-7),
                EndTime = endTime ?? DateTime.UtcNow
            };

            var result = await _timelineService.GetTimelineStatsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving timeline statistics");
            return StatusCode(500, new { message = "Internal server error while retrieving timeline statistics" });
        }
    }

    /// <summary>
    /// Gets detailed events for a specific time period
    /// </summary>
    [HttpGet("events/detailed")]
    public async Task<ActionResult<DetailedTimelineResponse>> GetDetailedTimelineEvents(
        [FromQuery, Required] DateTime startTime,
        [FromQuery, Required] DateTime endTime,
        [FromQuery] string[]? riskLevels = null,
        [FromQuery] string[]? eventTypes = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var request = new DetailedTimelineRequest
            {
                StartTime = startTime,
                EndTime = endTime,
                RiskLevels = riskLevels?.ToList() ?? new List<string>(),
                EventTypes = eventTypes?.ToList() ?? new List<string>(),
                Limit = limit
            };

            var result = await _timelineService.GetDetailedTimelineEventsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving detailed timeline events");
            return StatusCode(500, new { message = "Internal server error while retrieving detailed timeline events" });
        }
    }

    /// <summary>
    /// Gets timeline heatmap data for visualization
    /// </summary>
    [HttpGet("heatmap")]
    public async Task<ActionResult<TimelineHeatmapResponse>> GetTimelineHeatmap(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null,
        [FromQuery] TimelineGranularity granularity = TimelineGranularity.Hour,
        [FromQuery] string[]? riskLevels = null)
    {
        try
        {
            var request = new TimelineHeatmapRequest
            {
                StartTime = startTime ?? DateTime.UtcNow.AddDays(-7),
                EndTime = endTime ?? DateTime.UtcNow,
                Granularity = granularity,
                RiskLevels = riskLevels?.ToList() ?? new List<string>()
            };

            var result = await _timelineService.GetTimelineHeatmapAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving timeline heatmap");
            return StatusCode(500, new { message = "Internal server error while retrieving timeline heatmap" });
        }
    }

    /// <summary>
    /// Gets timeline metrics and trends
    /// </summary>
    [HttpGet("metrics")]
    public async Task<ActionResult<TimelineMetricsResponse>> GetTimelineMetrics(
        [FromQuery] DateTime? startTime = null,
        [FromQuery] DateTime? endTime = null)
    {
        try
        {
            var request = new TimelineMetricsRequest
            {
                StartTime = startTime ?? DateTime.UtcNow.AddDays(-30),
                EndTime = endTime ?? DateTime.UtcNow
            };

            var result = await _timelineService.GetTimelineMetricsAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving timeline metrics");
            return StatusCode(500, new { message = "Internal server error while retrieving timeline metrics" });
        }
    }
}

// Enums
public enum TimelineGranularity
{
    Minute,
    Hour,
    Day,
    Week,
    Month
}

// Request DTOs
public class TimelineRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimelineGranularity Granularity { get; set; } = TimelineGranularity.Hour;
    public List<string> RiskLevels { get; set; } = new();
    public List<string> EventTypes { get; set; } = new();
    public string? Search { get; set; }
}

public class TimelineStatsRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class DetailedTimelineRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<string> RiskLevels { get; set; } = new();
    public List<string> EventTypes { get; set; } = new();
    public int Limit { get; set; } = 50;
}

public class TimelineHeatmapRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimelineGranularity Granularity { get; set; } = TimelineGranularity.Hour;
    public List<string> RiskLevels { get; set; } = new();
}

public class TimelineMetricsRequest
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

// Response DTOs
public class TimelineResponse
{
    public List<TimelineDataPoint> DataPoints { get; set; } = new();
    public TimelineMetadata Metadata { get; set; } = new();
    public List<TimelineEvent> Events { get; set; } = new();
}

public class TimelineDataPoint
{
    public DateTime Timestamp { get; set; }
    public int Count { get; set; }
    public Dictionary<string, int> RiskLevelCounts { get; set; } = new();
    public Dictionary<string, int> EventTypeCounts { get; set; } = new();
    public double AverageRiskScore { get; set; }
    public List<string> TopMitreTechniques { get; set; } = new();
}

public class TimelineEvent
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string[] MitreTechniques { get; set; } = Array.Empty<string>();
    public double Confidence { get; set; }
    public string Machine { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
}

public class TimelineMetadata
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimelineGranularity Granularity { get; set; }
    public int TotalEvents { get; set; }
    public int DataPointCount { get; set; }
    public string TimeZone { get; set; } = "UTC";
}

public class TimelineStatsResponse
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> EventsByRiskLevel { get; set; } = new();
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public Dictionary<string, int> EventsByHour { get; set; } = new();
    public Dictionary<string, int> EventsByDayOfWeek { get; set; } = new();
    public List<string> TopMitreTechniques { get; set; } = new();
    public List<string> TopMachines { get; set; } = new();
    public List<string> TopUsers { get; set; } = new();
    public double AverageRiskScore { get; set; }
    public int HighRiskEvents { get; set; }
    public int CriticalRiskEvents { get; set; }
}

public class DetailedTimelineResponse
{
    public List<TimelineEvent> Events { get; set; } = new();
    public int TotalCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class TimelineHeatmapResponse
{
    public List<TimelineHeatmapPoint> HeatmapData { get; set; } = new();
    public TimelineHeatmapMetadata Metadata { get; set; } = new();
}

public class TimelineHeatmapPoint
{
    public DateTime Timestamp { get; set; }
    public int Intensity { get; set; } // 0-100 scale
    public int EventCount { get; set; }
    public Dictionary<string, int> RiskBreakdown { get; set; } = new();
}

public class TimelineHeatmapMetadata
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimelineGranularity Granularity { get; set; }
    public int MaxIntensity { get; set; }
    public int TotalDataPoints { get; set; }
}

public class TimelineMetricsResponse
{
    public TimelineTrends Trends { get; set; } = new();
    public TimelinePatterns Patterns { get; set; } = new();
    public List<TimelineAnomaly> Anomalies { get; set; } = new();
}

public class TimelineTrends
{
    public double EventVelocityTrend { get; set; } // Events per hour trend
    public double RiskLevelTrend { get; set; } // Risk level trend (increasing/decreasing)
    public Dictionary<string, double> EventTypeTrends { get; set; } = new();
    public List<TimelineTrendPoint> HistoricalTrends { get; set; } = new();
}

public class TimelineTrendPoint
{
    public DateTime Period { get; set; }
    public double Value { get; set; }
    public string Metric { get; set; } = string.Empty;
}

public class TimelinePatterns
{
    public List<string> RecurringPatterns { get; set; } = new();
    public Dictionary<int, int> BusyHours { get; set; } = new(); // Hour -> Event count
    public Dictionary<string, int> BusyDaysOfWeek { get; set; } = new();
    public List<TimelinePattern> DetectedPatterns { get; set; } = new();
}

public class TimelinePattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public int Frequency { get; set; }
}

public class TimelineAnomaly
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty; // Spike, Drop, Pattern Break
    public string Description { get; set; } = string.Empty;
    public double Severity { get; set; }
    public int AffectedEvents { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}
