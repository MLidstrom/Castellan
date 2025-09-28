using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Compliance;
using Castellan.Worker.Data;
using Microsoft.EntityFrameworkCore;

namespace Castellan.Worker.Services.Compliance.Frameworks;

/// <summary>
/// Sarbanes-Oxley (SOX) compliance framework implementation
/// Focuses on financial controls, audit trails, and data integrity
/// </summary>
public class SoxComplianceFramework : IComplianceFramework
{
    private readonly ISecurityEventStore _eventStore;
    private readonly CastellanDbContext _context;
    private readonly ILogger<SoxComplianceFramework> _logger;

    public string FrameworkName => "SOX";

    public SoxComplianceFramework(
        ISecurityEventStore eventStore,
        CastellanDbContext context,
        ILogger<SoxComplianceFramework> logger)
    {
        _eventStore = eventStore;
        _context = context;
        _logger = logger;
    }

    public async Task<ControlAssessment> AssessControlAsync(ComplianceControl control)
    {
        return control.ControlId switch
        {
            "302" => await AssessFinancialReportingControlsAsync(),           // Corporate responsibility for financial reports
            "404" => await AssessInternalControlsAsync(),                     // Management assessment of internal controls
            "409" => await AssessFinancialChangeControlsAsync(),              // Real-time issuer disclosures
            "802" => await AssessCriminalPenaltiesAsync(),                    // Criminal penalties for altering documents
            "906" => await AssessCeoSignoffAsync(),                          // Corporate responsibility for financial reports
            "IT-GC-01" => await AssessAccessControlsAsync(),                  // IT General Control - Access Management
            "IT-GC-02" => await AssessChangeManagementAsync(),               // IT General Control - Change Management
            "IT-GC-03" => await AssessDataBackupAsync(),                     // IT General Control - Data Backup
            "IT-GC-04" => await AssessSystemOperationsAsync(),               // IT General Control - System Operations
            "IT-GC-05" => await AssessSegregationOfDutiesAsync(),            // IT General Control - Segregation of Duties
            _ => await AssessGenericControlAsync(control)
        };
    }

    private async Task<ControlAssessment> AssessFinancialReportingControlsAsync()
    {
        var recentEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        // Check for financial system access and modifications
        var financialEvents = recentEvents.Where(e =>
            e.Summary?.Contains("financial", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("accounting", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("ledger", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("modify", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("data", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var auditedEvents = financialEvents.Count(e =>
            !string.IsNullOrEmpty(e.OriginalEvent.User) &&
            e.OriginalEvent.Time != default);

        if (financialEvents.Count == 0)
        {
            return new ControlAssessment
            {
                Status = "PartiallyCompliant",
                Score = 50,
                Findings = "No financial reporting events detected for assessment",
                Recommendations = "Ensure financial system logging is properly configured"
            };
        }

        var auditRate = (double)auditedEvents / financialEvents.Count * 100;

        return new ControlAssessment
        {
            Status = auditRate >= 95 ? "Compliant" : auditRate >= 80 ? "PartiallyCompliant" : "NonCompliant",
            Score = (int)Math.Min(auditRate, 100),
            Evidence = $"Financial reporting audit rate: {auditRate:F1}% ({auditedEvents}/{financialEvents.Count})",
            Findings = auditRate >= 95 ? "Financial reporting controls are properly implemented" : "Gaps in financial reporting audit trail",
            Recommendations = auditRate >= 95 ? "Continue monitoring financial controls" : "Enhance audit logging for all financial transactions"
        };
    }

    private async Task<ControlAssessment> AssessInternalControlsAsync()
    {
        var recentEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-90))
            .ToList();

        // Assess various internal control indicators
        var controlTypes = new[]
        {
            (SecurityEventType.AuthenticationSuccess, "Authentication"),
            (SecurityEventType.PrivilegeEscalation, "Authorization"),
            (SecurityEventType.SuspiciousActivity, "Data Changes"),
            (SecurityEventType.SecurityPolicyChange, "Configuration"),
            (SecurityEventType.AccountManagement, "Account Management")
        };

        var controlCoverage = controlTypes
            .Select(ct => new
            {
                Type = ct.Item2,
                Count = recentEvents.Count(e => e.EventType == ct.Item1),
                HasLogging = recentEvents.Any(e => e.EventType == ct.Item1)
            })
            .ToList();

        var coverageRate = (double)controlCoverage.Count(c => c.HasLogging) / controlTypes.Length * 100;
        var totalEvents = controlCoverage.Sum(c => c.Count);

        if (totalEvents < 10)
        {
            return new ControlAssessment
            {
                Status = "NonCompliant",
                Score = 25,
                Findings = "Insufficient internal control monitoring data",
                Recommendations = "Implement comprehensive internal control monitoring"
            };
        }

        var missingControls = controlCoverage
            .Where(c => !c.HasLogging)
            .Select(c => c.Type)
            .ToList();

        return new ControlAssessment
        {
            Status = coverageRate >= 90 ? "Compliant" : coverageRate >= 70 ? "PartiallyCompliant" : "NonCompliant",
            Score = (int)Math.Min(coverageRate, 100),
            Evidence = $"Internal control coverage: {coverageRate:F1}% ({controlCoverage.Count(c => c.HasLogging)}/{controlTypes.Length} control types)",
            Findings = missingControls.Any()
                ? $"Missing controls: {string.Join(", ", missingControls)}"
                : "All internal control types are monitored",
            Recommendations = coverageRate >= 90
                ? "Maintain current internal control monitoring"
                : "Implement monitoring for missing control types"
        };
    }

    private async Task<ControlAssessment> AssessAccessControlsAsync()
    {
        var recentEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        var accessEvents = recentEvents.Where(e =>
            e.EventType == SecurityEventType.AuthenticationSuccess ||
            e.EventType == SecurityEventType.AuthenticationFailure ||
            e.EventType == SecurityEventType.PrivilegeEscalation ||
            e.EventType == SecurityEventType.AccountManagement)
            .ToList();

        if (accessEvents.Count == 0)
        {
            return new ControlAssessment
            {
                Status = "NonCompliant",
                Score = 0,
                Findings = "No access control events found",
                Recommendations = "Enable comprehensive access control logging"
            };
        }

        // Check for proper access control indicators
        var failedAuthRate = (double)accessEvents.Count(e =>
            e.EventType == SecurityEventType.AuthenticationFailure ||
            e.EventType == SecurityEventType.PrivilegeEscalation) / accessEvents.Count;

        var hasAccountManagement = accessEvents.Any(e => e.EventType == SecurityEventType.AccountManagement);
        var hasUserIdentification = accessEvents.Count(e => !string.IsNullOrEmpty(e.OriginalEvent.User));

        var identificationRate = (double)hasUserIdentification / accessEvents.Count * 100;
        var score = (int)(identificationRate * 0.7 + (hasAccountManagement ? 30 : 0));

        // High failed auth rate might indicate attacks or poor controls
        if (failedAuthRate > 0.2)
        {
            score = Math.Max(score - 20, 0);
        }

        return new ControlAssessment
        {
            Status = score >= 90 ? "Compliant" : score >= 70 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Access events: {accessEvents.Count}, User identification: {identificationRate:F1}%, Failed auth rate: {failedAuthRate:P1}",
            Findings = score >= 90
                ? "Access controls are properly implemented and monitored"
                : "Access control monitoring needs improvement",
            Recommendations = failedAuthRate > 0.2
                ? "Investigate high authentication failure rate"
                : "Continue monitoring access control effectiveness"
        };
    }

    private async Task<ControlAssessment> AssessChangeManagementAsync()
    {
        var recentEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        var changeEvents = recentEvents.Where(e =>
            e.EventType == SecurityEventType.SecurityPolicyChange ||
            e.Summary?.Contains("change", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("update", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("modify", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (changeEvents.Count == 0)
        {
            return new ControlAssessment
            {
                Status = "PartiallyCompliant",
                Score = 60,
                Findings = "No change management events detected",
                Recommendations = "Verify change management logging is configured"
            };
        }

        // Check for proper change documentation
        var documentedChanges = changeEvents.Count(e =>
            !string.IsNullOrEmpty(e.OriginalEvent.User) &&
            !string.IsNullOrEmpty(e.Summary) &&
            e.OriginalEvent.Time != default);

        var documentationRate = (double)documentedChanges / changeEvents.Count * 100;

        // Check for unauthorized changes (high risk events without proper context)
        var suspiciousChanges = changeEvents.Count(e =>
            e.RiskLevel == "high" || e.RiskLevel == "critical");

        var score = (int)Math.Max(documentationRate - (suspiciousChanges * 5), 0);

        return new ControlAssessment
        {
            Status = score >= 85 ? "Compliant" : score >= 70 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Change events: {changeEvents.Count}, Documentation rate: {documentationRate:F1}%, Suspicious: {suspiciousChanges}",
            Findings = suspiciousChanges > 0
                ? $"Detected {suspiciousChanges} potentially unauthorized changes"
                : "Change management controls are functioning",
            Recommendations = score >= 85
                ? "Maintain current change management practices"
                : "Improve change documentation and approval processes"
        };
    }

    private async Task<ControlAssessment> AssessSegregationOfDutiesAsync()
    {
        var recentEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        // Group events by user to analyze duties
        var userActivities = recentEvents
            .Where(e => !string.IsNullOrEmpty(e.OriginalEvent.User))
            .GroupBy(e => e.OriginalEvent.User)
            .Select(g => new
            {
                User = g.Key,
                Activities = g.Select(e => e.EventType).Distinct().ToList(),
                EventCount = g.Count()
            })
            .ToList();

        if (!userActivities.Any())
        {
            return new ControlAssessment
            {
                Status = "NonCompliant",
                Score = 0,
                Findings = "No user activity data available for segregation of duties analysis",
                Recommendations = "Implement user activity tracking"
            };
        }

        // Check for conflicting duties (users performing both configuration and audit activities)
        var conflictingDuties = userActivities.Where(u =>
            (u.Activities.Contains(SecurityEventType.SecurityPolicyChange) &&
             u.Activities.Contains(SecurityEventType.SuspiciousActivity)) ||
            (u.Activities.Contains(SecurityEventType.AccountManagement) &&
             u.Activities.Contains(SecurityEventType.SuspiciousActivity)))
            .ToList();

        var conflictRate = (double)conflictingDuties.Count / userActivities.Count * 100;
        var score = Math.Max(100 - (int)(conflictRate * 2), 0);

        return new ControlAssessment
        {
            Status = conflictRate <= 5 ? "Compliant" : conflictRate <= 15 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Users analyzed: {userActivities.Count}, Conflicts detected: {conflictingDuties.Count}",
            Findings = conflictingDuties.Any()
                ? $"Users with conflicting duties: {string.Join(", ", conflictingDuties.Take(3).Select(u => u.User))}"
                : "No segregation of duties violations detected",
            Recommendations = conflictRate <= 5
                ? "Continue monitoring for segregation of duties compliance"
                : "Review and adjust user permissions to eliminate conflicts"
        };
    }

    private async Task<ControlAssessment> AssessFinancialChangeControlsAsync()
    {
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 75,
            Findings = "Financial change controls require manual review",
            Recommendations = "Implement automated financial change tracking"
        };
    }

    private async Task<ControlAssessment> AssessCriminalPenaltiesAsync()
    {
        // Check for document alteration or deletion events
        var recentEvents = _eventStore.GetSecurityEvents(1, 500)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-90))
            .ToList();

        var alterationEvents = recentEvents.Where(e =>
            e.EventType == SecurityEventType.SuspiciousActivity ||
            e.Summary?.Contains("delete", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("alter", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("modify", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var score = alterationEvents.Any() ? 80 : 100;

        return new ControlAssessment
        {
            Status = alterationEvents.Any() ? "PartiallyCompliant" : "Compliant",
            Score = score,
            Evidence = $"Document alteration events: {alterationEvents.Count}",
            Findings = alterationEvents.Any()
                ? "Document modifications detected - requires review"
                : "No suspicious document alterations detected",
            Recommendations = "Maintain audit trail for all document modifications"
        };
    }

    private async Task<ControlAssessment> AssessCeoSignoffAsync()
    {
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 70,
            Findings = "CEO sign-off processes require manual verification",
            Recommendations = "Implement digital signature verification for executive approvals"
        };
    }

    private async Task<ControlAssessment> AssessDataBackupAsync()
    {
        // Check for backup-related events
        var recentEvents = _eventStore.GetSecurityEvents(1, 500)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-7))
            .ToList();

        var backupEvents = recentEvents.Where(e =>
            e.Summary?.Contains("backup", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var hasRecentBackup = backupEvents.Any(e =>
            e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-1));

        var score = hasRecentBackup ? 100 : backupEvents.Any() ? 70 : 30;

        return new ControlAssessment
        {
            Status = hasRecentBackup ? "Compliant" : backupEvents.Any() ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Backup events in last 7 days: {backupEvents.Count}",
            Findings = hasRecentBackup
                ? "Regular backups are being performed"
                : "Backup schedule may not be meeting requirements",
            Recommendations = hasRecentBackup
                ? "Continue current backup schedule"
                : "Ensure daily backups are performed and logged"
        };
    }

    private async Task<ControlAssessment> AssessSystemOperationsAsync()
    {
        var recentEvents = _eventStore.GetSecurityEvents(1, 500)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-1))
            .ToList();

        // Check system health indicators
        var errorEvents = recentEvents.Count(e =>
            e.RiskLevel == "critical");

        var systemEvents = recentEvents.Count(e =>
            e.Summary?.Contains("system", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("service", StringComparison.OrdinalIgnoreCase) == true);

        var errorRate = recentEvents.Any() ? (double)errorEvents / recentEvents.Count : 0;
        var score = Math.Max(100 - (int)(errorRate * 200), 0);

        return new ControlAssessment
        {
            Status = errorRate <= 0.05 ? "Compliant" : errorRate <= 0.15 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"System events: {systemEvents}, Error rate: {errorRate:P1}",
            Findings = errorRate <= 0.05
                ? "System operations are stable"
                : "Elevated error rate detected in system operations",
            Recommendations = errorRate <= 0.05
                ? "Continue monitoring system health"
                : "Investigate and resolve system errors"
        };
    }

    private async Task<ControlAssessment> AssessGenericControlAsync(ComplianceControl control)
    {
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 50,
            Findings = $"Control {control.ControlId} requires manual assessment",
            Recommendations = $"Implement automated assessment for {control.ControlName}"
        };
    }

    public async Task<string> GenerateSummaryAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var resultsList = results.ToList();
        var avgScore = resultsList.Any() ? resultsList.Average(r => r.Score) : 0;
        var compliantCount = resultsList.Count(r => r.Status == "Compliant");
        var totalCount = resultsList.Count;

        var itControls = resultsList.Where(r => r.ControlId.StartsWith("IT-GC")).ToList();
        var itScore = itControls.Any() ? itControls.Average(r => r.Score) : 0;

        return $"SOX compliance assessment shows {avgScore:F0}% overall compliance. " +
               $"{compliantCount}/{totalCount} controls are compliant. " +
               $"IT General Controls average: {itScore:F0}%. " +
               "Focus areas: financial reporting accuracy, internal controls effectiveness, and audit trail completeness.";
    }

    public async Task<string> GenerateKeyFindingsAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var findings = new List<string>();

        var criticalGaps = results
            .Where(r => r.Status == "NonCompliant" && r.Score < 50)
            .OrderBy(r => r.Score)
            .Take(5)
            .ToList();

        if (criticalGaps.Any())
        {
            findings.Add("Critical compliance gaps:");
            findings.AddRange(criticalGaps.Select(r => $"• {r.ControlId}: {r.Findings}"));
        }

        var strengths = results
            .Where(r => r.Status == "Compliant" && r.Score >= 95)
            .Take(3)
            .ToList();

        if (strengths.Any())
        {
            findings.Add("\nCompliance strengths:");
            findings.AddRange(strengths.Select(r => $"• {r.ControlId}: Well-implemented controls"));
        }

        return findings.Any() ? string.Join("\n", findings) : "No significant findings identified.";
    }

    public async Task<string> GenerateRecommendationsAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var recommendations = new List<string>();

        // Prioritize recommendations based on SOX requirements
        var itGcGaps = results.Where(r =>
            r.ControlId.StartsWith("IT-GC") &&
            r.Status != "Compliant").ToList();

        if (itGcGaps.Any())
        {
            recommendations.Add("• Strengthen IT General Controls to meet SOX requirements");
        }

        var financialControlGaps = results.Where(r =>
            (r.ControlId == "302" || r.ControlId == "404") &&
            r.Score < 80).ToList();

        if (financialControlGaps.Any())
        {
            recommendations.Add("• Enhance financial reporting controls and documentation");
        }

        var changeManagementIssues = results.Any(r =>
            r.ControlId == "IT-GC-02" && r.Score < 80);

        if (changeManagementIssues)
        {
            recommendations.Add("• Implement formal change management procedures with approval workflows");
        }

        var segregationIssues = results.Any(r =>
            r.ControlId == "IT-GC-05" && r.Score < 80);

        if (segregationIssues)
        {
            recommendations.Add("• Review and adjust user roles to ensure proper segregation of duties");
        }

        if (!recommendations.Any())
        {
            recommendations.Add("• Continue current compliance practices and monitoring");
            recommendations.Add("• Schedule quarterly compliance reviews");
        }

        return string.Join("\n", recommendations);
    }
}