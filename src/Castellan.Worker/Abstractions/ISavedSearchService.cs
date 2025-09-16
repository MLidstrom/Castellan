using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service interface for managing saved search configurations
/// </summary>
public interface ISavedSearchService
{
    /// <summary>
    /// Get all saved searches for a user
    /// </summary>
    Task<IEnumerable<SavedSearchEntity>> GetUserSavedSearchesAsync(string userId);

    /// <summary>
    /// Get a specific saved search by ID
    /// </summary>
    Task<SavedSearchEntity?> GetSavedSearchAsync(int searchId, string userId);

    /// <summary>
    /// Create a new saved search
    /// </summary>
    Task<SavedSearchEntity> CreateSavedSearchAsync(string userId, string name, string? description, 
        AdvancedSearchRequest filters, string[]? tags = null);

    /// <summary>
    /// Update an existing saved search
    /// </summary>
    Task<SavedSearchEntity> UpdateSavedSearchAsync(int searchId, string userId, string name, 
        string? description, AdvancedSearchRequest filters, string[]? tags = null);

    /// <summary>
    /// Delete a saved search
    /// </summary>
    Task<bool> DeleteSavedSearchAsync(int searchId, string userId);

    /// <summary>
    /// Record usage of a saved search (updates LastUsedAt and UseCount)
    /// </summary>
    Task<bool> RecordSearchUsageAsync(int searchId, string userId);

    /// <summary>
    /// Get most frequently used saved searches for a user
    /// </summary>
    Task<IEnumerable<SavedSearchEntity>> GetMostUsedSearchesAsync(string userId, int limit = 5);

    /// <summary>
    /// Search saved searches by name or tags
    /// </summary>
    Task<IEnumerable<SavedSearchEntity>> SearchSavedSearchesAsync(string userId, string searchTerm);
}