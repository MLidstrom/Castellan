using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for event queue operations supporting distributed pipeline scaling
/// </summary>
public interface IEventQueue : IDisposable
{
    /// <summary>
    /// Enqueue an event for processing with optional priority
    /// </summary>
    /// <param name="logEvent">The log event to enqueue</param>
    /// <param name="priority">Event priority (higher values = higher priority)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task EnqueueAsync(LogEvent logEvent, int priority = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeue an event for processing
    /// </summary>
    /// <param name="timeoutMs">Maximum time to wait for an event in milliseconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The next event to process, or null if timeout reached</returns>
    Task<QueuedEvent?> DequeueAsync(int timeoutMs = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Peek at the next event without removing it from the queue
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The next event or null if queue is empty</returns>
    Task<QueuedEvent?> PeekAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current queue size
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Check if the queue is empty
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Get queue metrics for monitoring
    /// </summary>
    EventQueueMetrics GetMetrics();

    /// <summary>
    /// Event fired when queue size changes significantly
    /// </summary>
    event EventHandler<QueueSizeChangedEventArgs>? QueueSizeChanged;

    /// <summary>
    /// Event fired when an event is enqueued
    /// </summary>
    event EventHandler<EventQueuedEventArgs>? EventEnqueued;

    /// <summary>
    /// Event fired when an event is dequeued
    /// </summary>
    event EventHandler<EventDequeuedEventArgs>? EventDequeued;

    /// <summary>
    /// Clear all events from the queue
    /// </summary>
    void Clear();

    /// <summary>
    /// Get dead letter queue for failed events
    /// </summary>
    IReadOnlyList<QueuedEvent> GetDeadLetterQueue();

    /// <summary>
    /// Move an event to dead letter queue
    /// </summary>
    /// <param name="queuedEvent">The event that failed processing</param>
    /// <param name="reason">Reason for failure</param>
    Task MoveToDeadLetterQueueAsync(QueuedEvent queuedEvent, string reason);
}
