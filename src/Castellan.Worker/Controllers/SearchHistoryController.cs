using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Controllers;

/// <summary>
/// API controller for managing search history
/// </summary>
[ApiController]
[Route("api/search-history")]
[Authorize]
public class SearchHistoryController : ControllerBase
{
    private readonly ISearchHistoryService _searchHistoryService;
    private readonly ILogger<SearchHistoryController> _logger;

    public SearchHistoryController(
        ISearchHistoryService searchHistoryService,
        ILogger<SearchHistoryController> logger)
    {
        _searchHistoryService = searchHistoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get search history for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSearchHistory([FromQuery] int limit = 20)
    {
        try
        {
            if (limit < 1 || limit > 100)
            {
                return BadRequest(new { message = "Limit must be between 1 and 100" });
            }

            var userId = GetCurrentUserId();
            var history = await _searchHistoryService.GetUserSearchHistoryAsync(userId, limit);

            var response = history.Select(ConvertToDto).ToList();

            return Ok(new { data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search history");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get search history statistics for the current user
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetSearchHistoryStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            var stats = await _searchHistoryService.GetSearchHistoryStatsAsync(userId);

            return Ok(new { data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search history stats");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Clear all search history for the current user
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearSearchHistory()
    {
        try
        {
            var userId = GetCurrentUserId();
            await _searchHistoryService.ClearUserSearchHistoryAsync(userId);

            return Ok(new { message = "Search history cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing search history");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a specific search history entry
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteHistoryEntry(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _searchHistoryService.DeleteSearchHistoryEntryAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Search history entry not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting search history entry: {EntryId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Manually add a search to history (for testing or migration purposes)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddSearchToHistory([FromBody] AddSearchHistoryRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var historyEntry = await _searchHistoryService.AddSearchToHistoryAsync(
                userId, request.Filters, request.ResultCount, request.ExecutionTimeMs);

            return CreatedAtAction(nameof(GetSearchHistory), null, 
                new { data = ConvertToDto(historyEntry) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding search to history");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Administrative endpoint to cleanup old history entries
    /// </summary>
    [HttpPost("cleanup")]
    [Authorize(Roles = "Admin")] // Restrict to admin users
    public async Task<IActionResult> CleanupOldHistory([FromBody] CleanupHistoryRequest request)
    {
        try
        {
            var maxAge = TimeSpan.FromDays(request.MaxAgeDays);
            var deletedCount = await _searchHistoryService.CleanupOldHistoryEntriesAsync(maxAge);

            return Ok(new { 
                message = "Cleanup completed successfully",
                deletedEntries = deletedCount 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during history cleanup");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    private static object ConvertToDto(SearchHistoryEntity entity)
    {
        AdvancedSearchRequest? filters = null;
        try
        {
            filters = JsonSerializer.Deserialize<AdvancedSearchRequest>(entity.SearchFilters);
        }
        catch (JsonException)
        {
            // Handle corrupted filter data gracefully
        }

        return new
        {
            id = entity.Id,
            filters = filters,
            searchHash = entity.SearchHash,
            resultCount = entity.ResultCount,
            executionTimeMs = entity.ExecutionTimeMs,
            createdAt = entity.CreatedAt.ToString("O")
        };
    }
}

/// <summary>
/// Request model for manually adding search to history
/// </summary>
public class AddSearchHistoryRequest
{
    public AdvancedSearchRequest Filters { get; set; } = new();
    public int? ResultCount { get; set; }
    public int? ExecutionTimeMs { get; set; }
}

/// <summary>
/// Request model for cleanup old history entries
/// </summary>
public class CleanupHistoryRequest
{
    public int MaxAgeDays { get; set; } = 30;
}