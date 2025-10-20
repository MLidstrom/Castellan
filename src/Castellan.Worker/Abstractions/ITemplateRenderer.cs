using Castellan.Worker.Models.Notifications;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for rendering notification templates with tag substitution
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Renders a template with the provided context data
    /// </summary>
    /// <param name="template">The notification template to render</param>
    /// <param name="context">Context data for tag substitution</param>
    /// <returns>Rendered template string with all tags replaced</returns>
    string Render(NotificationTemplate template, Dictionary<string, string> context);

    /// <summary>
    /// Validates template syntax and tags
    /// </summary>
    /// <param name="templateContent">The template content to validate</param>
    /// <returns>Validation result with any errors</returns>
    TemplateValidationResult Validate(string templateContent);

    /// <summary>
    /// Gets all supported tags
    /// </summary>
    /// <returns>List of supported tag names</returns>
    IEnumerable<string> GetSupportedTags();
}

/// <summary>
/// Result of template validation
/// </summary>
public class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
