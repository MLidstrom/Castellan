using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Options;
using Castellan.Worker.Hubs;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service that orchestrates Windows Event Log watching across multiple channels
/// </summary>
public class WindowsEventLogWatcherService : BackgroundService
{
    private readonly WindowsEventLogOptions _options;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IScanProgressBroadcaster _broadcaster;
    private readonly DashboardDataBroadcastService _dashboardBroadcast;
    private readonly ILogger<WindowsEventLogWatcherService> _logger;
    private readonly IServiceProvider _serviceProvider;

    private readonly Channel<RawEvent> _eventQueue;
    private readonly List<WindowsEventChannelWatcher> _watchers = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public WindowsEventLogWatcherService(
        IOptions<WindowsEventLogOptions> options,
        IServiceScopeFactory serviceScopeFactory,
        IScanProgressBroadcaster broadcaster,
        DashboardDataBroadcastService dashboardBroadcast,
        ILogger<WindowsEventLogWatcherService> logger,
        IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _broadcaster = broadcaster;
        _dashboardBroadcast = dashboardBroadcast;
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // Create bounded channel for event processing
        var channelOptions = new BoundedChannelOptions(_options.DefaultMaxQueue)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        };
        _eventQueue = Channel.CreateBounded<RawEvent>(channelOptions);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Windows Event Log watching is disabled");
            return;
        }

        _logger.LogInformation("Starting Windows Event Log Watcher Service with {ChannelCount} channels and {ConsumerCount} consumers",
            _options.Channels.Count(c => c.Enabled), _options.ConsumerConcurrency);

        try
        {
            // Start all enabled channel watchers
            await StartWatchersAsync(stoppingToken);

            // Start event processing consumers
            var consumerTasks = StartConsumersAsync(stoppingToken);

            // Wait for all tasks to complete
            await Task.WhenAll(consumerTasks);

            _logger.LogInformation("Windows Event Log Watcher Service stopped");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Windows Event Log Watcher Service was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Windows Event Log Watcher Service encountered an error");
            throw;
        }
        finally
        {
            // Clean up watchers
            await StopWatchersAsync();
        }
    }

    /// <summary>
    /// Start all enabled channel watchers
    /// </summary>
    private async Task StartWatchersAsync(CancellationToken cancellationToken)
    {
        var enabledChannels = _options.Channels.Where(c => c.Enabled).ToList();
        
        if (!enabledChannels.Any())
        {
            _logger.LogWarning("No Windows Event Log channels are enabled");
            return;
        }

        foreach (var channelConfig in enabledChannels)
        {
            try
            {
                _logger.LogInformation("Starting watcher for channel: {ChannelName} with filter: {XPathFilter}",
                    channelConfig.Name, channelConfig.XPathFilter);

                using var scope = _serviceScopeFactory.CreateScope();
                var bookmarkStore = scope.ServiceProvider.GetRequiredService<IEventBookmarkStore>();
                var watcher = new WindowsEventChannelWatcher(
                    channelConfig,
                    bookmarkStore,
                    _eventQueue.Writer,
                    _serviceProvider.GetRequiredService<ILogger<WindowsEventChannelWatcher>>());

                await watcher.StartAsync();
                _watchers.Add(watcher);

                _logger.LogInformation("Successfully started watcher for channel: {ChannelName}", channelConfig.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start watcher for channel: {ChannelName}", channelConfig.Name);
                
                // Continue with other channels even if one fails
                if (channelConfig.Name.Equals("Security", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Security channel watcher failed - this may indicate insufficient privileges");
                    _logger.LogWarning("Ensure the service account has 'Log on as a service' and 'Generate security audits' privileges");
                }
            }
        }

        _logger.LogInformation("Started {ActiveWatchers} out of {TotalChannels} configured channel watchers",
            _watchers.Count, enabledChannels.Count);
    }

    /// <summary>
    /// Start event processing consumers
    /// </summary>
    private List<Task> StartConsumersAsync(CancellationToken cancellationToken)
    {
        var consumerTasks = new List<Task>();

        for (int i = 0; i < _options.ConsumerConcurrency; i++)
        {
            var consumerId = i + 1;
            var task = Task.Run(async () => await ProcessEventsAsync(consumerId, cancellationToken), cancellationToken);
            consumerTasks.Add(task);
        }

        _logger.LogInformation("Started {ConsumerCount} event processing consumers", _options.ConsumerConcurrency);
        return consumerTasks;
    }

    /// <summary>
    /// Process events from the queue
    /// </summary>
    private async Task ProcessEventsAsync(int consumerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Event processing consumer {ConsumerId} started", consumerId);

        try
        {
            await foreach (var rawEvent in _eventQueue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessEventAsync(rawEvent, consumerId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event {EventId} from channel {Channel} in consumer {ConsumerId}",
                        rawEvent.EventId, rawEvent.ChannelName, consumerId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Event processing consumer {ConsumerId} was cancelled", consumerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event processing consumer {ConsumerId} encountered an error", consumerId);
        }
        finally
        {
            _logger.LogDebug("Event processing consumer {ConsumerId} stopped", consumerId);
        }
    }

    /// <summary>
    /// Process a single event
    /// </summary>
    private async Task ProcessEventAsync(RawEvent rawEvent, int consumerId, CancellationToken cancellationToken)
    {
        // Normalize the raw event to SecurityEvent
        var securityEvent = EventNormalizationHandler.Normalize(rawEvent, _logger);

        // Store in security event store (single source of truth for memory + database)
        // This automatically triggers SignalR broadcasting via SignalRSecurityEventStore
        using var scope = _serviceScopeFactory.CreateScope();
        var securityEventStore = scope.ServiceProvider.GetRequiredService<ISecurityEventStore>();
        securityEventStore.AddSecurityEvent(securityEvent);

        // Trigger immediate dashboard broadcast if enabled
        if (_options.ImmediateDashboardBroadcast)
        {
            await _dashboardBroadcast.TriggerImmediateBroadcastWithCacheInvalidation();
        }

        _logger.LogDebug("Processed event {EventId} from channel {Channel} in consumer {ConsumerId} - Type: {EventType}, Risk: {RiskLevel}",
            rawEvent.EventId, rawEvent.ChannelName, consumerId, securityEvent.EventType, securityEvent.RiskLevel);
    }

    /// <summary>
    /// Stop all channel watchers
    /// </summary>
    private async Task StopWatchersAsync()
    {
        _logger.LogInformation("Stopping {WatcherCount} channel watchers", _watchers.Count);

        var stopTasks = _watchers.Select(async watcher =>
        {
            try
            {
                await watcher.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping watcher");
            }
        });

        await Task.WhenAll(stopTasks);
        _watchers.Clear();

        // Complete the channel writer to signal no more events
        _eventQueue.Writer.Complete();

        _logger.LogInformation("All channel watchers stopped");
    }

    /// <summary>
    /// Get current status of all watchers
    /// </summary>
    public Dictionary<string, string> GetWatcherStatus()
    {
        var status = new Dictionary<string, string>();
        
        foreach (var watcher in _watchers)
        {
            // Note: This would require exposing a GetStatus method from WindowsEventChannelWatcher
            status["Watcher"] = "Active";
        }

        return status;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Windows Event Log Watcher Service");
        
        _cancellationTokenSource.Cancel();
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("Windows Event Log Watcher Service stopped");
    }

    public override void Dispose()
    {
        _cancellationTokenSource.Dispose();
        _eventQueue.Writer.Complete();
        base.Dispose();
    }
}
