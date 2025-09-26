using System.Diagnostics.Eventing.Reader;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Options;

namespace Castellan.Worker.Services;

/// <summary>
/// Raw event data structure for passing between watcher and processor
/// </summary>
public class RawEvent
{
    public string Id { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public byte Level { get; set; }
    public DateTime TimeCreated { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public byte Opcode { get; set; }
    public ushort Task { get; set; }
    public long Keywords { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Xml { get; set; } = string.Empty;

    /// <summary>
    /// Create a RawEvent from an EventRecord
    /// </summary>
    public static RawEvent FromEventRecord(EventRecord eventRecord)
    {
        return new RawEvent
        {
            Id = eventRecord.Id.ToString(),
            EventId = eventRecord.Id,
            ProviderName = eventRecord.ProviderName ?? string.Empty,
            ChannelName = eventRecord.LogName ?? string.Empty,
            Level = eventRecord.Level ?? 0,
            TimeCreated = eventRecord.TimeCreated ?? DateTime.UtcNow,
            MachineName = eventRecord.MachineName ?? string.Empty,
            UserName = eventRecord.UserId?.ToString() ?? string.Empty,
            Opcode = (byte)(eventRecord.Opcode ?? 0),
            Task = (ushort)(eventRecord.Task ?? 0),
            Keywords = eventRecord.Keywords ?? 0,
            Message = eventRecord.FormatDescription() ?? string.Empty,
            Xml = eventRecord.ToXml()
        };
    }
}

/// <summary>
/// Manages EventLogWatcher for a specific Windows Event Log channel
/// </summary>
public class WindowsEventChannelWatcher : IDisposable
{
    private readonly WindowsEventChannelOptions _options;
    private readonly IEventBookmarkStore _bookmarkStore;
    private readonly ChannelWriter<RawEvent> _queueWriter;
    private readonly ILogger<WindowsEventChannelWatcher> _logger;
    
    private EventLogWatcher? _watcher;
    private EventBookmark? _bookmark;
    private bool _disposed = false;
    private int _eventsProcessed = 0;
    private DateTime _lastBookmarkSave = DateTime.UtcNow;
    private readonly TimeSpan _bookmarkSaveInterval = TimeSpan.FromSeconds(30);

    public WindowsEventChannelWatcher(
        WindowsEventChannelOptions options,
        IEventBookmarkStore bookmarkStore,
        ChannelWriter<RawEvent> queueWriter,
        ILogger<WindowsEventChannelWatcher> logger)
    {
        _options = options;
        _bookmarkStore = bookmarkStore;
        _queueWriter = queueWriter;
        _logger = logger;
    }

    /// <summary>
    /// Start watching the event log channel
    /// </summary>
    public async Task StartAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsEventChannelWatcher));

        try
        {
            // Load existing bookmark if available
            _bookmark = await _bookmarkStore.LoadAsync(_options.Name);
            if (_bookmark != null)
            {
                _logger.LogInformation("Resuming from bookmark for channel: {ChannelName}", _options.Name);
            }
            else
            {
                _logger.LogInformation("Starting fresh watch for channel: {ChannelName}", _options.Name);
            }

            // Create event log query with XPath filter
            var query = new EventLogQuery(_options.Name, PathType.LogName, _options.XPathFilter)
            {
                TolerateQueryErrors = true,
                ReverseDirection = false
            };

            // Create watcher with or without bookmark
            _watcher = _bookmark != null 
                ? new EventLogWatcher(query, _bookmark)
                : new EventLogWatcher(query);

            // Set up event handlers
            _watcher.EventRecordWritten += OnEventRecordWritten;
            _watcher.Enabled = true;

            _logger.LogInformation("Started watching channel: {ChannelName} with filter: {XPathFilter}", 
                _options.Name, _options.XPathFilter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watching channel: {ChannelName}", _options.Name);
            throw;
        }
    }

    /// <summary>
    /// Stop watching the event log channel
    /// </summary>
    public async Task StopAsync()
    {
        if (_watcher != null)
        {
            try
            {
                _watcher.EventRecordWritten -= OnEventRecordWritten;
                _watcher.Enabled = false;
                _watcher.Dispose();
                _watcher = null;

                // Save final bookmark if we have one
                if (_bookmark != null)
                {
                    await _bookmarkStore.SaveAsync(_options.Name, _bookmark);
                    _logger.LogDebug("Saved final bookmark for channel: {ChannelName}", _options.Name);
                }

                _logger.LogInformation("Stopped watching channel: {ChannelName}. Events processed: {EventCount}", 
                    _options.Name, _eventsProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping watcher for channel: {ChannelName}", _options.Name);
            }
        }
    }

    /// <summary>
    /// Handle new event records from the watcher
    /// </summary>
    private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null)
            return;

        try
        {
            // Convert to raw event
            var rawEvent = RawEvent.FromEventRecord(e.EventRecord);
            
            // Try to write to queue (non-blocking)
            if (_queueWriter.TryWrite(rawEvent))
            {
                Interlocked.Increment(ref _eventsProcessed);
                
                // Update bookmark
                _bookmark = e.EventRecord.Bookmark;
                
                // Periodically save bookmark
                if (DateTime.UtcNow - _lastBookmarkSave > _bookmarkSaveInterval)
                {
                    _ = Task.Run(async () => await SaveBookmarkAsync());
                }
            }
            else
            {
                _logger.LogWarning("Failed to write event to queue for channel: {ChannelName} - queue may be full", 
                    _options.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event from channel: {ChannelName}", _options.Name);
        }
    }

    /// <summary>
    /// Save the current bookmark asynchronously
    /// </summary>
    private async Task SaveBookmarkAsync()
    {
        if (_bookmark != null)
        {
            try
            {
                await _bookmarkStore.SaveAsync(_options.Name, _bookmark);
                _lastBookmarkSave = DateTime.UtcNow;
                _logger.LogDebug("Saved bookmark for channel: {ChannelName}", _options.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save bookmark for channel: {ChannelName}", _options.Name);
            }
        }
    }

    /// <summary>
    /// Get current status of the watcher
    /// </summary>
    public string GetStatus()
    {
        if (_disposed)
            return "Disposed";
        
        if (_watcher == null)
            return "Not Started";
        
        if (_watcher.Enabled)
            return $"Active (Events: {_eventsProcessed})";
        
        return "Disabled";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _ = Task.Run(async () => await StopAsync());
            _disposed = true;
        }
    }
}
