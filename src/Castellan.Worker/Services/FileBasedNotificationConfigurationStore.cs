using System.Text.Json;
using Castellan.Worker.Models;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services;

public class FileBasedNotificationConfigurationStore : INotificationConfigurationStore, IDisposable
{
    private readonly string _filePath;
    private readonly ILogger<FileBasedNotificationConfigurationStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileSemaphore;
    private List<NotificationConfiguration> _configurations;
    private DateTime _lastSaveTime;

    public FileBasedNotificationConfigurationStore(ILogger<FileBasedNotificationConfigurationStore> logger)
    {
        _logger = logger;
        
        // Use LocalAppData similar to security events
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var castellanPath = Path.Combine(appDataPath, "Castellan");
        Directory.CreateDirectory(castellanPath);
        _filePath = Path.Combine(castellanPath, "notification_configurations.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        
        _fileSemaphore = new SemaphoreSlim(1, 1);
        _configurations = new List<NotificationConfiguration>();
        _lastSaveTime = DateTime.MinValue;
        
        LoadFromFile();
        
        _logger.LogInformation("FileBasedNotificationConfigurationStore initialized with file: {FilePath}", _filePath);
    }

    public async Task<IEnumerable<NotificationConfiguration>> GetAllAsync()
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            return _configurations.ToList();
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<NotificationConfiguration?> GetByIdAsync(string id)
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            return _configurations.FirstOrDefault(c => c.Id == id);
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<NotificationConfiguration> CreateAsync(NotificationConfiguration configuration)
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            configuration.Id = Guid.NewGuid().ToString();
            configuration.CreatedAt = DateTime.UtcNow;
            configuration.UpdatedAt = DateTime.UtcNow;
            
            _configurations.Add(configuration);
            await SaveToFileAsync();
            
            _logger.LogInformation("Created notification configuration: {ConfigId} - {Name}", 
                configuration.Id, configuration.Name);
            
            return configuration;
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<NotificationConfiguration> UpdateAsync(NotificationConfiguration configuration)
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            var existingIndex = _configurations.FindIndex(c => c.Id == configuration.Id);
            if (existingIndex == -1)
            {
                throw new InvalidOperationException($"Notification configuration with ID {configuration.Id} not found");
            }
            
            configuration.UpdatedAt = DateTime.UtcNow;
            configuration.CreatedAt = _configurations[existingIndex].CreatedAt; // Preserve original creation time
            _configurations[existingIndex] = configuration;
            
            await SaveToFileAsync();
            
            _logger.LogInformation("Updated notification configuration: {ConfigId} - {Name}", 
                configuration.Id, configuration.Name);
            
            return configuration;
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            var removed = _configurations.RemoveAll(c => c.Id == id);
            if (removed > 0)
            {
                await SaveToFileAsync();
                _logger.LogInformation("Deleted notification configuration: {ConfigId}", id);
                return true;
            }
            
            return false;
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    public async Task<int> GetCountAsync()
    {
        await _fileSemaphore.WaitAsync();
        try
        {
            return _configurations.Count;
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    private void LoadFromFile()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Notification configuration file not found, starting with empty configuration");
                return;
            }

            var jsonContent = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogInformation("Notification configuration file is empty, starting with empty configuration");
                return;
            }

            var configurations = JsonSerializer.Deserialize<List<NotificationConfiguration>>(jsonContent, _jsonOptions);
            if (configurations != null)
            {
                _configurations = configurations;
                _logger.LogInformation("Loaded {Count} notification configurations from file", _configurations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading notification configurations from file: {FilePath}", _filePath);
            _configurations = new List<NotificationConfiguration>();
        }
    }

    private async Task SaveToFileAsync()
    {
        try
        {
            var jsonContent = JsonSerializer.Serialize(_configurations, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, jsonContent);
            _lastSaveTime = DateTime.UtcNow;
            _logger.LogDebug("Saved {Count} notification configurations to file", _configurations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving notification configurations to file: {FilePath}", _filePath);
            throw;
        }
    }

    public void Dispose()
    {
        _fileSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}