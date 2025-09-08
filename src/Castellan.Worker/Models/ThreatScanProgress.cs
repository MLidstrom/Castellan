namespace Castellan.Worker.Models;

public class ThreatScanProgress
{
    public string ScanId { get; set; } = string.Empty;
    public ThreatScanStatus Status { get; set; }
    public int FilesScanned { get; set; }
    public int TotalEstimatedFiles { get; set; }
    public int DirectoriesScanned { get; set; }
    public int FindingsCount { get; set; }
    public int ThreatsFound { get; set; } // Kept for backwards compatibility
    public string CurrentFile { get; set; } = string.Empty;
    public string CurrentDirectory { get; set; } = string.Empty;
    public double PercentComplete { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public long BytesScanned { get; set; }
    public string ScanPhase { get; set; } = "Initializing";
}

public class ScanProgressUpdate
{
    public string Type { get; set; } = "progress";
    public ThreatScanProgress Progress { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
