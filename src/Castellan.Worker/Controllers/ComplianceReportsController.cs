using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Services.Compliance;
using Castellan.Worker.Data;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/compliance-reports")]
[Authorize]
public class ComplianceReportsController : ControllerBase
{
    private readonly ILogger<ComplianceReportsController> _logger;
    private readonly IComplianceAssessmentService _assessmentService;
    private readonly IComplianceFrameworkService _frameworkService;
    private readonly CastellanDbContext _context;

    public ComplianceReportsController(
        ILogger<ComplianceReportsController> logger,
        IComplianceAssessmentService assessmentService,
        IComplianceFrameworkService frameworkService,
        CastellanDbContext context)
    {
        _logger = logger;
        _assessmentService = assessmentService;
        _frameworkService = frameworkService;
        _context = context;
    }

    [HttpGet("available-frameworks")]
    public async Task<IActionResult> GetAvailableFrameworks()
    {
        try
        {
            // Only return Organization-scope frameworks that are visible to users
            var frameworks = await _frameworkService.GetAvailableFrameworksAsync();
            _logger.LogInformation("Returning {Count} user-visible frameworks: {Frameworks}",
                frameworks.Count, string.Join(", ", frameworks));
            return Ok(new { frameworks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available frameworks");
            return StatusCode(500, new { message = "Error getting available frameworks", error = ex.Message });
        }
    }

    [HttpGet("debug/all-frameworks")]
    public async Task<IActionResult> GetAllFrameworks()
    {
        try
        {
            // Debug endpoint that shows all frameworks (including Application-scope)
            var organizationFrameworks = await _frameworkService.GetOrganizationFrameworksAsync();
            var applicationFrameworks = await _frameworkService.GetApplicationFrameworksAsync();

            return Ok(new {
                organization_frameworks = organizationFrameworks,
                application_frameworks = applicationFrameworks,
                total = organizationFrameworks.Count + applicationFrameworks.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all frameworks");
            return StatusCode(500, new { message = "Error getting all frameworks", error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int? perPage = null,
        [FromQuery] int? limit = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? framework = null,
        [FromQuery] string? reportType = null,
        [FromQuery] string? status = null)
    {
        try
        {
            int pageSize = perPage ?? limit ?? 10;

            _logger.LogInformation("Getting compliance reports - page: {Page}, pageSize: {PageSize}, framework: {Framework}",
                page, pageSize, framework);

            var query = _context.ComplianceReports.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(framework))
            {
                query = query.Where(r => r.Framework.Contains(framework));
            }
            if (!string.IsNullOrEmpty(reportType))
            {
                query = query.Where(r => r.ReportType.Contains(reportType));
            }
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var total = await query.CountAsync();
            var reports = await query
                .OrderByDescending(r => r.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ComplianceReportDto
                {
                    Id = r.Id,
                    Framework = r.Framework,
                    ReportType = r.ReportType,
                    Status = r.Status,
                    CreatedDate = r.CreatedDate,
                    Generated = r.Generated,
                    ValidUntil = r.ValidUntil,
                    GeneratedBy = r.GeneratedBy,
                    Version = r.Version,
                    ImplementationPercentage = r.ImplementationPercentage,
                    TotalControls = r.TotalControls,
                    ImplementedControls = r.ImplementedControls,
                    FailedControls = r.FailedControls,
                    GapCount = r.GapCount,
                    RiskScore = r.RiskScore,
                    Summary = r.Summary,
                    KeyFindings = r.KeyFindings,
                    Recommendations = r.Recommendations,
                    NextReview = r.NextReview
                })
                .ToListAsync();

            var response = new
            {
                data = reports,
                total,
                page,
                perPage = pageSize
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance reports list");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(string id)
    {
        try
        {
            _logger.LogInformation("Getting compliance report: {Id}", id);

            var report = await _context.ComplianceReports
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound(new { message = "Compliance report not found" });
            }

            var reportDto = new ComplianceReportDto
            {
                Id = report.Id,
                Framework = report.Framework,
                ReportType = report.ReportType,
                Status = report.Status,
                CreatedDate = report.CreatedDate,
                Generated = report.Generated,
                ValidUntil = report.ValidUntil,
                GeneratedBy = report.GeneratedBy,
                Version = report.Version,
                ImplementationPercentage = report.ImplementationPercentage,
                TotalControls = report.TotalControls,
                ImplementedControls = report.ImplementedControls,
                FailedControls = report.FailedControls,
                GapCount = report.GapCount,
                RiskScore = report.RiskScore,
                Summary = report.Summary,
                KeyFindings = report.KeyFindings,
                Recommendations = report.Recommendations,
                NextReview = report.NextReview
            };

            return Ok(new { data = reportDto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance report: {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ComplianceReportCreateRequest request)
    {
        try
        {
            _logger.LogInformation("Creating compliance report: {Framework} - {ReportType}", request.Framework, request.ReportType);

            // Validate that the framework is user-visible (Organization-scope only)
            var isUserVisible = await _frameworkService.IsFrameworkUserVisibleAsync(request.Framework);
            if (!isUserVisible)
            {
                _logger.LogWarning("Attempted to create report for non-user-visible framework: {Framework}", request.Framework);
                return BadRequest(new { message = $"Framework '{request.Framework}' is not available for user-generated reports" });
            }

            var report = await _assessmentService.GenerateReportAsync(request.Framework, request.ReportType);

            var reportDto = new ComplianceReportDto
            {
                Id = report.Id,
                Framework = report.Framework,
                ReportType = report.ReportType,
                Status = report.Status,
                CreatedDate = report.CreatedDate,
                Generated = report.Generated,
                ValidUntil = report.ValidUntil,
                GeneratedBy = report.GeneratedBy,
                Version = report.Version,
                ImplementationPercentage = report.ImplementationPercentage,
                TotalControls = report.TotalControls,
                ImplementedControls = report.ImplementedControls,
                FailedControls = report.FailedControls,
                GapCount = report.GapCount,
                RiskScore = report.RiskScore,
                Summary = report.Summary,
                KeyFindings = report.KeyFindings,
                Recommendations = report.Recommendations,
                NextReview = report.NextReview
            };

            return Ok(new { data = reportDto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating compliance report");
            return StatusCode(500, new { message = ex.Message });
        }
    }

}

// DTOs
public class ComplianceReportCreateRequest
{
    public string Framework { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Scope { get; set; } = "full";
    public string Priority { get; set; } = "medium";
    public string Notes { get; set; } = string.Empty;
}

public class ComplianceReportDto
{
    public string Id { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime Generated { get; set; }
    public DateTime ValidUntil { get; set; }
    public string GeneratedBy { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int ImplementationPercentage { get; set; }
    public int TotalControls { get; set; }
    public int ImplementedControls { get; set; }
    public int FailedControls { get; set; }
    public int GapCount { get; set; }
    public float RiskScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string KeyFindings { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
    public DateTime NextReview { get; set; }
}
