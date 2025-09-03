namespace Castellan.Worker.Configuration;

/// <summary>
/// Configuration options for Slack notifications
/// </summary>
public class SlackNotificationOptions
{
    /// <summary>
    /// Whether Slack notifications are enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The Slack incoming webhook URL
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Castellan web interface (used in action buttons)
    /// </summary>
    public string CastellanUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Default channel to send notifications to
    /// </summary>
    public string? DefaultChannel { get; set; }

    /// <summary>
    /// Channel for critical alerts
    /// </summary>
    public string? CriticalChannel { get; set; }

    /// <summary>
    /// Channel for high severity alerts
    /// </summary>
    public string? HighChannel { get; set; }

    /// <summary>
    /// Custom channel mappings by severity
    /// </summary>
    public Dictionary<string, string>? ChannelMappings { get; set; }

    /// <summary>
    /// User IDs to mention for critical alerts
    /// </summary>
    public List<string>? MentionUsersForCritical { get; set; }

    /// <summary>
    /// Rate limiting settings
    /// </summary>
    public RateLimitSettings RateLimit { get; set; } = new();

    /// <summary>
    /// Notification filtering by severity
    /// </summary>
    public List<string> EnabledSeverities { get; set; } = new() { "critical", "high", "medium", "low" };

    /// <summary>
    /// Maximum retries for failed notifications
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout for webhook requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to include rich formatting (blocks) in messages
    /// </summary>
    public bool UseRichFormatting { get; set; } = true;
}