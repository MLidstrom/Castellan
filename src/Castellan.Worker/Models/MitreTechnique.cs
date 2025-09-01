using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

public class MitreTechnique
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string TechniqueId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [MaxLength(100)]
    public string? Tactic { get; set; }
    
    [MaxLength(500)]
    public string? Platform { get; set; }
    
    public string? DataSources { get; set; }  // JSON string
    
    public string? Mitigations { get; set; }  // JSON string
    
    public string? Examples { get; set; }     // JSON string
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<ApplicationMitreAssociation> ApplicationAssociations { get; set; } = new List<ApplicationMitreAssociation>();
}
