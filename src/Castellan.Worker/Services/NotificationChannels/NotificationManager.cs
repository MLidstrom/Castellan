using System.Collections.Concurrent;
using Castellan.Worker.Models;
using Castellan.Worker.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services.NotificationChannels;

/// <summary>
/// Manages notification delivery across multiple channels with throttling
/// </summary>
public interface INotificationManager
{
    Task SendSecurityAlertAsync(SecurityEvent securityEvent);
    Task SendCorrelationAlertAsync(SecurityEvent securityEvent, EventCorrelation correlation);
    Task SendAttackChainAlertAsync(List<SecurityEvent> events, AttackChain attackChain);
    Task<bool> TestChannelAsync(NotificationChannelType channelType);
    Task<Dictionary<NotificationChannelType, ChannelHealthStatus>> GetHealthStatusAsync();
    void RegisterChannel(INotificationChannel channel);
}

public class NotificationManager : INotificationManager
{
    private readonly ILogger<NotificationManager> _logger;
    private readonly IMemoryCache _cache;
    private readonly List<INotificationChannel> _channels;
    private readonly ConcurrentDictionary<string, DateTime> _throttleCache;

    public NotificationManager(
        ILogger<NotificationManager> logger,
        IMemoryCache cache,
        IEnumerable<INotificationChannel> channels)
    {
        _logger = logger;
        _cache = cache;
        _channels = channels.ToList();
        _throttleCache = new ConcurrentDictionary<string, DateTime>();

        _logger.LogInformation("NotificationManager initialized with {ChannelCount} channels", _channels.Count);
    }

    public void RegisterChannel(INotificationChannel channel)
    {
        if (!_channels.Any(c => c.Type == channel.Type))
        {
            _channels.Add(channel);
            _logger.LogInformation("Registered notification channel: {ChannelType}", channel.Type);
        }
    }

    public async Task SendSecurityAlertAsync(SecurityEvent securityEvent)
    {
        var enabledChannels = _channels.Where(c => c.IsEnabled).ToList();
        
        if (!enabledChannels.Any())
        {
            _logger.LogWarning("No enabled notification channels found");
            return;
        }

        var tasks = new List<Task>();

        foreach (var channel in enabledChannels)
        {
            if (ShouldSendNotification(channel, securityEvent))
            {
                tasks.Add(SendToChannelWithRetryAsync(channel, securityEvent));
            }
            else
            {
                _logger.LogDebug("Notification throttled for channel {Channel} and severity {Severity}", 
                    channel.Type, securityEvent.RiskLevel);
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task SendCorrelationAlertAsync(SecurityEvent securityEvent, EventCorrelation correlation)
    {
        var enabledChannels = _channels.Where(c => c.IsEnabled).ToList();

        if (!enabledChannels.Any())
        {
            _logger.LogWarning("No enabled notification channels found for correlation alert");
            return;
        }

        var tasks = new List<Task>();

        foreach (var channel in enabledChannels)
        {
            // Use a more selective throttling key for correlations
            var throttleKey = $"{channel.Type}:correlation:{correlation.CorrelationType}:{securityEvent.RiskLevel}";

            if (ShouldSendCorrelationNotification(channel, correlation, throttleKey))
            {
                tasks.Add(SendCorrelationToChannelWithRetryAsync(channel, securityEvent, correlation));
            }
            else
            {
                _logger.LogDebug("Correlation notification throttled for channel {Channel} and type {CorrelationType}",
                    channel.Type, correlation.CorrelationType);
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task SendAttackChainAlertAsync(List<SecurityEvent> events, AttackChain attackChain)
    {
        var enabledChannels = _channels.Where(c => c.IsEnabled).ToList();

        if (!enabledChannels.Any())
        {
            _logger.LogWarning("No enabled notification channels found for attack chain alert");
            return;
        }

        var tasks = new List<Task>();

        foreach (var channel in enabledChannels)
        {
            // Attack chains bypass normal throttling due to criticality
            var throttleKey = $"{channel.Type}:attackchain:{attackChain.AttackType}";

            if (ShouldSendAttackChainNotification(channel, attackChain, throttleKey))
            {
                tasks.Add(SendAttackChainToChannelWithRetryAsync(channel, events, attackChain));
            }
        }

        await Task.WhenAll(tasks);
    }

    public async Task<bool> TestChannelAsync(NotificationChannelType channelType)
    {
        var channel = _channels.FirstOrDefault(c => c.Type == channelType);
        
        if (channel == null)
        {
            _logger.LogWarning("Channel {ChannelType} not found", channelType);
            return false;
        }

        return await channel.TestConnectionAsync();
    }

    public async Task<Dictionary<NotificationChannelType, ChannelHealthStatus>> GetHealthStatusAsync()
    {
        var healthStatuses = new Dictionary<NotificationChannelType, ChannelHealthStatus>();

        foreach (var channel in _channels)
        {
            healthStatuses[channel.Type] = await channel.GetHealthStatusAsync();
        }

        return healthStatuses;
    }

    private bool ShouldSendNotification(INotificationChannel channel, SecurityEvent securityEvent)
    {
        // Check rate limiting based on severity
        var throttleKey = $"{channel.Type}:{securityEvent.RiskLevel}";
        
        if (_throttleCache.TryGetValue(throttleKey, out var lastSent))
        {
            var throttlePeriod = GetThrottlePeriod(securityEvent.RiskLevel);
            
            if (DateTime.UtcNow - lastSent < throttlePeriod)
            {
                return false; // Throttled
            }
        }

        // Update last sent time
        _throttleCache.AddOrUpdate(throttleKey, DateTime.UtcNow, (k, v) => DateTime.UtcNow);
        
        // Check global rate limiting
        var rateLimitKey = $"ratelimit:{channel.Type}";
        var count = _cache.Get<int>(rateLimitKey);
        
        if (count >= 10) // Max 10 notifications per 5 minutes per channel
        {
            _logger.LogWarning("Rate limit exceeded for channel {Channel}", channel.Type);
            return false;
        }

        // Increment count with 5-minute expiration
        _cache.Set(rateLimitKey, count + 1, TimeSpan.FromMinutes(5));
        
        return true;
    }

    private TimeSpan GetThrottlePeriod(string riskLevel)
    {
        return riskLevel.ToLowerInvariant() switch
        {
            "critical" => TimeSpan.Zero,        // No throttling for critical
            "high" => TimeSpan.FromMinutes(5),
            "medium" => TimeSpan.FromMinutes(15),
            "low" => TimeSpan.FromHours(1),
            _ => TimeSpan.FromMinutes(30)
        };
    }

    private async Task SendToChannelWithRetryAsync(INotificationChannel channel, SecurityEvent securityEvent)
    {
        const int maxRetries = 3;
        var retryCount = 0;
        var backoffMs = 1000;

        while (retryCount < maxRetries)
        {
            try
            {
                var success = await channel.SendAsync(securityEvent);
                
                if (success)
                {
                    _logger.LogInformation("Successfully sent notification via {Channel} for event {EventId}", 
                        channel.Type, securityEvent.Id);
                    return;
                }

                _logger.LogWarning("Failed to send notification via {Channel} for event {EventId} (attempt {Attempt})", 
                    channel.Type, securityEvent.Id, retryCount + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification via {Channel} for event {EventId}", 
                    channel.Type, securityEvent.Id);
            }

            retryCount++;
            
            if (retryCount < maxRetries)
            {
                await Task.Delay(backoffMs);
                backoffMs *= 2; // Exponential backoff
            }
        }

        _logger.LogError("Failed to send notification via {Channel} after {MaxRetries} attempts",
            channel.Type, maxRetries);
    }

    private bool ShouldSendCorrelationNotification(INotificationChannel channel, EventCorrelation correlation, string throttleKey)
    {
        // Check throttling
        if (_throttleCache.ContainsKey(throttleKey))
        {
            var lastSent = _throttleCache[throttleKey];
            var throttleWindow = GetCorrelationThrottleWindow(correlation);

            if (DateTime.UtcNow - lastSent < throttleWindow)
            {
                return false;
            }
        }

        // Update throttle cache
        _throttleCache[throttleKey] = DateTime.UtcNow;

        return true; // Send all correlation notifications
    }

    private bool ShouldSendAttackChainNotification(INotificationChannel channel, AttackChain attackChain, string throttleKey)
    {
        // Attack chains have minimal throttling due to severity
        if (_throttleCache.ContainsKey(throttleKey))
        {
            var lastSent = _throttleCache[throttleKey];
            var throttleWindow = TimeSpan.FromMinutes(5); // Short throttle for attack chains

            if (DateTime.UtcNow - lastSent < throttleWindow)
            {
                return false;
            }
        }

        // Update throttle cache
        _throttleCache[throttleKey] = DateTime.UtcNow;

        return true; // Always send attack chain notifications
    }

    private TimeSpan GetCorrelationThrottleWindow(EventCorrelation correlation)
    {
        return correlation.CorrelationType.ToLower() switch
        {
            "attackchain" => TimeSpan.FromMinutes(10),
            "lateralmovement" => TimeSpan.FromMinutes(15),
            "privilegeescalation" => TimeSpan.FromMinutes(20),
            "temporalburst" => TimeSpan.FromMinutes(30),
            "mldetected" => TimeSpan.FromMinutes(45),
            _ => TimeSpan.FromMinutes(30)
        };
    }

    private async Task SendCorrelationToChannelWithRetryAsync(INotificationChannel channel, SecurityEvent securityEvent, EventCorrelation correlation)
    {
        const int maxRetries = 3;
        var retryCount = 0;
        var backoffMs = 1000;

        while (retryCount < maxRetries)
        {
            try
            {
                // For now, send the security event with correlation context
                // In the future, channels could be extended to handle correlation-specific formatting
                var success = await channel.SendAsync(securityEvent);

                if (success)
                {
                    _logger.LogInformation("Successfully sent correlation notification via {Channel} for event {EventId} ({CorrelationType})",
                        channel.Type, securityEvent.Id, correlation.CorrelationType);
                    return;
                }

                _logger.LogWarning("Failed to send correlation notification via {Channel} for event {EventId} (attempt {Attempt})",
                    channel.Type, securityEvent.Id, retryCount + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending correlation notification via {Channel} for event {EventId}",
                    channel.Type, securityEvent.Id);
            }

            retryCount++;

            if (retryCount < maxRetries)
            {
                await Task.Delay(backoffMs);
                backoffMs *= 2;
            }
        }

        _logger.LogError("Failed to send correlation notification via {Channel} after {MaxRetries} attempts",
            channel.Type, maxRetries);
    }

    private async Task SendAttackChainToChannelWithRetryAsync(INotificationChannel channel, List<SecurityEvent> events, AttackChain attackChain)
    {
        const int maxRetries = 3;
        var retryCount = 0;
        var backoffMs = 1000;

        while (retryCount < maxRetries)
        {
            try
            {
                // Send the first (triggering) event with attack chain context
                var triggeringEvent = events.FirstOrDefault();
                if (triggeringEvent != null)
                {
                    var success = await channel.SendAsync(triggeringEvent);

                    if (success)
                    {
                        _logger.LogInformation("Successfully sent attack chain notification via {Channel} for {AttackType} with {EventCount} events",
                            channel.Type, attackChain.AttackType, events.Count);
                        return;
                    }
                }

                _logger.LogWarning("Failed to send attack chain notification via {Channel} (attempt {Attempt})",
                    channel.Type, retryCount + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending attack chain notification via {Channel}", channel.Type);
            }

            retryCount++;

            if (retryCount < maxRetries)
            {
                await Task.Delay(backoffMs);
                backoffMs *= 2;
            }
        }

        _logger.LogError("Failed to send attack chain notification via {Channel} after {MaxRetries} attempts",
            channel.Type, maxRetries);
    }
}