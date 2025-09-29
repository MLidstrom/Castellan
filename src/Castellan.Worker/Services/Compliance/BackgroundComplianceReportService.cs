using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Services.Interfaces;

namespace Castellan.Worker.Services.Compliance;

public interface IBackgroundComplianceReportService
{
    Task<string> QueueReportGenerationAsync(string framework, ReportFormat format, ReportAudience audience, string userId);
    Task<BackgroundReportStatus?> GetReportStatusAsync(string jobId);
    Task<byte[]?> GetCompletedReportAsync(string jobId);
}

public class BackgroundComplianceReportService : BackgroundService, IBackgroundComplianceReportService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundComplianceReportService> _logger;
    private readonly Dictionary<string, BackgroundReportJob> _jobs = new();
    private readonly Queue<BackgroundReportJob> _jobQueue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    public BackgroundComplianceReportService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundComplianceReportService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<string> QueueReportGenerationAsync(string framework, ReportFormat format, ReportAudience audience, string userId)
    {
        var jobId = Guid.NewGuid().ToString();
        var job = new BackgroundReportJob
        {
            JobId = jobId,
            Framework = framework,
            Format = format,
            Audience = audience,
            UserId = userId,
            Status = BackgroundReportStatus.Queued,
            QueuedAt = DateTime.UtcNow
        };

        await _semaphore.WaitAsync();
        try
        {
            _jobs[jobId] = job;
            _jobQueue.Enqueue(job);
            _logger.LogInformation("Queued background report generation: {JobId} for {Framework} ({Format})",
                jobId, framework, format);

            return jobId;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<BackgroundReportStatus?> GetReportStatusAsync(string jobId)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _jobs.TryGetValue(jobId, out var job) ? job.Status : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<byte[]?> GetCompletedReportAsync(string jobId)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_jobs.TryGetValue(jobId, out var job) &&
                job.Status == BackgroundReportStatus.Completed &&
                job.ReportData != null)
            {
                return job.ReportData;
            }
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background compliance report service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessJobQueueAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background report processing");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Background compliance report service stopped");
    }

    private async Task ProcessJobQueueAsync(CancellationToken cancellationToken)
    {
        BackgroundReportJob? job = null;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_jobQueue.TryDequeue(out job))
            {
                job.Status = BackgroundReportStatus.Processing;
                job.StartedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _semaphore.Release();
        }

        if (job == null)
            return;

        _logger.LogInformation("Processing background report job: {JobId}", job.JobId);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IComplianceReportGenerationService>();

            // Generate the report document
            var document = await GenerateReportDocumentAsync(reportService, job);

            // Export to requested format
            job.ReportData = await reportService.ExportReportAsync(document, job.Format);

            job.Status = BackgroundReportStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Completed background report job: {JobId} in {Duration}ms",
                job.JobId, (job.CompletedAt - job.StartedAt)?.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process background report job: {JobId}", job.JobId);

            job.Status = BackgroundReportStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
        }

        // Clean up old completed jobs (keep for 1 hour)
        await CleanupOldJobsAsync();
    }

    private async Task<ComplianceReportDocument> GenerateReportDocumentAsync(
        IComplianceReportGenerationService reportService,
        BackgroundReportJob job)
    {
        // Choose appropriate report type based on audience
        return job.Audience switch
        {
            ReportAudience.Executive => await reportService.GenerateExecutiveSummaryAsync(new List<string> { job.Framework }),
            ReportAudience.Technical => await reportService.GenerateComprehensiveReportAsync(job.Framework, job.Format),
            ReportAudience.Auditor => await reportService.GenerateComprehensiveReportAsync(job.Framework, job.Format),
            ReportAudience.Operations => await reportService.GenerateTrendReportAsync(job.Framework, 30),
            _ => await reportService.GenerateComprehensiveReportAsync(job.Framework, job.Format)
        };
    }

    private async Task CleanupOldJobsAsync()
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-1);
        var jobsToRemove = new List<string>();

        await _semaphore.WaitAsync();
        try
        {
            foreach (var kvp in _jobs)
            {
                var job = kvp.Value;
                if ((job.Status == BackgroundReportStatus.Completed || job.Status == BackgroundReportStatus.Failed) &&
                    job.CompletedAt.HasValue && job.CompletedAt.Value < cutoffTime)
                {
                    jobsToRemove.Add(kvp.Key);
                }
            }

            foreach (var jobId in jobsToRemove)
            {
                _jobs.Remove(jobId);
            }

            if (jobsToRemove.Any())
            {
                _logger.LogDebug("Cleaned up {Count} old background report jobs", jobsToRemove.Count);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override void Dispose()
    {
        _shutdownTokenSource?.Cancel();
        _shutdownTokenSource?.Dispose();
        _semaphore?.Dispose();
        base.Dispose();
    }
}

public class BackgroundReportJob
{
    public string JobId { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public ReportFormat Format { get; set; }
    public ReportAudience Audience { get; set; }
    public string UserId { get; set; } = string.Empty;
    public BackgroundReportStatus Status { get; set; }
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public byte[]? ReportData { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum BackgroundReportStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}