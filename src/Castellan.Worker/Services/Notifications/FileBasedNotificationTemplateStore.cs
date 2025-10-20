using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models.Notifications;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services.Notifications;

/// <summary>
/// File-based storage for notification templates using JSON
/// </summary>
public class FileBasedNotificationTemplateStore : INotificationTemplateStore
{
    private readonly string _filePath;
    private readonly bool _createDefaultTemplates;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<FileBasedNotificationTemplateStore> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileBasedNotificationTemplateStore(
        IOptions<NotificationTemplateConfig> config,
        ILogger<FileBasedNotificationTemplateStore> logger)
    {
        _filePath = config.Value.TemplateStorePath;
        _createDefaultTemplates = config.Value.CreateDefaultTemplates;
        _logger = logger;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<IEnumerable<NotificationTemplate>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var templates = await LoadTemplatesAsync();

            // Create defaults if none exist and configured to do so
            if (!templates.Any() && _createDefaultTemplates)
            {
                await CreateDefaultTemplatesInternalAsync();
                templates = await LoadTemplatesAsync();
            }

            return templates;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<NotificationTemplate>> GetByPlatformAsync(NotificationPlatform platform)
    {
        var templates = await GetAllAsync();
        return templates.Where(t => t.Platform == platform);
    }

    public async Task<IEnumerable<NotificationTemplate>> GetByTypeAsync(NotificationTemplateType type)
    {
        var templates = await GetAllAsync();
        return templates.Where(t => t.Type == type);
    }

    public async Task<NotificationTemplate?> GetByIdAsync(string id)
    {
        var templates = await GetAllAsync();
        return templates.FirstOrDefault(t => t.Id == id);
    }

    public async Task<NotificationTemplate?> GetEnabledTemplateAsync(
        NotificationPlatform platform,
        NotificationTemplateType type)
    {
        var templates = await GetAllAsync();
        return templates.FirstOrDefault(t =>
            t.Platform == platform &&
            t.Type == type &&
            t.IsEnabled);
    }

    public async Task<NotificationTemplate> CreateAsync(NotificationTemplate template)
    {
        await _lock.WaitAsync();
        try
        {
            var templates = (await LoadTemplatesAsync()).ToList();

            // Generate new ID if not provided
            if (string.IsNullOrEmpty(template.Id))
            {
                template.Id = Guid.NewGuid().ToString();
            }

            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;

            templates.Add(template);
            await SaveTemplatesAsync(templates);

            _logger.LogInformation(
                "Created notification template {Id} for {Platform}/{Type}",
                template.Id,
                template.Platform,
                template.Type);

            return template;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<NotificationTemplate> UpdateAsync(NotificationTemplate template)
    {
        await _lock.WaitAsync();
        try
        {
            var templates = (await LoadTemplatesAsync()).ToList();
            var index = templates.FindIndex(t => t.Id == template.Id);

            if (index == -1)
            {
                throw new InvalidOperationException($"Template {template.Id} not found");
            }

            template.UpdatedAt = DateTime.UtcNow;
            templates[index] = template;

            await SaveTemplatesAsync(templates);

            _logger.LogInformation(
                "Updated notification template {Id} for {Platform}/{Type}",
                template.Id,
                template.Platform,
                template.Type);

            return template;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            var templates = (await LoadTemplatesAsync()).ToList();
            var removed = templates.RemoveAll(t => t.Id == id);

            if (removed > 0)
            {
                await SaveTemplatesAsync(templates);
                _logger.LogInformation("Deleted notification template {Id}", id);
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> HasDefaultTemplatesAsync()
    {
        var templates = await GetAllAsync();
        return templates.Any();
    }

    public async Task CreateDefaultTemplatesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await CreateDefaultTemplatesInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<NotificationTemplate>> LoadTemplatesAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<NotificationTemplate>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<NotificationTemplate>>(json, JsonOptions)
                   ?? new List<NotificationTemplate>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading notification templates from {FilePath}", _filePath);
            return new List<NotificationTemplate>();
        }
    }

    private async Task SaveTemplatesAsync(IEnumerable<NotificationTemplate> templates)
    {
        try
        {
            var json = JsonSerializer.Serialize(templates, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving notification templates to {FilePath}", _filePath);
            throw;
        }
    }

    private async Task CreateDefaultTemplatesInternalAsync()
    {
        var templates = DefaultTemplates.Create();

        _logger.LogInformation(
            "Creating {Count} default notification templates",
            templates.Count);

        await SaveTemplatesAsync(templates);
    }
}
