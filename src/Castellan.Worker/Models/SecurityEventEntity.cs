using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

using ApplicationModel = Castellan.Worker.Models.Application;

/// <summary>
/// Database entity for persisting SecurityEvent data to SQLite
/// This is separate from the main SecurityEvent class to avoid conflicts
/// </summary>
public class SecurityEventEntity
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string EventId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string RiskLevel { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Source { get; set; }
    
    public string? Message { get; set; }
    
    public string? Summary { get; set; }
    
    public string? EventData { get; set; }  // JSON string for flexible event data
    
    public DateTime Timestamp { get; set; }
    
    [MaxLength(45)]  // IPv6 max length
    public string? SourceIp { get; set; }
    
    [MaxLength(45)]  // IPv6 max length
    public string? DestinationIp { get; set; }
    
    public int? ApplicationId { get; set; }
    
    public string? MitreTechniques { get; set; }  // JSON array string
    
    public string? RecommendedActions { get; set; }  // JSON array string
    
    public double Confidence { get; set; }
    
    public double CorrelationScore { get; set; }
    
    public double BurstScore { get; set; }
    
    public double AnomalyScore { get; set; }
    
    public bool IsDeterministic { get; set; }
    
    public bool IsCorrelationBased { get; set; }
    
    public bool IsEnhanced { get; set; }
    
    public string? EnrichmentData { get; set; }  // JSON string
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ApplicationModel? Application { get; set; }
}
