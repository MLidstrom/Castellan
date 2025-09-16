using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

/// <summary>
/// Database entity for persisting saved search configurations
/// </summary>
public class SavedSearchEntity
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    public string SearchFilters { get; set; } = string.Empty; // JSON serialized AdvancedSearchRequest
    
    public bool IsPublic { get; set; } = false;
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public DateTime? LastUsedAt { get; set; }
    
    public int UseCount { get; set; } = 0;
    
    [MaxLength(500)]
    public string? Tags { get; set; } // Comma-separated tags
}