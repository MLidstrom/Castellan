using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Castellan.Worker.Models.Compliance;

public enum ComplianceScope
{
    Organization = 0,  // User-visible frameworks (HIPAA, SOX, PCI-DSS, ISO 27001, SOC2)
    Application = 1    // Hidden frameworks (CIS Controls, Windows Security Baselines)
}

public class ComplianceControl
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Framework { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ControlId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ControlName { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(20)]
    public string Priority { get; set; } = "Medium";

    [Column(TypeName = "text")]
    public string? ValidationQuery { get; set; } // SQL or logic for validation

    public bool IsActive { get; set; } = true;

    // Scope determines visibility: Organization (user-visible) vs Application (hidden)
    public ComplianceScope Scope { get; set; } = ComplianceScope.Organization;

    // Determines if control is visible to users (Organization scope = true, Application scope = false)
    public bool IsUserVisible { get; set; } = true;

    [MaxLength(200)]
    public string? ApplicableSectors { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}