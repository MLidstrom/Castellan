namespace Castellan.Worker.Models;

public sealed class PipelineOptions
{
    /// <summary>
    /// Enable parallel processing for independent operations in the pipeline
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent operations in parallel processing
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Timeout in milliseconds for parallel operations
    /// </summary>
    public int ParallelOperationTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Enable parallel vector operations (upsert and search concurrently)
    /// </summary>
    public bool EnableParallelVectorOperations { get; set; } = true;
}