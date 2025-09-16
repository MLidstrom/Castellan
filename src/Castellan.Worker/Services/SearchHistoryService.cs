using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Service for managing search history
/// </summary>
public class SearchHistoryService : ISearchHistoryService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<SearchHistoryService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SearchHistoryService(CastellanDbContext context, ILogger<SearchHistoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<SearchHistoryEntity>> GetUserSearchHistoryAsync(string userId, int limit = 20)
    {
        try
        {
            return await _context.SearchHistory
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search history for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<SearchHistoryEntity> AddSearchToHistoryAsync(string userId, AdvancedSearchRequest filters, 
        int? resultCount = null, int? executionTimeMs = null)
    {
        try
        {
            var filtersJson = JsonSerializer.Serialize(filters, JsonOptions);
            var searchHash = ComputeSearchHash(filtersJson);

            // Check if this exact search already exists recently (within last hour)
            var recentCutoff = DateTime.UtcNow.AddHours(-1);
            var existingEntry = await _context.SearchHistory
                .FirstOrDefaultAsync(h => h.UserId == userId && h.SearchHash == searchHash && h.CreatedAt >= recentCutoff);

            if (existingEntry != null)
            {
                // Update existing entry with new execution metrics
                if (resultCount.HasValue)
                    existingEntry.ResultCount = resultCount.Value;
                if (executionTimeMs.HasValue)
                    existingEntry.ExecutionTimeMs = executionTimeMs.Value;
                
                await _context.SaveChangesAsync();
                
                _logger.LogDebug("Updated recent search history entry for user: {UserId}", userId);
                return existingEntry;
            }

            // Create new history entry
            var historyEntry = new SearchHistoryEntity
            {
                UserId = userId,
                SearchFilters = filtersJson,
                SearchHash = searchHash,
                ResultCount = resultCount,
                ExecutionTimeMs = executionTimeMs,
                CreatedAt = DateTime.UtcNow
            };

            _context.SearchHistory.Add(historyEntry);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Added search to history for user: {UserId} (Results: {ResultCount}, Time: {ExecutionTime}ms)", 
                userId, resultCount, executionTimeMs);

            // Cleanup old entries if needed (keep only recent entries)
            await CleanupUserHistoryAsync(userId);

            return historyEntry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding search to history for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ClearUserSearchHistoryAsync(string userId)
    {
        try
        {
            var userEntries = await _context.SearchHistory
                .Where(h => h.UserId == userId)
                .ToListAsync();

            if (userEntries.Any())
            {
                _context.SearchHistory.RemoveRange(userEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleared {Count} search history entries for user: {UserId}", userEntries.Count, userId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing search history for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> DeleteSearchHistoryEntryAsync(int entryId, string userId)
    {
        try
        {
            var entry = await _context.SearchHistory
                .FirstOrDefaultAsync(h => h.Id == entryId && h.UserId == userId);

            if (entry == null)
            {
                return false;
            }

            _context.SearchHistory.Remove(entry);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Deleted search history entry: {EntryId} for user: {UserId}", entryId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting search history entry: {EntryId} for user: {UserId}", entryId, userId);
            throw;
        }
    }

    public async Task<SearchHistoryStats> GetSearchHistoryStatsAsync(string userId)
    {
        try
        {
            var userHistory = await _context.SearchHistory
                .Where(h => h.UserId == userId)
                .ToListAsync();

            if (!userHistory.Any())
            {
                return new SearchHistoryStats();
            }

            var stats = new SearchHistoryStats
            {
                TotalSearches = userHistory.Count,
                UniqueSearches = userHistory.Select(h => h.SearchHash).Distinct().Count(),
                LastSearchAt = userHistory.Max(h => h.CreatedAt)
            };

            var executionTimes = userHistory
                .Where(h => h.ExecutionTimeMs.HasValue)
                .Select(h => h.ExecutionTimeMs!.Value)
                .ToList();

            if (executionTimes.Any())
            {
                stats.AverageExecutionTimeMs = executionTimes.Average();
            }

            // Analyze most used filter types
            var filterUsage = new Dictionary<string, int>();
            foreach (var entry in userHistory)
            {
                try
                {
                    var filters = JsonSerializer.Deserialize<AdvancedSearchRequest>(entry.SearchFilters);
                    if (filters != null)
                    {
                        if (!string.IsNullOrEmpty(filters.FullTextQuery))
                            IncrementFilterUsage(filterUsage, "FullTextQuery");
                        if (filters.RiskLevels?.Any() == true)
                            IncrementFilterUsage(filterUsage, "RiskLevels");
                        if (filters.EventTypes?.Any() == true)
                            IncrementFilterUsage(filterUsage, "EventTypes");
                        if (filters.StartDate.HasValue || filters.EndDate.HasValue)
                            IncrementFilterUsage(filterUsage, "DateRange");
                        if (filters.MitreTechniques?.Any() == true)
                            IncrementFilterUsage(filterUsage, "MitreTechniques");
                    }
                }
                catch (JsonException)
                {
                    // Skip invalid JSON entries
                    continue;
                }
            }

            stats.MostUsedFilters = filterUsage;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search history stats for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<int> CleanupOldHistoryEntriesAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            
            var oldEntries = await _context.SearchHistory
                .Where(h => h.CreatedAt < cutoffDate)
                .ToListAsync();

            if (oldEntries.Any())
            {
                _context.SearchHistory.RemoveRange(oldEntries);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cleaned up {Count} old search history entries older than {MaxAge}", 
                    oldEntries.Count, maxAge);
            }

            return oldEntries.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old search history entries");
            throw;
        }
    }

    private async Task CleanupUserHistoryAsync(string userId)
    {
        const int maxHistoryEntries = 100; // Keep only the most recent 100 entries per user

        try
        {
            var totalEntries = await _context.SearchHistory.CountAsync(h => h.UserId == userId);

            if (totalEntries > maxHistoryEntries)
            {
                var entriesToDelete = await _context.SearchHistory
                    .Where(h => h.UserId == userId)
                    .OrderBy(h => h.CreatedAt)
                    .Take(totalEntries - maxHistoryEntries)
                    .ToListAsync();

                _context.SearchHistory.RemoveRange(entriesToDelete);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Cleaned up {Count} old history entries for user: {UserId}", 
                    entriesToDelete.Count, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during history cleanup for user: {UserId}", userId);
            // Don't rethrow - this is a background cleanup operation
        }
    }

    private static string ComputeSearchHash(string filtersJson)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(filtersJson));
        return Convert.ToHexString(hashBytes).ToLower();
    }

    private static void IncrementFilterUsage(Dictionary<string, int> filterUsage, string filterType)
    {
        if (filterUsage.ContainsKey(filterType))
        {
            filterUsage[filterType]++;
        }
        else
        {
            filterUsage[filterType] = 1;
        }
    }
}