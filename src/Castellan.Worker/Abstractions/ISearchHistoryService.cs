using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service interface for managing search history
/// </summary>
public interface ISearchHistoryService
{
    /// <summary>
    /// Get search history for a user (most recent first)
    /// </summary>
    Task<IEnumerable<SearchHistoryEntity>> GetUserSearchHistoryAsync(string userId, int limit = 20);

    /// <summary>
    /// Add a search to history (handles deduplication by hash)
    /// </summary>
    Task<SearchHistoryEntity> AddSearchToHistoryAsync(string userId, AdvancedSearchRequest filters, 
        int? resultCount = null, int? executionTimeMs = null);

    /// <summary>
    /// Clear all search history for a user
    /// </summary>
    Task<bool> ClearUserSearchHistoryAsync(string userId);

    /// <summary>
    /// Delete a specific search history entry
    /// </summary>
    Task<bool> DeleteSearchHistoryEntryAsync(int entryId, string userId);

    /// <summary>
    /// Get search history statistics for a user
    /// </summary>
    Task<SearchHistoryStats> GetSearchHistoryStatsAsync(string userId);

    /// <summary>
    /// Clean up old search history entries (retention policy)
    /// </summary>
    Task<int> CleanupOldHistoryEntriesAsync(TimeSpan maxAge);
}

/// <summary>
/// Search history statistics
/// </summary>
public class SearchHistoryStats
{
    public int TotalSearches { get; set; }
    public int UniqueSearches { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public DateTime? LastSearchAt { get; set; }
    public Dictionary<string, int> MostUsedFilters { get; set; } = new();
}