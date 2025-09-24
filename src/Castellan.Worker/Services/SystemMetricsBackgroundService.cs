using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service that periodically broadcasts system metrics via SignalR
/// </summary>
public class SystemMetricsBackgroundService : BackgroundService
{
    private readonly ILogger<SystemMetricsBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public SystemMetricsBackgroundService(
        ILogger<SystemMetricsBackgroundService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SystemMetricsBackgroundService starting...");

        // Wait a bit for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            _logger.LogInformation("Starting periodic system metrics broadcasting (every 1 second)...");

            // Run the broadcasting loop directly instead of calling StartPeriodicUpdates
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var progressTracker = scope.ServiceProvider.GetRequiredService<IEnhancedProgressTrackingService>();

                    await progressTracker.BroadcastSystemUpdate();
                    _logger.LogDebug("System metrics broadcast completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during system metrics broadcast");
                }

                // Wait 1 second before next broadcast for responsive real-time updates
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SystemMetricsBackgroundService failed");
        }

        _logger.LogInformation("SystemMetricsBackgroundService stopped");
    }
}