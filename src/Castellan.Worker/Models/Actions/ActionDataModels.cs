namespace Castellan.Worker.Models.Actions;

/// <summary>
/// Action data for blocking an IP address
/// </summary>
public class BlockIPActionData
{
    /// <summary>
    /// IP address to block
    /// </summary>
    public string IpAddress { get; set; } = "";

    /// <summary>
    /// Reason for blocking
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    /// Duration in hours (0 = permanent)
    /// </summary>
    public int DurationHours { get; set; }

    /// <summary>
    /// Security event ID that triggered this action
    /// </summary>
    public string? EventId { get; set; }
}

/// <summary>
/// Action data for isolating a host from the network
/// </summary>
public class IsolateHostActionData
{
    /// <summary>
    /// Hostname or machine name to isolate
    /// </summary>
    public string Hostname { get; set; } = "";

    /// <summary>
    /// Reason for isolation
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    /// Security event ID that triggered this action
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Whether to disable all network adapters or just external ones
    /// </summary>
    public bool DisableAllAdapters { get; set; } = true;
}

/// <summary>
/// Action data for quarantining a suspicious file
/// </summary>
public class QuarantineFileActionData
{
    /// <summary>
    /// Full path to the file to quarantine
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// File hash (SHA256)
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// Reason for quarantine
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    /// Security event ID that triggered this action
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// YARA rule that matched, if applicable
    /// </summary>
    public string? YaraRuleName { get; set; }
}

/// <summary>
/// Action data for adding an entity to watchlist
/// </summary>
public class AddToWatchlistActionData
{
    /// <summary>
    /// Type of entity (IP, User, Host, Hash)
    /// </summary>
    public WatchlistEntityType EntityType { get; set; }

    /// <summary>
    /// Entity value (IP address, username, hostname, or file hash)
    /// </summary>
    public string EntityValue { get; set; } = "";

    /// <summary>
    /// Reason for adding to watchlist
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    /// Severity level for monitoring
    /// </summary>
    public string Severity { get; set; } = "Medium";

    /// <summary>
    /// Duration in hours (0 = permanent)
    /// </summary>
    public int DurationHours { get; set; }

    /// <summary>
    /// Security event ID that triggered this action
    /// </summary>
    public string? EventId { get; set; }
}

/// <summary>
/// Type of watchlist entity
/// </summary>
public enum WatchlistEntityType
{
    IpAddress,
    Username,
    Hostname,
    FileHash
}

/// <summary>
/// Action data for creating an incident ticket
/// </summary>
public class CreateTicketActionData
{
    /// <summary>
    /// Ticket title
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Ticket description
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Priority level (Low, Medium, High, Critical)
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Ticket category
    /// </summary>
    public string Category { get; set; } = "Security Incident";

    /// <summary>
    /// Assignee username
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Related security event IDs
    /// </summary>
    public List<string> RelatedEventIds { get; set; } = new();

    /// <summary>
    /// External ticket system (Jira, ServiceNow, etc.)
    /// </summary>
    public string? TicketSystem { get; set; }
}

/// <summary>
/// System state snapshot before action execution
/// </summary>
public class SystemStateSnapshot
{
    /// <summary>
    /// Timestamp of the snapshot
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// State data specific to the action type (JSON)
    /// </summary>
    public Dictionary<string, object> StateData { get; set; } = new();
}
