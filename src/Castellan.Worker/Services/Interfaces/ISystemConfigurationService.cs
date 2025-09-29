using Castellan.Worker.Models;

namespace Castellan.Worker.Services.Interfaces;

public interface ISystemConfigurationService
{
    Task<string?> GetConfigurationValueAsync(string key);
    Task<T?> GetConfigurationValueAsync<T>(string key);
    Task<SystemConfiguration> SetConfigurationValueAsync(string key, string? value, string? description = null);
    Task<List<SystemConfiguration>> GetAllConfigurationsAsync();
    Task<bool> DeleteConfigurationAsync(string key);
}