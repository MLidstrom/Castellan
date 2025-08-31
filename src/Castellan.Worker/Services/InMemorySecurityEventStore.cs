using System.Collections.Concurrent;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

public class InMemorySecurityEventStore : ISecurityEventStore
{
    private readonly ConcurrentQueue<SecurityEvent> _events = new();
    private readonly ILogger<InMemorySecurityEventStore> _logger;
    private int _idCounter = 1;
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24); // 24-hour rolling window only

    public InMemorySecurityEventStore(ILogger<InMemorySecurityEventStore> logger)
    {
        _logger = logger;
    }

    public void AddSecurityEvent(SecurityEvent securityEvent)
    {
        // Assign a unique ID if not already set
        if (string.IsNullOrEmpty(securityEvent.Id))
        {
            securityEvent.Id = Interlocked.Increment(ref _idCounter).ToString();
        }

        _events.Enqueue(securityEvent);
        
        // Cleanup old events: enforce both count limit and time-based retention
        CleanupOldEvents();

        _logger.LogDebug("Added security event {Id}: {EventType} ({RiskLevel})", 
            securityEvent.Id, securityEvent.EventType, securityEvent.RiskLevel);
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page = 1, int pageSize = 10)
    {
        var allEvents = _events.ToArray().Reverse(); // Most recent first
        var skip = (page - 1) * pageSize;
        return allEvents.Skip(skip).Take(pageSize);
    }

    public SecurityEvent? GetSecurityEvent(string id)
    {
        return _events.FirstOrDefault(e => e.Id == id);
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page, int pageSize, Dictionary<string, object> filters)
    {
        var allEvents = _events.ToArray().Reverse(); // Most recent first
        
        // Apply filters
        var filteredEvents = ApplyFilters(allEvents, filters);
        
        var skip = (page - 1) * pageSize;
        return filteredEvents.Skip(skip).Take(pageSize);
    }

    public int GetTotalCount()
    {
        return _events.Count;
    }

    public int GetTotalCount(Dictionary<string, object> filters)
    {
        if (filters == null || filters.Count == 0)
            return _events.Count;

        var allEvents = _events.ToArray();
        var filteredEvents = ApplyFilters(allEvents, filters);
        return filteredEvents.Count();
    }

    private IEnumerable<SecurityEvent> ApplyFilters(IEnumerable<SecurityEvent> events, Dictionary<string, object> filters)
    {
        if (filters == null || filters.Count == 0)
            return events;

        var filtered = events.AsQueryable();

        foreach (var filter in filters)
        {
            switch (filter.Key.ToLower())
            {
                case "risklevel":
                    var riskLevel = filter.Value.ToString()?.ToLower();
                    if (!string.IsNullOrEmpty(riskLevel))
                        filtered = filtered.Where(e => e.RiskLevel.ToLower() == riskLevel);
                    break;

                case "eventtype":
                    var eventType = filter.Value.ToString();
                    if (!string.IsNullOrEmpty(eventType))
                        filtered = filtered.Where(e => e.EventType.ToString().Contains(eventType, StringComparison.OrdinalIgnoreCase));
                    break;

                case "machine":
                    var machine = filter.Value.ToString();
                    if (!string.IsNullOrEmpty(machine))
                        filtered = filtered.Where(e => e.OriginalEvent.Host != null && e.OriginalEvent.Host.Contains(machine, StringComparison.OrdinalIgnoreCase));
                    break;

                case "user":
                    var user = filter.Value.ToString();
                    if (!string.IsNullOrEmpty(user))
                        filtered = filtered.Where(e => e.OriginalEvent.User != null && e.OriginalEvent.User.Contains(user, StringComparison.OrdinalIgnoreCase));
                    break;

                case "source":
                    var source = filter.Value.ToString();
                    if (!string.IsNullOrEmpty(source))
                        filtered = filtered.Where(e => e.OriginalEvent.Channel != null && e.OriginalEvent.Channel.Contains(source, StringComparison.OrdinalIgnoreCase));
                    break;
            }
        }

        return filtered.ToList();
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
        _logger.LogInformation("Cleared all security events from store");
    }

    private void CleanupOldEvents()
    {
        var cutoffTime = DateTimeOffset.UtcNow - RetentionPeriod;
        var removedByTime = 0;

        // Remove events older than 24-hour retention period
        var eventsArray = _events.ToArray();
        var eventsToKeep = new List<SecurityEvent>();

        foreach (var evt in eventsArray)
        {
            if (evt.OriginalEvent.Time > cutoffTime)
            {
                eventsToKeep.Add(evt);
            }
            else
            {
                removedByTime++;
            }
        }

        // Clear and rebuild queue with events from last 24 hours
        while (_events.TryDequeue(out _)) { }
        
        foreach (var evt in eventsToKeep.OrderBy(e => e.OriginalEvent.Time))
        {
            _events.Enqueue(evt);
        }

        if (removedByTime > 0)
        {
            _logger.LogDebug("Cleaned up {RemovedByTime} security events older than 24 hours", removedByTime);
        }
    }
}