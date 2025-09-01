using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

using ApplicationModel = Castellan.Worker.Models.Application;

public class ApplicationMitreAssociation
{
    public int Id { get; set; }
    
    public int ApplicationId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string TechniqueId { get; set; } = string.Empty;
    
    public double Confidence { get; set; } = 1.0;
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ApplicationModel Application { get; set; } = null!;
    public MitreTechnique MitreTechnique { get; set; } = null!;
}
