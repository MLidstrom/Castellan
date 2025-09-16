using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Controllers;

/// <summary>
/// API controller for managing saved searches
/// </summary>
[ApiController]
[Route("api/saved-searches")]
[Authorize]
public class SavedSearchesController : ControllerBase
{
    private readonly ISavedSearchService _savedSearchService;
    private readonly ILogger<SavedSearchesController> _logger;

    public SavedSearchesController(
        ISavedSearchService savedSearchService,
        ILogger<SavedSearchesController> logger)
    {
        _savedSearchService = savedSearchService;
        _logger = logger;
    }

    /// <summary>
    /// Get all saved searches for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSavedSearches()
    {
        try
        {
            var userId = GetCurrentUserId();
            var savedSearches = await _savedSearchService.GetUserSavedSearchesAsync(userId);

            var response = savedSearches.Select(ConvertToDto).ToList();

            return Ok(new { data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting saved searches");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a specific saved search
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetSavedSearch(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var savedSearch = await _savedSearchService.GetSavedSearchAsync(id, userId);

            if (savedSearch == null)
            {
                return NotFound(new { message = "Saved search not found" });
            }

            return Ok(new { data = ConvertToDto(savedSearch) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting saved search: {SearchId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new saved search
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateSavedSearch([FromBody] CreateSavedSearchRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var savedSearch = await _savedSearchService.CreateSavedSearchAsync(
                userId, request.Name, request.Description, request.Filters, request.Tags);

            return CreatedAtAction(nameof(GetSavedSearch), new { id = savedSearch.Id }, 
                new { data = ConvertToDto(savedSearch) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating saved search: {Name}", request.Name);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update an existing saved search
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSavedSearch(int id, [FromBody] UpdateSavedSearchRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var savedSearch = await _savedSearchService.UpdateSavedSearchAsync(
                id, userId, request.Name, request.Description, request.Filters, request.Tags);

            return Ok(new { data = ConvertToDto(savedSearch) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating saved search: {SearchId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a saved search
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSavedSearch(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _savedSearchService.DeleteSavedSearchAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Saved search not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting saved search: {SearchId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Record usage of a saved search (increment use count)
    /// </summary>
    [HttpPost("{id}/use")]
    public async Task<IActionResult> RecordSearchUsage(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _savedSearchService.RecordSearchUsageAsync(id, userId);

            if (!success)
            {
                return NotFound(new { message = "Saved search not found" });
            }

            return Ok(new { message = "Usage recorded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording usage for saved search: {SearchId}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get most frequently used saved searches
    /// </summary>
    [HttpGet("most-used")]
    public async Task<IActionResult> GetMostUsedSearches([FromQuery] int limit = 5)
    {
        try
        {
            var userId = GetCurrentUserId();
            var searches = await _savedSearchService.GetMostUsedSearchesAsync(userId, limit);

            var response = searches.Select(ConvertToDto).ToList();

            return Ok(new { data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting most used searches");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Search saved searches by name, description, or tags
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchSavedSearches([FromQuery] string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { message = "Search query (q) parameter is required" });
            }

            var userId = GetCurrentUserId();
            var searches = await _savedSearchService.SearchSavedSearchesAsync(userId, q);

            var response = searches.Select(ConvertToDto).ToList();

            return Ok(new { data = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching saved searches with query: {Query}", q);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               throw new UnauthorizedAccessException("User ID not found in token");
    }

    private static object ConvertToDto(SavedSearchEntity entity)
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
            name = entity.Name,
            description = entity.Description,
            filters = filters,
            isPublic = entity.IsPublic,
            createdAt = entity.CreatedAt.ToString("O"),
            updatedAt = entity.UpdatedAt.ToString("O"),
            lastUsedAt = entity.LastUsedAt?.ToString("O"),
            useCount = entity.UseCount,
            tags = !string.IsNullOrEmpty(entity.Tags) 
                ? entity.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>()
        };
    }
}

/// <summary>
/// Request model for creating a saved search
/// </summary>
public class CreateSavedSearchRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AdvancedSearchRequest Filters { get; set; } = new();
    public string[]? Tags { get; set; }
}

/// <summary>
/// Request model for updating a saved search
/// </summary>
public class UpdateSavedSearchRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AdvancedSearchRequest Filters { get; set; } = new();
    public string[]? Tags { get; set; }
}