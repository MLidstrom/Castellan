namespace Castellan.Worker.Configuration;

/// <summary>
/// Configuration options for Microsoft Teams notifications
/// </summary>
public class TeamsNotificationOptions
{
    /// <summary>
    /// Whether Teams notifications are enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The Teams incoming webhook URL
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for Castellan web interface (used in action buttons)
    /// </summary>
    public string CastellanUrl { get; set; } = "http://localhost:8080";

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
}

/// <summary>
/// Rate limiting configuration
/// </summary>
public class RateLimitSettings
{
    /// <summary>
    /// Maximum notifications per time window
    /// </summary>
    public int MaxNotificationsPerWindow { get; set; } = 10;

    /// <summary>
    /// Time window in minutes
    /// </summary>
    public int WindowMinutes { get; set; } = 5;

    /// <summary>
    /// Throttle periods by severity (in minutes)
    /// </summary>
    public Dictionary<string, int> ThrottleBySeverity { get; set; } = new()
    {
        ["critical"] = 0,  // No throttling
        ["high"] = 5,
        ["medium"] = 15,
        ["low"] = 60
    };
}