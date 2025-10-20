using Castellan.Worker.Models.Notifications;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for notification template storage and retrieval
/// </summary>
public interface INotificationTemplateStore
{
    /// <summary>
    /// Gets all notification templates
    /// </summary>
    Task<IEnumerable<NotificationTemplate>> GetAllAsync();

    /// <summary>
    /// Gets templates filtered by platform
    /// </summary>
    Task<IEnumerable<NotificationTemplate>> GetByPlatformAsync(NotificationPlatform platform);

    /// <summary>
    /// Gets templates filtered by type
    /// </summary>
    Task<IEnumerable<NotificationTemplate>> GetByTypeAsync(NotificationTemplateType type);

    /// <summary>
    /// Gets a specific template by ID
    /// </summary>
    Task<NotificationTemplate?> GetByIdAsync(string id);

    /// <summary>
    /// Gets enabled templates by platform and type
    /// </summary>
    Task<NotificationTemplate?> GetEnabledTemplateAsync(NotificationPlatform platform, NotificationTemplateType type);

    /// <summary>
    /// Creates a new template
    /// </summary>
    Task<NotificationTemplate> CreateAsync(NotificationTemplate template);

    /// <summary>
    /// Updates an existing template
    /// </summary>
    Task<NotificationTemplate> UpdateAsync(NotificationTemplate template);

    /// <summary>
    /// Deletes a template by ID
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// Checks if default templates exist
    /// </summary>
    Task<bool> HasDefaultTemplatesAsync();

    /// <summary>
    /// Creates default templates for all platform/type combinations
    /// </summary>
    Task CreateDefaultTemplatesAsync();
}
