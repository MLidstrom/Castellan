using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Services;

/// <summary>
/// Background service for continuous correlation processing and ML model training
/// </summary>
public class CorrelationBackgroundService : BackgroundService
{
    private readonly ILogger<CorrelationBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _batchAnalysisInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _modelTrainingInterval = TimeSpan.FromHours(24);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6);

    public CorrelationBackgroundService(
        ILogger<CorrelationBackgroundService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Correlation background service started");

        var lastModelTraining = DateTime.MinValue;
        var lastCleanup = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Perform batch analysis every interval
                await PerformBatchAnalysisAsync(stoppingToken);

                // Perform ML model training if needed
                if (now - lastModelTraining >= _modelTrainingInterval)
                {
                    await PerformModelTrainingAsync(stoppingToken);
                    lastModelTraining = now;
                }

                // Perform cleanup if needed
                if (now - lastCleanup >= _cleanupInterval)
                {
                    await PerformCleanupAsync(stoppingToken);
                    lastCleanup = now;
                }

                // Wait for next batch analysis interval
                await Task.Delay(_batchAnalysisInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in correlation background service");
                // Wait a bit before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Correlation background service stopped");
    }

    private async Task PerformBatchAnalysisAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var correlationEngine = scope.ServiceProvider.GetRequiredService<ICorrelationEngine>();
            var eventStore = scope.ServiceProvider.GetRequiredService<ISecurityEventStore>();

            // Get recent events for batch analysis
            var endTime = DateTime.UtcNow;
            var startTime = endTime - _batchAnalysisInterval;

            var filterDict = new Dictionary<string, object>
            {
                ["StartTime"] = startTime,
                ["EndTime"] = endTime
            };

            var recentEvents = eventStore.GetSecurityEvents(1, 1000, filterDict).ToList();

            if (recentEvents.Count > 0)
            {
                _logger.LogDebug("Performing batch correlation analysis on {Count} events", recentEvents.Count);

                var correlations = await correlationEngine.AnalyzeBatchAsync(recentEvents, _batchAnalysisInterval);

                if (correlations.Count > 0)
                {
                    _logger.LogInformation("Batch analysis detected {Count} correlations", correlations.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing batch correlation analysis");
        }
    }

    private async Task PerformModelTrainingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var correlationEngine = scope.ServiceProvider.GetRequiredService<ICorrelationEngine>();

            // Get confirmed correlations for training
            // Note: In a real implementation, you would track which correlations have been
            // confirmed by security analysts. For now, we'll use all recent correlations.
            var endTime = DateTime.UtcNow;
            var startTime = endTime - TimeSpan.FromDays(30); // Last 30 days

            var allCorrelations = await correlationEngine.GetCorrelationsAsync(startTime, endTime);
            var confirmedCorrelations = allCorrelations
                .Where(c => c.ConfidenceScore > 0.7) // Use high-confidence correlations as "confirmed"
                .ToList();

            if (confirmedCorrelations.Count >= 10)
            {
                _logger.LogInformation("Training ML model with {Count} confirmed correlations", confirmedCorrelations.Count);
                await correlationEngine.TrainModelsAsync(confirmedCorrelations);
            }
            else
            {
                _logger.LogDebug("Insufficient confirmed correlations for training: {Count} (need 10+)", confirmedCorrelations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing ML model training");
        }
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var correlationEngine = scope.ServiceProvider.GetRequiredService<ICorrelationEngine>();

            // Clean up correlations older than configured retention period
            var maxAge = TimeSpan.FromDays(30);
            await correlationEngine.CleanupOldCorrelationsAsync(maxAge);

            _logger.LogDebug("Performed correlation cleanup for correlations older than {Days} days", maxAge.TotalDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing correlation cleanup");
        }
    }
}

/// <summary>
/// Configuration options for the correlation background service
/// </summary>
