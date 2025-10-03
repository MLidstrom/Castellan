using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Castellan.Worker.Controllers;

/// <summary>
/// API controller for managing security event detection rules
/// </summary>
[ApiController]
[Route("api/security-event-rules")]
[Authorize]
public class SecurityEventRulesController : ControllerBase
{
    private readonly ISecurityEventRuleStore _ruleStore;
    private readonly ILogger<SecurityEventRulesController> _logger;

    public SecurityEventRulesController(
        ISecurityEventRuleStore ruleStore,
        ILogger<SecurityEventRulesController> logger)
    {
        _ruleStore = ruleStore;
        _logger = logger;
    }

    /// <summary>
    /// Gets all security event rules with optional pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SecurityEventRuleDto>), 200)]
    public async Task<ActionResult<IEnumerable<SecurityEventRuleDto>>> GetAll(
        [FromQuery] bool? enabled = null,
        [FromQuery] int? page = null,
        [FromQuery] int? limit = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null)
    {
        try
        {
            var rules = enabled == true
                ? await _ruleStore.GetAllEnabledRulesAsync()
                : await _ruleStore.GetAllRulesAsync();

            // Apply sorting
            if (!string.IsNullOrEmpty(sort))
            {
                rules = sort.ToLower() switch
                {
                    "eventid" => order?.ToLower() == "desc"
                        ? rules.OrderByDescending(r => r.EventId).ToList()
                        : rules.OrderBy(r => r.EventId).ToList(),
                    "priority" => order?.ToLower() == "desc"
                        ? rules.OrderByDescending(r => r.Priority).ToList()
                        : rules.OrderBy(r => r.Priority).ToList(),
                    "risklevel" => order?.ToLower() == "desc"
                        ? rules.OrderByDescending(r => r.RiskLevel).ToList()
                        : rules.OrderBy(r => r.RiskLevel).ToList(),
                    "confidence" => order?.ToLower() == "desc"
                        ? rules.OrderByDescending(r => r.Confidence).ToList()
                        : rules.OrderBy(r => r.Confidence).ToList(),
                    _ => rules
                };
            }

            // Apply pagination if requested
            if (page.HasValue && limit.HasValue)
            {
                var skip = (page.Value - 1) * limit.Value;
                rules = rules.Skip(skip).Take(limit.Value).ToList();
            }

            var dtos = rules.Select(MapToDto);

            // Add total count header for pagination
            var totalCount = enabled == true
                ? (await _ruleStore.GetAllEnabledRulesAsync()).Count
                : (await _ruleStore.GetAllRulesAsync()).Count;

            Response.Headers["X-Total-Count"] = totalCount.ToString();
            Response.Headers["Access-Control-Expose-Headers"] = "X-Total-Count";

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security event rules: {ErrorMessage}", ex.Message);
            return StatusCode(500, $"Error retrieving security event rules: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a specific security event rule by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SecurityEventRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SecurityEventRuleDto>> GetById(int id)
    {
        try
        {
            var rules = await _ruleStore.GetAllRulesAsync();
            var rule = rules.FirstOrDefault(r => r.Id == id);

            if (rule == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(rule));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security event rule {RuleId}", id);
            return StatusCode(500, "Error retrieving security event rule");
        }
    }

    /// <summary>
    /// Gets rules for a specific event ID and channel
    /// </summary>
    [HttpGet("event/{eventId}/channel/{channel}")]
    [ProducesResponseType(typeof(SecurityEventRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SecurityEventRuleDto>> GetByEventAndChannel(
        int eventId,
        string channel)
    {
        try
        {
            var rule = await _ruleStore.GetRuleAsync(eventId, channel);

            if (rule == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(rule));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rule for Event {EventId} on {Channel}", eventId, channel);
            return StatusCode(500, "Error retrieving security event rule");
        }
    }

    /// <summary>
    /// Creates a new security event rule
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SecurityEventRuleDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<SecurityEventRuleDto>> Create(
        [FromBody] CreateSecurityEventRuleRequest request)
    {
        try
        {
            var entity = new SecurityEventRuleEntity
            {
                EventId = request.EventId,
                Channel = request.Channel,
                EventType = request.EventType,
                RiskLevel = request.RiskLevel,
                Confidence = request.Confidence,
                Summary = request.Summary,
                MitreTechniques = JsonSerializer.Serialize(request.MitreTechniques),
                RecommendedActions = JsonSerializer.Serialize(request.RecommendedActions),
                IsEnabled = request.IsEnabled ?? true,
                Priority = request.Priority ?? 100,
                Description = request.Description,
                Tags = request.Tags != null ? JsonSerializer.Serialize(request.Tags) : null,
                ModifiedBy = User.Identity?.Name ?? "Unknown"
            };

            var created = await _ruleStore.CreateRuleAsync(entity);
            _logger.LogInformation("Created security event rule {RuleId} for Event {EventId} on {Channel}",
                created.Id, created.EventId, created.Channel);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating security event rule");
            return StatusCode(500, "Error creating security event rule");
        }
    }

    /// <summary>
    /// Updates an existing security event rule
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SecurityEventRuleDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<SecurityEventRuleDto>> Update(
        int id,
        [FromBody] UpdateSecurityEventRuleRequest request)
    {
        try
        {
            var rules = await _ruleStore.GetAllRulesAsync();
            var existing = rules.FirstOrDefault(r => r.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            // Update fields
            existing.EventId = request.EventId ?? existing.EventId;
            existing.Channel = request.Channel ?? existing.Channel;
            existing.EventType = request.EventType ?? existing.EventType;
            existing.RiskLevel = request.RiskLevel ?? existing.RiskLevel;
            existing.Confidence = request.Confidence ?? existing.Confidence;
            existing.Summary = request.Summary ?? existing.Summary;
            existing.MitreTechniques = request.MitreTechniques != null
                ? JsonSerializer.Serialize(request.MitreTechniques)
                : existing.MitreTechniques;
            existing.RecommendedActions = request.RecommendedActions != null
                ? JsonSerializer.Serialize(request.RecommendedActions)
                : existing.RecommendedActions;
            existing.IsEnabled = request.IsEnabled ?? existing.IsEnabled;
            existing.Priority = request.Priority ?? existing.Priority;
            existing.Description = request.Description ?? existing.Description;
            existing.Tags = request.Tags != null
                ? JsonSerializer.Serialize(request.Tags)
                : existing.Tags;
            existing.ModifiedBy = User.Identity?.Name ?? "Unknown";

            var updated = await _ruleStore.UpdateRuleAsync(existing);
            _logger.LogInformation("Updated security event rule {RuleId}", id);

            return Ok(MapToDto(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating security event rule {RuleId}", id);
            return StatusCode(500, "Error updating security event rule");
        }
    }

    /// <summary>
    /// Deletes a security event rule
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var rules = await _ruleStore.GetAllRulesAsync();
            var existing = rules.FirstOrDefault(r => r.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            await _ruleStore.DeleteRuleAsync(id);
            _logger.LogInformation("Deleted security event rule {RuleId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting security event rule {RuleId}", id);
            return StatusCode(500, "Error deleting security event rule");
        }
    }

    /// <summary>
    /// Refreshes the rule cache
    /// </summary>
    [HttpPost("refresh-cache")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    public async Task<ActionResult> RefreshCache()
    {
        try
        {
            await _ruleStore.RefreshCacheAsync();
            _logger.LogInformation("Security event rules cache refreshed");
            return Ok(new { message = "Cache refreshed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache");
            return StatusCode(500, "Error refreshing cache");
        }
    }

    private static SecurityEventRuleDto MapToDto(SecurityEventRuleEntity entity)
    {
        return new SecurityEventRuleDto
        {
            Id = entity.Id,
            EventId = entity.EventId,
            Channel = entity.Channel,
            EventType = entity.EventType,
            RiskLevel = entity.RiskLevel,
            Confidence = entity.Confidence,
            Summary = entity.Summary,
            MitreTechniques = JsonSerializer.Deserialize<string[]>(entity.MitreTechniques) ?? Array.Empty<string>(),
            RecommendedActions = JsonSerializer.Deserialize<string[]>(entity.RecommendedActions) ?? Array.Empty<string>(),
            IsEnabled = entity.IsEnabled,
            Priority = entity.Priority,
            Description = entity.Description,
            Tags = entity.Tags != null ? JsonSerializer.Deserialize<string[]>(entity.Tags) : null,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            ModifiedBy = entity.ModifiedBy
        };
    }
}

/// <summary>
/// DTO for security event rules
/// </summary>
public class SecurityEventRuleDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string[] MitreTechniques { get; set; } = Array.Empty<string>();
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string[]? Tags { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>
/// Request model for creating security event rules
/// </summary>
public class CreateSecurityEventRuleRequest
{
    public int EventId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string[] MitreTechniques { get; set; } = Array.Empty<string>();
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
    public bool? IsEnabled { get; set; }
    public int? Priority { get; set; }
    public string? Description { get; set; }
    public string[]? Tags { get; set; }
}

/// <summary>
/// Request model for updating security event rules
/// </summary>
public class UpdateSecurityEventRuleRequest
{
    public int? EventId { get; set; }
    public string? Channel { get; set; }
    public string? EventType { get; set; }
    public string? RiskLevel { get; set; }
    public int? Confidence { get; set; }
    public string? Summary { get; set; }
    public string[]? MitreTechniques { get; set; }
    public string[]? RecommendedActions { get; set; }
    public bool? IsEnabled { get; set; }
    public int? Priority { get; set; }
    public string? Description { get; set; }
    public string[]? Tags { get; set; }
}
