using Microsoft.Extensions.Options;
using Castellan.Worker.Models;
using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Configuration.Validation;

/// <summary>
/// Validates PipelineOptions configuration at startup
/// </summary>
public class PipelineOptionsValidator : IValidateOptions<PipelineOptions>
{
    public ValidateOptionsResult Validate(string? name, PipelineOptions options)
    {
        var failures = new List<string>();
        var warnings = new List<string>();

        // Use DataAnnotations validation first
        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
        {
            failures.AddRange(validationResults.Select(r => r.ErrorMessage ?? "Unknown validation error"));
        }

        // Additional custom validations for business logic
        ValidateParallelProcessingSettings(options, failures, warnings);
        ValidateMemoryManagementSettings(options, failures, warnings);
        ValidateThrottlingSettings(options, failures, warnings);
        ValidateQueueManagementSettings(options, failures, warnings);
        ValidatePerformanceSettings(options, failures, warnings);

        // Output warnings to console
        foreach (var warning in warnings)
        {
            Console.WriteLine($"⚠️  Warning: {warning}");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail($"Pipeline configuration validation failed: {string.Join(", ", failures)}");
        }

        return ValidateOptionsResult.Success;
    }

    private static void ValidateParallelProcessingSettings(PipelineOptions options, List<string> failures, List<string> warnings)
    {
        if (options.EnableParallelProcessing)
        {
            // MaxConcurrency should be reasonable for the system
            if (options.MaxConcurrency > Environment.ProcessorCount * 4)
            {
                warnings.Add($"MaxConcurrency ({options.MaxConcurrency}) is very high for {Environment.ProcessorCount} processors. This may cause thread pool starvation.");
            }

            // MaxConcurrentTasks should be related to MaxConcurrency
            if (options.EnableSemaphoreThrottling && options.MaxConcurrentTasks < options.MaxConcurrency)
            {
                warnings.Add($"MaxConcurrentTasks ({options.MaxConcurrentTasks}) is less than MaxConcurrency ({options.MaxConcurrency}). This may limit parallelism.");
            }
        }

        // Batch size warnings
        if (options.BatchSize > 1000)
        {
            warnings.Add("Large batch size may impact memory usage and processing latency");
        }
    }

    private static void ValidateMemoryManagementSettings(PipelineOptions options, List<string> failures, List<string> warnings)
    {
        // Memory high water mark should be reasonable
        var systemMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        if (options.MemoryHighWaterMarkMB > systemMemoryMB * 0.8)
        {
            warnings.Add($"MemoryHighWaterMarkMB ({options.MemoryHighWaterMarkMB}MB) is close to system memory. Consider reducing.");
        }

        // Event retention should be reasonable
        if (options.EventHistoryRetentionMinutes > 360) // 6 hours
        {
            warnings.Add($"EventHistoryRetentionMinutes ({options.EventHistoryRetentionMinutes}) is very long. This may consume significant memory.");
        }

        // Max events per key validation
        if (options.MaxEventsPerCorrelationKey > 5000)
        {
            warnings.Add($"MaxEventsPerCorrelationKey ({options.MaxEventsPerCorrelationKey}) is very high. This may impact memory usage.");
        }

        // Cleanup interval should be reasonable relative to retention
        if (options.MemoryCleanupIntervalMinutes > options.EventHistoryRetentionMinutes / 2)
        {
            warnings.Add($"MemoryCleanupIntervalMinutes ({options.MemoryCleanupIntervalMinutes}) is too long relative to retention time. Consider reducing.");
        }
    }

    private static void ValidateThrottlingSettings(PipelineOptions options, List<string> failures, List<string> warnings)
    {
        if (options.EnableSemaphoreThrottling)
        {
            // Semaphore timeout should be reasonable
            if (options.SemaphoreTimeoutMs < 5000)
            {
                warnings.Add($"SemaphoreTimeoutMs ({options.SemaphoreTimeoutMs}ms) is quite short. This may cause frequent throttling.");
            }

            // MaxConcurrentTasks should be reasonable
            if (options.MaxConcurrentTasks > Environment.ProcessorCount * 8)
            {
                warnings.Add($"MaxConcurrentTasks ({options.MaxConcurrentTasks}) is very high for {Environment.ProcessorCount} processors.");
            }
        }

        if (options.EnableAdaptiveThrottling)
        {
            // CPU threshold should be reasonable
            if (options.CpuThrottleThreshold < 50)
            {
                warnings.Add($"CpuThrottleThreshold ({options.CpuThrottleThreshold}%) is quite low. This may cause aggressive throttling.");
            }
        }
    }

    private static void ValidateQueueManagementSettings(PipelineOptions options, List<string> failures, List<string> warnings)
    {
        if (options.EnableQueueBackPressure)
        {
            // Max queue depth should be reasonable
            if (options.MaxQueueDepth < 100)
            {
                warnings.Add($"MaxQueueDepth ({options.MaxQueueDepth}) is quite low. This may cause frequent back-pressure.");
            }

            if (options.MaxQueueDepth > 50000)
            {
                warnings.Add($"MaxQueueDepth ({options.MaxQueueDepth}) is very high. This may consume significant memory.");
            }
        }
    }

    private static void ValidatePerformanceSettings(PipelineOptions options, List<string> failures, List<string> warnings)
    {
        if (options.EnableDetailedMetrics)
        {
            // Metrics interval should be reasonable
            if (options.MetricsIntervalMs < 5000)
            {
                warnings.Add($"MetricsIntervalMs ({options.MetricsIntervalMs}ms) is very frequent. This may impact performance.");
            }
        }
    }
}
