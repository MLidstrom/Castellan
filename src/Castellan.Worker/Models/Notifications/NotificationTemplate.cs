namespace Castellan.Worker.Models.Notifications;

public class NotificationTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public NotificationPlatform Platform { get; set; }
    public NotificationTemplateType Type { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum NotificationPlatform
{
    Teams,
    Slack
}

public enum NotificationTemplateType
{
    SecurityEvent,
    SystemAlert,
    HealthWarning,
    PerformanceAlert
}
