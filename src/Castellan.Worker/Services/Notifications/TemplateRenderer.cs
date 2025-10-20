using System.Text;
using System.Text.RegularExpressions;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Notifications;

namespace Castellan.Worker.Services.Notifications;

/// <summary>
/// Service for rendering notification templates with tag substitution
/// </summary>
public class TemplateRenderer : ITemplateRenderer
{
    private static readonly Regex TagRegex = new(@"\{\{([A-Z_:]+?)(?:\:([^\}]+))?\}\}", RegexOptions.Compiled);

    // All supported tags from NOTIFICATION_TEMPLATES_PLAN.md
    private static readonly HashSet<string> SupportedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        // Security Event Tags
        "DATE",
        "HOST",
        "USER",
        "EVENT_ID",
        "SEVERITY",
        "SUMMARY",
        "MITRE_TECHNIQUES",
        "RECOMMENDED_ACTIONS",
        "DETAILS_URL",
        "EVENT_TYPE",
        "CHANNEL",
        "PROVIDER",
        "RISK_LEVEL",
        "CONFIDENCE",

        // Threat Intelligence Tags
        "IP_ADDRESS",
        "HASH",
        "DOMAIN",
        "VT_SCORE",
        "MB_THREAT_TYPE",
        "GEO_LOCATION",

        // System Tags
        "MACHINE_NAME",
        "TIMESTAMP",
        "ALERT_ID",
        "CORRELATION_ID",

        // Formatting Tags (special handling)
        "NEWLINE",
        "BOLD",
        "LINK"
    };

    /// <summary>
    /// Renders a template with the provided context data
    /// </summary>
    public string Render(NotificationTemplate template, Dictionary<string, string> context)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var result = template.TemplateContent;

        // Replace all tags
        result = TagRegex.Replace(result, match =>
        {
            var tagName = match.Groups[1].Value.ToUpperInvariant();
            var tagValue = match.Groups[2].Success ? match.Groups[2].Value : null;

            // Handle formatting tags with special syntax
            if (tagName == "BOLD" && !string.IsNullOrEmpty(tagValue))
            {
                return FormatBold(tagValue, template.Platform);
            }

            if (tagName == "LINK" && !string.IsNullOrEmpty(tagValue))
            {
                return FormatLink(tagValue, template.Platform);
            }

            if (tagName == "NEWLINE")
            {
                return Environment.NewLine;
            }

            // Standard tag replacement
            if (context.TryGetValue(tagName, out var value))
            {
                return value ?? string.Empty;
            }

            // Tag not found in context - return placeholder
            return $"[{tagName}]";
        });

        return result;
    }

    /// <summary>
    /// Validates template syntax and tags
    /// </summary>
    public TemplateValidationResult Validate(string templateContent)
    {
        var result = new TemplateValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(templateContent))
        {
            result.IsValid = false;
            result.Errors.Add("Template content cannot be empty");
            return result;
        }

        // Find all tags in template
        var matches = TagRegex.Matches(templateContent);

        foreach (Match match in matches)
        {
            var tagName = match.Groups[1].Value.ToUpperInvariant();
            var hasValue = match.Groups[2].Success;

            // Check if tag is supported
            if (!SupportedTags.Contains(tagName))
            {
                result.Warnings.Add($"Unknown tag: {tagName}");
            }

            // Validate formatting tags
            if (tagName == "BOLD" && !hasValue)
            {
                result.Errors.Add($"BOLD tag requires text parameter: {{{{BOLD:text}}}}");
                result.IsValid = false;
            }

            if (tagName == "LINK" && !hasValue)
            {
                result.Errors.Add($"LINK tag requires url|text parameter: {{{{LINK:url|text}}}}");
                result.IsValid = false;
            }

            if (tagName == "LINK" && hasValue)
            {
                var linkValue = match.Groups[2].Value;
                if (!linkValue.Contains("|"))
                {
                    result.Errors.Add($"LINK tag requires format {{{{LINK:url|text}}}}");
                    result.IsValid = false;
                }
            }
        }

        // Check for unclosed braces
        var openBraces = templateContent.Count(c => c == '{');
        var closeBraces = templateContent.Count(c => c == '}');

        if (openBraces != closeBraces)
        {
            result.Errors.Add("Mismatched braces in template");
            result.IsValid = false;
        }

        // Check for proper tag format (double braces)
        if (Regex.IsMatch(templateContent, @"(?<!\{)\{(?!\{)[A-Z_]|[A-Z_]\}(?!\})"))
        {
            result.Warnings.Add("Found single braces that may be intended as tags. Use {{TAG}} format.");
        }

        return result;
    }

    /// <summary>
    /// Gets all supported tags
    /// </summary>
    public IEnumerable<string> GetSupportedTags()
    {
        return SupportedTags.OrderBy(t => t);
    }

    /// <summary>
    /// Formats text as bold based on platform
    /// </summary>
    private static string FormatBold(string text, NotificationPlatform platform)
    {
        return platform switch
        {
            NotificationPlatform.Teams => $"**{text}**",
            NotificationPlatform.Slack => $"*{text}*",
            _ => text
        };
    }

    /// <summary>
    /// Formats hyperlink based on platform
    /// </summary>
    private static string FormatLink(string linkData, NotificationPlatform platform)
    {
        var parts = linkData.Split('|', 2);
        if (parts.Length != 2)
            return linkData;

        var url = parts[0].Trim();
        var text = parts[1].Trim();

        return platform switch
        {
            NotificationPlatform.Teams => $"[{text}]({url})",
            NotificationPlatform.Slack => $"<{url}|{text}>",
            _ => $"{text} ({url})"
        };
    }
}
