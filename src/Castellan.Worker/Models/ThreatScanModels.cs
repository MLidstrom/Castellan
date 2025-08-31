namespace Castellan.Worker.Models;

public class ThreatScanResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ThreatScanType ScanType { get; set; }
    public ThreatScanStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Scan Statistics
    public int FilesScanned { get; set; }
    public int DirectoriesScanned { get; set; }
    public long BytesScanned { get; set; }
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    
    // Threat Results
    public int ThreatsFound { get; set; }
    public int MalwareDetected { get; set; }
    public int BackdoorsDetected { get; set; }
    public int SuspiciousFiles { get; set; }
    public List<FileThreatResult> ThreatDetails { get; set; } = new();
    
    // Summary
    public string Summary => $"Scanned {FilesScanned} files, found {ThreatsFound} threats";
    public ThreatRiskLevel RiskLevel => ThreatsFound > 0 ? ThreatRiskLevel.High : ThreatRiskLevel.Low;
}

public class FileThreatResult
{
    public string FilePath { get; set; } = string.Empty;
    public ThreatType ThreatType { get; set; }
    public string ThreatName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ThreatRiskLevel RiskLevel { get; set; }
    public float Confidence { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string FileHash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Action { get; set; } = "Quarantine Recommended";
    public string[] MitreTechniques { get; set; } = Array.Empty<string>();
}

public enum ThreatScanType
{
    QuickScan,
    FullScan,
    DirectoryScan,
    FileScan
}

public enum ThreatScanStatus
{
    NotStarted,
    Running,
    Completed,
    CompletedWithThreats,
    Failed,
    Cancelled,
    Paused
}

public enum ThreatType
{
    Unknown,
    Malware,
    Virus,
    Trojan,
    Backdoor,
    Rootkit,
    Spyware,
    Adware,
    Ransomware,
    Worm,
    SuspiciousScript,
    SuspiciousBehavior,
    SuspiciousFile
}

public enum ThreatRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public class ThreatScanOptions
{
    public bool Enabled { get; set; } = true;
    public TimeSpan ScheduledScanInterval { get; set; } = TimeSpan.FromHours(24);
    public ThreatScanType DefaultScanType { get; set; } = ThreatScanType.QuickScan;
    public string[] ExcludedDirectories { get; set; } = Array.Empty<string>();
    public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();
    public int MaxFileSizeMB { get; set; } = 100; // Don't scan files larger than 100MB
    public int MaxConcurrentFiles { get; set; } = 4;
    public bool QuarantineThreats { get; set; } = false; // Don't auto-quarantine by default
    public string QuarantineDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "Castellan", "Quarantine");
    public bool EnableRealTimeProtection { get; set; } = false;
    public ThreatRiskLevel NotificationThreshold { get; set; } = ThreatRiskLevel.Medium;
}