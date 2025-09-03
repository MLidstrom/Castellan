using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

public interface INotificationConfigurationStore
{
    Task<IEnumerable<NotificationConfiguration>> GetAllAsync();
    Task<NotificationConfiguration?> GetByIdAsync(string id);
    Task<NotificationConfiguration> CreateAsync(NotificationConfiguration configuration);
    Task<NotificationConfiguration> UpdateAsync(NotificationConfiguration configuration);
    Task<bool> DeleteAsync(string id);
    Task<int> GetCountAsync();
}