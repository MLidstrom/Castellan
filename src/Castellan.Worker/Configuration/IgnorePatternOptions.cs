namespace Castellan.Worker.Configuration;

/// <summary>
/// Configuration for ignoring benign security event patterns
/// </summary>
public class IgnorePatternOptions
{
    public const string SectionName = "IgnorePatterns";

    /// <summary>
    /// Enable pattern-based filtering of benign events
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Filter ALL events from local machines (ignores all patterns, filters everything from these machines)
    /// WARNING: Use with caution - you will not see ANY security events from these machines
    /// </summary>
    public bool FilterAllLocalEvents { get; set; } = false;

    /// <summary>
    /// List of local machine names to filter ALL events from (only when FilterAllLocalEvents is true)
    /// Examples: ["LAPTOP-7JCL8LTS", "127.0.0.1", "localhost", "::1"]
    /// </summary>
    public List<string> LocalMachines { get; set; } = new();

    /// <summary>
    /// Time window to look for sequential patterns (in seconds)
    /// </summary>
    public int SequenceTimeWindowSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of recent events to keep in memory for pattern detection
    /// </summary>
    public int MaxRecentEvents { get; set; } = 100;

    /// <summary>
    /// List of sequential event patterns to ignore (sequences of events)
    /// </summary>
    public List<SequentialIgnorePattern> Patterns { get; set; } = new();
}

/// <summary>
/// Defines a sequential pattern of events to ignore
/// </summary>
public class SequentialIgnorePattern
{
    /// <summary>
    /// Ordered list of events that form the pattern (first event → second event → ...)
    /// </summary>
    public List<EventStep> Sequence { get; set; } = new();

    /// <summary>
    /// Description of why this pattern is ignored
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Ignore all events in the sequence when matched (default: true)
    /// If false, only the last event in the sequence is ignored
    /// </summary>
    public bool IgnoreAllEventsInSequence { get; set; } = false;
}

/// <summary>
/// Represents a single step in an event sequence
/// </summary>
public class EventStep
{
    /// <summary>
    /// Event type to match (e.g., "PrivilegeEscalation", "AuthenticationSuccess")
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// MITRE techniques to match (e.g., ["T1548", "T1055"])
    /// Empty list means event type match is sufficient
    /// </summary>
    public List<string> MitreTechniques { get; set; } = new();

    /// <summary>
    /// Only match if all specified MITRE techniques are present (default: false = any match)
    /// </summary>
    public bool RequireAllTechniques { get; set; } = false;

    /// <summary>
    /// Source machine names to match (e.g., ["LAPTOP-7JCL8LTS", "DESKTOP-ABC123"])
    /// Empty list means any source matches
    /// </summary>
    public List<string> SourceMachines { get; set; } = new();

    /// <summary>
    /// Account names to match (e.g., ["SYSTEM", "Administrator"])
    /// Empty list means any account matches
    /// </summary>
    public List<string> AccountNames { get; set; } = new();

    /// <summary>
    /// Logon types to match for authentication events (e.g., [2, 3, 5])
    /// 2 = Interactive, 3 = Network, 5 = Service, 7 = Unlock, 10 = RemoteInteractive
    /// Empty list means any logon type matches
    /// </summary>
    public List<int> LogonTypes { get; set; } = new();

    /// <summary>
    /// Source IP addresses to match (e.g., ["127.0.0.1", "192.168.1.100"])
    /// Empty list means any source IP matches
    /// Supports CIDR notation (e.g., "192.168.1.0/24")
    /// </summary>
    public List<string> SourceIPs { get; set; } = new();
}
