using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Advanced search service interface for v0.5.0 enhanced search capabilities
/// </summary>
public interface IAdvancedSearchService
{
    /// <summary>
    /// Perform advanced search with multiple criteria and full-text search
    /// </summary>
    Task<AdvancedSearchResult> SearchAsync(AdvancedSearchRequest request);
    
    /// <summary>
    /// Get search suggestions based on partial input
    /// </summary>
    Task<List<string>> GetSearchSuggestionsAsync(string partialQuery, int limit = 10);
    
    /// <summary>
    /// Save a search query for later use
    /// </summary>
    Task<SavedSearch> SaveSearchAsync(string name, AdvancedSearchRequest searchRequest, string userId);
    
    /// <summary>
    /// Get saved searches for a user
    /// </summary>
    Task<List<SavedSearch>> GetSavedSearchesAsync(string userId);
    
    /// <summary>
    /// Delete a saved search
    /// </summary>
    Task<bool> DeleteSavedSearchAsync(string savedSearchId, string userId);
}

/// <summary>
/// Advanced search request with multiple filter criteria
/// </summary>
public class AdvancedSearchRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    
    // Full-text search
    public string? FullTextQuery { get; set; }
    public bool UseExactMatch { get; set; } = false;
    
    // Date range filters
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    // Multi-select filters
    public List<string>? RiskLevels { get; set; }
    public List<string>? EventTypes { get; set; }
    public List<string>? Machines { get; set; }
    public List<string>? Users { get; set; }
    public List<string>? Sources { get; set; }
    public List<string>? MitreTechniques { get; set; }
    
    // Numeric range filters
    public double? MinConfidence { get; set; }
    public double? MaxConfidence { get; set; }
    public double? MinCorrelationScore { get; set; }
    public double? MaxCorrelationScore { get; set; }
    public double? MinBurstScore { get; set; }
    public double? MaxBurstScore { get; set; }
    public double? MinAnomalyScore { get; set; }
    public double? MaxAnomalyScore { get; set; }
    
    // Additional filters
    public List<string>? Statuses { get; set; }
    public List<string>? IPAddresses { get; set; }
    
    // Sorting
    public string? SortField { get; set; } = "Timestamp";
    public string? SortOrder { get; set; } = "DESC";
    
    // Search options
    public bool IncludeArchivedEvents { get; set; } = false;
    public bool EnableFuzzySearch { get; set; } = true;
}

/// <summary>
/// Advanced search result with metadata
/// </summary>
public class AdvancedSearchResult
{
    public List<SecurityEvent> Results { get; set; } = new();
    public int TotalCount { get; set; }
    public long QueryTimeMs { get; set; }
    public Dictionary<string, object> AppliedFilters { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

/// <summary>
/// Saved search configuration
/// </summary>
public class SavedSearch
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AdvancedSearchRequest SearchRequest { get; set; } = new();
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public int UseCount { get; set; } = 0;
    public bool IsPublic { get; set; } = false;
    public List<string> Tags { get; set; } = new();
}
