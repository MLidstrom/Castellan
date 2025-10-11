using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

public class FileBasedSecurityEventStore : ISecurityEventStore, IDisposable
{
    private readonly ConcurrentQueue<SecurityEvent> _events = new();
    private readonly ILogger<FileBasedSecurityEventStore> _logger;
    private readonly string _dataFilePath;
    private readonly System.Threading.Timer _saveTimer;
    private int _idCounter = 1;
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24); // 24-hour rolling window only
    private volatile bool _isDirty = false;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileBasedSecurityEventStore(ILogger<FileBasedSecurityEventStore> logger)
    {
        _logger = logger;
        
        // Store data in AppData\Local\Castellan
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var castellanDataPath = Path.Combine(appDataPath, "Castellan");
        Directory.CreateDirectory(castellanDataPath);
        
        _dataFilePath = Path.Combine(castellanDataPath, "security_events.json");
        
        // Load existing data on startup
        LoadFromFile();
        
        // Auto-save every 30 seconds if data has changed
        _saveTimer = new System.Threading.Timer(AutoSave, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        _logger.LogInformation("FileBasedSecurityEventStore initialized with data file: {DataFilePath}", _dataFilePath);
    }

    public Task AddSecurityEventAsync(SecurityEvent securityEvent, CancellationToken cancellationToken = default)
    {
        // Assign a unique ID if not already set
        if (string.IsNullOrEmpty(securityEvent.Id))
        {
            securityEvent.Id = Interlocked.Increment(ref _idCounter).ToString();
        }

        _events.Enqueue(securityEvent);
        _isDirty = true;

        // Cleanup old events: enforce time-based retention
        CleanupOldEvents();

        _logger.LogDebug("Added security event {Id}: {EventType} ({RiskLevel})",
            securityEvent.Id, securityEvent.EventType, securityEvent.RiskLevel);

        return Task.CompletedTask;
    }

    public void AddSecurityEvent(SecurityEvent securityEvent)
    {
        // Delegate to async version for backward compatibility
        AddSecurityEventAsync(securityEvent).GetAwaiter().GetResult();
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

    public Dictionary<string, int> GetRiskLevelCounts()
    {
        return GetRiskLevelCounts(new Dictionary<string, object>());
    }

    public Dictionary<string, int> GetRiskLevelCounts(Dictionary<string, object> filters)
    {
        var events = _events.ToArray().AsEnumerable();

        if (filters != null && filters.Count > 0)
        {
            events = ApplyFilters(events, filters);
        }

        return events
            .GroupBy(e => e.RiskLevel.ToLower())
            .ToDictionary(g => g.Key, g => g.Count());
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
                // Existing single-value filters (backward compatibility)
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

                // New v0.4.0 Advanced Search filters
                case "startdate":
                    if (filter.Value is DateTime startDate)
                        filtered = filtered.Where(e => e.OriginalEvent.Time.DateTime >= startDate);
                    break;

                case "enddate":
                    if (filter.Value is DateTime endDate)
                        filtered = filtered.Where(e => e.OriginalEvent.Time.DateTime <= endDate);
                    break;

                case "eventtypes": // Multi-select event types
                    if (filter.Value is string[] eventTypes && eventTypes.Any())
                    {
                        filtered = filtered.Where(e => eventTypes.Any(et => 
                            e.EventType.ToString().Contains(et, StringComparison.OrdinalIgnoreCase)));
                    }
                    break;

                case "risklevels": // Multi-select risk levels
                    if (filter.Value is string[] riskLevels && riskLevels.Any())
                    {
                        filtered = filtered.Where(e => riskLevels.Any(rl => 
                            e.RiskLevel.Equals(rl, StringComparison.OrdinalIgnoreCase)));
                    }
                    break;

                case "search": // Full-text search
                    var searchTerm = filter.Value.ToString();
                    if (!string.IsNullOrEmpty(searchTerm))
                    {
                        filtered = filtered.Where(e => 
                            (e.Summary != null && e.Summary.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                            (e.OriginalEvent.Message != null && e.OriginalEvent.Message.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                            e.EventType.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            (e.OriginalEvent.User != null && e.OriginalEvent.User.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                            (e.OriginalEvent.Host != null && e.OriginalEvent.Host.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
                    }
                    break;

                case "minconfidence":
                    if (filter.Value is float minConfidence)
                        filtered = filtered.Where(e => e.Confidence >= minConfidence);
                    break;

                case "maxconfidence":
                    if (filter.Value is float maxConfidence)
                        filtered = filtered.Where(e => e.Confidence <= maxConfidence);
                    break;

                case "mincorrelationscore":
                    if (filter.Value is float minCorrelationScore)
                        filtered = filtered.Where(e => e.CorrelationScore >= minCorrelationScore);
                    break;

                case "maxcorrelationscore":
                    if (filter.Value is float maxCorrelationScore)
                        filtered = filtered.Where(e => e.CorrelationScore <= maxCorrelationScore);
                    break;

                case "status":
                    var status = filter.Value.ToString();
                    if (!string.IsNullOrEmpty(status))
                    {
                        // Note: Current SecurityEvent model doesn't have a Status field
                        // This would need to be added to the model or mapped to another field
                        // For now, we'll skip this filter
                    }
                    break;

                case "mitretechnique":
                    var mitreTechnique = filter.Value.ToString();
                    if (!string.IsNullOrEmpty(mitreTechnique))
                    {
                        filtered = filtered.Where(e => e.MitreTechniques != null && 
                            e.MitreTechniques.Any(technique => technique.Contains(mitreTechnique, StringComparison.OrdinalIgnoreCase)));
                    }
                    break;
            }
        }

        return filtered.ToList();
    }

    public void Clear()
    {
        while (_events.TryDequeue(out _)) { }
        _isDirty = true;
        _logger.LogInformation("Cleared all security events from store");
        
        // Save immediately when cleared
        _ = Task.Run(SaveToFile);
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

        if (removedByTime > 0)
        {
            // Clear and rebuild queue with events from last 24 hours
            while (_events.TryDequeue(out _)) { }
            
            foreach (var evt in eventsToKeep.OrderBy(e => e.OriginalEvent.Time))
            {
                _events.Enqueue(evt);
            }
            
            _isDirty = true;
            _logger.LogDebug("Cleaned up {RemovedByTime} security events older than 24 hours", removedByTime);
        }
    }

    private void LoadFromFile()
    {
        try
        {
            if (!File.Exists(_dataFilePath))
            {
                _logger.LogInformation("No existing security events file found. Starting with empty store.");
                return;
            }

            var json = File.ReadAllText(_dataFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogInformation("Security events file is empty. Starting with empty store.");
                return;
            }

            var data = JsonSerializer.Deserialize<SecurityEventFileData>(json, JsonOptions);
            if (data?.Events != null)
            {
                var loadedCount = 0;
                var cutoffTime = DateTimeOffset.UtcNow - RetentionPeriod;
                
                // Load events that are still within the retention period
                foreach (var evt in data.Events)
                {
                    if (evt.OriginalEvent.Time > cutoffTime)
                    {
                        _events.Enqueue(evt);
                        loadedCount++;
                        
                        // Update ID counter to prevent conflicts
                        if (int.TryParse(evt.Id, out var eventId) && eventId >= _idCounter)
                        {
                            _idCounter = eventId + 1;
                        }
                    }
                }

                _logger.LogInformation("Loaded {LoadedCount} security events from persistent storage (excluded {ExcludedCount} expired events)", 
                    loadedCount, data.Events.Count - loadedCount);
                    
                // If we excluded any events, mark as dirty to save the cleaned version
                if (data.Events.Count != loadedCount)
                {
                    _isDirty = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading security events from file: {FilePath}", _dataFilePath);
        }
    }

    private async void AutoSave(object? state)
    {
        if (_isDirty)
        {
            await SaveToFile();
        }
    }

    private async Task SaveToFile()
    {
        await _saveLock.WaitAsync();
        try
        {
            if (!_isDirty) return;

            var eventsArray = _events.ToArray();
            var data = new SecurityEventFileData
            {
                SavedAt = DateTimeOffset.UtcNow,
                EventCount = eventsArray.Length,
                Events = eventsArray.ToList()
            };

            var json = JsonSerializer.Serialize(data, JsonOptions);
            
            // Write to temp file first, then rename for atomic operation
            var tempFilePath = _dataFilePath + ".tmp";
            await File.WriteAllTextAsync(tempFilePath, json);
            
            if (File.Exists(_dataFilePath))
            {
                File.Delete(_dataFilePath);
            }
            File.Move(tempFilePath, _dataFilePath);
            
            _isDirty = false;
            _logger.LogDebug("Saved {EventCount} security events to persistent storage", eventsArray.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving security events to file: {FilePath}", _dataFilePath);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public void Dispose()
    {
        _saveTimer?.Dispose();
        
        // Save final state on disposal
        if (_isDirty)
        {
            try
            {
                SaveToFile().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving security events during disposal");
            }
        }
        
        _saveLock?.Dispose();
        GC.SuppressFinalize(this);
    }

    private class SecurityEventFileData
    {
        public DateTimeOffset SavedAt { get; set; }
        public int EventCount { get; set; }
        public List<SecurityEvent> Events { get; set; } = new();
    }
}
