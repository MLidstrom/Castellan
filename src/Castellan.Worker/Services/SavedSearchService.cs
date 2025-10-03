using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Service for managing saved search configurations
/// </summary>
public class SavedSearchService : ISavedSearchService
{
    private readonly IDbContextFactory<CastellanDbContext> _contextFactory;
    private readonly ILogger<SavedSearchService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SavedSearchService(IDbContextFactory<CastellanDbContext> contextFactory, ILogger<SavedSearchService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<SavedSearchEntity>> GetUserSavedSearchesAsync(string userId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.SavedSearches
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.LastUsedAt ?? s.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting saved searches for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<SavedSearchEntity?> GetSavedSearchAsync(int searchId, string userId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == searchId && s.UserId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting saved search: {SearchId} for user: {UserId}", searchId, userId);
            throw;
        }
    }

    public async Task<SavedSearchEntity> CreateSavedSearchAsync(string userId, string name, string? description,
        AdvancedSearchRequest filters, string[]? tags = null)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Check for duplicate names
            var existingSearch = await context.SavedSearches
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Name == name);

            if (existingSearch != null)
            {
                throw new InvalidOperationException($"A saved search with the name '{name}' already exists for this user.");
            }

            var savedSearch = new SavedSearchEntity
            {
                UserId = userId,
                Name = name,
                Description = description,
                SearchFilters = JsonSerializer.Serialize(filters, JsonOptions),
                Tags = tags != null ? string.Join(",", tags) : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UseCount = 0
            };

            context.SavedSearches.Add(savedSearch);
            await context.SaveChangesAsync();

            _logger.LogInformation("Created saved search: {Name} for user: {UserId}", name, userId);

            return savedSearch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating saved search: {Name} for user: {UserId}", name, userId);
            throw;
        }
    }

    public async Task<SavedSearchEntity> UpdateSavedSearchAsync(int searchId, string userId, string name,
        string? description, AdvancedSearchRequest filters, string[]? tags = null)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var savedSearch = await context.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == searchId && s.UserId == userId);

            if (savedSearch == null)
            {
                throw new InvalidOperationException($"Saved search with ID {searchId} not found or access denied.");
            }

            // Check for duplicate names (excluding current search)
            var duplicateSearch = await context.SavedSearches
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Name == name && s.Id != searchId);

            if (duplicateSearch != null)
            {
                throw new InvalidOperationException($"A saved search with the name '{name}' already exists for this user.");
            }

            savedSearch.Name = name;
            savedSearch.Description = description;
            savedSearch.SearchFilters = JsonSerializer.Serialize(filters, JsonOptions);
            savedSearch.Tags = tags != null ? string.Join(",", tags) : null;
            savedSearch.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogInformation("Updated saved search: {SearchId} for user: {UserId}", searchId, userId);

            return savedSearch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating saved search: {SearchId} for user: {UserId}", searchId, userId);
            throw;
        }
    }

    public async Task<bool> DeleteSavedSearchAsync(int searchId, string userId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var savedSearch = await context.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == searchId && s.UserId == userId);

            if (savedSearch == null)
            {
                return false;
            }

            context.SavedSearches.Remove(savedSearch);
            await context.SaveChangesAsync();

            _logger.LogInformation("Deleted saved search: {SearchId} for user: {UserId}", searchId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting saved search: {SearchId} for user: {UserId}", searchId, userId);
            throw;
        }
    }

    public async Task<bool> RecordSearchUsageAsync(int searchId, string userId)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var savedSearch = await context.SavedSearches
                .FirstOrDefaultAsync(s => s.Id == searchId && s.UserId == userId);

            if (savedSearch == null)
            {
                return false;
            }

            savedSearch.UseCount++;
            savedSearch.LastUsedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            _logger.LogDebug("Recorded usage for saved search: {SearchId} (total uses: {UseCount})", searchId, savedSearch.UseCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording usage for saved search: {SearchId} for user: {UserId}", searchId, userId);
            throw;
        }
    }

    public async Task<IEnumerable<SavedSearchEntity>> GetMostUsedSearchesAsync(string userId, int limit = 5)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.SavedSearches
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UseCount)
                .ThenByDescending(s => s.LastUsedAt ?? s.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most used searches for user: {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<SavedSearchEntity>> SearchSavedSearchesAsync(string userId, string searchTerm)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var lowerSearchTerm = searchTerm.ToLower();

            return await context.SavedSearches
                .Where(s => s.UserId == userId &&
                           (s.Name.ToLower().Contains(lowerSearchTerm) ||
                            (s.Description != null && s.Description.ToLower().Contains(lowerSearchTerm)) ||
                            (s.Tags != null && s.Tags.ToLower().Contains(lowerSearchTerm))))
                .OrderByDescending(s => s.LastUsedAt ?? s.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching saved searches for user: {UserId} with term: {SearchTerm}", userId, searchTerm);
            throw;
        }
    }
}