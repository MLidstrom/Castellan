using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Castellan.Worker.Models.Compliance;

public class ComplianceReport
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(100)]
    public string Framework { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ReportType { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public DateTime Generated { get; set; }

    public DateTime ValidUntil { get; set; }

    [Required]
    [MaxLength(100)]
    public string GeneratedBy { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = string.Empty;

    public int ImplementationPercentage { get; set; }

    public int TotalControls { get; set; }

    public int ImplementedControls { get; set; }

    public int FailedControls { get; set; }

    public int GapCount { get; set; }

    public float RiskScore { get; set; }

    [Column(TypeName = "text")]
    public string? Summary { get; set; }

    [Column(TypeName = "text")]
    public string? KeyFindings { get; set; }

    [Column(TypeName = "text")]
    public string? Recommendations { get; set; }

    public DateTime NextReview { get; set; }

    [Column(TypeName = "text")]
    public string? ReportData { get; set; } // JSON for detailed results

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual ICollection<ComplianceAssessmentResult> AssessmentResults { get; set; } = new List<ComplianceAssessmentResult>();
}