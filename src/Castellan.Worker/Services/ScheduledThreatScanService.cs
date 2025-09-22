using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

public class ScheduledThreatScanService : BackgroundService
{
    private readonly ILogger<ScheduledThreatScanService> _logger;
    private readonly IOptionsMonitor<ThreatScanOptions> _optionsMonitor;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _scanSemaphore = new(1, 1);
    private DateTime _lastScanTime = DateTime.MinValue;

    public ScheduledThreatScanService(
        ILogger<ScheduledThreatScanService> logger,
        IOptionsMonitor<ThreatScanOptions> optionsMonitor,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled threat scan service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var options = _optionsMonitor.CurrentValue;

                // Check if scheduled scanning is enabled
                if (!options.Enabled)
                {
                    _logger.LogDebug("Scheduled scanning is disabled, waiting...");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                // Check if it's time for a scan
                var timeSinceLastScan = DateTime.UtcNow - _lastScanTime;
                if (timeSinceLastScan >= options.ScheduledScanInterval)
                {
                    await PerformScheduledScanAsync(options, stoppingToken);
                }

                // Wait before next check (check every 5 minutes)
                var nextCheckDelay = TimeSpan.FromMinutes(5);
                _logger.LogDebug("Next scan check in {Delay} minutes", nextCheckDelay.TotalMinutes);
                await Task.Delay(nextCheckDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Scheduled threat scan service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled threat scan service");
                // Wait before retrying on error
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Scheduled threat scan service stopped");
    }

    private async Task PerformScheduledScanAsync(ThreatScanOptions options, CancellationToken cancellationToken)
    {
        // Prevent overlapping scans
        if (!await _scanSemaphore.WaitAsync(100, cancellationToken))
        {
            _logger.LogInformation("Skipping scheduled scan - another scan is already in progress");
            return;
        }

        try
        {
            _logger.LogInformation("Starting scheduled {ScanType} scan", options.DefaultScanType);

            // Create a scope to get the threat scanner service
            using var scope = _serviceProvider.CreateScope();
            var threatScanner = scope.ServiceProvider.GetRequiredService<IThreatScanner>();

            ThreatScanResult? result = null;

            // Perform the scan based on configured type
            switch (options.DefaultScanType)
            {
                case ThreatScanType.QuickScan:
                    result = await threatScanner.PerformQuickScanAsync(cancellationToken);
                    break;
                case ThreatScanType.FullScan:
                    result = await threatScanner.PerformFullScanAsync(cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Unsupported scheduled scan type: {ScanType}, defaulting to QuickScan", options.DefaultScanType);
                    result = await threatScanner.PerformQuickScanAsync(cancellationToken);
                    break;
            }

            if (result != null)
            {
                _lastScanTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Scheduled scan completed: {Status}, Files: {FilesScanned}, Threats: {ThreatsFound}, Duration: {Duration}",
                    result.Status, result.FilesScanned, result.ThreatsFound, result.Duration);

                // Log any threats found
                if (result.ThreatsFound > 0)
                {
                    _logger.LogWarning("Scheduled scan found {ThreatsFound} threats!", result.ThreatsFound);

                    // Log individual threats with high risk
                    var highRiskThreats = result.ThreatDetails
                        .Where(t => t.RiskLevel >= options.NotificationThreshold)
                        .ToList();

                    foreach (var threat in highRiskThreats)
                    {
                        _logger.LogWarning(
                            "High-risk threat detected: {ThreatName} in {FilePath} (Risk: {RiskLevel}, Confidence: {Confidence:P0})",
                            threat.ThreatName, threat.FilePath, threat.RiskLevel, threat.Confidence);
                    }

                    // TODO: Send notifications if configured
                    // This could integrate with the existing notification system
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scheduled scan was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing scheduled threat scan");
        }
        finally
        {
            _scanSemaphore.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping scheduled threat scan service...");
        await base.StopAsync(cancellationToken);
        _scanSemaphore.Dispose();
    }

    public async Task<bool> IsScanInProgressAsync()
    {
        return !await _scanSemaphore.WaitAsync(0);
    }

    public DateTime GetLastScanTime()
    {
        return _lastScanTime;
    }

    public DateTime GetNextScanTime()
    {
        var options = _optionsMonitor.CurrentValue;
        return _lastScanTime.Add(options.ScheduledScanInterval);
    }
}