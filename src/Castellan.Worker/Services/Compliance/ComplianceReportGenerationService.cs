using System.Text;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Compliance;
using Castellan.Worker.Services.Interfaces;
using System.Text.Json;
using iTextSharp.text;
using iTextSharp.text.pdf;
using SkiaSharp;
using System.Drawing;

namespace Castellan.Worker.Services.Compliance;

public interface IComplianceReportGenerationService
{
    Task<ComplianceReportDocument> GenerateComprehensiveReportAsync(string framework, ReportFormat format = ReportFormat.Json);
    Task<ComplianceReportDocument> GenerateExecutiveSummaryAsync(List<string>? frameworks = null);
    Task<ComplianceReportDocument> GenerateComparisonReportAsync(List<string> frameworks);
    Task<ComplianceReportDocument> GenerateTrendReportAsync(string framework, int days = 30);
    Task<byte[]> ExportReportAsync(ComplianceReportDocument document, ReportFormat format);
}

public enum ReportFormat
{
    Json,
    Html,
    Pdf,
    Csv,
    Markdown
}

public enum ReportAudience
{
    Executive,      // High-level summary for executives
    Technical,      // Detailed technical report
    Auditor,       // Compliance auditor format
    Operations     // Operational team format
}

public class ComplianceReportDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public ReportFormat Format { get; set; }
    public ReportAudience Audience { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string GeneratedBy { get; set; } = "System";

    // Report sections
    public ExecutiveSummarySection? ExecutiveSummary { get; set; }
    public ComplianceOverviewSection? Overview { get; set; }
    public ControlAssessmentSection? ControlAssessment { get; set; }
    public RiskAnalysisSection? RiskAnalysis { get; set; }
    public RecommendationsSection? Recommendations { get; set; }
    public TrendAnalysisSection? TrendAnalysis { get; set; }
    public AppendixSection? Appendix { get; set; }

    // Metadata
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsOrganizationScope { get; set; } = true;
}

public class ExecutiveSummarySection
{
    public string Summary { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> KeyFindings { get; set; } = new();
    public List<string> CriticalGaps { get; set; } = new();
    public string Recommendation { get; set; } = string.Empty;
    public Dictionary<string, double> ScoresByCategory { get; set; } = new();
}

public class ComplianceOverviewSection
{
    public string Framework { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalControls { get; set; }
    public int ImplementedControls { get; set; }
    public int PartiallyImplementedControls { get; set; }
    public int NotImplementedControls { get; set; }
    public double CompliancePercentage { get; set; }
    public DateTime LastAssessment { get; set; }
    public DateTime NextReview { get; set; }
}

public class ControlAssessmentSection
{
    public List<ControlAssessmentDetail> Controls { get; set; } = new();
    public Dictionary<string, int> ControlsByCategory { get; set; } = new();
    public Dictionary<string, double> ScoresByCategory { get; set; } = new();
}

public class ControlAssessmentDetail
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public List<string> Gaps { get; set; } = new();
    public string Recommendation { get; set; } = string.Empty;
}

public class RiskAnalysisSection
{
    public float OverallRiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public List<RiskItem> HighRiskAreas { get; set; } = new();
    public List<RiskItem> MediumRiskAreas { get; set; } = new();
    public Dictionary<string, float> RiskByCategory { get; set; } = new();
    public string RiskTrend { get; set; } = string.Empty;
}

public class RiskItem
{
    public string Area { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float RiskScore { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string Likelihood { get; set; } = string.Empty;
    public string Mitigation { get; set; } = string.Empty;
}

public class RecommendationsSection
{
    public List<Recommendation> ImmediateActions { get; set; } = new();
    public List<Recommendation> ShortTermActions { get; set; } = new();
    public List<Recommendation> LongTermActions { get; set; } = new();
    public string ImplementationRoadmap { get; set; } = string.Empty;
}

public class Recommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string EstimatedEffort { get; set; } = string.Empty;
    public string ExpectedImpact { get; set; } = string.Empty;
    public List<string> AffectedControls { get; set; } = new();
}

public class TrendAnalysisSection
{
    public List<ComplianceTrendPoint> TrendData { get; set; } = new();
    public double CurrentScore { get; set; }
    public double PreviousScore { get; set; }
    public string Trend { get; set; } = string.Empty;
    public double ProjectedScore { get; set; }
    public string Analysis { get; set; } = string.Empty;
}

public class ComplianceTrendPoint
{
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public int ImplementedControls { get; set; }
    public float RiskScore { get; set; }
}

public class AppendixSection
{
    public List<string> Glossary { get; set; } = new();
    public List<string> References { get; set; } = new();
    public Dictionary<string, string> Definitions { get; set; } = new();
    public string Methodology { get; set; } = string.Empty;
}

public class ComplianceReportGenerationService : IComplianceReportGenerationService
{
    private readonly ILogger<ComplianceReportGenerationService> _logger;
    private readonly CastellanDbContext _context;
    private readonly IComplianceAssessmentService _assessmentService;
    private readonly IComplianceFrameworkService _frameworkService;
    private readonly IComplianceReportCacheService _cacheService;
    private readonly IOptimizedPdfReportService _pdfService;

    // Organization-scope frameworks (user-visible)
    private readonly HashSet<string> _organizationFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "HIPAA", "SOX", "PCI-DSS", "ISO 27001", "SOC2"
    };

    public ComplianceReportGenerationService(
        ILogger<ComplianceReportGenerationService> logger,
        CastellanDbContext context,
        IComplianceAssessmentService assessmentService,
        IComplianceFrameworkService frameworkService,
        IComplianceReportCacheService cacheService,
        IOptimizedPdfReportService pdfService)
    {
        _logger = logger;
        _context = context;
        _assessmentService = assessmentService;
        _frameworkService = frameworkService;
        _cacheService = cacheService;
        _pdfService = pdfService;
    }

    public async Task<ComplianceReportDocument> GenerateComprehensiveReportAsync(string framework, ReportFormat format = ReportFormat.Json)
    {
        _logger.LogInformation("Generating comprehensive report for {Framework}", framework);

        // Ensure only organizational frameworks can have comprehensive reports
        if (!_organizationFrameworks.Contains(framework))
        {
            throw new InvalidOperationException($"Framework '{framework}' is not accessible for reporting");
        }

        // Check cache first
        var cacheKey = _cacheService.GenerateCacheKey("comprehensive", framework, new { format });
        var cachedReport = await _cacheService.GetCachedReportAsync<ComplianceReportDocument>(cacheKey);
        if (cachedReport != null)
        {
            _logger.LogDebug("Returning cached comprehensive report for {Framework}", framework);
            return cachedReport;
        }

        var report = await _context.ComplianceReports
            .Where(r => r.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (report == null)
        {
            throw new InvalidOperationException($"No assessment found for framework: {framework}");
        }

        var controls = await _context.ComplianceControls
            .Where(c => c.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase) && c.IsUserVisible)
            .ToListAsync();

        var assessmentResults = await _context.ComplianceAssessmentResults
            .Where(a => a.ReportId == report.Id)
            .ToListAsync();

        var document = new ComplianceReportDocument
        {
            Title = $"{framework} Compliance Report",
            Framework = framework,
            Format = format,
            Audience = ReportAudience.Technical,
            IsOrganizationScope = true,

            ExecutiveSummary = GenerateExecutiveSummary(report, controls, assessmentResults),
            Overview = GenerateOverview(report, controls),
            ControlAssessment = GenerateControlAssessment(controls, assessmentResults),
            RiskAnalysis = GenerateRiskAnalysis(report, assessmentResults),
            Recommendations = GenerateRecommendations(report, assessmentResults),
            Appendix = GenerateAppendix(framework)
        };

        document.Metadata["ReportId"] = report.Id;
        document.Metadata["GeneratedFrom"] = "ComplianceReportGenerationService";
        document.Metadata["Version"] = "1.0";

        // Cache the generated report
        await _cacheService.SetCachedReportAsync(cacheKey, document, TimeSpan.FromMinutes(10));

        return document;
    }

    public async Task<ComplianceReportDocument> GenerateExecutiveSummaryAsync(List<string>? frameworks = null)
    {
        _logger.LogInformation("Generating executive summary report");

        frameworks ??= _organizationFrameworks.ToList();

        // Filter to only organizational frameworks
        frameworks = frameworks.Where(f => _organizationFrameworks.Contains(f)).ToList();

        // Check cache first
        var cacheKey = _cacheService.GenerateCacheKey("executive-summary", "multi-framework", frameworks);
        var cachedReport = await _cacheService.GetCachedReportAsync<ComplianceReportDocument>(cacheKey);
        if (cachedReport != null)
        {
            _logger.LogDebug("Returning cached executive summary report");
            return cachedReport;
        }

        var reports = new List<ComplianceReport>();
        foreach (var framework in frameworks)
        {
            var report = await _context.ComplianceReports
                .Where(r => r.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (report != null)
                reports.Add(report);
        }

        var document = new ComplianceReportDocument
        {
            Title = "Executive Compliance Summary",
            Framework = "Multi-Framework",
            Format = ReportFormat.Html,
            Audience = ReportAudience.Executive,
            IsOrganizationScope = true,

            ExecutiveSummary = new ExecutiveSummarySection
            {
                Summary = GenerateExecutiveSummaryText(reports),
                OverallScore = reports.Any() ? reports.Average(r => r.ImplementationPercentage) : 0,
                RiskLevel = CalculateOverallRiskLevel(reports),
                KeyFindings = GenerateKeyFindings(reports),
                CriticalGaps = IdentifyCriticalGaps(reports),
                Recommendation = GenerateTopRecommendation(reports),
                ScoresByCategory = reports.ToDictionary(r => r.Framework, r => (double)r.ImplementationPercentage)
            }
        };

        // Cache the generated executive summary
        await _cacheService.SetCachedReportAsync(cacheKey, document, TimeSpan.FromMinutes(5));

        return document;
    }

    public async Task<ComplianceReportDocument> GenerateComparisonReportAsync(List<string> frameworks)
    {
        _logger.LogInformation("Generating comparison report for {Frameworks}", string.Join(", ", frameworks));

        // Filter to only organizational frameworks
        frameworks = frameworks.Where(f => _organizationFrameworks.Contains(f)).ToList();

        if (frameworks.Count < 2)
        {
            throw new ArgumentException("At least 2 frameworks are required for comparison");
        }

        var reports = new Dictionary<string, ComplianceReport>();
        var allControls = new Dictionary<string, List<ComplianceControl>>();

        foreach (var framework in frameworks)
        {
            var report = await _context.ComplianceReports
                .Where(r => r.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (report != null)
            {
                reports[framework] = report;

                var controls = await _context.ComplianceControls
                    .Where(c => c.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase) && c.IsUserVisible)
                    .ToListAsync();

                allControls[framework] = controls;
            }
        }

        var document = new ComplianceReportDocument
        {
            Title = $"Compliance Framework Comparison Report",
            Framework = string.Join(" vs ", frameworks),
            Format = ReportFormat.Html,
            Audience = ReportAudience.Technical,
            IsOrganizationScope = true
        };

        // Create comparison overview
        var overview = new ComplianceOverviewSection
        {
            Framework = "Comparison Summary",
            Description = $"Comparative analysis of {string.Join(", ", frameworks)} frameworks",
            TotalControls = allControls.Sum(c => c.Value.Count),
            ImplementedControls = reports.Sum(r => r.Value.ImplementedControls),
            CompliancePercentage = reports.Any() ? reports.Average(r => r.Value.ImplementationPercentage) : 0
        };

        document.Overview = overview;
        document.Metadata["ComparedFrameworks"] = frameworks;
        document.Metadata["ReportCount"] = reports.Count;

        return document;
    }

    public async Task<ComplianceReportDocument> GenerateTrendReportAsync(string framework, int days = 30)
    {
        _logger.LogInformation("Generating trend report for {Framework} over {Days} days", framework, days);

        if (!_organizationFrameworks.Contains(framework))
        {
            throw new InvalidOperationException($"Framework '{framework}' is not accessible for reporting");
        }

        var startDate = DateTime.UtcNow.AddDays(-days);

        var historicalReports = await _context.ComplianceReports
            .Where(r => r.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase)
                     && r.Generated >= startDate)
            .OrderBy(r => r.Generated)
            .ToListAsync();

        if (!historicalReports.Any())
        {
            throw new InvalidOperationException($"No historical data found for framework: {framework}");
        }

        var trendData = historicalReports.Select(r => new ComplianceTrendPoint
        {
            Date = r.Generated,
            Score = r.ImplementationPercentage,
            ImplementedControls = r.ImplementedControls,
            RiskScore = r.RiskScore
        }).ToList();

        var currentScore = trendData.LastOrDefault()?.Score ?? 0;
        var previousScore = trendData.FirstOrDefault()?.Score ?? 0;
        var trend = CalculateTrend(previousScore, currentScore);

        var document = new ComplianceReportDocument
        {
            Title = $"{framework} Compliance Trend Report",
            Framework = framework,
            Format = ReportFormat.Json,
            Audience = ReportAudience.Operations,
            IsOrganizationScope = true,

            TrendAnalysis = new TrendAnalysisSection
            {
                TrendData = trendData,
                CurrentScore = currentScore,
                PreviousScore = previousScore,
                Trend = trend,
                ProjectedScore = ProjectScore(trendData),
                Analysis = GenerateTrendAnalysis(trendData, trend)
            }
        };

        return document;
    }

    public async Task<byte[]> ExportReportAsync(ComplianceReportDocument document, ReportFormat format)
    {
        _logger.LogInformation("Exporting report {Title} to {Format}", document.Title, format);

        return format switch
        {
            ReportFormat.Json => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            })),
            ReportFormat.Html => GenerateHtmlReport(document),
            ReportFormat.Csv => GenerateCsvReport(document),
            ReportFormat.Markdown => GenerateMarkdownReport(document),
            ReportFormat.Pdf => await _pdfService.GenerateOptimizedPdfReportAsync(document),
            _ => throw new NotSupportedException($"Format {format} is not supported")
        };
    }

    private ExecutiveSummarySection GenerateExecutiveSummary(
        ComplianceReport report,
        List<ComplianceControl> controls,
        List<ComplianceAssessmentResult> results)
    {
        var implementedCount = results.Count(r => r.Status == "Implemented");
        var partialCount = results.Count(r => r.Status == "Partial");
        var notImplementedCount = results.Count(r => r.Status == "Not Implemented");

        var criticalControls = controls.Where(c => c.Priority == "Critical").ToList();
        var criticalGaps = criticalControls
            .Where(c => results.Any(r => r.ControlId == c.ControlId && r.Status != "Implemented"))
            .Select(c => $"{c.ControlId}: {c.ControlName}")
            .ToList();

        var categoriesScores = results
            .GroupBy(r => controls.FirstOrDefault(c => c.ControlId == r.ControlId)?.Category ?? "Unknown")
            .ToDictionary(
                g => g.Key,
                g => g.Average(r => r.Score)
            );

        return new ExecutiveSummarySection
        {
            Summary = $"{report.Framework} compliance assessment shows {report.ImplementationPercentage}% implementation with {implementedCount} fully implemented controls out of {report.TotalControls} total controls.",
            OverallScore = report.ImplementationPercentage,
            RiskLevel = CalculateRiskLevel(report.RiskScore),
            KeyFindings = new List<string>
            {
                $"{implementedCount} controls fully implemented",
                $"{partialCount} controls partially implemented",
                $"{notImplementedCount} controls not implemented",
                $"Overall risk score: {report.RiskScore:F1}"
            },
            CriticalGaps = criticalGaps,
            Recommendation = GeneratePrimaryRecommendation(report, criticalGaps),
            ScoresByCategory = categoriesScores
        };
    }

    private ComplianceOverviewSection GenerateOverview(ComplianceReport report, List<ComplianceControl> controls)
    {
        return new ComplianceOverviewSection
        {
            Framework = report.Framework,
            Description = GetFrameworkDescription(report.Framework),
            TotalControls = report.TotalControls,
            ImplementedControls = report.ImplementedControls,
            PartiallyImplementedControls = report.TotalControls - report.ImplementedControls - report.FailedControls,
            NotImplementedControls = report.FailedControls,
            CompliancePercentage = report.ImplementationPercentage,
            LastAssessment = report.Generated,
            NextReview = report.NextReview
        };
    }

    private ControlAssessmentSection GenerateControlAssessment(
        List<ComplianceControl> controls,
        List<ComplianceAssessmentResult> results)
    {
        var controlDetails = controls.Select(control =>
        {
            var result = results.FirstOrDefault(r => r.ControlId == control.ControlId);
            return new ControlAssessmentDetail
            {
                ControlId = control.ControlId,
                ControlName = control.ControlName,
                Category = control.Category ?? "General",
                Priority = control.Priority ?? "Medium",
                Status = result?.Status ?? "Not Assessed",
                Score = result?.Score ?? 0,
                Evidence = result?.Evidence ?? string.Empty,
                Gaps = ParseGaps(result?.Findings),
                Recommendation = result?.Recommendations ?? GenerateControlRecommendation(control, result)
            };
        }).ToList();

        var controlsByCategory = controlDetails
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var scoresByCategory = controlDetails
            .GroupBy(c => c.Category)
            .ToDictionary(g => g.Key, g => g.Average(c => c.Score));

        return new ControlAssessmentSection
        {
            Controls = controlDetails,
            ControlsByCategory = controlsByCategory,
            ScoresByCategory = scoresByCategory
        };
    }

    private RiskAnalysisSection GenerateRiskAnalysis(ComplianceReport report, List<ComplianceAssessmentResult> results)
    {
        var riskItems = results
            .Where(r => r.Score < 80) // Consider low scores as risk items
            .Select(r => new RiskItem
            {
                Area = r.ControlId,
                Description = r.Findings ?? "Compliance gap identified",
                RiskScore = CalculateRiskScore(r),
                Impact = DetermineImpact(DetermineRiskLevel(r.Score)),
                Likelihood = DetermineLikelihood(r.Status),
                Mitigation = r.Recommendations ?? "Implement control requirements"
            })
            .OrderByDescending(r => r.RiskScore)
            .ToList();

        var highRisk = riskItems.Where(r => r.RiskScore >= 70).ToList();
        var mediumRisk = riskItems.Where(r => r.RiskScore >= 40 && r.RiskScore < 70).ToList();

        return new RiskAnalysisSection
        {
            OverallRiskScore = report.RiskScore,
            RiskLevel = CalculateRiskLevel(report.RiskScore),
            HighRiskAreas = highRisk,
            MediumRiskAreas = mediumRisk,
            RiskByCategory = new Dictionary<string, float>(), // Would need category mapping
            RiskTrend = "Stable" // Would need historical data
        };
    }

    private RecommendationsSection GenerateRecommendations(ComplianceReport report, List<ComplianceAssessmentResult> results)
    {
        var notImplemented = results.Where(r => r.Status == "Not Implemented").ToList();
        var partial = results.Where(r => r.Status == "Partial").ToList();

        var immediateActions = notImplemented
            .Where(r => r.Score < 40) // Critical risk threshold
            .Take(5)
            .Select(r => new Recommendation
            {
                Title = $"Implement {r.ControlId}",
                Description = r.Findings ?? "Address compliance gap",
                Priority = "Critical",
                EstimatedEffort = "High",
                ExpectedImpact = "High",
                AffectedControls = new List<string> { r.ControlId }
            })
            .ToList();

        var shortTermActions = partial
            .Take(5)
            .Select(r => new Recommendation
            {
                Title = $"Complete implementation of {r.ControlId}",
                Description = r.Recommendations ?? "Finalize partial implementation",
                Priority = "High",
                EstimatedEffort = "Medium",
                ExpectedImpact = "Medium",
                AffectedControls = new List<string> { r.ControlId }
            })
            .ToList();

        return new RecommendationsSection
        {
            ImmediateActions = immediateActions,
            ShortTermActions = shortTermActions,
            LongTermActions = new List<Recommendation>(),
            ImplementationRoadmap = GenerateRoadmap(report, immediateActions, shortTermActions)
        };
    }

    private AppendixSection GenerateAppendix(string framework)
    {
        return new AppendixSection
        {
            Glossary = new List<string>
            {
                "Control: A specific security or compliance requirement",
                "Implementation: The degree to which a control is deployed",
                "Risk Score: Numerical assessment of compliance risk",
                "Framework: Set of compliance standards and requirements"
            },
            References = new List<string>
            {
                $"Official {framework} documentation",
                "Castellan Compliance Assessment Methodology",
                "Industry best practices"
            },
            Definitions = new Dictionary<string, string>
            {
                ["Implemented"] = "Control is fully deployed and operational",
                ["Partial"] = "Control is partially deployed or in progress",
                ["Not Implemented"] = "Control has not been deployed"
            },
            Methodology = "Assessments are performed using automated security event analysis combined with configuration validation and policy review."
        };
    }

    // Helper methods
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

    private string GenerateExecutiveSummaryText(List<ComplianceReport> reports)
    {
        if (!reports.Any())
            return "No compliance assessments available.";

        var avgCompliance = reports.Average(r => r.ImplementationPercentage);
        var frameworks = string.Join(", ", reports.Select(r => r.Framework));

        return $"Organization compliance posture across {reports.Count} frameworks ({frameworks}) " +
               $"shows an average implementation of {avgCompliance:F1}%. " +
               $"Latest assessments indicate {reports.Count(r => r.RiskScore < 40)} frameworks at acceptable risk levels.";
    }

    private string CalculateOverallRiskLevel(List<ComplianceReport> reports)
    {
        if (!reports.Any())
            return "Unknown";

        var avgRisk = reports.Average(r => r.RiskScore);
        return CalculateRiskLevel(avgRisk);
    }

    private List<string> GenerateKeyFindings(List<ComplianceReport> reports)
    {
        var findings = new List<string>();

        if (reports.Any())
        {
            var bestFramework = reports.OrderByDescending(r => r.ImplementationPercentage).First();
            var worstFramework = reports.OrderBy(r => r.ImplementationPercentage).First();

            findings.Add($"Highest compliance: {bestFramework.Framework} at {bestFramework.ImplementationPercentage}%");
            findings.Add($"Needs attention: {worstFramework.Framework} at {worstFramework.ImplementationPercentage}%");
            findings.Add($"Total controls assessed: {reports.Sum(r => r.TotalControls)}");
            findings.Add($"Average risk score: {reports.Average(r => r.RiskScore):F1}");
        }

        return findings;
    }

    private List<string> IdentifyCriticalGaps(List<ComplianceReport> reports)
    {
        var gaps = new List<string>();

        foreach (var report in reports.Where(r => r.FailedControls > 0))
        {
            if (report.RiskScore >= 60)
            {
                gaps.Add($"{report.Framework}: {report.FailedControls} critical controls not implemented");
            }
        }

        return gaps;
    }

    private string GenerateTopRecommendation(List<ComplianceReport> reports)
    {
        if (!reports.Any())
            return "Conduct initial compliance assessments.";

        var highestRisk = reports.OrderByDescending(r => r.RiskScore).FirstOrDefault();
        if (highestRisk != null && highestRisk.RiskScore >= 60)
        {
            return $"Priority: Address critical gaps in {highestRisk.Framework} compliance (Risk: {highestRisk.RiskScore:F1})";
        }

        var lowestCompliance = reports.OrderBy(r => r.ImplementationPercentage).FirstOrDefault();
        if (lowestCompliance != null && lowestCompliance.ImplementationPercentage < 70)
        {
            return $"Focus on improving {lowestCompliance.Framework} implementation from {lowestCompliance.ImplementationPercentage}%";
        }

        return "Maintain current compliance levels and focus on continuous improvement.";
    }

    private string GetFrameworkDescription(string framework)
    {
        return framework.ToUpperInvariant() switch
        {
            "HIPAA" => "Health Insurance Portability and Accountability Act - Healthcare data protection standards",
            "SOX" => "Sarbanes-Oxley Act - Financial reporting and internal controls",
            "PCI-DSS" => "Payment Card Industry Data Security Standard - Credit card data protection",
            "ISO 27001" => "International standard for information security management systems",
            "SOC2" => "Service Organization Control 2 - Trust service criteria for service providers",
            _ => $"{framework} compliance framework"
        };
    }

    private List<string> ParseGaps(string? gaps)
    {
        if (string.IsNullOrWhiteSpace(gaps))
            return new List<string>();

        return gaps.Split(';', StringSplitOptions.RemoveEmptyEntries)
                  .Select(g => g.Trim())
                  .ToList();
    }

    private string GenerateControlRecommendation(ComplianceControl control, ComplianceAssessmentResult? result)
    {
        if (result == null || result.Status == "Not Assessed")
            return $"Assess implementation of {control.ControlName}";

        if (result.Status == "Not Implemented")
            return $"Implement {control.ControlName} to meet {control.Framework} requirements";

        if (result.Status == "Partial")
            return $"Complete implementation of {control.ControlName}";

        return $"Maintain compliance for {control.ControlName}";
    }

    private float CalculateRiskScore(ComplianceAssessmentResult result)
    {
        // Calculate risk based on score and implementation status
        var scoreRisk = (float)(100 - result.Score); // Lower scores = higher risk

        // Adjust based on implementation status
        if (result.Status == "Implemented")
            scoreRisk *= 0.2f;
        else if (result.Status == "Partial")
            scoreRisk *= 0.6f;
        else if (result.Status == "Not Implemented")
            scoreRisk = Math.Max(scoreRisk, 70f); // Minimum high risk for not implemented

        return Math.Min(100f, scoreRisk);
    }

    private string DetermineRiskLevel(int score)
    {
        return score switch
        {
            >= 80 => "Low",
            >= 60 => "Medium",
            >= 40 => "High",
            _ => "Critical"
        };
    }

    private string DetermineImpact(string? riskLevel)
    {
        return riskLevel switch
        {
            "Critical" => "Severe",
            "High" => "Major",
            "Medium" => "Moderate",
            "Low" => "Minor",
            _ => "Minimal"
        };
    }

    private string DetermineLikelihood(string? status)
    {
        return status switch
        {
            "Not Implemented" => "Very Likely",
            "Partial" => "Likely",
            "Implemented" => "Unlikely",
            _ => "Possible"
        };
    }

    private double ProjectScore(List<ComplianceTrendPoint> trendData)
    {
        if (trendData.Count < 2)
            return trendData.LastOrDefault()?.Score ?? 0;

        // Simple linear projection
        var recentTrend = trendData.TakeLast(3).ToList();
        var avgChange = recentTrend.Count > 1
            ? (recentTrend.Last().Score - recentTrend.First().Score) / (recentTrend.Count - 1)
            : 0;

        return Math.Min(100, Math.Max(0, recentTrend.Last().Score + (avgChange * 3)));
    }

    private string GenerateTrendAnalysis(List<ComplianceTrendPoint> trendData, string trend)
    {
        var improvement = trendData.Last().Score - trendData.First().Score;
        var periodDays = (trendData.Last().Date - trendData.First().Date).Days;

        return $"Over the past {periodDays} days, compliance has {trend.ToLower()} with a " +
               $"{Math.Abs(improvement):F1}% {(improvement >= 0 ? "improvement" : "decline")}. " +
               $"Current trajectory suggests continued {trend.ToLower()} trend.";
    }

    private string GeneratePrimaryRecommendation(ComplianceReport report, List<string> criticalGaps)
    {
        if (criticalGaps.Any())
            return $"Immediate action required: Address {criticalGaps.Count} critical control gaps";

        if (report.ImplementationPercentage < 70)
            return "Focus on improving overall implementation to achieve minimum compliance threshold";

        if (report.RiskScore > 60)
            return "Implement additional controls to reduce risk exposure";

        return "Continue monitoring and maintain current compliance posture";
    }

    private string GenerateRoadmap(ComplianceReport report, List<Recommendation> immediate, List<Recommendation> shortTerm)
    {
        var roadmap = new StringBuilder();
        roadmap.AppendLine("Implementation Roadmap:");
        roadmap.AppendLine();
        roadmap.AppendLine("Phase 1 (0-30 days): Critical Controls");
        foreach (var action in immediate.Take(3))
        {
            roadmap.AppendLine($"  - {action.Title}");
        }
        roadmap.AppendLine();
        roadmap.AppendLine("Phase 2 (30-90 days): Gap Remediation");
        foreach (var action in shortTerm.Take(3))
        {
            roadmap.AppendLine($"  - {action.Title}");
        }
        roadmap.AppendLine();
        roadmap.AppendLine("Phase 3 (90+ days): Continuous Improvement");
        roadmap.AppendLine("  - Regular assessments and monitoring");
        roadmap.AppendLine("  - Process optimization");
        roadmap.AppendLine("  - Training and awareness");

        return roadmap.ToString();
    }

    private byte[] GenerateHtmlReport(ComplianceReportDocument document)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine($"<title>{document.Title}</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine("h1 { color: #333; }");
        html.AppendLine("h2 { color: #666; margin-top: 30px; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine(".critical { color: #d9534f; }");
        html.AppendLine(".high { color: #f0ad4e; }");
        html.AppendLine(".medium { color: #5bc0de; }");
        html.AppendLine(".low { color: #5cb85c; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        html.AppendLine($"<h1>{document.Title}</h1>");
        html.AppendLine($"<p>Generated: {document.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}</p>");

        if (document.ExecutiveSummary != null)
        {
            html.AppendLine("<h2>Executive Summary</h2>");
            html.AppendLine($"<p>{document.ExecutiveSummary.Summary}</p>");
            html.AppendLine($"<p>Overall Score: <strong>{document.ExecutiveSummary.OverallScore:F1}%</strong></p>");
            html.AppendLine($"<p>Risk Level: <span class='{document.ExecutiveSummary.RiskLevel.ToLower()}'>{document.ExecutiveSummary.RiskLevel}</span></p>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return Encoding.UTF8.GetBytes(html.ToString());
    }

    private byte[] GenerateCsvReport(ComplianceReportDocument document)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Report Title,Framework,Generated At,Format,Audience");
        csv.AppendLine($"\"{document.Title}\",\"{document.Framework}\",\"{document.GeneratedAt:yyyy-MM-dd HH:mm:ss}\",\"{document.Format}\",\"{document.Audience}\"");

        if (document.ControlAssessment != null && document.ControlAssessment.Controls.Any())
        {
            csv.AppendLine();
            csv.AppendLine("Control ID,Control Name,Category,Priority,Status,Score,Recommendation");
            foreach (var control in document.ControlAssessment.Controls)
            {
                csv.AppendLine($"\"{control.ControlId}\",\"{control.ControlName}\",\"{control.Category}\",\"{control.Priority}\",\"{control.Status}\",{control.Score:F2},\"{control.Recommendation}\"");
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private byte[] GenerateMarkdownReport(ComplianceReportDocument document)
    {
        var md = new StringBuilder();
        md.AppendLine($"# {document.Title}");
        md.AppendLine();
        md.AppendLine($"**Generated:** {document.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}");
        md.AppendLine($"**Framework:** {document.Framework}");
        md.AppendLine();

        if (document.ExecutiveSummary != null)
        {
            md.AppendLine("## Executive Summary");
            md.AppendLine();
            md.AppendLine(document.ExecutiveSummary.Summary);
            md.AppendLine();
            md.AppendLine($"- **Overall Score:** {document.ExecutiveSummary.OverallScore:F1}%");
            md.AppendLine($"- **Risk Level:** {document.ExecutiveSummary.RiskLevel}");
            md.AppendLine();

            if (document.ExecutiveSummary.KeyFindings.Any())
            {
                md.AppendLine("### Key Findings");
                foreach (var finding in document.ExecutiveSummary.KeyFindings)
                {
                    md.AppendLine($"- {finding}");
                }
                md.AppendLine();
            }
        }

        return Encoding.UTF8.GetBytes(md.ToString());
    }

    private async Task<byte[]> GeneratePdfReportAsync(ComplianceReportDocument document)
    {
        _logger.LogInformation("Generating PDF report for {Title}", document.Title);

        using var stream = new MemoryStream();
        var pdfDocument = new Document(PageSize.A4, 50, 50, 50, 50);
        var writer = PdfWriter.GetInstance(pdfDocument, stream);

        try
        {
            pdfDocument.Open();

            // Title
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.Black);
            var title = new Paragraph(document.Title, titleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 20
            };
            pdfDocument.Add(title);

            // Framework
            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 14, BaseColor.Black);
            var framework = new Paragraph($"Framework: {document.Framework}", subtitleFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 10
            };
            pdfDocument.Add(framework);

            // Generated date
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.Black);
            var genDate = new Paragraph($"Generated: {document.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}", normalFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingAfter = 30
            };
            pdfDocument.Add(genDate);

            // Executive Summary
            if (document.ExecutiveSummary != null)
            {
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
                pdfDocument.Add(new Paragraph("Executive Summary", headerFont) { SpacingAfter = 15 });

                pdfDocument.Add(new Paragraph($"Overall Score: {document.ExecutiveSummary.OverallScore:F1}%", normalFont) { SpacingAfter = 10 });
                pdfDocument.Add(new Paragraph($"Risk Level: {document.ExecutiveSummary.RiskLevel}", normalFont) { SpacingAfter = 10 });
                pdfDocument.Add(new Paragraph(document.ExecutiveSummary.Summary, normalFont) { SpacingAfter = 20 });
            }

            // Overview
            if (document.Overview != null)
            {
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
                pdfDocument.Add(new Paragraph("Compliance Overview", headerFont) { SpacingAfter = 15 });

                // Create overview table
                var table = new PdfPTable(2) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 1, 1 });

                AddSimpleTableRow(table, "Framework", document.Overview.Framework);
                AddSimpleTableRow(table, "Total Controls", document.Overview.TotalControls.ToString());
                AddSimpleTableRow(table, "Implemented", document.Overview.ImplementedControls.ToString());
                AddSimpleTableRow(table, "Partially Implemented", document.Overview.PartiallyImplementedControls.ToString());
                AddSimpleTableRow(table, "Not Implemented", document.Overview.NotImplementedControls.ToString());
                AddSimpleTableRow(table, "Compliance Percentage", $"{document.Overview.CompliancePercentage:F1}%");

                pdfDocument.Add(table);
                pdfDocument.Add(new Paragraph(" ") { SpacingAfter = 20 });
            }

            // Control Assessment Summary
            if (document.ControlAssessment != null && document.ControlAssessment.Controls.Any())
            {
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
                pdfDocument.Add(new Paragraph("Control Assessment Summary", headerFont) { SpacingAfter = 15 });

                var summaryTable = new PdfPTable(4) { WidthPercentage = 100 };
                summaryTable.SetWidths(new float[] { 2, 1, 1, 1 });

                // Headers
                AddSimpleTableHeader(summaryTable, "Control Name");
                AddSimpleTableHeader(summaryTable, "Category");
                AddSimpleTableHeader(summaryTable, "Status");
                AddSimpleTableHeader(summaryTable, "Score");

                // First 20 controls for PDF readability
                foreach (var control in document.ControlAssessment.Controls.Take(20))
                {
                    AddSimpleTableCell(summaryTable, control.ControlName);
                    AddSimpleTableCell(summaryTable, control.Category);
                    AddSimpleTableCell(summaryTable, control.Status);
                    AddSimpleTableCell(summaryTable, $"{control.Score:F1}");
                }

                pdfDocument.Add(summaryTable);
            }

            pdfDocument.Close();
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF report");
            pdfDocument?.Close();
            throw;
        }
    }

    private void AddSimpleTableRow(PdfPTable table, string label, string value)
    {
        var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.Black);
        var valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.Black);

        table.AddCell(new PdfPCell(new Phrase(label, labelFont)) { BackgroundColor = BaseColor.LightGray, Padding = 5 });
        table.AddCell(new PdfPCell(new Phrase(value, valueFont)) { Padding = 5 });
    }

    private void AddSimpleTableHeader(PdfPTable table, string text)
    {
        var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.White);
        table.AddCell(new PdfPCell(new Phrase(text, headerFont))
        {
            BackgroundColor = BaseColor.DarkGray,
            Padding = 5,
            HorizontalAlignment = Element.ALIGN_CENTER
        });
    }

    private void AddSimpleTableCell(PdfPTable table, string text)
    {
        var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.Black);
        table.AddCell(new PdfPCell(new Phrase(text, cellFont)) { Padding = 3 });
    }
}
