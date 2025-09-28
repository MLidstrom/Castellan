using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Castellan.Worker.Models.Compliance;

public class ComplianceAssessmentResult
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string ReportId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ControlId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Framework { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty; // Compliant, NonCompliant, PartiallyCompliant

    public int Score { get; set; } // 0-100

    [Column(TypeName = "text")]
    public string? Evidence { get; set; }

    [Column(TypeName = "text")]
    public string? Findings { get; set; }

    [Column(TypeName = "text")]
    public string? Recommendations { get; set; }

    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("ReportId")]
    public virtual ComplianceReport Report { get; set; } = null!;
}