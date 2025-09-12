using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Advanced search service with FTS5 full-text search and multi-criteria filtering
/// v0.5.0 Implementation
/// </summary>
public class AdvancedSearchService : IAdvancedSearchService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<AdvancedSearchService> _logger;
    private readonly ISecurityEventStore _securityEventStore;

    public AdvancedSearchService(
        CastellanDbContext context,
        ILogger<AdvancedSearchService> logger,
        ISecurityEventStore securityEventStore)
    {
        _context = context;
        _logger = logger;
        _securityEventStore = securityEventStore;
    }

    public async Task<AdvancedSearchResult> SearchAsync(AdvancedSearchRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting advanced search with request: {@Request}", request);

            var query = _context.SecurityEvents.AsQueryable();
            var appliedFilters = new Dictionary<string, object>();

            // Full-text search using FTS5
            if (!string.IsNullOrEmpty(request.FullTextQuery))
            {
                query = await ApplyFullTextSearch(query, request.FullTextQuery, request.UseExactMatch);
                appliedFilters["fullTextQuery"] = request.FullTextQuery;
                appliedFilters["useExactMatch"] = request.UseExactMatch;
            }

            // Date range filters
            if (request.StartDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= request.StartDate.Value);
                appliedFilters["startDate"] = request.StartDate.Value;
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= request.EndDate.Value);
                appliedFilters["endDate"] = request.EndDate.Value;
            }

            // Multi-select filters
            if (request.RiskLevels?.Any() == true)
            {
                query = query.Where(e => request.RiskLevels.Contains(e.RiskLevel));
                appliedFilters["riskLevels"] = request.RiskLevels;
            }

            if (request.EventTypes?.Any() == true)
            {
                query = query.Where(e => request.EventTypes.Contains(e.EventType));
                appliedFilters["eventTypes"] = request.EventTypes;
            }

            if (request.Sources?.Any() == true)
            {
                query = query.Where(e => e.Source != null && request.Sources.Contains(e.Source));
                appliedFilters["sources"] = request.Sources;
            }

            // MITRE technique filtering (JSON contains)
            if (request.MitreTechniques?.Any() == true)
            {
                foreach (var technique in request.MitreTechniques)
                {
                    query = query.Where(e => e.MitreTechniques != null && e.MitreTechniques.Contains(technique));
                }
                appliedFilters["mitreTechniques"] = request.MitreTechniques;
            }

            // Numeric range filters
            if (request.MinConfidence.HasValue)
            {
                query = query.Where(e => e.Confidence >= request.MinConfidence.Value);
                appliedFilters["minConfidence"] = request.MinConfidence.Value;
            }

            if (request.MaxConfidence.HasValue)
            {
                query = query.Where(e => e.Confidence <= request.MaxConfidence.Value);
                appliedFilters["maxConfidence"] = request.MaxConfidence.Value;
            }

            if (request.MinCorrelationScore.HasValue)
            {
                query = query.Where(e => e.CorrelationScore >= request.MinCorrelationScore.Value);
                appliedFilters["minCorrelationScore"] = request.MinCorrelationScore.Value;
            }

            if (request.MaxCorrelationScore.HasValue)
            {
                query = query.Where(e => e.CorrelationScore <= request.MaxCorrelationScore.Value);
                appliedFilters["maxCorrelationScore"] = request.MaxCorrelationScore.Value;
            }

            if (request.MinBurstScore.HasValue)
            {
                query = query.Where(e => e.BurstScore >= request.MinBurstScore.Value);
                appliedFilters["minBurstScore"] = request.MinBurstScore.Value;
            }

            if (request.MaxBurstScore.HasValue)
            {
                query = query.Where(e => e.BurstScore <= request.MaxBurstScore.Value);
                appliedFilters["maxBurstScore"] = request.MaxBurstScore.Value;
            }

            if (request.MinAnomalyScore.HasValue)
            {
                query = query.Where(e => e.AnomalyScore >= request.MinAnomalyScore.Value);
                appliedFilters["minAnomalyScore"] = request.MinAnomalyScore.Value;
            }

            if (request.MaxAnomalyScore.HasValue)
            {
                query = query.Where(e => e.AnomalyScore <= request.MaxAnomalyScore.Value);
                appliedFilters["maxAnomalyScore"] = request.MaxAnomalyScore.Value;
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            query = ApplySorting(query, request.SortField ?? "Timestamp", request.SortOrder ?? "DESC");

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            query = query.Skip(skip).Take(request.PageSize);

            // Execute query and convert to SecurityEvent objects
            var securityEventEntities = await query.Include(e => e.Application).ToListAsync();
            var securityEvents = securityEventEntities.Select(ConvertEntityToSecurityEvent).ToList();

            stopwatch.Stop();

            _logger.LogInformation(
                "Advanced search completed: {Count} results in {ElapsedMs}ms", 
                securityEvents.Count, 
                stopwatch.ElapsedMilliseconds);

            return new AdvancedSearchResult
            {
                Results = securityEvents,
                TotalCount = totalCount,
                QueryTimeMs = stopwatch.ElapsedMilliseconds,
                AppliedFilters = appliedFilters
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing advanced search");
            throw;
        }
    }

    private async Task<IQueryable<SecurityEventEntity>> ApplyFullTextSearch(
        IQueryable<SecurityEventEntity> query, 
        string searchQuery, 
        bool useExactMatch)
    {
        try
        {
            // Use FTS5 virtual table for full-text search
            var ftsQuery = useExactMatch ? $"\"{searchQuery}\"" : searchQuery;
            
            // Get matching event IDs from FTS5 table
            var matchingIds = await _context.Database
                .SqlQueryRaw<int>(@"
                    SELECT rowid 
                    FROM SecurityEvents_FTS 
                    WHERE SecurityEvents_FTS MATCH {0}
                    ORDER BY rank", ftsQuery)
                .ToListAsync();

            if (matchingIds.Any())
            {
                query = query.Where(e => matchingIds.Contains(e.Id));
            }
            else
            {
                // If no FTS matches, return empty result
                query = query.Where(e => false);
            }

            return query;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTS5 search failed, falling back to LIKE search");
            
            // Fallback to traditional LIKE search if FTS5 fails
            return query.Where(e => 
                (e.Message != null && e.Message.Contains(searchQuery)) ||
                (e.Summary != null && e.Summary.Contains(searchQuery)) ||
                (e.EventData != null && e.EventData.Contains(searchQuery)));
        }
    }

    private IQueryable<SecurityEventEntity> ApplySorting(
        IQueryable<SecurityEventEntity> query, 
        string sortField, 
        string sortOrder)
    {
        var isDescending = sortOrder.Equals("DESC", StringComparison.OrdinalIgnoreCase);

        return sortField.ToLowerInvariant() switch
        {
            "timestamp" => isDescending 
                ? query.OrderByDescending(e => e.Timestamp) 
                : query.OrderBy(e => e.Timestamp),
            "eventtype" => isDescending 
                ? query.OrderByDescending(e => e.EventType) 
                : query.OrderBy(e => e.EventType),
            "risklevel" => isDescending 
                ? query.OrderByDescending(e => e.RiskLevel) 
                : query.OrderBy(e => e.RiskLevel),
            "confidence" => isDescending 
                ? query.OrderByDescending(e => e.Confidence) 
                : query.OrderBy(e => e.Confidence),
            "correlationscore" => isDescending 
                ? query.OrderByDescending(e => e.CorrelationScore) 
                : query.OrderBy(e => e.CorrelationScore),
            "severity" => isDescending 
                ? query.OrderByDescending(e => e.Severity) 
                : query.OrderBy(e => e.Severity),
            _ => isDescending 
                ? query.OrderByDescending(e => e.Timestamp) 
                : query.OrderBy(e => e.Timestamp)
        };
    }

    private SecurityEvent ConvertEntityToSecurityEvent(SecurityEventEntity entity)
    {
        // Convert SecurityEventEntity back to SecurityEvent
        // This is a simplified conversion - in a real implementation, 
        // you'd need to reconstruct the full SecurityEvent with LogEvent
        
        var originalEvent = new LogEvent(
            entity.Timestamp,
            Environment.MachineName, // Simplified - should be stored
            entity.Source ?? "Unknown",
            0, // Simplified - EventId should be stored
            entity.Severity,
            "Unknown", // Simplified - User should be stored
            entity.Message ?? "No message"
        );

        return SecurityEvent.CreateDeterministic(
            originalEvent,
            Enum.TryParse<SecurityEventType>(entity.EventType, true, out var eventType) 
                ? eventType 
                : SecurityEventType.Unknown,
            entity.RiskLevel,
            (int)entity.Confidence,
            entity.Summary ?? "No summary",
            ParseStringArray(entity.MitreTechniques) ?? Array.Empty<string>(),
            ParseStringArray(entity.RecommendedActions) ?? Array.Empty<string>()
        );
    }

    private string[]? ParseStringArray(string? jsonArray)
    {
        if (string.IsNullOrEmpty(jsonArray)) return null;
        
        try
        {
            return JsonSerializer.Deserialize<string[]>(jsonArray);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<string>> GetSearchSuggestionsAsync(string partialQuery, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(partialQuery)) return new List<string>();

        try
        {
            // Get suggestions from recent search terms
            var suggestions = await _context.SecurityEvents
                .Where(e => e.Message != null && e.Message.Contains(partialQuery))
                .Select(e => e.Message!)
                .Distinct()
                .Take(limit)
                .ToListAsync();

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting search suggestions for query: {Query}", partialQuery);
            return new List<string>();
        }
    }

    public async Task<SavedSearch> SaveSearchAsync(string name, AdvancedSearchRequest searchRequest, string userId)
    {
        var savedSearch = new SavedSearch
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            SearchRequest = searchRequest,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };

        // In a real implementation, you'd save this to a database table
        // For now, we'll just return it
        _logger.LogInformation("Saved search '{Name}' for user {UserId}", name, userId);
        
        return savedSearch;
    }

    public async Task<List<SavedSearch>> GetSavedSearchesAsync(string userId)
    {
        // In a real implementation, you'd retrieve from database
        // For now, return empty list
        _logger.LogInformation("Getting saved searches for user {UserId}", userId);
        return new List<SavedSearch>();
    }

    public async Task<bool> DeleteSavedSearchAsync(string savedSearchId, string userId)
    {
        // In a real implementation, you'd delete from database
        _logger.LogInformation("Deleting saved search {SearchId} for user {UserId}", savedSearchId, userId);
        return true;
    }
}
