namespace Castellan.Worker.Models;

/// <summary>
/// Metrics for event queue monitoring and performance tracking
/// </summary>
public class EventQueueMetrics
{
    /// <summary>
    /// Current number of events in the queue
    /// </summary>
    public int CurrentSize { get; init; }

    /// <summary>
    /// Maximum queue size reached
    /// </summary>
    public int MaxSize { get; init; }

    /// <summary>
    /// Total number of events enqueued since startup
    /// </summary>
    public long TotalEnqueued { get; init; }

    /// <summary>
    /// Total number of events dequeued since startup
    /// </summary>
    public long TotalDequeued { get; init; }

    /// <summary>
    /// Total number of events moved to dead letter queue
    /// </summary>
    public long TotalDeadLettered { get; init; }

    /// <summary>
    /// Average time events spend waiting in queue
    /// </summary>
    public TimeSpan AverageWaitTime { get; init; }

    /// <summary>
    /// Current events per second throughput (enqueue rate)
    /// </summary>
    public double EnqueueRate { get; init; }

    /// <summary>
    /// Current events per second throughput (dequeue rate)
    /// </summary>
    public double DequeueRate { get; init; }

    /// <summary>
    /// Number of events currently being processed
    /// </summary>
    public int EventsBeingProcessed { get; init; }

    /// <summary>
    /// Current number of events in dead letter queue
    /// </summary>
    public int DeadLetterQueueSize { get; init; }

    /// <summary>
    /// Timestamp when metrics were collected
    /// </summary>
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Queue utilization percentage (current size / max capacity)
    /// </summary>
    public double UtilizationPercent { get; init; }

    /// <summary>
    /// Whether the queue is currently healthy (not overloaded)
    /// </summary>
    public bool IsHealthy => UtilizationPercent < 90.0 && DeadLetterQueueSize < 100;

    /// <summary>
    /// Processing efficiency (dequeue rate / enqueue rate)
    /// </summary>
    public double ProcessingEfficiency => EnqueueRate > 0 ? DequeueRate / EnqueueRate : 1.0;

    /// <summary>
    /// Get a summary string of key metrics
    /// </summary>
    public override string ToString()
    {
        return $"Queue: {CurrentSize} events, " +
               $"Throughput: {EnqueueRate:F1}/s in, {DequeueRate:F1}/s out, " +
               $"Avg Wait: {AverageWaitTime.TotalMilliseconds:F0}ms, " +
               $"Health: {(IsHealthy ? "Healthy" : "Unhealthy")}";
    }
}

/// <summary>
/// Event arguments for queue size change notifications
/// </summary>
public class QueueSizeChangedEventArgs : EventArgs
{
    public int PreviousSize { get; init; }
    public int CurrentSize { get; init; }
    public int MaxCapacity { get; init; }
    public double UtilizationPercent => MaxCapacity > 0 ? (double)CurrentSize / MaxCapacity * 100 : 0;
}

/// <summary>
/// Event arguments for event enqueue notifications
/// </summary>
public class EventQueuedEventArgs : EventArgs
{
    public required QueuedEvent QueuedEvent { get; init; }
    public int QueueSize { get; init; }
}

/// <summary>
/// Event arguments for event dequeue notifications  
/// </summary>
public class EventDequeuedEventArgs : EventArgs
{
    public required QueuedEvent QueuedEvent { get; init; }
    public int QueueSize { get; init; }
    public TimeSpan WaitTime { get; init; }
}
