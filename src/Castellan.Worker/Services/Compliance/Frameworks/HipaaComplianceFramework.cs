using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Compliance;
using Castellan.Worker.Data;
using Microsoft.EntityFrameworkCore;

namespace Castellan.Worker.Services.Compliance.Frameworks;

public class HipaaComplianceFramework : IComplianceFramework
{
    private readonly ISecurityEventStore _eventStore;
    private readonly CastellanDbContext _context;
    private readonly ILogger<HipaaComplianceFramework> _logger;

    public string FrameworkName => "HIPAA";

    public HipaaComplianceFramework(
        ISecurityEventStore eventStore,
        CastellanDbContext context,
        ILogger<HipaaComplianceFramework> logger)
    {
        _eventStore = eventStore;
        _context = context;
        _logger = logger;
    }

    public async Task<ControlAssessment> AssessControlAsync(ComplianceControl control)
    {
        return control.ControlId switch
        {
            "164.312(a)(1)" => await AssessAccessControlAsync(), // Unique user identification
            "164.312(a)(2)(i)" => await AssessAutomaticLogoffAsync(), // Automatic logoff
            "164.312(b)" => await AssessAuditControlsAsync(), // Audit controls
            "164.312(c)(1)" => await AssessIntegrityAsync(), // Integrity controls
            "164.312(d)" => await AssessPersonAuthenticationAsync(), // Person authentication
            "164.312(e)(1)" => await AssessTransmissionSecurityAsync(), // Transmission security
            "164.308(a)(1)(i)" => await AssessSecurityOfficerAsync(), // Security officer
            "164.308(a)(3)(i)" => await AssessWorkforceTrainingAsync(), // Workforce training
            "164.308(a)(4)(i)" => await AssessAccessManagementAsync(), // Access management
            "164.308(a)(5)(i)" => await AssessSecurityAwarenessAsync(), // Security awareness
            _ => await AssessGenericControlAsync(control)
        };
    }

    private async Task<ControlAssessment> AssessAccessControlAsync()
    {
        // Check if we have proper user identification in security events
        var recentEvents = _eventStore.GetSecurityEvents(1, 1000);
        var authEvents = recentEvents.Where(e =>
            e.EventType == SecurityEventType.AuthenticationSuccess ||
            e.EventType == SecurityEventType.AuthenticationFailure)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        var eventsWithUserInfo = authEvents.Count(e => !string.IsNullOrEmpty(e.OriginalEvent.User));
        var totalEvents = authEvents.Count();

        if (totalEvents == 0)
        {
            return new ControlAssessment
            {
                Status = "NonCompliant",
                Score = 0,
                Findings = "No authentication events found in the last 30 days",
                Recommendations = "Ensure authentication logging is enabled and working properly"
            };
        }

        var userIdentificationRate = (double)eventsWithUserInfo / totalEvents * 100;

        if (userIdentificationRate >= 95)
        {
            return new ControlAssessment
            {
                Status = "Compliant",
                Score = 100,
                Evidence = $"User identification present in {userIdentificationRate:F1}% of authentication events ({eventsWithUserInfo}/{totalEvents})",
                Findings = "Unique user identification is properly implemented",
                Recommendations = "Continue monitoring user identification compliance"
            };
        }
        else if (userIdentificationRate >= 80)
        {
            return new ControlAssessment
            {
                Status = "PartiallyCompliant",
                Score = 75,
                Evidence = $"User identification present in {userIdentificationRate:F1}% of authentication events",
                Findings = "Some authentication events lack proper user identification",
                Recommendations = "Improve user identification logging to achieve 95%+ compliance"
            };
        }
        else
        {
            return new ControlAssessment
            {
                Status = "NonCompliant",
                Score = 25,
                Evidence = $"User identification only present in {userIdentificationRate:F1}% of authentication events",
                Findings = "Significant gaps in user identification logging",
                Recommendations = "Implement comprehensive user identification logging for all authentication events"
            };
        }
    }

    private async Task<ControlAssessment> AssessAuditControlsAsync()
    {
        // Check audit trail completeness
        var recentEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-7))
            .ToList();

        var requiredAuditEventTypes = new[]
        {
            SecurityEventType.AuthenticationSuccess,
            SecurityEventType.AuthenticationFailure,
            SecurityEventType.AccountManagement,
            SecurityEventType.PrivilegeEscalation,
            SecurityEventType.SecurityPolicyChange,
            SecurityEventType.NetworkConnection
        };

        var presentEventTypes = recentEvents
            .Select(e => e.EventType)
            .Distinct()
            .ToHashSet();

        var missingEventTypes = requiredAuditEventTypes
            .Where(type => !presentEventTypes.Contains(type))
            .ToList();

        var completenessScore = ((double)(requiredAuditEventTypes.Length - missingEventTypes.Count) / requiredAuditEventTypes.Length) * 100;

        if (completenessScore >= 90)
        {
            return new ControlAssessment
            {
                Status = "Compliant",
                Score = (int)completenessScore,
                Evidence = $"Audit event types captured: {presentEventTypes.Count}/{requiredAuditEventTypes.Length}",
                Findings = "Comprehensive audit controls are in place",
                Recommendations = "Continue monitoring audit trail completeness"
            };
        }
        else
        {
            return new ControlAssessment
            {
                Status = completenessScore >= 70 ? "PartiallyCompliant" : "NonCompliant",
                Score = (int)completenessScore,
                Evidence = $"Missing audit event types: {missingEventTypes.Count}/{requiredAuditEventTypes.Length}",
                Findings = "Audit controls have gaps in event coverage",
                Recommendations = $"Implement logging for missing event types. Found {presentEventTypes.Count} of {requiredAuditEventTypes.Length} required types"
            };
        }
    }

    private async Task<ControlAssessment> AssessTransmissionSecurityAsync()
    {
        // Check for secure transmission indicators
        var networkEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.EventType == SecurityEventType.NetworkConnection)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        var secureTransmissions = networkEvents.Count(e =>
            e.OriginalEvent.Message?.Contains("HTTPS", StringComparison.OrdinalIgnoreCase) == true ||
            e.OriginalEvent.Message?.Contains("TLS", StringComparison.OrdinalIgnoreCase) == true ||
            e.OriginalEvent.Message?.Contains("SSL", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("secure", StringComparison.OrdinalIgnoreCase) == true);

        var totalTransmissions = networkEvents.Count();

        if (totalTransmissions == 0)
        {
            return new ControlAssessment
            {
                Status = "PartiallyCompliant",
                Score = 50,
                Findings = "No network transmission events found for assessment",
                Recommendations = "Enable network transmission logging to verify encryption compliance"
            };
        }

        var encryptionRate = (double)secureTransmissions / totalTransmissions * 100;

        return new ControlAssessment
        {
            Status = encryptionRate >= 95 ? "Compliant" : encryptionRate >= 80 ? "PartiallyCompliant" : "NonCompliant",
            Score = (int)Math.Min(encryptionRate, 100),
            Evidence = $"Secure transmissions: {encryptionRate:F1}% ({secureTransmissions}/{totalTransmissions})",
            Findings = encryptionRate >= 95 ? "Transmission security is properly implemented" : "Some transmissions may lack proper encryption",
            Recommendations = encryptionRate >= 95 ? "Continue monitoring transmission security" : "Ensure all data transmissions use encryption (HTTPS/TLS)"
        };
    }

    private async Task<ControlAssessment> AssessAutomaticLogoffAsync()
    {
        // Check for session timeout events
        var sessionEvents = _eventStore.GetSecurityEvents(1, 500)
            .Where(e => e.EventType == SecurityEventType.AuthenticationFailure ||
                       e.EventType == SecurityEventType.AuthenticationSuccess)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-7))
            .ToList();

        var automaticLogoffs = sessionEvents.Count(e =>
            e.OriginalEvent.Message?.Contains("automatic", StringComparison.OrdinalIgnoreCase) == true ||
            e.OriginalEvent.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true);

        if (sessionEvents.Count == 0)
        {
            return new ControlAssessment
            {
                Status = "NonCompliant",
                Score = 30,
                Findings = "No session management events found - automatic logoff cannot be verified",
                Recommendations = "Implement session timeout logging and automatic logoff mechanisms"
            };
        }

        var logoffRate = sessionEvents.Count > 0 ? (double)automaticLogoffs / sessionEvents.Count * 100 : 0;

        return new ControlAssessment
        {
            Status = logoffRate >= 50 ? "Compliant" : logoffRate >= 25 ? "PartiallyCompliant" : "NonCompliant",
            Score = Math.Max(30, (int)Math.Min(logoffRate * 2, 100)), // Baseline 30 points for having session events
            Evidence = $"Automatic logoff events: {automaticLogoffs}/{sessionEvents.Count} session events",
            Findings = logoffRate >= 50 ? "Automatic logoff mechanisms are functioning" : "Automatic logoff implementation needs improvement",
            Recommendations = logoffRate >= 50 ? "Continue monitoring session timeout compliance" : "Implement and configure automatic session timeout mechanisms"
        };
    }

    private async Task<ControlAssessment> AssessIntegrityAsync()
    {
        // Check for data integrity monitoring - using security policy changes as proxy
        var integrityEvents = _eventStore.GetSecurityEvents(1, 500)
            .Where(e => e.EventType == SecurityEventType.SecurityPolicyChange ||
                       e.OriginalEvent.Message.Contains("integrity", StringComparison.OrdinalIgnoreCase) ||
                       e.Summary.Contains("integrity", StringComparison.OrdinalIgnoreCase))
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        var score = integrityEvents.Count > 0 ? Math.Min(75 + integrityEvents.Count * 5, 100) : 40;

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Integrity monitoring events found: {integrityEvents.Count}",
            Findings = score >= 80 ? "Data integrity controls are active" : "Limited data integrity monitoring detected",
            Recommendations = score >= 80 ? "Continue integrity monitoring" : "Enhance data integrity validation and monitoring systems"
        };
    }

    private async Task<ControlAssessment> AssessPersonAuthenticationAsync()
    {
        // Check authentication strength and methods
        var authEvents = _eventStore.GetSecurityEvents(1, 1000)
            .Where(e => e.EventType == SecurityEventType.AuthenticationSuccess ||
                       e.EventType == SecurityEventType.AuthenticationFailure)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        var strongAuthEvents = authEvents.Count(e =>
            e.OriginalEvent.Message?.Contains("MFA", StringComparison.OrdinalIgnoreCase) == true ||
            e.OriginalEvent.Message?.Contains("Multi-Factor", StringComparison.OrdinalIgnoreCase) == true ||
            e.OriginalEvent.Message?.Contains("2FA", StringComparison.OrdinalIgnoreCase) == true ||
            e.OriginalEvent.Message?.Contains("Certificate", StringComparison.OrdinalIgnoreCase) == true ||
            e.Summary?.Contains("strong", StringComparison.OrdinalIgnoreCase) == true);

        if (authEvents.Count == 0)
        {
            return new ControlAssessment
            {
                Status = "NonCompliant",
                Score = 0,
                Findings = "No authentication events found",
                Recommendations = "Implement person authentication logging"
            };
        }

        var strongAuthRate = (double)strongAuthEvents / authEvents.Count * 100;
        var baseScore = strongAuthRate > 0 ? 70 : 50; // Base score for having authentication
        var finalScore = (int)Math.Min(baseScore + (strongAuthRate / 2), 100);

        return new ControlAssessment
        {
            Status = finalScore >= 80 ? "Compliant" : finalScore >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = finalScore,
            Evidence = $"Strong authentication rate: {strongAuthRate:F1}% ({strongAuthEvents}/{authEvents.Count})",
            Findings = strongAuthRate >= 50 ? "Strong person authentication is implemented" : "Person authentication could be strengthened",
            Recommendations = strongAuthRate >= 50 ? "Continue monitoring authentication strength" : "Implement multi-factor authentication where possible"
        };
    }

    private async Task<ControlAssessment> AssessSecurityOfficerAsync()
    {
        // This is typically a policy/organizational control - assign moderate score
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 75,
            Findings = "Security officer role requires manual verification",
            Recommendations = "Ensure designated security officer is assigned and trained"
        };
    }

    private async Task<ControlAssessment> AssessWorkforceTrainingAsync()
    {
        // This is typically a policy/organizational control - assign moderate score
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 70,
            Findings = "Workforce training requires manual verification and documentation",
            Recommendations = "Maintain training records and conduct regular security awareness training"
        };
    }

    private async Task<ControlAssessment> AssessAccessManagementAsync()
    {
        // Check for access management events
        var accessEvents = _eventStore.GetSecurityEvents(1, 500)
            .Where(e => e.EventType == SecurityEventType.AccountManagement ||
                       e.EventType == SecurityEventType.PrivilegeEscalation ||
                       e.EventType == SecurityEventType.SecurityPolicyChange)
            .Where(e => e.OriginalEvent.Time >= DateTimeOffset.UtcNow.AddDays(-30))
            .ToList();

        var score = Math.Min(50 + accessEvents.Count * 2, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Access management events: {accessEvents.Count}",
            Findings = score >= 80 ? "Access management controls are active" : "Access management monitoring could be improved",
            Recommendations = "Implement comprehensive access control logging and regular access reviews"
        };
    }

    private async Task<ControlAssessment> AssessSecurityAwarenessAsync()
    {
        // This is typically a policy/training control - assign moderate score
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 70,
            Findings = "Security awareness program requires manual verification",
            Recommendations = "Maintain security awareness training program with regular updates"
        };
    }

    private async Task<ControlAssessment> AssessGenericControlAsync(ComplianceControl control)
    {
        // Generic assessment for controls not specifically implemented
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
        var totalControls = resultsList.Count;
        var compliantControls = resultsList.Count(r => r.Status == "Compliant");
        var partiallyCompliantControls = resultsList.Count(r => r.Status == "PartiallyCompliant");
        var nonCompliantControls = resultsList.Count(r => r.Status == "NonCompliant");

        var avgScore = resultsList.Any() ? resultsList.Average(r => r.Score) : 0;

        var categories = new[]
        {
            ("Administrative Safeguards", resultsList.Where(r => r.ControlId.StartsWith("164.308"))),
            ("Physical Safeguards", resultsList.Where(r => r.ControlId.StartsWith("164.310"))),
            ("Technical Safeguards", resultsList.Where(r => r.ControlId.StartsWith("164.312")))
        };

        var categoryScores = categories
            .Where(c => c.Item2.Any())
            .Select(c => $"{c.Item1}: {c.Item2.Average(r => r.Score):F0}% compliant")
            .ToList();

        return $"HIPAA compliance assessment shows {avgScore:F0}% overall compliance. " +
               $"Controls status: {compliantControls} compliant, {partiallyCompliantControls} partially compliant, {nonCompliantControls} non-compliant. " +
               $"{string.Join(", ", categoryScores)}.";
    }

    public async Task<string> GenerateKeyFindingsAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var criticalFindings = results
            .Where(r => r.Status == "NonCompliant" || r.Score < 70)
            .OrderBy(r => r.Score)
            .Take(5)
            .Select(r => $"• {r.ControlId}: {r.Findings}")
            .ToList();

        var positiveFindings = results
            .Where(r => r.Status == "Compliant" && r.Score >= 95)
            .Take(3)
            .Select(r => $"• {r.ControlId}: {r.Findings}")
            .ToList();

        var findings = new List<string>();

        if (criticalFindings.Any())
        {
            findings.Add("Areas requiring attention:");
            findings.AddRange(criticalFindings);
        }

        if (positiveFindings.Any())
        {
            findings.Add("\nStrong compliance areas:");
            findings.AddRange(positiveFindings);
        }

        return findings.Any() ? string.Join("\n", findings) : "No critical findings identified.";
    }

    public async Task<string> GenerateRecommendationsAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var recommendations = results
            .Where(r => !string.IsNullOrEmpty(r.Recommendations) && r.Status != "Compliant")
            .OrderBy(r => r.Score)
            .Select(r => $"• {r.Recommendations}")
            .Distinct()
            .Take(10)
            .ToList();

        // Add general recommendations based on patterns
        var lowAuditScore = results.Any(r => r.ControlId.Contains("audit") && r.Score < 80);
        if (lowAuditScore)
        {
            recommendations.Add("• Enhance audit trail completeness and review procedures");
        }

        var lowAccessControlScore = results.Any(r => r.ControlId.Contains("access") && r.Score < 80);
        if (lowAccessControlScore)
        {
            recommendations.Add("• Strengthen access controls and user authentication mechanisms");
        }

        return recommendations.Any()
            ? string.Join("\n", recommendations)
            : "No specific recommendations - maintain current compliance practices.";
    }
}