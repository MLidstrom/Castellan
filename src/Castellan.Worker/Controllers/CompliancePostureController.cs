using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Compliance;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/compliance-posture")]
[Authorize]
public class CompliancePostureController : ControllerBase
{
    private readonly ILogger<CompliancePostureController> _logger;
    private readonly CastellanDbContext _context;

    public CompliancePostureController(
        ILogger<CompliancePostureController> logger,
        CastellanDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetCompliancePostureSummary()
    {
        try
        {
            _logger.LogInformation("Getting compliance posture summary for organizational frameworks");

            // Get only Organization-scope frameworks (user-visible)
            var organizationFrameworks = new[] { "HIPAA", "SOX", "PCI-DSS", "ISO 27001", "SOC2" };

            var reports = await _context.ComplianceReports
                .Where(r => organizationFrameworks.Contains(r.Framework))
                .GroupBy(r => r.Framework)
                .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
                .ToListAsync();

            var summary = new
            {
                totalFrameworks = organizationFrameworks.Length,
                assessedFrameworks = reports.Count,
                overallScore = reports.Any() ? reports.Average(r => r.ImplementationPercentage) : 0.0,
                riskLevel = CalculateRiskLevel(reports.Any() ? reports.Average(r => r.RiskScore) : 100.0f),
                lastAssessment = reports.Any() ? reports.Max(r => r.Generated) : (DateTime?)null,
                frameworks = reports.Select(r => new
                {
                    framework = r.Framework,
                    implementationPercentage = r.ImplementationPercentage,
                    riskScore = r.RiskScore,
                    status = r.Status,
                    lastAssessment = r.Generated,
                    totalControls = r.TotalControls,
                    implementedControls = r.ImplementedControls,
                    failedControls = r.FailedControls
                }).OrderBy(f => f.framework)
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance posture summary");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("framework/{framework}")]
    public async Task<IActionResult> GetFrameworkPosture(string framework)
    {
        try
        {
            // Validate that this is an organizational framework
            var organizationFrameworks = new[] { "HIPAA", "SOX", "PCI-DSS", "ISO 27001", "SOC2" };
            if (!organizationFrameworks.Contains(framework, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Framework not found or not accessible" });
            }

            _logger.LogInformation("Getting posture for framework: {Framework}", framework);

            var latestReport = await _context.ComplianceReports
                .Where(r => r.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestReport == null)
            {
                return NotFound(new { error = $"No assessment found for framework: {framework}" });
            }

            var controls = await _context.ComplianceControls
                .Where(c => c.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase) && c.IsUserVisible)
                .ToListAsync();

            var posture = new
            {
                framework = latestReport.Framework,
                implementationPercentage = latestReport.ImplementationPercentage,
                riskScore = latestReport.RiskScore,
                riskLevel = CalculateRiskLevel(latestReport.RiskScore),
                status = latestReport.Status,
                lastAssessment = latestReport.Generated,
                nextReview = latestReport.NextReview,
                totalControls = latestReport.TotalControls,
                implementedControls = latestReport.ImplementedControls,
                failedControls = latestReport.FailedControls,
                gapCount = latestReport.GapCount,
                summary = latestReport.Summary,
                keyFindings = latestReport.KeyFindings,
                recommendations = latestReport.Recommendations,
                controls = controls.Select(c => new
                {
                    controlId = c.ControlId,
                    controlName = c.ControlName,
                    description = c.Description,
                    category = c.Category,
                    priority = c.Priority,
                    isActive = c.IsActive
                }).OrderBy(c => c.controlId)
            };

            return Ok(posture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting framework posture for {Framework}", framework);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("compare")]
    public async Task<IActionResult> CompareFrameworks([FromBody] CompareFrameworksRequest request)
    {
        try
        {
            if (request?.Frameworks?.Any() != true || request.Frameworks.Count < 2)
            {
                return BadRequest(new { error = "At least 2 frameworks are required for comparison" });
            }

            var organizationFrameworks = new[] { "HIPAA", "SOX", "PCI-DSS", "ISO 27001", "SOC2" };
            var invalidFrameworks = request.Frameworks
                .Where(f => !organizationFrameworks.Contains(f, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (invalidFrameworks.Any())
            {
                return BadRequest(new { error = $"Invalid frameworks: {string.Join(", ", invalidFrameworks)}" });
            }

            _logger.LogInformation("Comparing frameworks: {Frameworks}", string.Join(", ", request.Frameworks));

            var reports = new List<object>();
            foreach (var framework in request.Frameworks)
            {
                var latestReport = await _context.ComplianceReports
                    .Where(r => r.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestReport != null)
                {
                    reports.Add(new
                    {
                        framework = latestReport.Framework,
                        implementationPercentage = latestReport.ImplementationPercentage,
                        riskScore = latestReport.RiskScore,
                        riskLevel = CalculateRiskLevel(latestReport.RiskScore),
                        totalControls = latestReport.TotalControls,
                        implementedControls = latestReport.ImplementedControls,
                        failedControls = latestReport.FailedControls,
                        lastAssessment = latestReport.Generated
                    });
                }
            }

            var comparison = new
            {
                comparedFrameworks = request.Frameworks,
                comparisonDate = DateTime.UtcNow,
                averageImplementation = reports.Any() ? ((IEnumerable<dynamic>)reports).Average(r => r.implementationPercentage) : 0.0,
                averageRiskScore = reports.Any() ? ((IEnumerable<dynamic>)reports).Average(r => r.riskScore) : 0.0,
                bestPerforming = reports.OrderByDescending(r => ((dynamic)r).implementationPercentage).FirstOrDefault(),
                needsAttention = reports.OrderBy(r => ((dynamic)r).implementationPercentage).FirstOrDefault(),
                frameworks = reports
            };

            return Ok(comparison);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing frameworks");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetComplianceTrends([FromQuery] int days = 30)
    {
        try
        {
            var startDate = DateTime.UtcNow.AddDays(-days);
            var organizationFrameworks = new[] { "HIPAA", "SOX", "PCI-DSS", "ISO 27001", "SOC2" };

            _logger.LogInformation("Getting compliance trends for last {Days} days", days);

            var trends = await _context.ComplianceReports
                .Where(r => organizationFrameworks.Contains(r.Framework) && r.Generated >= startDate)
                .GroupBy(r => r.Framework)
                .Select(g => new
                {
                    framework = g.Key,
                    reports = g.OrderBy(r => r.Generated).Select(r => new
                    {
                        date = r.Generated,
                        implementationPercentage = r.ImplementationPercentage,
                        riskScore = r.RiskScore
                    }).ToList()
                })
                .ToListAsync();

            var trendAnalysis = new
            {
                periodDays = days,
                startDate = startDate,
                endDate = DateTime.UtcNow,
                frameworks = trends.Select(t => new
                {
                    framework = t.framework,
                    currentImplementation = t.reports.LastOrDefault()?.implementationPercentage ?? 0,
                    previousImplementation = t.reports.FirstOrDefault()?.implementationPercentage ?? 0,
                    trend = CalculateTrend(t.reports.FirstOrDefault()?.implementationPercentage ?? 0, t.reports.LastOrDefault()?.implementationPercentage ?? 0),
                    assessmentCount = t.reports.Count,
                    dataPoints = t.reports
                }).OrderBy(f => f.framework)
            };

            return Ok(trendAnalysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance trends");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("actions")]
    public async Task<IActionResult> GetPrioritizedActions()
    {
        try
        {
            _logger.LogInformation("Getting prioritized compliance actions");

            var organizationFrameworks = new[] { "HIPAA", "SOX", "PCI-DSS", "ISO 27001", "SOC2" };

            var reports = await _context.ComplianceReports
                .Where(r => organizationFrameworks.Contains(r.Framework))
                .GroupBy(r => r.Framework)
                .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
                .ToListAsync();

            var actions = new List<object>();

            foreach (var report in reports.OrderBy(r => r.ImplementationPercentage))
            {
                var priority = CalculateActionPriority(report.ImplementationPercentage, report.RiskScore);

                actions.Add(new
                {
                    framework = report.Framework,
                    priority = priority,
                    action = GenerateActionRecommendation(report),
                    currentImplementation = report.ImplementationPercentage,
                    riskScore = report.RiskScore,
                    urgency = CalculateUrgency(report.RiskScore),
                    estimatedEffort = EstimateEffort(report.GapCount),
                    expectedImpact = EstimateImpact(report.FailedControls, report.TotalControls)
                });
            }

            var prioritizedActions = new
            {
                generateDate = DateTime.UtcNow,
                totalActions = actions.Count,
                highPriorityActions = actions.Count(a => ((string)((dynamic)a).priority) == "High"),
                criticalActions = actions.Where(a => ((string)((dynamic)a).urgency) == "Critical").ToList(),
                recommendations = actions.OrderBy(a => GetPriorityOrder(((string)((dynamic)a).priority)))
            };

            return Ok(prioritizedActions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting prioritized actions");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static string CalculateRiskLevel(double riskScore)
    {
        return riskScore switch
        {
            >= 80 => "Critical",
            >= 60 => "High",
            >= 40 => "Medium",
            >= 20 => "Low",
            _ => "Minimal"
        };
    }

    private static string CalculateTrend(double previous, double current)
    {
        var change = current - previous;
        return change switch
        {
            > 5 => "Improving",
            < -5 => "Declining",
            _ => "Stable"
        };
    }

    private static string CalculateActionPriority(int implementation, float riskScore)
    {
        if (riskScore >= 80 || implementation < 50) return "Critical";
        if (riskScore >= 60 || implementation < 70) return "High";
        if (riskScore >= 40 || implementation < 85) return "Medium";
        return "Low";
    }

    private static string GenerateActionRecommendation(ComplianceReport report)
    {
        if (report.ImplementationPercentage < 50)
            return $"Immediate attention required for {report.Framework} - implementation critically low at {report.ImplementationPercentage}%";
        if (report.ImplementationPercentage < 80)
            return $"Focus on addressing {report.FailedControls} failed controls in {report.Framework}";
        return $"Maintain and optimize {report.Framework} compliance posture";
    }

    private static string CalculateUrgency(float riskScore)
    {
        return riskScore switch
        {
            >= 90 => "Critical",
            >= 70 => "High",
            >= 50 => "Medium",
            _ => "Low"
        };
    }

    private static string EstimateEffort(int gapCount)
    {
        return gapCount switch
        {
            >= 10 => "High",
            >= 5 => "Medium",
            >= 1 => "Low",
            _ => "Minimal"
        };
    }

    private static string EstimateImpact(int failedControls, int totalControls)
    {
        var percentage = totalControls > 0 ? (double)failedControls / totalControls * 100 : 0;
        return percentage switch
        {
            >= 30 => "High",
            >= 15 => "Medium",
            >= 5 => "Low",
            _ => "Minimal"
        };
    }

    private static int GetPriorityOrder(string priority)
    {
        return priority switch
        {
            "Critical" => 1,
            "High" => 2,
            "Medium" => 3,
            "Low" => 4,
            _ => 5
        };
    }
}

public class CompareFrameworksRequest
{
    public List<string> Frameworks { get; set; } = new();
}