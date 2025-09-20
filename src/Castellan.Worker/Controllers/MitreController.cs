using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Castellan.Worker.Services;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MitreController : ControllerBase
{
    private readonly MitreAttackImportService _importService;
    private readonly MitreService _mitreService;
    private readonly ILogger<MitreController> _logger;

    public MitreController(
        MitreAttackImportService importService,
        MitreService mitreService,
        ILogger<MitreController> logger)
    {
        _importService = importService;
        _mitreService = mitreService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current count of MITRE techniques in the database
    /// </summary>
    [HttpGet("count")]
    public async Task<ActionResult<MitreTechniqueCountResponse>> GetTechniqueCount()
    {
        try
        {
            var count = await _importService.GetTechniqueCountAsync();
            var shouldImport = await _importService.ShouldImportTechniquesAsync();
            
            return Ok(new MitreTechniqueCountResponse
            {
                Count = count,
                ShouldImport = shouldImport,
                LastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting MITRE technique count");
            return StatusCode(500, new { error = "Failed to get technique count" });
        }
    }

    /// <summary>
    /// Imports all MITRE ATT&CK techniques from the official source
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> ImportTechniques()
    {
        try
        {
            _logger.LogInformation("Starting MITRE ATT&CK import via API request");
            
            var result = await _importService.ImportAllTechniquesAsync();
            
            if (result.HasErrors)
            {
                _logger.LogWarning("MITRE import completed with {ErrorCount} errors", result.Errors.Count);
                return Ok(new 
                {
                    success = true,
                    message = $"Import completed with {result.Errors.Count} errors",
                    result = result
                });
            }
            
            return Ok(new 
            {
                success = true,
                message = $"Successfully imported {result.TechniquesImported} new techniques and updated {result.TechniquesUpdated} existing techniques",
                result = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import MITRE techniques via API");
            return StatusCode(500, new { error = "Import failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets all MITRE techniques with pagination (React Admin compatible)
    /// </summary>
    [HttpGet("techniques")]
    public async Task<ActionResult<MitreTechniqueListResponse>> GetTechniques(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        [FromQuery] string? tactic = null,
        [FromQuery] string? search = null)
    {
        try
        {
            // React Admin uses 'limit' parameter, fallback to pageSize
            var actualPageSize = limit > 0 ? limit : pageSize;
            if (actualPageSize > 100) actualPageSize = 100; // Limit page size
            if (page < 1) page = 1;
            
            List<Models.MitreTechnique> techniques;
            
            if (!string.IsNullOrEmpty(tactic))
            {
                techniques = await _mitreService.GetTechniquesByTacticAsync(tactic);
            }
            else if (!string.IsNullOrEmpty(search))
            {
                techniques = await _mitreService.SearchTechniquesAsync(search);
            }
            else
            {
                techniques = await _mitreService.GetAllTechniquesAsync();
            }
            
            // Apply sorting
            if (!string.IsNullOrEmpty(sort))
            {
                var isDescending = order?.ToLowerInvariant() == "desc";
                techniques = sort.ToLowerInvariant() switch
                {
                    "techniqueid" => isDescending ? techniques.OrderByDescending(t => t.TechniqueId).ToList()
                                                  : techniques.OrderBy(t => t.TechniqueId).ToList(),
                    "name" => isDescending ? techniques.OrderByDescending(t => t.Name).ToList()
                                           : techniques.OrderBy(t => t.Name).ToList(),
                    "tactic" => isDescending ? techniques.OrderByDescending(t => t.Tactic).ToList()
                                             : techniques.OrderBy(t => t.Tactic).ToList(),
                    "platform" => isDescending ? techniques.OrderByDescending(t => t.Platform).ToList()
                                                : techniques.OrderBy(t => t.Platform).ToList(),
                    "createdat" => isDescending ? techniques.OrderByDescending(t => t.CreatedAt).ToList()
                                                : techniques.OrderBy(t => t.CreatedAt).ToList(),
                    _ => techniques.OrderBy(t => t.TechniqueId).ToList()
                };
            }

            var totalCount = techniques.Count;
            var pagedTechniques = techniques
                .Skip((page - 1) * actualPageSize)
                .Take(actualPageSize)
                .Select(t => new MitreTechniqueDto
                {
                    Id = t.Id,
                    TechniqueId = t.TechniqueId,
                    Name = t.Name,
                    Description = t.Description,
                    Tactic = t.Tactic,
                    Platform = t.Platform,
                    CreatedAt = t.CreatedAt
                })
                .ToList();
            
            return Ok(new MitreTechniqueListResponse
            {
                Techniques = pagedTechniques,
                TotalCount = totalCount,
                Page = page,
                PageSize = actualPageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / actualPageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MITRE techniques");
            return StatusCode(500, new { error = "Failed to retrieve techniques" });
        }
    }

    /// <summary>
    /// Gets a specific MITRE technique by ID
    /// </summary>
    [HttpGet("techniques/{techniqueId}")]
    public async Task<ActionResult<MitreTechniqueDetailDto>> GetTechnique(string techniqueId)
    {
        try
        {
            var technique = await _mitreService.GetTechniqueByIdAsync(techniqueId);
            
            if (technique == null)
            {
                return NotFound(new { error = $"Technique {techniqueId} not found" });
            }
            
            return Ok(new MitreTechniqueDetailDto
            {
                Id = technique.Id,
                TechniqueId = technique.TechniqueId,
                Name = technique.Name,
                Description = technique.Description,
                Tactic = technique.Tactic,
                Platform = technique.Platform,
                DataSources = technique.DataSources,
                Mitigations = technique.Mitigations,
                Examples = technique.Examples,
                CreatedAt = technique.CreatedAt,
                AssociatedApplications = technique.ApplicationAssociations?.Select(a => (object)new
                {
                    ApplicationId = a.ApplicationId,
                    ApplicationName = a.Application?.Name,
                    Confidence = a.Confidence,
                    Notes = a.Notes
                }).ToList() ?? new List<object>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MITRE technique {TechniqueId}", techniqueId);
            return StatusCode(500, new { error = "Failed to retrieve technique details" });
        }
    }

    /// <summary>
    /// Gets all available tactics
    /// </summary>
    [HttpGet("tactics")]
    public async Task<ActionResult<List<string>>> GetTactics()
    {
        try
        {
            var tactics = await _mitreService.GetAllTacticsAsync();
            return Ok(tactics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MITRE tactics");
            return StatusCode(500, new { error = "Failed to retrieve tactics" });
        }
    }

    /// <summary>
    /// Gets technique counts by tactic
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<MitreStatisticsResponse>> GetStatistics()
    {
        try
        {
            var tacticCounts = await _mitreService.GetTechniqueCountsByTacticAsync();
            var totalTechniques = await _importService.GetTechniqueCountAsync();
            
            return Ok(new MitreStatisticsResponse
            {
                TotalTechniques = totalTechniques,
                TechniquesByTactic = tacticCounts,
                LastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MITRE statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    /// <summary>
    /// Diagnostic endpoint to check database content
    /// </summary>
    [HttpGet("debug/sample")]
    [AllowAnonymous]
    public async Task<ActionResult> GetSampleTechniques()
    {
        try
        {
            var sampleTechniques = await _mitreService.GetAllTechniquesAsync();
            var sample = sampleTechniques.Take(10).Select(t => new
            {
                t.TechniqueId,
                t.Name,
                t.Tactic,
                t.Platform,
                DescriptionLength = t.Description?.Length ?? 0,
                t.CreatedAt
            }).ToList();

            return Ok(new
            {
                TotalCount = sampleTechniques.Count,
                SampleTechniques = sample,
                EmptyTacticCount = sampleTechniques.Count(t => string.IsNullOrEmpty(t.Tactic)),
                EmptyPlatformCount = sampleTechniques.Count(t => string.IsNullOrEmpty(t.Platform))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sample MITRE techniques");
            return StatusCode(500, new { error = "Failed to retrieve sample techniques" });
        }
    }
}

// DTOs for API responses
public class MitreTechniqueCountResponse
{
    public int Count { get; set; }
    public bool ShouldImport { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class MitreTechniqueListResponse
{
    public List<MitreTechniqueDto> Techniques { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class MitreTechniqueDto
{
    public int Id { get; set; }
    public string TechniqueId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tactic { get; set; }
    public string? Platform { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MitreTechniqueDetailDto : MitreTechniqueDto
{
    public string? DataSources { get; set; }
    public string? Mitigations { get; set; }
    public string? Examples { get; set; }
    public List<object> AssociatedApplications { get; set; } = new();
}

public class MitreStatisticsResponse
{
    public int TotalTechniques { get; set; }
    public Dictionary<string, int> TechniquesByTactic { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}
