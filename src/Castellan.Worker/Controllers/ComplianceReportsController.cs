using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/compliance-reports")]
[Authorize]
public class ComplianceReportsController : ControllerBase
{
    private readonly ILogger<ComplianceReportsController> _logger;

    public ComplianceReportsController(ILogger<ComplianceReportsController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public Task<IActionResult> GetList(
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

            var allReports = GenerateMockComplianceReports();
            
            // Apply filters
            if (!string.IsNullOrEmpty(framework))
            {
                allReports = allReports.Where(r => r.Framework.Contains(framework, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrEmpty(reportType))
            {
                allReports = allReports.Where(r => r.ReportType.Contains(reportType, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (!string.IsNullOrEmpty(status))
            {
                allReports = allReports.Where(r => r.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Apply pagination
            var skip = (page - 1) * pageSize;
            var pagedReports = allReports.Skip(skip).Take(pageSize).ToList();

            var response = new
            {
                data = pagedReports,
                total = allReports.Count,
                page,
                perPage = pageSize
            };

            return Task.FromResult<IActionResult>(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance reports list");
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }

    [HttpGet("{id}")]
    public Task<IActionResult> GetOne(string id)
    {
        try
        {
            _logger.LogInformation("Getting compliance report: {Id}", id);

            var allReports = GenerateMockComplianceReports();
            var report = allReports.FirstOrDefault(r => r.Id == id);

            if (report == null)
            {
                return Task.FromResult<IActionResult>(NotFound(new { message = "Compliance report not found" }));
            }

            return Task.FromResult<IActionResult>(Ok(new { data = report }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance report: {Id}", id);
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] ComplianceReportCreateRequest request)
    {
        try
        {
            _logger.LogInformation("Creating compliance report: {Framework} - {ReportType}", request.Framework, request.ReportType);

            // Generate new report based on request
            var newReport = new ComplianceReportDto
            {
                Id = Guid.NewGuid().ToString(),
                Framework = request.Framework,
                ReportType = request.ReportType,
                Status = "in_progress",
                CreatedDate = DateTime.UtcNow,
                Generated = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddMonths(3),
                GeneratedBy = "System",
                Version = "1.0",
                ImplementationPercentage = Random.Shared.Next(60, 95),
                TotalControls = GetControlCountForFramework(request.Framework),
                ImplementedControls = 0,
                FailedControls = 0,
                GapCount = 0,
                RiskScore = 7.5f,
                Summary = $"Compliance report for {request.Framework} framework - {request.ReportType} assessment.",
                KeyFindings = GetKeyFindings(request.Framework),
                Recommendations = GetRecommendations(request.Framework),
                NextReview = DateTime.UtcNow.AddMonths(3)
            };

            // Update calculated fields
            newReport.ImplementedControls = (int)(newReport.TotalControls * (newReport.ImplementationPercentage / 100.0));
            newReport.GapCount = newReport.TotalControls - newReport.ImplementedControls;
            newReport.FailedControls = newReport.GapCount;

            return Task.FromResult<IActionResult>(Ok(new { data = newReport }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating compliance report");
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }

    private List<ComplianceReportDto> GenerateMockComplianceReports()
    {
        return new List<ComplianceReportDto>
        {
            new()
            {
                Id = "1",
                Framework = "HIPAA",
                ReportType = "assessment",
                Status = "complete",
                CreatedDate = DateTime.UtcNow.AddDays(-15),
                Generated = DateTime.UtcNow.AddDays(-15),
                ValidUntil = DateTime.UtcNow.AddMonths(6),
                GeneratedBy = "System",
                Version = "1.2",
                ImplementationPercentage = 87,
                TotalControls = 164,
                ImplementedControls = 143,
                FailedControls = 21,
                GapCount = 21,
                RiskScore = 6.8f,
                Summary = "HIPAA compliance assessment shows good overall compliance with key privacy and security safeguards implemented.",
                KeyFindings = "Administrative safeguards: 92% compliant, Physical safeguards: 85% compliant, Technical safeguards: 84% compliant",
                Recommendations = "Focus on access controls and audit trail improvements. Implement additional technical safeguards for data transmission.",
                NextReview = DateTime.UtcNow.AddMonths(3)
            },
            new()
            {
                Id = "2",
                Framework = "SOC2",
                ReportType = "gap_analysis",
                Status = "complete",
                CreatedDate = DateTime.UtcNow.AddDays(-8),
                Generated = DateTime.UtcNow.AddDays(-8),
                ValidUntil = DateTime.UtcNow.AddMonths(3),
                GeneratedBy = "System",
                Version = "1.0",
                ImplementationPercentage = 92,
                TotalControls = 64,
                ImplementedControls = 59,
                FailedControls = 5,
                GapCount = 5,
                RiskScore = 4.2f,
                Summary = "SOC 2 Type II gap analysis reveals strong security posture with minor gaps in availability controls.",
                KeyFindings = "Security: 95% compliant, Availability: 87% compliant, Processing integrity: 94% compliant",
                Recommendations = "Enhance monitoring systems for availability. Improve incident response procedures.",
                NextReview = DateTime.UtcNow.AddMonths(6)
            },
            new()
            {
                Id = "3",
                Framework = "FedRAMP",
                ReportType = "continuous",
                Status = "in_progress",
                CreatedDate = DateTime.UtcNow.AddDays(-3),
                Generated = DateTime.UtcNow.AddDays(-3),
                ValidUntil = DateTime.UtcNow.AddMonths(12),
                GeneratedBy = "System",
                Version = "2.1",
                ImplementationPercentage = 78,
                TotalControls = 325,
                ImplementedControls = 254,
                FailedControls = 71,
                GapCount = 71,
                RiskScore = 8.1f,
                Summary = "FedRAMP continuous monitoring assessment in progress. Moderate compliance level achieved.",
                KeyFindings = "Access control: 82% compliant, System integrity: 75% compliant, Contingency planning: 71% compliant",
                Recommendations = "Strengthen system integrity controls. Enhance contingency planning and backup procedures.",
                NextReview = DateTime.UtcNow.AddMonths(1)
            },
            new()
            {
                Id = "4",
                Framework = "GDPR",
                ReportType = "audit_ready",
                Status = "complete",
                CreatedDate = DateTime.UtcNow.AddDays(-20),
                Generated = DateTime.UtcNow.AddDays(-20),
                ValidUntil = DateTime.UtcNow.AddMonths(12),
                GeneratedBy = "Compliance Team",
                Version = "1.5",
                ImplementationPercentage = 89,
                TotalControls = 99,
                ImplementedControls = 88,
                FailedControls = 11,
                GapCount = 11,
                RiskScore = 5.7f,
                Summary = "GDPR audit readiness assessment shows strong data protection compliance with minor gaps in consent management.",
                KeyFindings = "Data protection by design: 91% compliant, Consent management: 83% compliant, Breach notification: 95% compliant",
                Recommendations = "Improve consent management workflows. Enhance data subject rights procedures.",
                NextReview = DateTime.UtcNow.AddMonths(6)
            },
            new()
            {
                Id = "5",
                Framework = "ISO27001",
                ReportType = "implementation",
                Status = "complete",
                CreatedDate = DateTime.UtcNow.AddDays(-12),
                Generated = DateTime.UtcNow.AddDays(-12),
                ValidUntil = DateTime.UtcNow.AddMonths(12),
                GeneratedBy = "Security Team",
                Version = "3.0",
                ImplementationPercentage = 94,
                TotalControls = 114,
                ImplementedControls = 107,
                FailedControls = 7,
                GapCount = 7,
                RiskScore = 3.9f,
                Summary = "ISO 27001 implementation plan shows excellent progress with strong information security management system.",
                KeyFindings = "Risk management: 97% compliant, Access control: 92% compliant, Incident management: 98% compliant",
                Recommendations = "Complete remaining physical security controls. Finalize business continuity testing.",
                NextReview = DateTime.UtcNow.AddMonths(12)
            }
        };
    }

    private int GetControlCountForFramework(string framework)
    {
        return framework.ToUpper() switch
        {
            "HIPAA" => 164,
            "SOC2" => 64,
            "FEDRAMP" => 325,
            "GDPR" => 99,
            "ISO27001" => 114,
            _ => 50
        };
    }

    private string GetKeyFindings(string framework)
    {
        return framework.ToUpper() switch
        {
            "HIPAA" => "Strong administrative and technical safeguards, minor gaps in physical security controls",
            "SOC2" => "Excellent security controls, availability monitoring needs improvement",
            "FEDRAMP" => "Good baseline security, access control and system integrity require attention",
            "GDPR" => "Strong data protection measures, consent management workflows need enhancement",
            "ISO27001" => "Comprehensive ISMS implementation, final physical security controls pending",
            _ => "Assessment in progress, detailed findings will be available upon completion"
        };
    }

    private string GetRecommendations(string framework)
    {
        return framework.ToUpper() switch
        {
            "HIPAA" => "Implement additional physical safeguards, enhance audit trail monitoring, conduct security training",
            "SOC2" => "Deploy availability monitoring tools, update incident response procedures, enhance backup validation",
            "FEDRAMP" => "Strengthen access control mechanisms, implement system integrity monitoring, enhance security documentation",
            "GDPR" => "Improve consent management systems, enhance data subject request workflows, update privacy notices",
            "ISO27001" => "Complete physical security controls implementation, finalize business continuity plans, conduct management review",
            _ => "Recommendations will be provided based on assessment results"
        };
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
