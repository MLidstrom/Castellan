using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

public class ThreatScanHistoryEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string ScanType { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public double Duration { get; set; } // in minutes

    public int FilesScanned { get; set; }

    public int DirectoriesScanned { get; set; }

    public long BytesScanned { get; set; }

    public int ThreatsFound { get; set; }

    public int MalwareDetected { get; set; }

    public int BackdoorsDetected { get; set; }

    public int SuspiciousFiles { get; set; }

    [Required]
    public string RiskLevel { get; set; } = "Low";

    public string? Summary { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ScanPath { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}