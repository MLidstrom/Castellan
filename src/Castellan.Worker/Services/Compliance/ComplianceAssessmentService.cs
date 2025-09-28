using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Compliance;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services.Compliance;

public class ComplianceAssessmentService : IComplianceAssessmentService
{
    private readonly CastellanDbContext _context;
    private readonly ISecurityEventStore _eventStore;
    private readonly ILogger<ComplianceAssessmentService> _logger;
    private readonly Dictionary<string, IComplianceFramework> _frameworks;

    public ComplianceAssessmentService(
        CastellanDbContext context,
        ISecurityEventStore eventStore,
        ILogger<ComplianceAssessmentService> logger,
        IEnumerable<IComplianceFramework> frameworks)
    {
        _context = context;
        _eventStore = eventStore;
        _logger = logger;
        _frameworks = frameworks.ToDictionary(f => f.FrameworkName, f => f);
    }

    public async Task<ComplianceReport> GenerateReportAsync(string framework, string reportType)
    {
        _logger.LogInformation("Generating {ReportType} compliance report for {Framework}", reportType, framework);

        // Normalize framework name to handle UI/backend naming differences
        var normalizedFramework = NormalizeFrameworkName(framework);

        if (!_frameworks.TryGetValue(normalizedFramework, out var frameworkImpl))
        {
            throw new InvalidOperationException($"Framework {framework} not supported");
        }

        var controls = await _context.ComplianceControls
            .Where(c => c.Framework == normalizedFramework && c.IsActive)
            .ToListAsync();

        var report = new ComplianceReport
        {
            Framework = normalizedFramework,
            ReportType = reportType,
            Status = "in_progress",
            CreatedDate = DateTime.UtcNow,
            Generated = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddMonths(GetValidityPeriod(framework)),
            GeneratedBy = "System",
            Version = "1.0",
            TotalControls = controls.Count
        };

        // Assess each control
        var assessmentTasks = controls.Select(async control =>
        {
            try
            {
                return await AssessControlInternalAsync(frameworkImpl, control, report.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to assess control {ControlId}", control.ControlId);
                return new ComplianceAssessmentResult
                {
                    ReportId = report.Id,
                    ControlId = control.ControlId,
                    Framework = framework,
                    Status = "Error",
                    Score = 0,
                    Findings = $"Assessment failed: {ex.Message}",
                    AssessedAt = DateTime.UtcNow
                };
            }
        });

        var results = await Task.WhenAll(assessmentTasks);
        report.AssessmentResults = results.ToList();

        // Calculate summary metrics
        report.ImplementedControls = results.Count(r => r.Status == "Compliant");
        report.FailedControls = results.Count(r => r.Status == "NonCompliant");
        report.GapCount = report.TotalControls - report.ImplementedControls;
        report.ImplementationPercentage = report.TotalControls > 0
            ? (report.ImplementedControls * 100) / report.TotalControls
            : 0;
        report.RiskScore = CalculateRiskScore(results);

        // Generate summary and recommendations
        report.Summary = await frameworkImpl.GenerateSummaryAsync(results);
        report.KeyFindings = await frameworkImpl.GenerateKeyFindingsAsync(results);
        report.Recommendations = await frameworkImpl.GenerateRecommendationsAsync(results);
        report.NextReview = DateTime.UtcNow.AddMonths(GetReviewPeriod(framework));

        report.Status = "complete";

        // Save to database
        _context.ComplianceReports.Add(report);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Compliance report generated successfully. ID: {ReportId}, Score: {Score}%",
            report.Id, report.ImplementationPercentage);

        return report;
    }

    public async Task<ComplianceAssessmentResult> AssessControlAsync(string framework, string controlId)
    {
        if (!_frameworks.TryGetValue(framework, out var frameworkImpl))
        {
            throw new InvalidOperationException($"Framework {framework} not supported");
        }

        var control = await _context.ComplianceControls
            .FirstOrDefaultAsync(c => c.Framework == framework && c.ControlId == controlId);

        if (control == null)
        {
            throw new InvalidOperationException($"Control {controlId} not found for framework {framework}");
        }

        return await AssessControlInternalAsync(frameworkImpl, control, Guid.NewGuid().ToString());
    }

    public async Task<int> CalculateImplementationPercentageAsync(string framework)
    {
        var controls = await _context.ComplianceControls
            .Where(c => c.Framework == framework && c.IsActive)
            .ToListAsync();

        if (controls.Count == 0)
        {
            return 0;
        }

        if (!_frameworks.TryGetValue(framework, out var frameworkImpl))
        {
            return 50; // Default fallback
        }

        var assessmentTasks = controls.Select(async control =>
        {
            try
            {
                var assessment = await frameworkImpl.AssessControlAsync(control);
                return assessment.Status == "Compliant" ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        });

        var results = await Task.WhenAll(assessmentTasks);
        return (results.Sum() * 100) / controls.Count;
    }

    public async Task<List<ComplianceGap>> IdentifyGapsAsync(string framework)
    {
        var controls = await _context.ComplianceControls
            .Where(c => c.Framework == framework && c.IsActive)
            .ToListAsync();

        var gaps = new List<ComplianceGap>();

        if (!_frameworks.TryGetValue(framework, out var frameworkImpl))
        {
            return gaps;
        }

        foreach (var control in controls)
        {
            try
            {
                var assessment = await frameworkImpl.AssessControlAsync(control);
                if (assessment.Status == "NonCompliant" || assessment.Score < 70)
                {
                    gaps.Add(new ComplianceGap
                    {
                        ControlId = control.ControlId,
                        ControlName = control.ControlName,
                        Framework = framework,
                        Severity = assessment.Score < 30 ? "High" : assessment.Score < 60 ? "Medium" : "Low",
                        Description = assessment.Findings ?? "Control not fully compliant",
                        Recommendation = assessment.Recommendations ?? "Review and improve control implementation",
                        Priority = assessment.Score < 30 ? 1 : assessment.Score < 60 ? 2 : 3
                    });
                }
            }
            catch (Exception ex)
            {
                gaps.Add(new ComplianceGap
                {
                    ControlId = control.ControlId,
                    ControlName = control.ControlName,
                    Framework = framework,
                    Severity = "High",
                    Description = $"Assessment failed: {ex.Message}",
                    Recommendation = "Fix assessment implementation for this control",
                    Priority = 1
                });
            }
        }

        return gaps.OrderBy(g => g.Priority).ThenBy(g => g.ControlId).ToList();
    }

    public async Task<float> CalculateRiskScoreAsync(string framework)
    {
        var implementationPercentage = await CalculateImplementationPercentageAsync(framework);

        // Risk score: 1-10 (lower is better)
        var baseRisk = (100 - implementationPercentage) / 10.0f; // 0-10 based on implementation percentage

        // Additional risk factors could be added here based on the framework
        var additionalRisk = framework switch
        {
            "HIPAA" => 1.5f, // Higher baseline risk due to healthcare data sensitivity
            "PCI-DSS" => 2.0f, // Highest risk due to payment card data
            "SOX" => 1.2f, // Moderate risk due to financial reporting
            "ISO27001" => 1.0f, // Standard risk
            _ => 1.0f
        };

        return Math.Min(baseRisk + additionalRisk, 10.0f);
    }

    private async Task<ComplianceAssessmentResult> AssessControlInternalAsync(
        IComplianceFramework framework, ComplianceControl control, string reportId)
    {
        var result = new ComplianceAssessmentResult
        {
            ReportId = reportId,
            ControlId = control.ControlId,
            Framework = control.Framework,
            AssessedAt = DateTime.UtcNow
        };

        try
        {
            var assessment = await framework.AssessControlAsync(control);
            result.Status = assessment.Status;
            result.Score = assessment.Score;
            result.Evidence = assessment.Evidence;
            result.Findings = assessment.Findings;
            result.Recommendations = assessment.Recommendations;
        }
        catch (Exception ex)
        {
            result.Status = "Error";
            result.Score = 0;
            result.Findings = $"Assessment error: {ex.Message}";
        }

        return result;
    }

    private float CalculateRiskScore(ComplianceAssessmentResult[] results)
    {
        if (results.Length == 0) return 10.0f;

        var averageScore = results.Average(r => r.Score);
        var criticalFailures = results.Count(r => r.Status == "NonCompliant" &&
            r.Findings?.Contains("critical", StringComparison.OrdinalIgnoreCase) == true);

        // Risk score: 1-10 (lower is better)
        var baseRisk = (100 - averageScore) / 10; // 0-10 based on average score
        var criticalPenalty = Math.Min(criticalFailures * 2, 5); // Up to 5 points for critical failures

        return Math.Min((float)(baseRisk + criticalPenalty), 10.0f);
    }

    public async Task<List<string>> GetAvailableFrameworksAsync()
    {
        return _frameworks.Keys.ToList();
    }

    private string NormalizeFrameworkName(string framework)
    {
        return framework switch
        {
            "ISO27001" => "ISO 27001",
            "PCIDF" => "PCI DSS",
            _ => framework
        };
    }

    private int GetValidityPeriod(string framework) => framework switch
    {
        "HIPAA" => 12,
        "SOX" => 6,
        "PCI DSS" => 12,
        "ISO 27001" => 12,
        _ => 6
    };

    private int GetReviewPeriod(string framework) => framework switch
    {
        "HIPAA" => 6,
        "SOX" => 3,
        "PCI DSS" => 6,
        "ISO 27001" => 6,
        _ => 3
    };
}