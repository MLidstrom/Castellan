using Castellan.Worker.Models.Compliance;

namespace Castellan.Worker.Services.Compliance;

public interface IComplianceFramework
{
    string FrameworkName { get; }
    Task<ControlAssessment> AssessControlAsync(ComplianceControl control);
    Task<string> GenerateSummaryAsync(IEnumerable<ComplianceAssessmentResult> results);
    Task<string> GenerateKeyFindingsAsync(IEnumerable<ComplianceAssessmentResult> results);
    Task<string> GenerateRecommendationsAsync(IEnumerable<ComplianceAssessmentResult> results);
}

public class ControlAssessment
{
    public string Status { get; set; } = string.Empty; // Compliant, NonCompliant, PartiallyCompliant
    public int Score { get; set; } // 0-100
    public string? Evidence { get; set; }
    public string? Findings { get; set; }
    public string? Recommendations { get; set; }
}