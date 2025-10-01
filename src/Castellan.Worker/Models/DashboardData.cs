namespace Castellan.Worker.Models;

/// <summary>
/// Consolidated dashboard data model combining all dashboard widgets into a single data structure
/// </summary>
public class ConsolidatedDashboardData
{
    public SecurityEventsSummary SecurityEvents { get; set; } = new();
    public SystemStatusSummary SystemStatus { get; set; } = new();
    public ThreatScannerSummary ThreatScanner { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string TimeRange { get; set; } = "24h";
}

/// <summary>
/// Summary of security events for dashboard display
/// </summary>
public class SecurityEventsSummary
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> RiskLevelCounts { get; set; } = new();
    public List<SecurityEventBasic> RecentEvents { get; set; } = new();
    public DateTime LastEventTime { get; set; }
}

/// <summary>
/// Summary of system health status for dashboard display
/// </summary>
public class SystemStatusSummary
{
    public int TotalComponents { get; set; }
    public int HealthyComponents { get; set; }
    public List<ComponentHealthBasic> Components { get; set; } = new();
    public Dictionary<string, string> ComponentStatuses { get; set; } = new();
}

/// <summary>
/// Summary of threat scanner activity for dashboard display
/// </summary>
public class ThreatScannerSummary
{
    public int TotalScans { get; set; }
    public int ActiveScans { get; set; }
    public int CompletedScans { get; set; }
    public int ThreatsFound { get; set; }
    public DateTime LastScanTime { get; set; }
    public List<ThreatScanBasic> RecentScans { get; set; } = new();
}

// Lightweight versions for dashboard - only essential fields to reduce payload size

/// <summary>
/// Basic security event information for dashboard display
/// </summary>
public class SecurityEventBasic
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
}

/// <summary>
/// Basic component health information for dashboard display
/// </summary>
public class ComponentHealthBasic
{
    public string Component { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ResponseTime { get; set; }
    public DateTime LastCheck { get; set; }
}

/// <summary>
/// Basic threat scan information for dashboard display
/// </summary>
public class ThreatScanBasic
{
    public string Id { get; set; } = string.Empty;
    public string ScanType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FilesScanned { get; set; }
    public int ThreatsFound { get; set; }
}