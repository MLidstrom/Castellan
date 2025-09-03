using System.Collections.Concurrent;
using Castellan.Worker.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services.NotificationChannels;

/// <summary>
/// Manages notification delivery across multiple channels with throttling
/// </summary>
public interface INotificationManager
{
    Task SendSecurityAlertAsync(SecurityEvent securityEvent);
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
}