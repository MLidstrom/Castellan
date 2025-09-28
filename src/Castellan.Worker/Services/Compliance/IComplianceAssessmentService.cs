using Castellan.Worker.Models.Compliance;

namespace Castellan.Worker.Services.Compliance;

public interface IComplianceAssessmentService
{
    Task<ComplianceAssessmentResult> AssessControlAsync(string framework, string controlId);
    Task<ComplianceReport> GenerateReportAsync(string framework, string reportType);
    Task<int> CalculateImplementationPercentageAsync(string framework);
    Task<List<ComplianceGap>> IdentifyGapsAsync(string framework);
    Task<float> CalculateRiskScoreAsync(string framework);
    Task<List<string>> GetAvailableFrameworksAsync();
}

public class ComplianceGap
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public int Priority { get; set; }
}