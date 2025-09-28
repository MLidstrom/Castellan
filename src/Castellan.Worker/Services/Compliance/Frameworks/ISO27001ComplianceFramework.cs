using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Models.Compliance;

namespace Castellan.Worker.Services.Compliance.Frameworks;

/// <summary>
/// ISO 27001:2022 Information Security Management System (ISMS) compliance framework implementation.
/// Focuses on information security controls, risk management, and continuous improvement.
/// </summary>
public class ISO27001ComplianceFramework : IComplianceFramework
{
    private readonly ILogger<ISO27001ComplianceFramework> _logger;
    private readonly ISecurityEventStore _securityEventStore;

    public string FrameworkName => "ISO 27001";

    public ISO27001ComplianceFramework(
        ILogger<ISO27001ComplianceFramework> logger,
        ISecurityEventStore securityEventStore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _securityEventStore = securityEventStore ?? throw new ArgumentNullException(nameof(securityEventStore));
    }

    public async Task<ControlAssessment> AssessControlAsync(ComplianceControl control)
    {
        return control.ControlId switch
        {
            "ISO-5.1" => await AssessInformationSecurityPolicyAsync(),
            "ISO-5.2" => await AssessRiskManagementAsync(),
            "ISO-6.1" => await AssessPersonnelSecurityAsync(),
            "ISO-6.3" => await AssessSecurityAwarenessAsync(),
            "ISO-8.1" => await AssessAccessManagementAsync(),
            "ISO-8.24" => await AssessCryptographyAsync(),
            "ISO-8.8" => await AssessSystemSecurityAsync(),
            "ISO-8.20" => await AssessNetworkSecurityManagementAsync(),
            "ISO-8.25" => await AssessApplicationSecurityAsync(),
            "ISO-8.9" => await AssessSecureConfigurationAsync(),
            "ISO-8.15" => await AssessLoggingMonitoringAsync(),
            "ISO-8.13" => await AssessBackupAsync(),
            "ISO-7.1" => await AssessPhysicalEnvironmentalSecurityAsync(),
            "ISO-5.24" => await AssessIncidentManagementAsync(),
            "ISO-5.29" => await AssessBusinessContinuityAsync(),
            _ => await AssessGenericControlAsync(control)
        };
    }

    private async Task<ControlAssessment> AssessInformationSecurityPolicyAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var policyEvents = events.Where(e =>
            e.Summary.Contains("policy", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(70 + policyEvents.Count, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Policy-related events: {policyEvents.Count}",
            Findings = score >= 80 ? "Information security policies are being monitored" : "Policy governance needs enhancement",
            Recommendations = "Develop comprehensive information security policies and ensure regular reviews"
        };
    }

    private async Task<ControlAssessment> AssessRiskManagementAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var riskEvents = events.Where(e =>
            e.RiskLevel == "high" || e.RiskLevel == "critical" ||
            e.Summary.Contains("risk", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Max(50, 100 - riskEvents.Count * 2);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"High/critical risk events: {riskEvents.Count}",
            Findings = riskEvents.Count > 10 ? "High number of risk events requiring attention" : "Risk management appears effective",
            Recommendations = "Implement formal risk assessment process and regular risk reviews"
        };
    }

    private async Task<ControlAssessment> AssessPersonnelSecurityAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var personnelEvents = events.Where(e =>
            e.Summary.Contains("insider", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("personnel", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = personnelEvents.Any() ? 65 : 80; // Lower if insider incidents

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Personnel security events: {personnelEvents.Count}",
            Findings = personnelEvents.Any() ? "Personnel security incidents detected" : "No personnel security issues detected",
            Recommendations = "Implement background verification checks and regular security awareness training"
        };
    }

    private async Task<ControlAssessment> AssessSecurityAwarenessAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var phishingEvents = events.Where(e =>
            e.Summary.Contains("phishing", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("social engineering", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = phishingEvents.Count > 10 ? 60 : 75;

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Phishing/social engineering events: {phishingEvents.Count}",
            Findings = phishingEvents.Count > 10 ? "High susceptibility to social engineering" : "Security awareness training effectiveness needs verification",
            Recommendations = "Implement regular security awareness training and phishing simulation tests"
        };
    }

    private async Task<ControlAssessment> AssessAccessManagementAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var accessEvents = events.Where(e =>
            e.EventType == SecurityEventType.PrivilegeEscalation ||
            e.EventType == SecurityEventType.AccountManagement ||
            e.Summary.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Max(50, 90 - accessEvents.Count * 3);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Access management events: {accessEvents.Count}",
            Findings = accessEvents.Count > 10 ? "Access control issues detected" : "Access management controls functioning",
            Recommendations = "Implement comprehensive access management with regular access reviews"
        };
    }

    private async Task<ControlAssessment> AssessCryptographyAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var cryptoEvents = events.Where(e =>
            e.Summary.Contains("encryption", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("TLS", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(70 + cryptoEvents.Count * 2, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Cryptography-related events: {cryptoEvents.Count}",
            Findings = score >= 80 ? "Cryptography controls are monitored" : "Cryptographic protection needs enhancement",
            Recommendations = "Implement strong encryption policies and key management procedures"
        };
    }

    private async Task<ControlAssessment> AssessSystemSecurityAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var malwareEvents = events.Where(e =>
            e.EventType == SecurityEventType.SuspiciousActivity ||
            e.Summary.Contains("malware", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = malwareEvents.Count > 5 ? 65 : 85;

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Malware detection events: {malwareEvents.Count}",
            Findings = malwareEvents.Count > 5 ? "Multiple malware incidents detected" : "System security controls appear effective",
            Recommendations = "Maintain up-to-date anti-malware protection and system monitoring"
        };
    }

    private async Task<ControlAssessment> AssessNetworkSecurityManagementAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var networkEvents = events.Where(e =>
            e.EventType == SecurityEventType.NetworkConnection ||
            e.Summary.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("firewall", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(60 + networkEvents.Count * 2, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Network security events: {networkEvents.Count}",
            Findings = score >= 80 ? "Network security monitoring active" : "Network security management needs improvement",
            Recommendations = "Implement comprehensive network monitoring and intrusion detection"
        };
    }

    private async Task<ControlAssessment> AssessApplicationSecurityAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var appEvents = events.Where(e =>
            e.Summary.Contains("SQL injection", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("XSS", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("application", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = appEvents.Count > 5 ? 60 : 75;

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Application security events: {appEvents.Count}",
            Findings = appEvents.Count > 5 ? "Application security vulnerabilities detected" : "Application security monitoring needs verification",
            Recommendations = "Implement secure development practices and regular application security testing"
        };
    }

    private async Task<ControlAssessment> AssessSecureConfigurationAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var configEvents = events.Where(e =>
            e.EventType == SecurityEventType.SecurityPolicyChange ||
            e.Summary.Contains("configuration", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(65 + configEvents.Count, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Configuration change events: {configEvents.Count}",
            Findings = score >= 80 ? "Configuration management is monitored" : "Secure configuration practices need verification",
            Recommendations = "Implement configuration management controls and change monitoring"
        };
    }

    private async Task<ControlAssessment> AssessLoggingMonitoringAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var logEvents = events.Where(e =>
            e.Summary.Contains("audit", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("log", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(70 + logEvents.Count, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Logging/audit events: {logEvents.Count}",
            Findings = score >= 80 ? "Logging and monitoring controls active" : "Audit trail monitoring needs enhancement",
            Recommendations = "Implement comprehensive logging and regular log review procedures"
        };
    }

    private async Task<ControlAssessment> AssessBackupAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var backupEvents = events.Where(e =>
            e.Summary.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("restore", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(75 + backupEvents.Count, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Backup/restore events: {backupEvents.Count}",
            Findings = score >= 80 ? "Backup procedures are monitored" : "Backup and recovery procedures need verification",
            Recommendations = "Implement regular backup testing and recovery procedures"
        };
    }

    private async Task<ControlAssessment> AssessPhysicalEnvironmentalSecurityAsync()
    {
        // Physical security typically requires manual assessment
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 70,
            Evidence = "Physical security requires on-site assessment",
            Findings = "Physical and environmental controls need manual verification",
            Recommendations = "Conduct regular physical security assessments and environmental monitoring"
        };
    }

    private async Task<ControlAssessment> AssessIncidentManagementAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var incidentEvents = events.Where(e =>
            e.RiskLevel == "critical" || e.RiskLevel == "high").ToList();

        var score = Math.Max(60, 90 - incidentEvents.Count);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"High-severity security events: {incidentEvents.Count}",
            Findings = incidentEvents.Count > 5 ? "High number of security incidents" : "Incident management appears effective",
            Recommendations = "Establish formal incident response procedures and team"
        };
    }

    private async Task<ControlAssessment> AssessBusinessContinuityAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var continuityEvents = events.Where(e =>
            e.Summary.Contains("outage", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("service disruption", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = continuityEvents.Count > 3 ? 65 : 80;

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Service disruption events: {continuityEvents.Count}",
            Findings = continuityEvents.Count > 3 ? "Multiple service disruptions detected" : "Business continuity monitoring active",
            Recommendations = "Develop and test business continuity and disaster recovery plans"
        };
    }

    private async Task<ControlAssessment> AssessGenericControlAsync(ComplianceControl control)
    {
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 65,
            Evidence = $"Control {control.ControlId} requires manual assessment",
            Findings = $"Generic assessment for {control.ControlName}",
            Recommendations = $"Implement specific monitoring and assessment for {control.ControlName}"
        };
    }

    public async Task<string> GenerateSummaryAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var resultsList = results.ToList();
        var avgScore = resultsList.Any() ? resultsList.Average(r => r.Score) : 0;
        var compliantCount = resultsList.Count(r => r.Status == "Compliant");
        var partialCount = resultsList.Count(r => r.Status == "PartiallyCompliant");
        var nonCompliantCount = resultsList.Count(r => r.Status == "NonCompliant");

        return $"ISO 27001 compliance assessment shows {avgScore:F1}% overall compliance. " +
               $"Controls status: {compliantCount} compliant, {partialCount} partially compliant, " +
               $"{nonCompliantCount} non-compliant. Key focus areas include access management, " +
               $"cryptography controls, and incident management processes.";
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
            .Where(r => r.Status == "Compliant" && r.Score >= 90)
            .Take(2)
            .Select(r => $"• {r.ControlId}: Strong compliance")
            .ToList();

        var findings = new List<string>();

        if (criticalFindings.Any())
        {
            findings.Add("Areas requiring immediate attention:");
            findings.AddRange(criticalFindings);
        }

        if (positiveFindings.Any())
        {
            findings.Add("\nWell-implemented controls:");
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
            .Take(8)
            .ToList();

        // Add specific ISO 27001 recommendations
        var hasAccessIssues = results.Any(r => r.ControlId.Contains("ISO-8.1") && r.Score < 80);
        if (hasAccessIssues)
        {
            recommendations.Add("• Implement comprehensive identity and access management (IAM) solution");
        }

        var hasCryptoIssues = results.Any(r => r.ControlId.Contains("ISO-8.24") && r.Score < 80);
        if (hasCryptoIssues)
        {
            recommendations.Add("• Strengthen cryptographic controls and key management procedures");
        }

        var hasIncidentIssues = results.Any(r => r.ControlId.Contains("ISO-5.24") && r.Score < 80);
        if (hasIncidentIssues)
        {
            recommendations.Add("• Establish formal incident response team and documented procedures");
        }

        return recommendations.Any()
            ? string.Join("\n", recommendations)
            : "Maintain current information security practices and conduct regular ISMS reviews.";
    }
}