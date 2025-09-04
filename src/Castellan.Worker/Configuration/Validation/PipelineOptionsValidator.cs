using Microsoft.Extensions.Options;
using Castellan.Worker.Models;

namespace Castellan.Worker.Configuration.Validation;

/// <summary>
/// Validates PipelineOptions configuration at startup
/// </summary>
public class PipelineOptionsValidator : IValidateOptions<PipelineOptions>
{
    public ValidateOptionsResult Validate(string? name, PipelineOptions options)
    {
        var failures = new List<string>();

        // Validate BatchSize
        if (options.BatchSize <= 0 || options.BatchSize > 10000)
        {
            failures.Add("Pipeline BatchSize must be between 1 and 10000");
        }

        // Validate ProcessingInterval
        if (options.ProcessingIntervalMs < 100 || options.ProcessingIntervalMs > 300000) // 100ms to 5 minutes
        {
            failures.Add("Pipeline ProcessingIntervalMs must be between 100 and 300000 (5 minutes)");
        }

        // Validate MaxConcurrency for parallel processing
        if (options.EnableParallelProcessing)
        {
            if (options.MaxConcurrency <= 0 || options.MaxConcurrency > Environment.ProcessorCount * 4)
            {
                failures.Add($"Pipeline MaxConcurrency must be between 1 and {Environment.ProcessorCount * 4} (4x processor count)");
            }

            if (options.ParallelOperationTimeoutMs < 1000 || options.ParallelOperationTimeoutMs > 120000) // 1s to 2 minutes
            {
                failures.Add("Pipeline ParallelOperationTimeoutMs must be between 1000 and 120000 (2 minutes)");
            }
        }

        // Validate RetryAttempts
        if (options.RetryAttempts < 0 || options.RetryAttempts > 10)
        {
            failures.Add("Pipeline RetryAttempts must be between 0 and 10");
        }

        // Validate RetryDelayMs
        if (options.RetryDelayMs < 100 || options.RetryDelayMs > 60000) // 100ms to 1 minute
        {
            failures.Add("Pipeline RetryDelayMs must be between 100 and 60000 (1 minute)");
        }

        // Performance warnings
        if (options.BatchSize > 1000)
        {
            Console.WriteLine("⚠️  Warning: Large batch size may impact memory usage and processing latency");
        }

        if (options.EnableParallelProcessing && options.MaxConcurrency > Environment.ProcessorCount * 2)
        {
            Console.WriteLine($"⚠️  Warning: MaxConcurrency ({options.MaxConcurrency}) is high for {Environment.ProcessorCount} processors. Monitor CPU usage.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail($"Pipeline configuration validation failed: {string.Join(", ", failures)}");
        }

        return ValidateOptionsResult.Success;
    }
}
