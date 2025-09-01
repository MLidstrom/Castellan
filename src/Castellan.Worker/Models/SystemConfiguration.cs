using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

public class SystemConfiguration
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;
    
    public string? Value { get; set; }
    
    [MaxLength(255)]
    public string? Description { get; set; }
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
