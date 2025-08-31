using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Collectors;

public sealed class EvtxCollector(IOptions<EvtxOptions> opts, ILogger<EvtxCollector> log) : ILogCollector
{
    private static string[] InitChannels(IOptions<EvtxOptions> opts, ILogger<EvtxCollector> log)
    {
        if (opts?.Value == null)
        {
            log.LogError("EVTX collector configuration is null");
            throw new InvalidOperationException("EVTX collector configuration is null");
        }
        
        var configured = opts.Value.Channels ?? Array.Empty<string>();
        var distinctConfigured = configured.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        
        log.LogInformation("EVTX collector configuration - Channels: {Channels}, XPath: {XPath}, PollSeconds: {PollSeconds}", 
            string.Join(", ", distinctConfigured), opts.Value.XPath, opts.Value.PollSeconds);

        var accessible = new List<string>(distinctConfigured.Length);
        foreach (var channel in distinctConfigured)
        {
            try
            {
                var q = new EventLogQuery(channel, PathType.LogName, opts.Value.XPath);
                using var r = new EventLogReader(q);
                // Try to read a single event; null is fine (empty log), exceptions mean no access
                using var _ = r.ReadEvent();
                accessible.Add(channel);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "EVTX channel {Channel} not accessible; it will be skipped.", channel);
            }
        }
        if (accessible.Count == 0)
        {
            log.LogWarning("No accessible EVTX channels from configured set: {Configured}", string.Join(", ", distinctConfigured));
        }
        else if (accessible.Count < distinctConfigured.Length)
        {
            var skipped = distinctConfigured.Except(accessible, StringComparer.OrdinalIgnoreCase);
            log.LogInformation("EVTX collector will use channels: {Use}; skipped: {Skip}", string.Join(", ", accessible), string.Join(", ", skipped));
        }
        else
        {
            log.LogInformation("EVTX collector will use channels: {Use}", string.Join(", ", accessible));
        }
        return accessible.ToArray();
    }

    private readonly string[] _channels = InitChannels(opts, log);
    private readonly HashSet<string> _processedEvents = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// Collects historical events from the configured time window for backfill purposes
    /// </summary>
    public async IAsyncEnumerable<LogEvent> CollectHistoricalAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var endTime = DateTimeOffset.UtcNow;
        var timeWindowMs = ExtractTimeWindowFromXPath(opts.Value.XPath);
        var startTime = endTime.AddMilliseconds(-timeWindowMs);
        
        log.LogInformation("EVTX collector starting historical backfill from {StartTime} to {EndTime} (window: {WindowMs}ms)", startTime, endTime, timeWindowMs);
        
        foreach (var channel in _channels)
        {
            await foreach (var logEvent in CollectHistoricalFromChannelAsync(channel, startTime, endTime, ct))
            {
                yield return logEvent;
            }
        }
        
        log.LogInformation("EVTX collector completed historical backfill");
    }

    /// <summary>
    /// Extracts the time window in milliseconds from the XPath configuration
    /// </summary>
    private static double ExtractTimeWindowFromXPath(string xpath)
    {
        try
        {
            // Extract time value from XPath like "*[System[TimeCreated[timediff(@SystemTime) <= 30000]]]"
            var match = System.Text.RegularExpressions.Regex.Match(xpath, @"timediff\(@SystemTime\)\s*<=\s*(\d+)");
            if (match.Success && double.TryParse(match.Groups[1].Value, out var timeMs))
            {
                return timeMs;
            }
        }
        catch
        {
            // If parsing fails, fall back to default
        }

        // Default to 10 minutes (600000ms) if parsing fails
        return 600000;
    }

    private async IAsyncEnumerable<LogEvent> CollectHistoricalFromChannelAsync(string channel, DateTimeOffset startTime, DateTimeOffset endTime, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var events = new List<LogEvent>();
        
        try
        {
            // Create XPath query for the configured time window
            var historicalXPath = $"*[System[TimeCreated[@SystemTime >= '{startTime:yyyy-MM-ddTHH:mm:ss.fffZ}' and @SystemTime <= '{endTime:yyyy-MM-ddTHH:mm:ss.fffZ}']]]";
            
            log.LogDebug("EVTX collector querying historical events from channel {Channel} with XPath: {XPath}", channel, historicalXPath);
            
            var q = new EventLogQuery(channel, PathType.LogName, historicalXPath);
            using var r = new EventLogReader(q);
            
            int eventCount = 0;
            for (EventRecord? rec = r.ReadEvent(); rec != null; rec = r.ReadEvent())
            {
                using (rec)
                {
                    if (ct.IsCancellationRequested) break;
                    
                    string level = string.Empty;
                    string message = string.Empty;
                    try { level = rec.LevelDisplayName ?? string.Empty; } catch { /* ignore */ }
                    try { message = rec.FormatDescription() ?? string.Empty; } catch { /* ignore */ }

                    // Create unique event identifier for deduplication
                    var eventId = $"{channel}_{rec.Id}_{rec.TimeCreated:yyyyMMddHHmmss}_{rec.MachineName}";
                    
                    // For historical backfill, we don't check against processed events since these are old
                    var logEvent = new LogEvent(
                        rec.TimeCreated ?? DateTimeOffset.UtcNow,
                        rec.MachineName ?? Environment.MachineName,
                        rec.LogName ?? channel,
                        rec.Id,
                        level,
                        rec.UserId?.Value ?? string.Empty,
                        message,
                        "", // RawJson
                        eventId // UniqueId for vector store deduplication
                    );
                    
                    events.Add(logEvent);
                    eventCount++;
                    
                    log.LogDebug("EVTX collector found HISTORICAL event {EventId} from {Channel} at {Time}", 
                        rec.Id, channel, rec.TimeCreated);
                }
            }
            
            log.LogInformation("EVTX collector found {EventCount} historical events from channel {Channel}", eventCount, channel);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "EVTX collector historical backfill error for channel {Channel}", channel);
        }

        // Yield all collected events
        foreach (var logEvent in events)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return logEvent;
        }
    }

    public async IAsyncEnumerable<LogEvent> CollectAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var channel in _channels)
            {
                var batch = new List<LogEvent>(128);
                try
                {
                    var xpath = opts.Value.XPath;
                    log.LogDebug("EVTX collector querying channel {Channel} with XPath: {XPath}", channel, xpath);
                    
                    var q = new EventLogQuery(channel, PathType.LogName, xpath);
                    using var r = new EventLogReader(q);
                    
                    int eventCount = 0;
                    for (EventRecord? rec = r.ReadEvent(); rec != null; rec = r.ReadEvent())
                    {
                        using (rec)
                        {
                            string level = string.Empty;
                            string message = string.Empty;
                            try { level = rec.LevelDisplayName ?? string.Empty; } catch { /* ignore */ }
                            try { message = rec.FormatDescription() ?? string.Empty; } catch { /* ignore */ }

                            // Create unique event identifier for deduplication
                            var eventId = $"{channel}_{rec.Id}_{rec.TimeCreated:yyyyMMddHHmmss}_{rec.MachineName}";
                            
                            // Check if event has already been processed
                            bool isNewEvent;
                            lock (_lockObject)
                            {
                                isNewEvent = _processedEvents.Add(eventId);
                            }
                            
                            if (isNewEvent)
                            {
                                var logEvent = new LogEvent(
                                    rec.TimeCreated ?? DateTimeOffset.UtcNow,
                                    rec.MachineName ?? Environment.MachineName,
                                    rec.LogName ?? channel,
                                    rec.Id,
                                    level,
                                    rec.UserId?.Value ?? string.Empty,
                                    message,
                                    "", // RawJson
                                    eventId // UniqueId for vector store deduplication
                                );
                                
                                batch.Add(logEvent);
                                eventCount++;
                                
                                log.LogDebug("EVTX collector found NEW event {EventId} from {Channel} at {Time}", 
                                    rec.Id, channel, rec.TimeCreated);
                            }
                            else
                            {
                                log.LogDebug("EVTX collector skipped duplicate event {EventId} from {Channel} at {Time}", 
                                    rec.Id, channel, rec.TimeCreated);
                            }
                        }
                    }
                    
                    log.LogInformation("EVTX collector found {EventCount} events from channel {Channel}", eventCount, channel);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "EVTX collector error for channel {Channel}", channel);
                }
                foreach (var evt in batch)
                    yield return evt;
            }
            
            // Cleanup old processed events (keep last 10000 events)
            lock (_lockObject)
            {
                if (_processedEvents.Count > 10000)
                {
                    var eventsToRemove = _processedEvents.Take(_processedEvents.Count - 10000).ToArray();
                    foreach (var eventToRemove in eventsToRemove)
                    {
                        _processedEvents.Remove(eventToRemove);
                    }
                    log.LogDebug("EVTX collector cleaned up {Count} old processed events", eventsToRemove.Length);
                }
            }
            
            try { await Task.Delay(TimeSpan.FromSeconds(opts.Value.PollSeconds), ct); }
            catch (OperationCanceledException) { yield break; }
        }
    }
}

public sealed class EvtxOptions
{
    public string[] Channels { get; set; } = new[] { "Security", "System", "Application" };
    public string XPath { get; set; } = "*[System[TimeCreated[timediff(@SystemTime) <= 600000]]]";
    public int PollSeconds { get; set; } = 30;
}

