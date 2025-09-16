using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

/// <summary>
/// Database entity for persisting search history entries
/// </summary>
public class SearchHistoryEntity
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string SearchFilters { get; set; } = string.Empty; // JSON serialized AdvancedSearchRequest
    
    [Required]
    [MaxLength(64)]
    public string SearchHash { get; set; } = string.Empty; // Hash of filters to prevent duplicates
    
    public int? ResultCount { get; set; }
    
    public int? ExecutionTimeMs { get; set; }
    
    public DateTime CreatedAt { get; set; }
}