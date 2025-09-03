using Castellan.Worker.Models;

namespace Castellan.Worker.Services.NotificationChannels;

/// <summary>
/// Interface for notification channels (Teams, Slack, etc.)
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// Gets the type of notification channel
    /// </summary>
    NotificationChannelType Type { get; }

    /// <summary>
    /// Gets whether the channel is enabled and configured
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends a security alert through this channel
    /// </summary>
    Task<bool> SendAsync(SecurityEvent securityEvent);

    /// <summary>
    /// Tests the connection to the channel
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Gets the channel's current health status
    /// </summary>
    Task<ChannelHealthStatus> GetHealthStatusAsync();
}

/// <summary>
/// Types of notification channels
/// </summary>
public enum NotificationChannelType
{
    Desktop,
    Teams,
    Slack,
    Email
}

/// <summary>
/// Health status of a notification channel
/// </summary>
public class ChannelHealthStatus
{
    public bool IsHealthy { get; set; }
    public DateTime LastCheckTime { get; set; }
    public string? LastError { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}