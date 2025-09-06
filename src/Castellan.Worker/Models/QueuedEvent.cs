namespace Castellan.Worker.Models;

/// <summary>
/// Represents an event in the processing queue
/// </summary>
public class QueuedEvent
{
    /// <summary>
    /// Unique identifier for this queued event
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The original log event
    /// </summary>
    public required LogEvent LogEvent { get; init; }

    /// <summary>
    /// Priority of this event (higher values processed first)
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// Timestamp when the event was enqueued
    /// </summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when the event was dequeued (null if not yet dequeued)
    /// </summary>
    public DateTimeOffset? DequeuedAt { get; set; }

    /// <summary>
    /// Number of times this event has been retried
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of retry attempts allowed
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Instance ID that should process this event (for affinity)
    /// </summary>
    public string? AffinityInstanceId { get; init; }

    /// <summary>
    /// Instance ID that is currently processing this event
    /// </summary>
    public string? ProcessingInstanceId { get; set; }

    /// <summary>
    /// Timestamp when processing started
    /// </summary>
    public DateTimeOffset? ProcessingStartedAt { get; set; }

    /// <summary>
    /// Reason for last failure (if any)
    /// </summary>
    public string? LastFailureReason { get; set; }

    /// <summary>
    /// Time spent waiting in queue (calculated property)
    /// </summary>
    public TimeSpan? WaitTime => DequeuedAt.HasValue 
        ? DequeuedAt.Value - EnqueuedAt 
        : null;

    /// <summary>
    /// Whether this event can be retried
    /// </summary>
    public bool CanRetry => RetryCount < MaxRetries;

    /// <summary>
    /// Whether this event is expired (in queue too long)
    /// </summary>
    public bool IsExpired(TimeSpan maxAge)
    {
        return DateTimeOffset.UtcNow - EnqueuedAt > maxAge;
    }

    /// <summary>
    /// Create a copy of this event for retry
    /// </summary>
    public QueuedEvent CreateRetry(string failureReason)
    {
        return new QueuedEvent
        {
            LogEvent = LogEvent,
            Priority = Priority,
            EnqueuedAt = DateTimeOffset.UtcNow,
            RetryCount = RetryCount + 1,
            MaxRetries = MaxRetries,
            AffinityInstanceId = AffinityInstanceId,
            LastFailureReason = failureReason
        };
    }
}
