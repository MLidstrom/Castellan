using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

public class Application
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Version { get; set; }
    
    public string? Description { get; set; }
    
    public string? SecurityProfile { get; set; }  // JSON string for flexible security data
    
    public int RiskScore { get; set; } = 0;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<ApplicationMitreAssociation> MitreAssociations { get; set; } = new List<ApplicationMitreAssociation>();
    public ICollection<SecurityEventEntity> SecurityEvents { get; set; } = new List<SecurityEventEntity>();
}
