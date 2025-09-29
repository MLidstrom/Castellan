using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Castellan.Worker.Services.Interfaces;

namespace Castellan.Worker.Services;

public class SystemConfigurationService : ISystemConfigurationService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<SystemConfigurationService> _logger;

    public SystemConfigurationService(CastellanDbContext context, ILogger<SystemConfigurationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> GetConfigurationValueAsync(string key)
    {
        var config = await _context.SystemConfiguration
            .FirstOrDefaultAsync(c => c.Key == key);
        
        return config?.Value;
    }

    public async Task<T?> GetConfigurationValueAsync<T>(string key)
    {
        var value = await GetConfigurationValueAsync(key);
        
        if (string.IsNullOrEmpty(value))
            return default(T);

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert configuration value {Key} to type {Type}", key, typeof(T).Name);
            return default(T);
        }
    }

    public async Task<SystemConfiguration> SetConfigurationValueAsync(string key, string? value, string? description = null)
    {
        try
        {
            var existing = await _context.SystemConfiguration
                .FirstOrDefaultAsync(c => c.Key == key);

            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(description))
                    existing.Description = description;
            }
            else
            {
                existing = new SystemConfiguration
                {
                    Key = key,
                    Value = value,
                    Description = description,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.SystemConfiguration.Add(existing);
            }

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Set configuration: {Key} = {Value}", key, value);
            
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting configuration: {Key}", key);
            throw;
        }
    }

    public async Task<List<SystemConfiguration>> GetAllConfigurationsAsync()
    {
        return await _context.SystemConfiguration
            .OrderBy(c => c.Key)
            .ToListAsync();
    }

    public async Task<bool> DeleteConfigurationAsync(string key)
    {
        try
        {
            var config = await _context.SystemConfiguration
                .FirstOrDefaultAsync(c => c.Key == key);

            if (config == null)
                return false;

            _context.SystemConfiguration.Remove(config);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted configuration: {Key}", key);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration: {Key}", key);
            throw;
        }
    }

    public async Task<Dictionary<string, string?>> GetConfigurationGroupAsync(string keyPrefix)
    {
        return await _context.SystemConfiguration
            .Where(c => c.Key.StartsWith(keyPrefix))
            .ToDictionaryAsync(c => c.Key, c => c.Value);
    }

    public async Task<bool> ConfigurationExistsAsync(string key)
    {
        return await _context.SystemConfiguration
            .AnyAsync(c => c.Key == key);
    }

    // Helper methods for common configurations
    public async Task<string> GetDatabaseVersionAsync()
    {
        return await GetConfigurationValueAsync("DatabaseVersion") ?? "1.0.0";
    }

    public async Task SetDatabaseVersionAsync(string version)
    {
        await SetConfigurationValueAsync("DatabaseVersion", version, "Current database schema version");
    }

    public async Task<DateTime?> GetLastMitreUpdateAsync()
    {
        var value = await GetConfigurationValueAsync("LastMitreUpdate");
        if (DateTime.TryParse(value, out var date))
            return date;
        return null;
    }

    public async Task SetLastMitreUpdateAsync(DateTime updateTime)
    {
        await SetConfigurationValueAsync("LastMitreUpdate", updateTime.ToString("yyyy-MM-dd HH:mm:ss"), 
            "Last date MITRE ATT&CK data was updated");
    }
}
