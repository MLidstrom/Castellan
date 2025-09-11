using Castellan.Worker.Controllers;

namespace Castellan.Worker.Services;

public interface ITimelineService
{
    /// <summary>
    /// Gets timeline data aggregated by the specified granularity
    /// </summary>
    Task<TimelineResponse> GetTimelineDataAsync(TimelineRequest request);

    /// <summary>
    /// Gets timeline statistics for the specified time range
    /// </summary>
    Task<TimelineStatsResponse> GetTimelineStatsAsync(TimelineStatsRequest request);

    /// <summary>
    /// Gets detailed timeline events for a specific time period
    /// </summary>
    Task<DetailedTimelineResponse> GetDetailedTimelineEventsAsync(DetailedTimelineRequest request);

    /// <summary>
    /// Gets timeline heatmap data for visualization
    /// </summary>
    Task<TimelineHeatmapResponse> GetTimelineHeatmapAsync(TimelineHeatmapRequest request);

    /// <summary>
    /// Gets timeline metrics, trends, and anomaly detection
    /// </summary>
    Task<TimelineMetricsResponse> GetTimelineMetricsAsync(TimelineMetricsRequest request);
}
