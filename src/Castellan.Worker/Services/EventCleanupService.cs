using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service that automatically deletes security events older than 24 hours
/// This ensures the system maintains a 24-hour rolling window for AI pattern detection
/// </summary>
public class EventCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventCleanupService> _logger;

    // Run cleanup every minute to maintain 24-hour rolling window
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);

    // Delete events older than 24 hours
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromHours(24);

    public EventCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<EventCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventCleanupService starting - will delete events older than {RetentionHours} hours every {IntervalMinutes} minutes",
            _retentionPeriod.TotalHours, _cleanupInterval.TotalMinutes);

        // Wait a bit for application to fully start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldEvents(stoppingToken);
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during event cleanup cycle");
                // Wait a shorter period before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("EventCleanupService stopped");
    }

    private async Task CleanupOldEvents(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CastellanDbContext>();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Calculate cutoff time (24 hours ago)
            var cutoffTime = DateTime.UtcNow.Subtract(_retentionPeriod);

            _logger.LogInformation("Starting event cleanup - deleting events older than {CutoffTime:yyyy-MM-dd HH:mm:ss} UTC",
                cutoffTime);

            // Query events older than 24 hours directly from database
            var eventsToDelete = await dbContext.SecurityEvents
                .Where(e => e.Timestamp < cutoffTime)
                .ToListAsync(cancellationToken);

            if (eventsToDelete.Count == 0)
            {
                _logger.LogInformation("No events older than 24 hours found - cleanup complete");
                return;
            }

            _logger.LogInformation("Found {EventCount} events older than 24 hours - beginning deletion",
                eventsToDelete.Count);

            // Delete events in batch
            dbContext.SecurityEvents.RemoveRange(eventsToDelete);
            await dbContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("Event cleanup complete - deleted {DeletedCount} events in {ElapsedMs}ms. " +
                                 "24-hour rolling window maintained.",
                eventsToDelete.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event cleanup operation");
            throw;
        }
    }
}
