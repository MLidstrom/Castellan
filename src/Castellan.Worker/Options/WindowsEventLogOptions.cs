using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Options;

/// <summary>
/// Configuration options for Windows Event Log watching functionality
/// </summary>
public class WindowsEventLogOptions
{
    /// <summary>
    /// Whether Windows Event Log watching is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// List of event log channels to watch
    /// </summary>
    public List<WindowsEventChannelOptions> Channels { get; set; } = new();

    /// <summary>
    /// Default maximum queue size for event processing
    /// </summary>
    [Range(100, 50000)]
    public int DefaultMaxQueue { get; set; } = 5000;

    /// <summary>
    /// Number of concurrent consumers for processing events
    /// </summary>
    [Range(1, 16)]
    public int ConsumerConcurrency { get; set; } = 4;

    /// <summary>
    /// Backoff intervals in seconds for reconnection attempts
    /// </summary>
    public int[] ReconnectBackoffSeconds { get; set; } = { 1, 2, 5, 10, 30 };

    /// <summary>
    /// Whether to trigger immediate dashboard broadcasts when events arrive
    /// </summary>
    public bool ImmediateDashboardBroadcast { get; set; } = true;
}

/// <summary>
/// Configuration options for a specific Windows Event Log channel
/// </summary>
public class WindowsEventChannelOptions
{
    /// <summary>
    /// Name of the event log channel (e.g., "Security", "Microsoft-Windows-Sysmon/Operational")
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this channel is enabled for watching
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// XPath filter to apply to events in this channel
    /// </summary>
    [Required]
    public string XPathFilter { get; set; } = string.Empty;

    /// <summary>
    /// Type of bookmark persistence to use ("Database" or "File")
    /// </summary>
    public string BookmarkPersistence { get; set; } = "Database";

    /// <summary>
    /// Maximum queue size for this specific channel
    /// </summary>
    [Range(100, 50000)]
    public int MaxQueue { get; set; } = 5000;
}
