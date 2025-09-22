using Castellan.Worker.Models.ThreatIntelligence;

namespace Castellan.Worker.Models;

public class ThreatScanResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ScanId { get; set; }
    public string? ScanPath { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ThreatScanType ScanType { get; set; }
    public ThreatScanStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    // Scan Statistics
    public int FilesScanned { get; set; }
    public int DirectoriesScanned { get; set; }
    public long BytesScanned { get; set; }
    private TimeSpan? _duration;
    public TimeSpan Duration
    {
        get => _duration ?? (EndTime?.Subtract(StartTime) ?? TimeSpan.Zero);
        set => _duration = value;
    }

    // Threat Results
    public int ThreatsFound { get; set; }
    public int MalwareDetected { get; set; }
    public int BackdoorsDetected { get; set; }
    public int SuspiciousFiles { get; set; }
    public List<FileThreatResult> ThreatDetails { get; set; } = new();

    // Summary
    private string? _summary;
    public string Summary
    {
        get => _summary ?? $"Scanned {FilesScanned} files, found {ThreatsFound} threats";
        set => _summary = value;
    }

    private ThreatRiskLevel? _riskLevel;
    public ThreatRiskLevel RiskLevel
    {
        get => _riskLevel ?? (ThreatsFound > 0 ? ThreatRiskLevel.High : ThreatRiskLevel.Low);
        set => _riskLevel = value;
    }
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