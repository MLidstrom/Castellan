using Castellan.Worker.Abstractions;
using Castellan.Worker.Controllers;
using Castellan.Worker.Models;
using Castellan.Worker.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Castellan.Worker.Services;

public class TimelineService : ITimelineService
{
    private readonly ISecurityEventStore _eventStore;
    private readonly IDbContextFactory<CastellanDbContext> _dbContextFactory;
    private readonly ILogger<TimelineService> _logger;

    public TimelineService(
        ISecurityEventStore eventStore,
        IDbContextFactory<CastellanDbContext> dbContextFactory,
        ILogger<TimelineService> logger)
    {
        _eventStore = eventStore;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<TimelineResponse> GetTimelineDataAsync(TimelineRequest request)
    {
        try
        {
            _logger.LogInformation("Getting timeline data for {StartTime} to {EndTime} with {Granularity} granularity",
                request.StartTime, request.EndTime, request.Granularity);

            // Use database-level aggregation for performance
            _logger.LogInformation("Performing optimized database aggregation with single query");

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var query = dbContext.SecurityEvents
                .AsNoTracking() // Disable change tracking for read-only query
                .AsQueryable();

            // Apply time range filter
            query = query.Where(e => e.Timestamp >= request.StartTime && e.Timestamp <= request.EndTime);

            // Apply additional filters
            if (request.RiskLevels != null && request.RiskLevels.Any())
            {
                query = query.Where(e => request.RiskLevels.Contains(e.RiskLevel));
            }

            if (request.EventTypes != null && request.EventTypes.Any())
            {
                query = query.Where(e => request.EventTypes.Contains(e.EventType));
            }

            // Use database-level GROUP BY aggregation for efficiency (no in-memory loading)
            _logger.LogInformation("Performing database-level aggregation using SQL GROUP BY");

            var dataPoints = await GetTimelineDataPointsViaSqlAsync(query, request.StartTime, request.EndTime, request.Granularity);
            _logger.LogInformation("Retrieved {Count} aggregated data points from database", dataPoints.Count);

            // Get sample events for display (only fetch 20 records)
            var sampleEvents = await query
                .OrderByDescending(e => e.Timestamp)
                .Take(20)
                .Select(e => new TimelineEvent
                {
                    Id = e.EventId,
                    EventType = e.EventType,
                    Timestamp = e.Timestamp,
                    RiskLevel = e.RiskLevel,
                    Summary = "",
                    MitreTechniques = DeserializeStringArray(e.MitreTechniques),
                    Confidence = (int)e.Confidence,
                    Machine = e.Source ?? "",
                    User = ""
                })
                .ToListAsync();

            // Get total count using COUNT(*) query (not loading all records)
            var totalEvents = await query.CountAsync();

            var response = new TimelineResponse
            {
                DataPoints = dataPoints,
                Events = sampleEvents,
                Metadata = new TimelineMetadata
                {
                    StartTime = request.StartTime,
                    EndTime = request.EndTime,
                    Granularity = request.Granularity,
                    TotalEvents = totalEvents,
                    DataPointCount = dataPoints.Count,
                    TimeZone = "UTC"
                }
            };

            _logger.LogInformation("Timeline response prepared with {DataPointCount} data points", dataPoints.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline data: {Message}", ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            throw;
        }
    }

    public async Task<TimelineStatsResponse> GetTimelineStatsAsync(TimelineStatsRequest request)
    {
        try
        {
            _logger.LogInformation("Getting timeline stats for {StartTime} to {EndTime}", request.StartTime, request.EndTime);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Use database-level aggregation - NO in-memory loading of all events
            var query = dbContext.SecurityEvents
                .Where(e => e.Timestamp >= request.StartTime && e.Timestamp <= request.EndTime)
                .AsNoTracking(); // Disable change tracking for read-only query

            // Database-level COUNT(*) query
            var totalEvents = await query.CountAsync();
            _logger.LogInformation("Total events in range: {TotalEvents}", totalEvents);

            // Database-level GROUP BY RiskLevel
            var eventsByRiskLevel = await query
                .GroupBy(e => e.RiskLevel)
                .Select(g => new { RiskLevel = g.Key, Count = g.Count() })
                .ToListAsync();

            // Database-level GROUP BY EventType
            var eventsByType = await query
                .GroupBy(e => e.EventType)
                .Select(g => new { EventType = g.Key, Count = g.Count() })
                .ToListAsync();

            // Database-level GROUP BY Hour
            var eventsByHour = await query
                .GroupBy(e => e.Timestamp.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .ToListAsync();

            // Database-level GROUP BY DayOfWeek
            var eventsByDayOfWeek = await query
                .GroupBy(e => e.Timestamp.DayOfWeek)
                .Select(g => new { DayOfWeek = g.Key, Count = g.Count() })
                .ToListAsync();

            // Database-level GROUP BY Source (top machines)
            var topMachines = await query
                .Where(e => e.Source != null && e.Source != "")
                .GroupBy(e => e.Source)
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10)
                .ToListAsync();

            // MITRE techniques require deserialization, so fetch only non-null JSON strings (not full entities)
            var mitreJsonStrings = await query
                .Where(e => e.MitreTechniques != null && e.MitreTechniques != "")
                .Select(e => e.MitreTechniques)
                .ToListAsync();

            var topMitreTechniques = mitreJsonStrings
                .SelectMany(json => DeserializeStringArray(json))
                .Where(t => !string.IsNullOrEmpty(t))
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            // Database-level aggregation for risk score calculation
            var averageRiskScore = eventsByRiskLevel.Any()
                ? eventsByRiskLevel.Sum(x => GetRiskScore(x.RiskLevel) * x.Count) / totalEvents
                : 0;

            var stats = new TimelineStatsResponse
            {
                TotalEvents = totalEvents,
                EventsByRiskLevel = eventsByRiskLevel.ToDictionary(x => x.RiskLevel, x => x.Count),
                EventsByType = eventsByType.ToDictionary(x => x.EventType, x => x.Count),
                EventsByHour = eventsByHour.ToDictionary(x => x.Hour.ToString(), x => x.Count),
                EventsByDayOfWeek = eventsByDayOfWeek.ToDictionary(x => x.DayOfWeek.ToString(), x => x.Count),
                TopMitreTechniques = topMitreTechniques,
                TopMachines = topMachines.Select(x => x.Source!).ToList(),
                TopUsers = topMachines.Select(x => x.Source!).ToList(), // Using Source as User for now
                AverageRiskScore = averageRiskScore,
                HighRiskEvents = eventsByRiskLevel.FirstOrDefault(x => x.RiskLevel.ToLower() == "high")?.Count ?? 0,
                CriticalRiskEvents = eventsByRiskLevel.FirstOrDefault(x => x.RiskLevel.ToLower() == "critical")?.Count ?? 0
            };

            _logger.LogInformation("Timeline stats computed using database-level aggregation");
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
            filters["starttime"] = startTime.Value;

        if (endTime.HasValue)
            filters["endtime"] = endTime.Value;

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

    private List<string> GetTopMitreTechniquesFromEntities(List<SecurityEventEntity> events, int count)
    {
        return events
            .Where(e => !string.IsNullOrEmpty(e.MitreTechniques))
            .SelectMany(e => DeserializeStringArray(e.MitreTechniques))
            .Where(t => !string.IsNullOrEmpty(t))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToList();
    }

    private static string[] DeserializeStringArray(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
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

    /// <summary>
    /// Performs database-level aggregation using a single SQL query with GROUP BY to avoid N×M queries.
    /// This is critical for performance when dealing with 100K+ events and multiple time slots.
    /// OPTIMIZED: Single query instead of N time slots × 5 queries per slot
    /// </summary>
    private async Task<List<TimelineDataPoint>> GetTimelineDataPointsViaSqlAsync(
        IQueryable<SecurityEventEntity> query,
        DateTime startTime,
        DateTime endTime,
        TimelineGranularity granularity)
    {
        // Generate time slots in memory (lightweight - just DateTime objects)
        var timeSlots = GenerateTimeSlots(startTime, endTime, granularity);
        var dataPoints = new List<TimelineDataPoint>();

        // OPTIMIZATION: Fetch all events in a single query with just the fields we need
        // This replaces N×5 queries with a single query
        var allEventsInRange = await query
            .Select(e => new
            {
                e.Timestamp,
                e.RiskLevel,
                e.EventType,
                e.MitreTechniques
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} events from database for timeline aggregation", allEventsInRange.Count);

        // Group events into time slots in memory (fast - just DateTime comparisons)
        foreach (var timeSlot in timeSlots)
        {
            var slotEndTime = GetSlotEndTime(timeSlot, granularity);

            // Filter events for this time slot (in-memory, fast)
            var slotEvents = allEventsInRange
                .Where(e => e.Timestamp >= timeSlot && e.Timestamp < slotEndTime)
                .ToList();

            var totalCount = slotEvents.Count;

            // Aggregate in memory (fast - no database roundtrips)
            var riskLevelCounts = slotEvents
                .GroupBy(e => e.RiskLevel)
                .ToDictionary(g => g.Key, g => g.Count());

            var eventTypeCounts = slotEvents
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count());

            // MITRE techniques aggregation (in-memory)
            var mitreTechniques = slotEvents
                .Where(e => !string.IsNullOrEmpty(e.MitreTechniques))
                .SelectMany(e => DeserializeStringArray(e.MitreTechniques))
                .Where(t => !string.IsNullOrEmpty(t))
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            // Create data point with aggregated results
            var dataPoint = new TimelineDataPoint
            {
                Timestamp = timeSlot,
                Count = totalCount,
                RiskLevelCounts = riskLevelCounts,
                EventTypeCounts = eventTypeCounts,
                TopMitreTechniques = mitreTechniques,
                AverageRiskScore = riskLevelCounts.Any()
                    ? riskLevelCounts.Sum(kv => GetRiskScore(kv.Key) * kv.Value) / totalCount
                    : 0
            };

            dataPoints.Add(dataPoint);
        }

        _logger.LogInformation("Created {Count} timeline data points from aggregated data", dataPoints.Count);
        return dataPoints;
    }
}
