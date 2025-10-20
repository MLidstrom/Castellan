using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Castellan.Worker.Controllers;

/// <summary>
/// Controller for managing notification templates
/// </summary>
[ApiController]
[Route("api/notification-templates")]
[Authorize]
public class NotificationTemplateController : ControllerBase
{
    private readonly INotificationTemplateStore _store;
    private readonly ITemplateRenderer _renderer;
    private readonly ILogger<NotificationTemplateController> _logger;

    public NotificationTemplateController(
        INotificationTemplateStore store,
        ITemplateRenderer renderer,
        ILogger<NotificationTemplateController> logger)
    {
        _store = store;
        _renderer = renderer;
        _logger = logger;
    }

    /// <summary>
    /// Gets all notification templates
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationTemplate>>> GetAll()
    {
        try
        {
            var templates = await _store.GetAllAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification templates");
            return StatusCode(500, new { error = "Failed to retrieve notification templates" });
        }
    }

    /// <summary>
    /// Gets a specific notification template by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationTemplate>> GetById(string id)
    {
        try
        {
            var template = await _store.GetByIdAsync(id);

            if (template == null)
            {
                return NotFound(new { error = $"Template {id} not found" });
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {Id}", id);
            return StatusCode(500, new { error = "Failed to retrieve notification template" });
        }
    }

    /// <summary>
    /// Gets templates filtered by platform
    /// </summary>
    [HttpGet("platform/{platform}")]
    public async Task<ActionResult<IEnumerable<NotificationTemplate>>> GetByPlatform(NotificationPlatform platform)
    {
        try
        {
            var templates = await _store.GetByPlatformAsync(platform);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for platform {Platform}", platform);
            return StatusCode(500, new { error = "Failed to retrieve notification templates" });
        }
    }

    /// <summary>
    /// Gets templates filtered by type
    /// </summary>
    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<NotificationTemplate>>> GetByType(NotificationTemplateType type)
    {
        try
        {
            var templates = await _store.GetByTypeAsync(type);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for type {Type}", type);
            return StatusCode(500, new { error = "Failed to retrieve notification templates" });
        }
    }

    /// <summary>
    /// Gets the enabled template for a specific platform and type
    /// </summary>
    [HttpGet("enabled/{platform}/{type}")]
    public async Task<ActionResult<NotificationTemplate>> GetEnabledTemplate(
        NotificationPlatform platform,
        NotificationTemplateType type)
    {
        try
        {
            var template = await _store.GetEnabledTemplateAsync(platform, type);

            if (template == null)
            {
                return NotFound(new
                {
                    error = $"No enabled template found for {platform}/{type}"
                });
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving enabled template for {Platform}/{Type}",
                platform,
                type);

            return StatusCode(500, new { error = "Failed to retrieve notification template" });
        }
    }

    /// <summary>
    /// Creates a new notification template
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<NotificationTemplate>> Create([FromBody] NotificationTemplate template)
    {
        try
        {
            // Validate template content
            var validation = _renderer.Validate(template.TemplateContent);

            if (!validation.IsValid)
            {
                return BadRequest(new
                {
                    error = "Template validation failed",
                    errors = validation.Errors,
                    warnings = validation.Warnings
                });
            }

            var created = await _store.CreateAsync(template);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification template");
            return StatusCode(500, new { error = "Failed to create notification template" });
        }
    }

    /// <summary>
    /// Updates an existing notification template
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<NotificationTemplate>> Update(
        string id,
        [FromBody] NotificationTemplate template)
    {
        try
        {
            if (id != template.Id)
            {
                return BadRequest(new { error = "ID mismatch" });
            }

            // Validate template content
            var validation = _renderer.Validate(template.TemplateContent);

            if (!validation.IsValid)
            {
                return BadRequest(new
                {
                    error = "Template validation failed",
                    errors = validation.Errors,
                    warnings = validation.Warnings
                });
            }

            var updated = await _store.UpdateAsync(template);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {Id}", id);
            return StatusCode(500, new { error = "Failed to update notification template" });
        }
    }

    /// <summary>
    /// Deletes a notification template
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            var deleted = await _store.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound(new { error = $"Template {id} not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {Id}", id);
            return StatusCode(500, new { error = "Failed to delete notification template" });
        }
    }

    /// <summary>
    /// Validates template syntax without saving
    /// </summary>
    [HttpPost("validate")]
    public ActionResult<TemplateValidationResult> Validate([FromBody] ValidateRequest request)
    {
        try
        {
            var result = _renderer.Validate(request.TemplateContent);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template");
            return StatusCode(500, new { error = "Failed to validate template" });
        }
    }

    /// <summary>
    /// Gets all supported template tags
    /// </summary>
    [HttpGet("tags")]
    public ActionResult<IEnumerable<string>> GetSupportedTags()
    {
        try
        {
            var tags = _renderer.GetSupportedTags();
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supported tags");
            return StatusCode(500, new { error = "Failed to retrieve supported tags" });
        }
    }

    /// <summary>
    /// Renders a template with sample data for preview
    /// </summary>
    [HttpPost("preview")]
    public ActionResult<PreviewResponse> Preview([FromBody] PreviewRequest request)
    {
        try
        {
            // Validate first
            var validation = _renderer.Validate(request.TemplateContent);

            if (!validation.IsValid)
            {
                return BadRequest(new
                {
                    error = "Template validation failed",
                    errors = validation.Errors,
                    warnings = validation.Warnings
                });
            }

            // Create temporary template for rendering
            var template = new NotificationTemplate
            {
                Platform = request.Platform,
                TemplateContent = request.TemplateContent
            };

            // Use sample context
            var context = GetSampleContext();

            var rendered = _renderer.Render(template, context);

            return Ok(new PreviewResponse
            {
                RenderedContent = rendered,
                Validation = validation
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing template");
            return StatusCode(500, new { error = "Failed to preview template" });
        }
    }

    /// <summary>
    /// Creates default templates (Admin only)
    /// </summary>
    [HttpPost("create-defaults")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CreateDefaults()
    {
        try
        {
            await _store.CreateDefaultTemplatesAsync();
            return Ok(new { message = "Default templates created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating default templates");
            return StatusCode(500, new { error = "Failed to create default templates" });
        }
    }

    private static Dictionary<string, string> GetSampleContext()
    {
        return new Dictionary<string, string>
        {
            ["DATE"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["HOST"] = "WORKSTATION-01",
            ["USER"] = "john.doe",
            ["EVENT_ID"] = "4625",
            ["SEVERITY"] = "High",
            ["SUMMARY"] = "Failed login attempt detected from suspicious IP address",
            ["MITRE_TECHNIQUES"] = "T1110 - Brute Force",
            ["RECOMMENDED_ACTIONS"] = "Review login attempts, verify user credentials, check firewall rules",
            ["DETAILS_URL"] = "http://localhost:3000/security-events/12345",
            ["EVENT_TYPE"] = "Security Event",
            ["MACHINE_NAME"] = "WORKSTATION-01",
            ["TIMESTAMP"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["ALERT_ID"] = "ALT-2025-001"
        };
    }
}

/// <summary>
/// Request model for template validation
/// </summary>
public class ValidateRequest
{
    public string TemplateContent { get; set; } = string.Empty;
}

/// <summary>
/// Request model for template preview
/// </summary>
public class PreviewRequest
{
    public NotificationPlatform Platform { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
}

/// <summary>
/// Response model for template preview
/// </summary>
public class PreviewResponse
{
    public string RenderedContent { get; set; } = string.Empty;
    public TemplateValidationResult Validation { get; set; } = new();
}
