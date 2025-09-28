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
/// PCI DSS (Payment Card Industry Data Security Standard) compliance framework implementation.
/// Focuses on cardholder data protection, access control, network security, and vulnerability management.
/// </summary>
public class PCIDSSComplianceFramework : IComplianceFramework
{
    private readonly ILogger<PCIDSSComplianceFramework> _logger;
    private readonly ISecurityEventStore _securityEventStore;

    public string FrameworkName => "PCI DSS";

    public PCIDSSComplianceFramework(
        ILogger<PCIDSSComplianceFramework> logger,
        ISecurityEventStore securityEventStore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _securityEventStore = securityEventStore ?? throw new ArgumentNullException(nameof(securityEventStore));
    }

    public async Task<ControlAssessment> AssessControlAsync(ComplianceControl control)
    {
        return control.ControlId switch
        {
            "PCI-DSS-1" => await AssessNetworkSecurityAsync(),
            "PCI-DSS-2" => await AssessSecureConfigurationsAsync(),
            "PCI-DSS-3" => await AssessDataProtectionAsync(),
            "PCI-DSS-4" => await AssessTransmissionEncryptionAsync(),
            "PCI-DSS-5" => await AssessMalwareProtectionAsync(),
            "PCI-DSS-6" => await AssessSecureDevelopmentAsync(),
            "PCI-DSS-7" => await AssessAccessRestrictionAsync(),
            "PCI-DSS-8" => await AssessUserAuthenticationAsync(),
            "PCI-DSS-9" => await AssessPhysicalSecurityAsync(),
            "PCI-DSS-10" => await AssessLoggingMonitoringAsync(),
            "PCI-DSS-11" => await AssessSecurityTestingAsync(),
            "PCI-DSS-12" => await AssessPolicyComplianceAsync(),
            _ => await AssessGenericControlAsync(control)
        };
    }

    private async Task<ControlAssessment> AssessNetworkSecurityAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var firewallEvents = events.Where(e =>
            e.EventType == SecurityEventType.NetworkConnection ||
            e.Summary.Contains("firewall", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("network", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(50 + firewallEvents.Count * 3, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Firewall events monitored: {firewallEvents.Count}",
            Findings = score >= 80 ? "Network security controls are active" : "Network security monitoring needs improvement",
            Recommendations = "Implement comprehensive firewall logging and network segmentation"
        };
    }

    private async Task<ControlAssessment> AssessSecureConfigurationsAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var configEvents = events.Where(e =>
            e.EventType == SecurityEventType.SecurityPolicyChange ||
            e.Summary.Contains("configuration", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(60 + configEvents.Count * 2, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Configuration changes monitored: {configEvents.Count}",
            Findings = score >= 80 ? "Secure configurations maintained" : "Configuration management requires attention",
            Recommendations = "Implement configuration change control and hardening standards"
        };
    }

    private async Task<ControlAssessment> AssessDataProtectionAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var dataEvents = events.Where(e =>
            e.EventType == SecurityEventType.SuspiciousActivity ||
            e.Summary.Contains("cardholder", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("data", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = dataEvents.Any() ? 40 : 85; // Lower score if data incidents detected

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Data security events: {dataEvents.Count}",
            Findings = dataEvents.Any() ? "Data protection incidents detected" : "No data security incidents",
            Recommendations = "Implement data discovery, classification, and encryption for cardholder data"
        };
    }

    private async Task<ControlAssessment> AssessTransmissionEncryptionAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var encryptionEvents = events.Where(e =>
            e.Summary.Contains("encryption", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("TLS", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(70 + encryptionEvents.Count, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Encryption-related events: {encryptionEvents.Count}",
            Findings = score >= 80 ? "Transmission encryption is monitored" : "Transmission security needs verification",
            Recommendations = "Ensure all cardholder data transmission uses strong encryption (TLS 1.2+)"
        };
    }

    private async Task<ControlAssessment> AssessMalwareProtectionAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var malwareEvents = events.Where(e =>
            e.EventType == SecurityEventType.SuspiciousActivity ||
            e.Summary.Contains("malware", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("virus", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = malwareEvents.Count > 5 ? 60 : 85;

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Malware events detected: {malwareEvents.Count}",
            Findings = malwareEvents.Count > 5 ? "Multiple malware incidents detected" : "Malware protection appears effective",
            Recommendations = "Maintain up-to-date antivirus and anti-malware solutions on all systems"
        };
    }

    private async Task<ControlAssessment> AssessSecureDevelopmentAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var vulnEvents = events.Where(e =>
            e.Summary.Contains("vulnerability", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("patch", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Max(50, 90 - vulnEvents.Count * 2);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Vulnerability/patch events: {vulnEvents.Count}",
            Findings = vulnEvents.Count > 10 ? "High number of vulnerability events" : "Development security practices need verification",
            Recommendations = "Implement secure development lifecycle and regular vulnerability scanning"
        };
    }

    private async Task<ControlAssessment> AssessAccessRestrictionAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var accessEvents = events.Where(e =>
            e.EventType == SecurityEventType.PrivilegeEscalation ||
            e.EventType == SecurityEventType.SuspiciousActivity ||
            e.Summary.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Max(50, 90 - accessEvents.Count * 5);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Access violation events: {accessEvents.Count}",
            Findings = accessEvents.Any() ? "Access control violations detected" : "Access restrictions appear effective",
            Recommendations = "Implement role-based access controls and principle of least privilege"
        };
    }

    private async Task<ControlAssessment> AssessUserAuthenticationAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var authEvents = events.Where(e =>
            e.EventType == SecurityEventType.AuthenticationFailure ||
            e.Summary.Contains("account lockout", StringComparison.OrdinalIgnoreCase)).ToList();

        var successEvents = events.Where(e =>
            e.EventType == SecurityEventType.AuthenticationSuccess).ToList();

        var failureRate = successEvents.Any() ? (double)authEvents.Count / (authEvents.Count + successEvents.Count) : 0;
        var score = Math.Max(60, (int)(100 - failureRate * 100));

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Auth failures: {authEvents.Count}, successes: {successEvents.Count}",
            Findings = failureRate > 0.1 ? "High authentication failure rate" : "Authentication controls functioning",
            Recommendations = "Implement multi-factor authentication and strong password policies"
        };
    }

    private async Task<ControlAssessment> AssessPhysicalSecurityAsync()
    {
        // Physical security typically requires manual assessment
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 70,
            Evidence = "Physical security controls require manual verification",
            Findings = "Physical access controls need on-site assessment",
            Recommendations = "Conduct regular physical security assessments of cardholder data environments"
        };
    }

    private async Task<ControlAssessment> AssessLoggingMonitoringAsync()
    {
        var events = _securityEventStore.GetSecurityEvents(1, 1000);
        var auditEvents = events.Where(e =>
            e.Summary.Contains("audit", StringComparison.OrdinalIgnoreCase) ||
            e.Summary.Contains("log", StringComparison.OrdinalIgnoreCase)).ToList();

        var score = Math.Min(60 + auditEvents.Count * 2, 100);

        return new ControlAssessment
        {
            Status = score >= 80 ? "Compliant" : score >= 60 ? "PartiallyCompliant" : "NonCompliant",
            Score = score,
            Evidence = $"Audit/logging events: {auditEvents.Count}",
            Findings = score >= 80 ? "Logging and monitoring active" : "Audit trail monitoring needs improvement",
            Recommendations = "Implement comprehensive logging and real-time security monitoring"
        };
    }

    private async Task<ControlAssessment> AssessSecurityTestingAsync()
    {
        // Security testing typically requires external verification
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 65,
            Evidence = "Security testing requires regular external assessment",
            Findings = "Penetration testing and vulnerability scanning need verification",
            Recommendations = "Conduct quarterly penetration testing and regular vulnerability scans"
        };
    }

    private async Task<ControlAssessment> AssessPolicyComplianceAsync()
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
            Findings = score >= 80 ? "Information security policies monitored" : "Policy compliance needs enhancement",
            Recommendations = "Develop comprehensive information security policies and training programs"
        };
    }

    private async Task<ControlAssessment> AssessGenericControlAsync(ComplianceControl control)
    {
        return new ControlAssessment
        {
            Status = "PartiallyCompliant",
            Score = 60,
            Evidence = $"Control {control.ControlId} requires manual assessment",
            Findings = $"Generic assessment for {control.ControlName}",
            Recommendations = $"Implement specific monitoring for {control.ControlName}"
        };
    }

    public async Task<string> GenerateSummaryAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var resultsList = results.ToList();
        var avgScore = resultsList.Any() ? resultsList.Average(r => r.Score) : 0;
        var compliantCount = resultsList.Count(r => r.Status == "Compliant");
        var partialCount = resultsList.Count(r => r.Status == "PartiallyCompliant");
        var nonCompliantCount = resultsList.Count(r => r.Status == "NonCompliant");

        return $"PCI DSS compliance assessment shows {avgScore:F1}% overall compliance. " +
               $"Status breakdown: {compliantCount} compliant, {partialCount} partially compliant, " +
               $"{nonCompliantCount} non-compliant controls. Critical areas include data protection, " +
               $"access controls, and network security monitoring.";
    }

    public async Task<string> GenerateKeyFindingsAsync(IEnumerable<ComplianceAssessmentResult> results)
    {
        var criticalFindings = results
            .Where(r => r.Status == "NonCompliant" || r.Score < 70)
            .OrderBy(r => r.Score)
            .Take(5)
            .Select(r => $"• {r.ControlId}: {r.Findings}")
            .ToList();

        if (!criticalFindings.Any())
            return "No critical compliance gaps identified.";

        return "Critical findings requiring immediate attention:\n" + string.Join("\n", criticalFindings);
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

        // Add specific PCI DSS recommendations
        var hasDataIssues = results.Any(r => r.ControlId.Contains("PCI-DSS-3") && r.Score < 80);
        if (hasDataIssues)
        {
            recommendations.Add("• Implement tokenization or strong encryption for all stored cardholder data");
        }

        var hasNetworkIssues = results.Any(r => r.ControlId.Contains("PCI-DSS-1") && r.Score < 80);
        if (hasNetworkIssues)
        {
            recommendations.Add("• Enhance network segmentation and firewall rules for cardholder data environment");
        }

        return recommendations.Any()
            ? string.Join("\n", recommendations)
            : "Maintain current security practices and conduct regular compliance reviews.";
    }
}