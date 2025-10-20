using Castellan.Worker.Models.Notifications;

namespace Castellan.Worker.Services.Notifications;

/// <summary>
/// Factory for creating default notification templates
/// </summary>
public static class DefaultTemplates
{
    public static List<NotificationTemplate> Create()
    {
        var templates = new List<NotificationTemplate>();

        // Create templates for all platform/type combinations
        foreach (var platform in Enum.GetValues<NotificationPlatform>())
        {
            foreach (var type in Enum.GetValues<NotificationTemplateType>())
            {
                templates.Add(CreateTemplate(platform, type));
            }
        }

        return templates;
    }

    private static NotificationTemplate CreateTemplate(
        NotificationPlatform platform,
        NotificationTemplateType type)
    {
        var template = new NotificationTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"Default {platform} {type} Template",
            Platform = platform,
            Type = type,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        template.TemplateContent = type switch
        {
            NotificationTemplateType.SecurityEvent => GetSecurityEventTemplate(platform),
            NotificationTemplateType.SystemAlert => GetSystemAlertTemplate(platform),
            NotificationTemplateType.HealthWarning => GetHealthWarningTemplate(platform),
            NotificationTemplateType.PerformanceAlert => GetPerformanceAlertTemplate(platform),
            _ => GetSecurityEventTemplate(platform)
        };

        return template;
    }

    private static string GetSecurityEventTemplate(NotificationPlatform platform)
    {
        return @"🚨 {{BOLD:SECURITY ALERT}} - {{SEVERITY}} Severity

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📋 {{BOLD:Event Details}}
• {{BOLD:Event Type:}} {{EVENT_TYPE}}
• {{BOLD:Event ID:}} {{EVENT_ID}}
• {{BOLD:Timestamp:}} {{DATE}}

🖥️  {{BOLD:Affected System}}
• {{BOLD:Machine:}} {{HOST}}
• {{BOLD:User Account:}} {{USER}}

📊 {{BOLD:Risk Assessment}}
{{SUMMARY}}

🎯 {{BOLD:MITRE ATT&CK Framework}}
{{MITRE_TECHNIQUES}}

✅ {{BOLD:Recommended Response Actions}}
{{RECOMMENDED_ACTIONS}}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

{{LINK:{{DETAILS_URL}}|🔍 View Full Investigation Details}}

⚡ Powered by CastellanAI Security Platform";
    }

    private static string GetSystemAlertTemplate(NotificationPlatform platform)
    {
        return @"⚠️ {{BOLD:SYSTEM ALERT}} - {{SEVERITY}} Priority

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🖥️  {{BOLD:System Information}}
• {{BOLD:Machine Name:}} {{MACHINE_NAME}}
• {{BOLD:Alert ID:}} {{ALERT_ID}}
• {{BOLD:Timestamp:}} {{TIMESTAMP}}

📋 {{BOLD:Alert Description}}
{{SUMMARY}}

📊 {{BOLD:Event Classification}}
• {{BOLD:Type:}} {{EVENT_TYPE}}
• {{BOLD:Severity:}} {{SEVERITY}}

🔧 {{BOLD:Next Steps}}
{{RECOMMENDED_ACTIONS}}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

{{LINK:{{DETAILS_URL}}|📊 View System Dashboard}}

⚡ Powered by CastellanAI Security Platform";
    }

    private static string GetHealthWarningTemplate(NotificationPlatform platform)
    {
        return @"🏥 {{BOLD:SYSTEM HEALTH WARNING}}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🖥️  {{BOLD:Component Information}}
• {{BOLD:Component:}} {{MACHINE_NAME}}
• {{BOLD:Event Type:}} {{EVENT_TYPE}}
• {{BOLD:Detection Time:}} {{TIMESTAMP}}

⚠️ {{BOLD:Health Issue Detected}}
{{SUMMARY}}

🔍 {{BOLD:Impact Assessment}}
This health warning indicates potential system degradation or service disruption. Immediate attention may be required to prevent further issues.

🛠️  {{BOLD:Remediation Steps}}
{{RECOMMENDED_ACTIONS}}

💡 {{BOLD:Additional Information}}
• Monitor system resources and performance metrics
• Check logs for related errors or warnings
• Verify all services are running correctly
• Review recent configuration changes

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

{{LINK:{{DETAILS_URL}}|🏥 View System Health Dashboard}}

⚡ Powered by CastellanAI Security Platform";
    }

    private static string GetPerformanceAlertTemplate(NotificationPlatform platform)
    {
        return @"📊 {{BOLD:PERFORMANCE ALERT}} - {{SEVERITY}} Priority

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🖥️  {{BOLD:System Information}}
• {{BOLD:System:}} {{MACHINE_NAME}}
• {{BOLD:Alert ID:}} {{ALERT_ID}}
• {{BOLD:Detection Time:}} {{TIMESTAMP}}

⚡ {{BOLD:Performance Issue}}
{{SUMMARY}}

📈 {{BOLD:Metric Details}}
• {{BOLD:Issue Type:}} {{EVENT_TYPE}}
• {{BOLD:Severity:}} {{SEVERITY}}
• {{BOLD:Event ID:}} {{EVENT_ID}}

🔍 {{BOLD:Impact Analysis}}
This performance alert indicates system resource constraints or performance degradation. Continued degradation may impact service availability and user experience.

🛠️  {{BOLD:Optimization Recommendations}}
{{RECOMMENDED_ACTIONS}}

💡 {{BOLD:Performance Best Practices}}
• Monitor CPU, memory, and disk utilization trends
• Review application and service resource consumption
• Check for memory leaks or resource exhaustion
• Consider scaling resources if sustained high load
• Review recent deployments or configuration changes

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

{{LINK:{{DETAILS_URL}}|📈 View Performance Metrics & Trends}}

⚡ Powered by CastellanAI Security Platform";
    }
}
