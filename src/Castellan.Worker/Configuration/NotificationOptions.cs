namespace Castellan.Worker.Configuration;

public class NotificationOptions
{
    public const string SectionName = "Notifications";
    
    /// <summary>
    /// Enable desktop notifications for security events
    /// </summary>
    public bool EnableDesktopNotifications { get; set; } = true;
    
    /// <summary>
    /// Enable sound alerts for security events
    /// </summary>
    public bool EnableSoundAlerts { get; set; } = false;
    
    /// <summary>
    /// Minimum notification level (Info, Warning, Error, Critical)
    /// </summary>
    public string NotificationLevel { get; set; } = "Warning";
    
    /// <summary>
    /// Show detailed event information in notifications
    /// </summary>
    public bool ShowEventDetails { get; set; } = true;
    
    /// <summary>
    /// Show IP enrichment data (City, Country, ASN) in notifications
    /// </summary>
    public bool ShowIPEnrichment { get; set; } = true;
    
    /// <summary>
    /// Notification display timeout in milliseconds
    /// </summary>
    public int NotificationTimeout { get; set; } = 5000;
}