using Castellan.Worker.Abstractions;
using Castellan.Worker.Controllers;
using Castellan.Worker.Models;
using System.Text.Json;

namespace Castellan.Worker.Services;

public class TimelineService : ITimelineService
{
    private readonly ISecurityEventStore _eventStore;
    private readonly SecurityEventService _securityEventService;
    private readonly ILogger<TimelineService> _logger;

    public TimelineService(
        ISecurityEventStore eventStore,
        SecurityEventService securityEventService,
        ILogger<TimelineService> logger)
    {
        _eventStore = eventStore;
        _securityEventService = securityEventService;
        _logger = logger;
    }

    public async Task<TimelineResponse> GetTimelineDataAsync(TimelineRequest request)
    {
        try
        {
            _logger.LogDebug("Getting timeline data for {StartTime} to {EndTime} with {Granularity} granularity", 
                request.StartTime, request.EndTime, request.Granularity);

            // Get events for the time range
            var filters = BuildFilters(request.RiskLevels, request.EventTypes, request.Search, request.StartTime, request.EndTime);
            var allEvents = _eventStore.GetSecurityEvents(1, 10000, filters).ToList();

            // Generate time slots based on granularity
            var timeSlots = GenerateTimeSlots(request.StartTime, request.EndTime, request.Granularity);
            var dataPoints = new List<TimelineDataPoint>();

            foreach (var timeSlot in timeSlots)
            {
                var slotEndTime = GetSlotEndTime(timeSlot, request.Granularity);
                var eventsInSlot = allEvents.Where(e => 
                    e.OriginalEvent.Time.DateTime >= timeSlot && 
                    e.OriginalEvent.Time.DateTime < slotEndTime).ToList();

                var dataPoint = new TimelineDataPoint
                {
                    Timestamp = timeSlot,
                    Count = eventsInSlot.Count,
                    RiskLevelCounts = eventsInSlot.GroupBy(e => e.RiskLevel)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    EventTypeCounts = eventsInSlot.GroupBy(e => e.EventType.ToString())
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AverageRiskScore = eventsInSlot.Any() ? eventsInSlot.Average(e => GetRiskScore(e.RiskLevel)) : 0,
                    TopMitreTechniques = GetTopMitreTechniques(eventsInSlot, 3)
                };

                dataPoints.Add(dataPoint);
            }

            // Get sample events for display
            var sampleEvents = allEvents
                .OrderByDescending(e => e.OriginalEvent.Time)
                .Take(20)
                .Select(e => new TimelineEvent
                {
                    Id = e.Id,
                    EventType = e.EventType.ToString(),
                    Timestamp = e.OriginalEvent.Time.DateTime,
                    RiskLevel = e.RiskLevel,
                    Summary = e.Summary,
                    MitreTechniques = e.MitreTechniques,
                    Confidence = e.Confidence,
                    Machine = e.OriginalEvent.Host ?? "",
                    User = e.OriginalEvent.User ?? ""
                })
                .ToList();

            var response = new TimelineResponse
            {
                DataPoints = dataPoints,
                Events = sampleEvents,
                Metadata = new TimelineMetadata
                {
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Granularity = request.Granularity,
                    TotalEvents = allEvents.Count,
                    DataPointCount = dataPoints.Count,
                    TimeZone = "UTC"
                }
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline data");
            throw;
        }
    }

    public async Task<TimelineStatsResponse> GetTimelineStatsAsync(TimelineStatsRequest request)
    {
        try
        {
            _logger.LogDebug("Getting timeline stats for {StartTime} to {EndTime}", request.StartTime, request.EndTime);

            var filters = BuildFilters(null, null, null, request.StartTime, request.EndTime);
            var events = _eventStore.GetSecurityEvents(1, 10000, filters).ToList();

            var stats = new TimelineStatsResponse
            {
                TotalEvents = events.Count,
                EventsByRiskLevel = events.GroupBy(e => e.RiskLevel)
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsByType = events.GroupBy(e => e.EventType.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsByHour = events.GroupBy(e => e.OriginalEvent.Time.Hour.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                EventsByDayOfWeek = events.GroupBy(e => e.OriginalEvent.Time.DayOfWeek.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                TopMitreTechniques = GetTopMitreTechniques(events, 10),
                TopMachines = events.Where(e => !string.IsNullOrEmpty(e.OriginalEvent.Host))
                    .GroupBy(e => e.OriginalEvent.Host!)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => g.Key)
                    .ToList(),
                TopUsers = events.Where(e => !string.IsNullOrEmpty(e.OriginalEvent.User))
                    .GroupBy(e => e.OriginalEvent.User!)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => g.Key)
                    .ToList(),
                AverageRiskScore = events.Any() ? events.Average(e => GetRiskScore(e.RiskLevel)) : 0,
                HighRiskEvents = events.Count(e => e.RiskLevel.ToLower() == "high"),
                CriticalRiskEvents = events.Count(e => e.RiskLevel.ToLower() == "critical")
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline stats");
            throw;
        }
    }

    public async Task<DetailedTimelineResponse> GetDetailedTimelineEventsAsync(DetailedTimelineRequest request)
    {
        try
        {
            _logger.LogDebug("Getting detailed timeline events for {StartTime} to {EndTime}", request.StartTime, request.EndTime);

            var filters = BuildFilters(request.RiskLevels, request.EventTypes, null, request.StartTime, request.EndTime);
            var allEvents = _eventStore.GetSecurityEvents(1, 10000, filters).ToList();

            var events = allEvents
                .OrderByDescending(e => e.OriginalEvent.Time)
                .Take(request.Limit)
                .Select(e => new TimelineEvent
                {
                    Id = e.Id,
                    EventType = e.EventType.ToString(),
                    Timestamp = e.OriginalEvent.Time.DateTime,
                    RiskLevel = e.RiskLevel,
                    Summary = e.Summary,
                    MitreTechniques = e.MitreTechniques,
                    Confidence = e.Confidence,
                    Machine = e.OriginalEvent.Host ?? "",
                    User = e.OriginalEvent.User ?? ""
                })
                .ToList();

            return new DetailedTimelineResponse
            {
                Events = events,
                TotalCount = allEvents.Count,
                StartTime = request.StartTime,
                EndTime = request.EndTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting detailed timeline events");
            throw;
        }
    }

    public async Task<TimelineHeatmapResponse> GetTimelineHeatmapAsync(TimelineHeatmapRequest request)
    {
        try
        {
            _logger.LogDebug("Getting timeline heatmap for {StartTime} to {EndTime}", request.StartTime, request.EndTime);

            var filters = BuildFilters(request.RiskLevels, null, null, request.StartTime, request.EndTime);
            var events = _eventStore.GetSecurityEvents(1, 10000, filters).ToList();

            var timeSlots = GenerateTimeSlots(request.StartTime, request.EndTime, request.Granularity);
            var heatmapData = new List<TimelineHeatmapPoint>();
            var maxEventCount = 0;

            foreach (var timeSlot in timeSlots)
            {
                var slotEndTime = GetSlotEndTime(timeSlot, request.Granularity);
                var eventsInSlot = events.Where(e => 
                    e.OriginalEvent.Time.DateTime >= timeSlot && 
                    e.OriginalEvent.Time.DateTime < slotEndTime).ToList();

                var eventCount = eventsInSlot.Count;
                if (eventCount > maxEventCount)
                    maxEventCount = eventCount;

                var heatmapPoint = new TimelineHeatmapPoint
                {
                    Timestamp = timeSlot,
                    EventCount = eventCount,
                    RiskBreakdown = eventsInSlot.GroupBy(e => e.RiskLevel)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                heatmapData.Add(heatmapPoint);
            }

            // Calculate intensity (0-100 scale)
            foreach (var point in heatmapData)
            {
                point.Intensity = maxEventCount > 0 ? (int)Math.Round((double)point.EventCount / maxEventCount * 100) : 0;
            }

            return new TimelineHeatmapResponse
            {
                HeatmapData = heatmapData,
                Metadata = new TimelineHeatmapMetadata
                {
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Granularity = request.Granularity,
                    MaxIntensity = maxEventCount,
                    TotalDataPoints = heatmapData.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline heatmap");
            throw;
        }
    }

    public async Task<TimelineMetricsResponse> GetTimelineMetricsAsync(TimelineMetricsRequest request)
    {
        try
        {
            _logger.LogDebug("Getting timeline metrics for {StartTime} to {EndTime}", request.StartTime, request.EndTime);

            var filters = BuildFilters(null, null, null, request.StartTime, request.EndTime);
            var events = _eventStore.GetSecurityEvents(1, 10000, filters).ToList();

            // Calculate trends
            var trends = CalculateTrends(events, request.StartTime, request.EndTime);
            
            // Detect patterns
            var patterns = DetectPatterns(events);
            
            // Detect anomalies
            var anomalies = DetectAnomalies(events, request.StartTime, request.EndTime);

            return new TimelineMetricsResponse
            {
                Trends = trends,
                Patterns = patterns,
                Anomalies = anomalies
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline metrics");
            throw;
        }
    }

    // Helper methods
    private Dictionary<string, object> BuildFilters(List<string>? riskLevels, List<string>? eventTypes, string? search, DateTime? startTime, DateTime? endTime)
    {
        var filters = new Dictionary<string, object>();

        if (startTime.HasValue)
            filters["startdate"] = startTime.Value;

        if (endTime.HasValue)
            filters["enddate"] = endTime.Value;

        if (riskLevels != null && riskLevels.Any())
            filters["risklevels"] = riskLevels.ToArray();

        if (eventTypes != null && eventTypes.Any())
            filters["eventtypes"] = eventTypes.ToArray();

        if (!string.IsNullOrEmpty(search))
            filters["search"] = search;

        return filters;
    }

    private List<DateTime> GenerateTimeSlots(DateTime startTime, DateTime endTime, TimelineGranularity granularity)
    {
        var slots = new List<DateTime>();
        var current = RoundDownToGranularity(startTime, granularity);
        
        while (current < endTime)
        {
            slots.Add(current);
            current = AddGranularityUnit(current, granularity);
        }

        return slots;
    }

    private DateTime RoundDownToGranularity(DateTime dateTime, TimelineGranularity granularity)
    {
        return granularity switch
        {
            TimelineGranularity.Minute => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, 0),
            TimelineGranularity.Hour => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, 0, 0),
            TimelineGranularity.Day => new DateTime(dateTime.Year, dateTime.Month, dateTime.Day),
            TimelineGranularity.Week => dateTime.Date.AddDays(-(int)dateTime.DayOfWeek),
            TimelineGranularity.Month => new DateTime(dateTime.Year, dateTime.Month, 1),
            _ => dateTime
        };
    }

    private DateTime AddGranularityUnit(DateTime dateTime, TimelineGranularity granularity)
    {
        return granularity switch
        {
            TimelineGranularity.Minute => dateTime.AddMinutes(1),
            TimelineGranularity.Hour => dateTime.AddHours(1),
            TimelineGranularity.Day => dateTime.AddDays(1),
            TimelineGranularity.Week => dateTime.AddDays(7),
            TimelineGranularity.Month => dateTime.AddMonths(1),
            _ => dateTime
        };
    }

    private DateTime GetSlotEndTime(DateTime slotStart, TimelineGranularity granularity)
    {
        return AddGranularityUnit(slotStart, granularity);
    }

    private double GetRiskScore(string riskLevel)
    {
        return riskLevel.ToLower() switch
        {
            "critical" => 100,
            "high" => 75,
            "medium" => 50,
            "low" => 25,
            _ => 0
        };
    }

    private List<string> GetTopMitreTechniques(List<SecurityEvent> events, int count)
    {
        return events
            .SelectMany(e => e.MitreTechniques)
            .Where(t => !string.IsNullOrEmpty(t))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToList();
    }

    private TimelineTrends CalculateTrends(List<SecurityEvent> events, DateTime startTime, DateTime endTime)
    {
        // Simple trend calculation - in a production system, this would be more sophisticated
        var totalHours = (endTime - startTime).TotalHours;
        var eventsPerHour = totalHours > 0 ? events.Count / totalHours : 0;

        var eventTypeTrends = events
            .GroupBy(e => e.EventType.ToString())
            .ToDictionary(g => g.Key, g => g.Count() / totalHours);

        var historicalTrends = new List<TimelineTrendPoint>
        {
            new() { Period = startTime, Value = eventsPerHour, Metric = "EventsPerHour" }
        };

        return new TimelineTrends
        {
            EventVelocityTrend = eventsPerHour,
            RiskLevelTrend = events.Any() ? events.Average(e => GetRiskScore(e.RiskLevel)) : 0,
            EventTypeTrends = eventTypeTrends,
            HistoricalTrends = historicalTrends
        };
    }

    private TimelinePatterns DetectPatterns(List<SecurityEvent> events)
    {
        var busyHours = events
            .GroupBy(e => e.OriginalEvent.Time.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        var busyDaysOfWeek = events
            .GroupBy(e => e.OriginalEvent.Time.DayOfWeek.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var detectedPatterns = new List<TimelinePattern>();

        // Detect burst patterns (simplified)
        if (events.Any())
        {
            var busiestHour = busyHours.OrderByDescending(kv => kv.Value).FirstOrDefault();
            if (busiestHour.Value > events.Count * 0.3) // If 30% of events occur in one hour
            {
                detectedPatterns.Add(new TimelinePattern
                {
                    Name = "HourlyBurst",
                    Description = $"High activity detected during hour {busiestHour.Key}:00",
                    Confidence = 0.8,
                    FirstOccurrence = events.Min(e => e.OriginalEvent.Time.DateTime),
                    LastOccurrence = events.Max(e => e.OriginalEvent.Time.DateTime),
                    Frequency = busiestHour.Value
                });
            }
        }

        return new TimelinePatterns
        {
            BusyHours = busyHours,
            BusyDaysOfWeek = busyDaysOfWeek,
            DetectedPatterns = detectedPatterns,
            RecurringPatterns = detectedPatterns.Select(p => p.Name).ToList()
        };
    }

    private List<TimelineAnomaly> DetectAnomalies(List<SecurityEvent> events, DateTime startTime, DateTime endTime)
    {
        var anomalies = new List<TimelineAnomaly>();

        // Simple anomaly detection - spike detection
        var hourlyEvents = events.GroupBy(e => e.OriginalEvent.Time.ToString("yyyy-MM-dd HH:00"))
            .ToDictionary(g => g.Key, g => g.Count());

        if (hourlyEvents.Any())
        {
            var average = hourlyEvents.Values.Average();
            var threshold = average * 2; // Simple threshold - 2x average

            foreach (var hourly in hourlyEvents.Where(h => h.Value > threshold))
            {
                if (DateTime.TryParse(hourly.Key.Replace(":00", ":00:00"), out var anomalyTime))
                {
                    anomalies.Add(new TimelineAnomaly
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = anomalyTime,
                        Type = "Spike",
                        Description = $"Event spike detected: {hourly.Value} events (avg: {average:F1})",
                        Severity = hourly.Value > threshold * 2 ? 0.9 : 0.6,
                        AffectedEvents = hourly.Value,
                        Details = new Dictionary<string, object>
                        {
                            ["average"] = average,
                            ["actual"] = hourly.Value,
                            ["threshold"] = threshold
                        }
                    });
                }
            }
        }

        return anomalies;
    }
}
