using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service that ensures notification templates exist on startup
/// </summary>
public class TemplateInitializationService : IHostedService
{
    private readonly INotificationTemplateStore _templateStore;
    private readonly ILogger<TemplateInitializationService> _logger;

    public TemplateInitializationService(
        INotificationTemplateStore templateStore,
        ILogger<TemplateInitializationService> logger)
    {
        _templateStore = templateStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing notification templates...");

            // This will trigger creation of default templates if none exist
            // The FileBasedNotificationTemplateStore already has logic to create defaults
            var templates = await _templateStore.GetAllAsync();

            _logger.LogInformation(
                "Notification templates initialized. Found {Count} templates.",
                templates.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing notification templates");
            // Don't throw - this shouldn't prevent startup
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
