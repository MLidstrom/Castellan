namespace Castellan.Worker.Models.Chat;

/// <summary>
/// Context information retrieved from various sources to enhance chat responses.
/// Uses RAG (Retrieval-Augmented Generation) to provide relevant security event data.
/// </summary>
public class ChatContext
{
    /// <summary>
    /// Similar security events found via vector search
    /// </summary>
    public List<SecurityEvent> SimilarEvents { get; set; } = new();

    /// <summary>
    /// Recent high-risk or critical events
    /// </summary>
    public List<SecurityEvent> RecentCriticalEvents { get; set; } = new();

    /// <summary>
    /// Active correlation patterns detected by the correlation engine
    /// </summary>
    public List<CorrelationPattern> ActivePatterns { get; set; } = new();

    /// <summary>
    /// Current system metrics (event counts, risk distribution, etc.)
    /// </summary>
    public SystemMetrics? CurrentMetrics { get; set; }

    /// <summary>
    /// User's classified intent
    /// </summary>
    public ChatIntent? Intent { get; set; }

    /// <summary>
    /// Time range for context retrieval
    /// </summary>
    public TimeRange TimeRange { get; set; } = new()
    {
        Start = DateTime.UtcNow.AddHours(-24),
        End = DateTime.UtcNow
    };

    /// <summary>
    /// Number of events retrieved for context
    /// </summary>
    public int EventCount => SimilarEvents.Count + RecentCriticalEvents.Count;

    /// <summary>
    /// Whether sufficient context was retrieved
    /// </summary>
    public bool HasSufficientContext => EventCount > 0 || CurrentMetrics != null;
}

/// <summary>
/// Represents a correlation pattern detected by the correlation engine
/// </summary>
public class CorrelationPattern
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float Score { get; set; }
    public int EventCount { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public List<string> RelatedEventIds { get; set; } = new();
}

/// <summary>
/// Current system metrics snapshot
/// </summary>
public class SystemMetrics
{
    public int TotalEvents24h { get; set; }
    public int CriticalEvents { get; set; }
    public int HighRiskEvents { get; set; }
    public int OpenEvents { get; set; }
    public Dictionary<string, int> EventsByRiskLevel { get; set; } = new();
    public Dictionary<string, int> EventsByType { get; set; } = new();
    public int ActiveYaraRules { get; set; }
    public int TotalThreatScans { get; set; }
    public string SystemStatus { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Time range for context retrieval
/// </summary>
public class TimeRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public TimeSpan Duration => End - Start;

    public static TimeRange Last24Hours => new()
    {
        Start = DateTime.UtcNow.AddHours(-24),
        End = DateTime.UtcNow
    };

    public static TimeRange LastHour => new()
    {
        Start = DateTime.UtcNow.AddHours(-1),
        End = DateTime.UtcNow
    };

    public static TimeRange LastWeek => new()
    {
        Start = DateTime.UtcNow.AddDays(-7),
        End = DateTime.UtcNow
    };
}
