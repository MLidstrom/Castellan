using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Compliance;
using Castellan.Worker.Data;
using Microsoft.EntityFrameworkCore;

namespace Castellan.Worker.Services.Compliance.Frameworks;

/// <summary>
/// SOC 2 Type II Compliance Framework - Organization/User Compliance (Visible to Users)
/// Assesses organizational compliance with SOC 2 Type II security controls
/// This framework is visible in the user interface for organizational compliance reporting
/// </summary>
public class SOC2ComplianceFramework : IComplianceFramework
{
    private readonly ISecurityEventStore _eventStore;
    private readonly CastellanDbContext _context;
    private readonly ILogger<SOC2ComplianceFramework> _logger;

    public string FrameworkName => "SOC2";

    public SOC2ComplianceFramework(
        ISecurityEventStore eventStore,
        CastellanDbContext context,
        ILogger<SOC2ComplianceFramework> logger)
    {
        _eventStore = eventStore;
        _context = context;
        _logger = logger;
    }

    public async Task<ControlAssessment> AssessControlAsync(ComplianceControl control)
    {
        return control.ControlId switch
        {
            "CC1.1" => await AssessControlEnvironmentAsync(),
            "CC2.1" => await AssessCommunicationAsync(),
            "CC3.1" => await AssessRiskAssessmentAsync(),
            "CC4.1" => await AssessMonitoringAsync(),
            "CC5.1" => await AssessControlActivitiesAsync(),
            "CC6.1" => await AssessLogicalAccessAsync(),
            "CC6.2" => await AssessAccessProvisioningAsync(),
            "CC6.3" => await AssessAccessReviewAsync(),
            "CC6.6" => await AssessDataClassificationAsync(),
            "CC6.7" => await AssessDataRetentionAsync(),
            "CC7.1" => await AssessSystemOperationsAsync(),
            "CC7.2" => await AssessChangeManagementAsync(),
            "CC7.3" => await AssessIncidentResponseAsync(),
            "CC8.1" => await AssessRiskMitigationAsync(),
            "A1.1" => await AssessAvailabilityAsync(),
            "A1.2" => await AssessDisasterRecoveryAsync(),
            "C1.1" => await AssessConfidentialityAsync(),
            "C1.2" => await AssessDataProtectionAsync(),
            "PI1.1" => await AssessProcessingIntegrityAsync(),
            "P1.1" => await AssessPrivacyAsync(),
            _ => await AssessGenericControlAsync(control)
        };
    }

    private async Task<ControlAssessment> AssessControlEnvironmentAsync()
    {
        // CC1.1: Control Environment
        try
        {
            var recentEvents = _eventStore.GetSecurityEvents(1, 1000);
            var configEvents = recentEvents.Where(e => e.EventType == SecurityEventType.SecurityPolicyChange);
            var systemEvents = recentEvents.Where(e =>
                e.EventType == SecurityEventType.SystemStartup ||
                e.EventType == SecurityEventType.SystemShutdown);

            var hasConfigManagement = configEvents.Any();
            var hasSystemMonitoring = systemEvents.Any();
            var configChangesCount = configEvents.Count();

            if (hasConfigManagement && hasSystemMonitoring && configChangesCount < 50)
            {
                return new ControlAssessment
                {
                    Status = "Compliant",
                    Score = 90,
                    Evidence = $"Control environment monitored with {configChangesCount} config changes and {systemEvents.Count()} system events",
                    Findings = "Strong control environment with proper change management and monitoring",
                    Recommendations = "Continue maintaining current control environment practices"
                };
            }
            else if (hasConfigManagement || hasSystemMonitoring)
            {
                return new ControlAssessment
                {
                    Status = "PartiallyCompliant",
                    Score = 60,
                    Evidence = $"Some control monitoring with {configChangesCount} config changes",
                    Findings = "Control environment partially implemented",
                    Recommendations = "Enhance monitoring and formalize change control processes"
                };
            }
            else
            {
                return new ControlAssessment
                {
                    Status = "NonCompliant",
                    Score = 30,
                    Evidence = "Limited control environment monitoring detected",
                    Findings = "Control environment needs significant improvement",
                    Recommendations = "Implement comprehensive control environment monitoring and governance"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing CC1.1 - Control Environment");
            return CreateErrorAssessment("CC1.1", ex.Message);
        }
    }

    private async Task<ControlAssessment> AssessLogicalAccessAsync()
    {
        // CC6.1: Logical and Physical Access Controls
        try
        {
            var recentEvents = _eventStore.GetSecurityEvents(1, 1000);
            var accessEvents = recentEvents.Where(e =>
                e.EventType == SecurityEventType.AuthenticationSuccess ||
                e.EventType == SecurityEventType.AuthenticationFailure ||
                e.EventType == SecurityEventType.AccountManagement ||
                e.EventType == SecurityEventType.PrivilegeEscalation);

            var totalAccess = accessEvents.Count();
            var failedLogins = accessEvents.Count(e => e.EventType == SecurityEventType.AuthenticationFailure);
            var uniqueUsers = accessEvents.Select(e => e.OriginalEvent.User).Distinct().Count();
            var failureRate = totalAccess > 0 ? (double)failedLogins / totalAccess * 100 : 0;

            if (totalAccess > 100 && failureRate < 10 && uniqueUsers > 1)
            {
                return new ControlAssessment
                {
                    Status = "Compliant",
                    Score = 95,
                    Evidence = $"Strong access controls: {totalAccess} access events, {failureRate:F1}% failure rate, {uniqueUsers} unique users",
                    Findings = "Logical access controls are properly implemented and monitored",
                    Recommendations = "Maintain current access control practices"
                };
            }
            else if (totalAccess > 50)
            {
                return new ControlAssessment
                {
                    Status = "PartiallyCompliant",
                    Score = 70,
                    Evidence = $"Moderate access control: {totalAccess} events, {failureRate:F1}% failure rate",
                    Findings = "Access controls present but could be enhanced",
                    Recommendations = "Strengthen access control monitoring and review processes"
                };
            }
            else
            {
                return new ControlAssessment
                {
                    Status = "NonCompliant",
                    Score = 40,
                    Evidence = $"Limited access control data: only {totalAccess} events recorded",
                    Findings = "Insufficient access control monitoring",
                    Recommendations = "Implement comprehensive access control monitoring and regular reviews"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing CC6.1 - Logical Access");
            return CreateErrorAssessment("CC6.1", ex.Message);
        }
    }

    private async Task<ControlAssessment> AssessAvailabilityAsync()
    {
        // A1.1: Availability
        try
        {
            var recentEvents = _eventStore.GetSecurityEvents(1, 1000);
            var systemEvents = recentEvents.Where(e =>
                e.EventType == SecurityEventType.SystemStartup ||
                e.EventType == SecurityEventType.SystemShutdown ||
                e.EventType == SecurityEventType.NetworkConnection);

            var startupCount = systemEvents.Count(e => e.EventType == SecurityEventType.SystemStartup);
            var shutdownCount = systemEvents.Count(e => e.EventType == SecurityEventType.SystemShutdown);
            var serviceEvents = systemEvents.Count(e =>
                e.EventType == SecurityEventType.NetworkConnection);

            var unexpectedShutdowns = Math.Max(0, shutdownCount - startupCount + 1);
            var availabilityScore = 100 - (unexpectedShutdowns * 10) - (serviceEvents > 100 ? 10 : 0);
            availabilityScore = Math.Max(0, Math.Min(100, availabilityScore));

            if (availabilityScore >= 95)
            {
                return new ControlAssessment
                {
                    Status = "Compliant",
                    Score = availabilityScore,
                    Evidence = $"High availability: {startupCount} startups, {shutdownCount} shutdowns, {serviceEvents} service events",
                    Findings = "System demonstrates high availability and stability",
                    Recommendations = "Continue current availability monitoring and maintenance"
                };
            }
            else if (availabilityScore >= 80)
            {
                return new ControlAssessment
                {
                    Status = "PartiallyCompliant",
                    Score = availabilityScore,
                    Evidence = $"Moderate availability: {unexpectedShutdowns} unexpected shutdowns detected",
                    Findings = "System availability meets baseline requirements but could improve",
                    Recommendations = "Investigate causes of unexpected shutdowns and service disruptions"
                };
            }
            else
            {
                return new ControlAssessment
                {
                    Status = "NonCompliant",
                    Score = availabilityScore,
                    Evidence = $"Availability concerns: {unexpectedShutdowns} unexpected shutdowns, {serviceEvents} service events",
                    Findings = "System availability below acceptable thresholds",
                    Recommendations = "Implement availability monitoring and improve system stability"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing A1.1 - Availability");
            return CreateErrorAssessment("A1.1", ex.Message);
        }
    }

    // Placeholder implementations for missing methods
    private async Task<ControlAssessment> AssessCommunicationAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC2.1", ControlName = "Communication" });
    private async Task<ControlAssessment> AssessRiskAssessmentAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC3.1", ControlName = "Risk Assessment" });
    private async Task<ControlAssessment> AssessMonitoringAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC4.1", ControlName = "Monitoring" });
    private async Task<ControlAssessment> AssessControlActivitiesAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC5.1", ControlName = "Control Activities" });
    private async Task<ControlAssessment> AssessAccessProvisioningAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC6.2", ControlName = "Access Provisioning" });
    private async Task<ControlAssessment> AssessAccessReviewAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC6.3", ControlName = "Access Review" });
    private async Task<ControlAssessment> AssessDataClassificationAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC6.6", ControlName = "Data Classification" });
    private async Task<ControlAssessment> AssessDataRetentionAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC6.7", ControlName = "Data Retention" });
    private async Task<ControlAssessment> AssessSystemOperationsAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC7.1", ControlName = "System Operations" });
    private async Task<ControlAssessment> AssessChangeManagementAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC7.2", ControlName = "Change Management" });
    private async Task<ControlAssessment> AssessIncidentResponseAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC7.3", ControlName = "Incident Response" });
    private async Task<ControlAssessment> AssessRiskMitigationAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "CC8.1", ControlName = "Risk Mitigation" });
    private async Task<ControlAssessment> AssessDisasterRecoveryAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "A1.2", ControlName = "Disaster Recovery" });
    private async Task<ControlAssessment> AssessConfidentialityAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "C1.1", ControlName = "Confidentiality" });
    private async Task<ControlAssessment> AssessDataProtectionAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "C1.2", ControlName = "Data Protection" });
    private async Task<ControlAssessment> AssessProcessingIntegrityAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "PI1.1", ControlName = "Processing Integrity" });
    private async Task<ControlAssessment> AssessPrivacyAsync() => await AssessGenericControlAsync(new ComplianceControl { ControlId = "P1.1", ControlName = "Privacy" });

    private async Task<ControlAssessment> AssessGenericControlAsync(ComplianceControl control)
    {
        // Generic assessment for controls not specifically implemented
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 50,
            Evidence = $"Standard SOC2 control assessment for {control.ControlId}",
            Findings = $"Control {control.ControlId} requires detailed SOC2 assessment: {control.ControlName}",
            Recommendations = $"Implement specific assessment logic for {control.ControlName}"
        };
    }

    private ControlAssessment CreateErrorAssessment(string controlId, string errorMessage)
    {
        return new ControlAssessment
        {
            Status = "Error",
            Score = 0,
            Evidence = $"Assessment error for {controlId}",
            Findings = $"Assessment failed for {controlId}: {errorMessage}",
            Recommendations = $"Fix assessment implementation for {controlId}"
        };
    }

    public async Task<string> GenerateSummaryAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var resultsList = results.ToList();
        var totalControls = resultsList.Count;
        var compliantControls = resultsList.Count(r => r.Status == "Compliant");
        var avgScore = resultsList.Any() ? resultsList.Average(r => r.Score) : 0;

        var trustCategories = new[]
        {
            ("Security", resultsList.Where(r => r.ControlId.StartsWith("CC6") || r.ControlId.StartsWith("CC7"))),
            ("Availability", resultsList.Where(r => r.ControlId.StartsWith("A1"))),
            ("Processing Integrity", resultsList.Where(r => r.ControlId.StartsWith("PI1"))),
            ("Confidentiality", resultsList.Where(r => r.ControlId.StartsWith("C1"))),
            ("Privacy", resultsList.Where(r => r.ControlId.StartsWith("P1")))
        };

        var categoryScores = trustCategories
            .Where(c => c.Item2.Any())
            .Select(c => $"{c.Item1}: {c.Item2.Average(r => r.Score):F0}%")
            .ToList();

        return $"SOC 2 Type II assessment shows {avgScore:F0}% overall compliance. " +
               $"Trust service criteria: {compliantControls}/{totalControls} fully compliant. " +
               $"Category scores: {string.Join(", ", categoryScores)}. " +
               "Assessment covers Security, Availability, Processing Integrity, Confidentiality, and Privacy criteria.";
    }

    public async Task<string> GenerateKeyFindingsAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var findings = new List<string>();

        var criticalFindings = results
            .Where(r => r.Status == "NonCompliant" || r.Score < 60)
            .OrderBy(r => r.Score)
            .Take(3)
            .Select(r => $"• {r.ControlId}: {r.Findings}")
            .ToList();

        var strongFindings = results
            .Where(r => r.Status == "Compliant" && r.Score >= 90)
            .Take(3)
            .Select(r => $"• {r.ControlId}: {r.Findings}")
            .ToList();

        if (criticalFindings.Any())
        {
            findings.Add("Areas requiring immediate attention:");
            findings.AddRange(criticalFindings);
        }

        if (strongFindings.Any())
        {
            findings.Add("\nStrong controls identified:");
            findings.AddRange(strongFindings);
        }

        // Add SOC2-specific observations
        var securityScore = results.Where(r => r.ControlId.StartsWith("CC6")).DefaultIfEmpty().Average(r => r?.Score ?? 0);
        var availabilityScore = results.Where(r => r.ControlId.StartsWith("A1")).DefaultIfEmpty().Average(r => r?.Score ?? 0);

        if (securityScore < 70)
        {
            findings.Add("\n• Security controls need strengthening to meet SOC 2 requirements");
        }
        if (availabilityScore < 90)
        {
            findings.Add("• Availability monitoring should be enhanced for SOC 2 compliance");
        }

        return string.Join("\n", findings);
    }

    public async Task<string> GenerateRecommendationsAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var recommendations = results
            .Where(r => !string.IsNullOrEmpty(r.Recommendations) && r.Status != "Compliant")
            .OrderBy(r => r.Score)
            .Select(r => $"• {r.Recommendations}")
            .Distinct()
            .Take(5)
            .ToList();

        // Add SOC2-specific recommendations
        var avgScore = results.Any() ? results.Average(r => r.Score) : 0;
        if (avgScore < 80)
        {
            recommendations.Add("• Implement comprehensive monitoring for all SOC 2 trust service criteria");
            recommendations.Add("• Establish formal change management and incident response procedures");
        }

        if (results.Any(r => r.ControlId.StartsWith("CC") && r.Score < 70))
        {
            recommendations.Add("• Strengthen common criteria controls to meet SOC 2 baseline requirements");
        }

        recommendations.Add("• Schedule regular SOC 2 readiness assessments to maintain compliance");

        return recommendations.Any()
            ? string.Join("\n", recommendations)
            : "SOC 2 controls are well-implemented. Continue regular monitoring and assessment.";
    }
}