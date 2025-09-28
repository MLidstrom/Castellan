using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Castellan.Worker.Models.Compliance;

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}