namespace Castellan.Worker.Models.Notifications;

/// <summary>
/// Configuration for notification template storage and management
/// </summary>
public class NotificationTemplateConfig
{
    /// <summary>
    /// Path to the JSON file storing notification templates
    /// Default: data/notification-templates.json
    /// </summary>
    public string TemplateStorePath { get; set; } = "data/notification-templates.json";

    /// <summary>
    /// Whether to create default templates if none exist
    /// </summary>
    public bool CreateDefaultTemplates { get; set; } = true;

    /// <summary>
    /// Whether to validate templates on load
    /// </summary>
    public bool ValidateOnLoad { get; set; } = true;

    /// <summary>
    /// Maximum template content length (in characters)
    /// </summary>
    public int MaxTemplateLength { get; set; } = 10000;
}
