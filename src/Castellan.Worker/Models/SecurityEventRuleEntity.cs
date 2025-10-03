using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

/// <summary>
/// Database entity representing a security event detection rule.
/// This replaces the hard-coded dictionary in SecurityEventDetector.
/// </summary>
public class SecurityEventRuleEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Windows Event ID (e.g., 4624, 4625, 4104)
    /// </summary>
    [Required]
    public int EventId { get; set; }

    /// <summary>
    /// Event channel/log source (e.g., "Security", "Microsoft-Windows-PowerShell/Operational")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Security event type
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Default risk level (low, medium, high, critical)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (0-100)
    /// </summary>
    [Required]
    [Range(0, 100)]
    public int Confidence { get; set; }

    /// <summary>
    /// Human-readable summary of the event
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// MITRE ATT&CK technique IDs (JSON array)
    /// </summary>
    [Required]
    public string MitreTechniques { get; set; } = "[]";

    /// <summary>
    /// Recommended actions for analysts (JSON array)
    /// </summary>
    [Required]
    public string RecommendedActions { get; set; } = "[]";

    /// <summary>
    /// Whether this rule is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Rule priority for conflict resolution (higher wins)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Optional description or notes
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Optional tags for categorization (JSON array)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// When this rule was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this rule was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Created/modified by which user or system
    /// </summary>
    [MaxLength(256)]
    public string? ModifiedBy { get; set; }
}
