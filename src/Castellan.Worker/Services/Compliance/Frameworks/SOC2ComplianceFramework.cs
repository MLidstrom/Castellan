using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Compliance;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services.Compliance.Frameworks;

/// <summary>
/// SOC 2 Type II Compliance Framework - Organization/User Compliance (Visible to Users)
/// Assesses organizational compliance with SOC 2 Type II security controls
/// This framework is visible in the user interface for organizational compliance reporting
/// </summary>
public class SOC2ComplianceFramework : IComplianceFramework
{
    private readonly ISecurityEventStore _eventStore;
    private readonly ISystemConfigurationService _configService;
    private readonly CastellanDbContext _context;
    private readonly ILogger<SOC2ComplianceFramework> _logger;

    public string FrameworkName => "SOC2";

    public SOC2ComplianceFramework(
        ISecurityEventStore eventStore,
        ISystemConfigurationService configService,
        CastellanDbContext context,
        ILogger<SOC2ComplianceFramework> logger)
    {
        _eventStore = eventStore;
        _configService = configService;
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
        // Assess organizational control environment and culture

        try
        {
            var configEvents = await _eventStore.GetEventsAsync(
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                eventTypes: new[] { SecurityEventType.ConfigurationChange.ToString() });

            var systemEvents = await _eventStore.GetEventsAsync(
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                eventTypes: new[] { SecurityEventType.SystemStartup.ToString(), SecurityEventType.SystemShutdown.ToString() });

            var hasConfigManagement = configEvents.Any();
            var hasSystemMonitoring = systemEvents.Any();
            var configChangesPerWeek = configEvents.Count() / 4.0;

            if (hasConfigManagement && hasSystemMonitoring && configChangesPerWeek < 10)
            {
                return new ControlAssessment
                {
                    Status = "Compliant",
                    Score = 90,
                    Evidence = $"Control environment monitored with {configEvents.Count()} config changes and {systemEvents.Count()} system events in 30 days",
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
                    Evidence = $"Some control monitoring with {configEvents.Count()} config changes",
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
        // Assess access control implementation

        try
        {
            var accessEvents = await _eventStore.GetEventsAsync(
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                eventTypes: new[] {
                    SecurityEventType.Login.ToString(),
                    SecurityEventType.LoginFailed.ToString(),
                    SecurityEventType.Logout.ToString(),
                    SecurityEventType.DataAccess.ToString()
                });

            var totalAccess = accessEvents.Count();
            var failedLogins = accessEvents.Count(e => e.EventType == SecurityEventType.LoginFailed.ToString());
            var uniqueUsers = accessEvents.Select(e => e.UserName).Distinct().Count();
            var failureRate = totalAccess > 0 ? (double)failedLogins / totalAccess * 100 : 0;

            // Assess access control effectiveness
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
        // Assess system availability and uptime

        try
        {
            var systemEvents = await _eventStore.GetEventsAsync(
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                eventTypes: new[] {
                    SecurityEventType.SystemStartup.ToString(),
                    SecurityEventType.SystemShutdown.ToString(),
                    SecurityEventType.ServiceStart.ToString(),
                    SecurityEventType.ServiceStop.ToString()
                });

            var startupCount = systemEvents.Count(e => e.EventType == SecurityEventType.SystemStartup.ToString());
            var shutdownCount = systemEvents.Count(e => e.EventType == SecurityEventType.SystemShutdown.ToString());
            var serviceEvents = systemEvents.Count(e =>
                e.EventType == SecurityEventType.ServiceStart.ToString() ||
                e.EventType == SecurityEventType.ServiceStop.ToString());

            // Calculate availability score based on system stability
            var unexpectedShutdowns = Math.Max(0, shutdownCount - startupCount + 1);
            var availabilityScore = 100 - (unexpectedShutdowns * 10) - (serviceEvents > 100 ? 10 : 0);
            availabilityScore = Math.Max(0, Math.Min(100, availabilityScore));

            if (availabilityScore >= 95)
            {
                return new ControlAssessment
                {
                    Status = "Compliant",
                    Score = availabilityScore,
                    Evidence = $"High availability: {startupCount} startups, {shutdownCount} shutdowns, {serviceEvents} service events in 30 days",
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

    private async Task<ControlAssessment> AssessConfidentialityAsync()
    {
        // C1.1: Confidentiality
        // Assess data confidentiality controls

        try
        {
            var dataAccessEvents = await _eventStore.GetEventsAsync(
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                eventTypes: new[] { SecurityEventType.DataAccess.ToString() });

            var configChanges = await _eventStore.GetEventsAsync(
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow,
                eventTypes: new[] { SecurityEventType.ConfigurationChange.ToString() });

            var hasDataAccessControls = dataAccessEvents.Any();
            var hasEncryption = await CheckEncryptionConfigurationAsync();
            var unauthorizedAccessAttempts = dataAccessEvents.Count(e =>
                e.Details?.Contains("denied", StringComparison.OrdinalIgnoreCase) == true ||
                e.Details?.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) == true);

            var score = 0;
            var findings = new List<string>();

            if (hasDataAccessControls)
            {
                score += 40;
                findings.Add($"Data access monitoring active ({dataAccessEvents.Count()} events)");
            }

            if (hasEncryption)
            {
                score += 40;
                findings.Add("Encryption controls configured");
            }

            if (unauthorizedAccessAttempts == 0)
            {
                score += 20;
                findings.Add("No unauthorized access attempts detected");
            }
            else if (unauthorizedAccessAttempts < 5)
            {
                score += 10;
                findings.Add($"Low unauthorized access attempts ({unauthorizedAccessAttempts})");
            }

            var status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant";

            return new ControlAssessment
            {
                Status = status,
                Score = score,
                Evidence = string.Join(", ", findings),
                Findings = score >= 80 ? "Strong confidentiality controls in place" : "Confidentiality controls need improvement",
                Recommendations = score >= 80 ? "Maintain current confidentiality practices" :
                                 "Enhance data access controls and encryption implementation"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing C1.1 - Confidentiality");
            return CreateErrorAssessment("C1.1", ex.Message);
        }
    }

    private async Task<ControlAssessment> AssessProcessingIntegrityAsync()
    {
        // PI1.1: Processing Integrity
        // Assess system processing integrity and accuracy

        try
        {
            var allEvents = await _eventStore.GetEventsAsync(
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow);

            var errorEvents = allEvents.Where(e =>
                e.Details?.Contains("error", StringComparison.OrdinalIgnoreCase) == true ||
                e.Details?.Contains("fail", StringComparison.OrdinalIgnoreCase) == true);

            var totalEvents = allEvents.Count();
            var errorCount = errorEvents.Count();
            var errorRate = totalEvents > 0 ? (double)errorCount / totalEvents * 100 : 0;
            var hasCorrelationIds = allEvents.Count(e => !string.IsNullOrEmpty(e.CorrelationId));
            var correlationRate = totalEvents > 0 ? (double)hasCorrelationIds / totalEvents * 100 : 0;

            // Score based on error rate and correlation tracking
            var score = Math.Max(0, 100 - (int)(errorRate * 5));
            if (correlationRate >= 80) score = Math.Min(100, score + 10);

            var status = score >= 85 ? "Compliant" : score >= 65 ? "PartiallyCompliant" : "NonCompliant";

            return new ControlAssessment
            {
                Status = status,
                Score = score,
                Evidence = $"Processing metrics: {errorRate:F1}% error rate, {correlationRate:F1}% correlation coverage from {totalEvents} events",
                Findings = score >= 85 ? "High processing integrity demonstrated" :
                          score >= 65 ? "Acceptable processing integrity with room for improvement" :
                                       "Processing integrity concerns detected",
                Recommendations = score >= 85 ? "Continue monitoring processing integrity" :
                                 "Implement additional data validation and error handling controls"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing PI1.1 - Processing Integrity");
            return CreateErrorAssessment("PI1.1", ex.Message);
        }
    }

    private async Task<ControlAssessment> AssessGenericControlAsync(ComplianceControl control)
    {
        // Generic assessment for controls not specifically implemented
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 50,
            Findings = $"Control {control.ControlId} requires detailed SOC2 assessment",
            Recommendations = $"Implement specific assessment logic for {control.ControlName}"
        };
    }

    // Helper methods
    private async Task<bool> CheckEncryptionConfigurationAsync()
    {
        try
        {
            var config = await _configService.GetConfigurationAsync("Security");
            return config?.ContainsKey("Encryption") == true;
        }
        catch
        {
            return false;
        }
    }

    private ControlAssessment CreateErrorAssessment(string controlId, string errorMessage)
    {
        return new ControlAssessment
        {
            Status = "Error",
            Score = 0,
            Findings = $"Assessment failed for {controlId}: {errorMessage}",
            Recommendations = $"Fix assessment implementation for {controlId}"
        };
    }

    public async Task<string> GenerateSummaryAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var resultsList = results.ToList();
        var totalControls = resultsList.Count;
        var compliantControls = resultsList.Count(r => r.Status == "Compliant");
        var avgScore = resultsList.Average(r => r.Score);

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
        var avgScore = results.Average(r => r.Score);
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