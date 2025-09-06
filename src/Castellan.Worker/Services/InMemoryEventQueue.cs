using System.Collections.Concurrent;
using System.Diagnostics;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services;

/// <summary>
/// In-memory implementation of event queue for pipeline scaling
/// </summary>
public class InMemoryEventQueue : IEventQueue
{
    private readonly ILogger<InMemoryEventQueue> _logger;
    private readonly PriorityQueue<QueuedEvent, QueuedEventPriority> _queue = new();
    private readonly ConcurrentQueue<QueuedEvent> _deadLetterQueue = new();
    private readonly SemaphoreSlim _dequeueSemaphore = new(0);
    private readonly object _queueLock = new();
    private readonly int _maxQueueSize;
    private readonly TimeSpan _maxEventAge;
    
    // Metrics tracking
    private long _totalEnqueued;
    private long _totalDequeued;
    private long _totalDeadLettered;
    private int _maxSizeReached;
    private readonly ConcurrentQueue<DateTimeOffset> _recentEnqueueTimes = new();
    private readonly ConcurrentQueue<DateTimeOffset> _recentDequeueTimes = new();
    private readonly ConcurrentQueue<TimeSpan> _recentWaitTimes = new();
    private int _eventsBeingProcessed;

    // Events
    public event EventHandler<QueueSizeChangedEventArgs>? QueueSizeChanged;
    public event EventHandler<EventQueuedEventArgs>? EventEnqueued;
    public event EventHandler<EventDequeuedEventArgs>? EventDequeued;

    private bool _disposed;

    public InMemoryEventQueue(ILogger<InMemoryEventQueue> logger, int maxQueueSize = 10000, int maxEventAgeMinutes = 30)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxQueueSize = maxQueueSize;
        _maxEventAge = TimeSpan.FromMinutes(maxEventAgeMinutes);
        
        _logger.LogInformation("InMemoryEventQueue initialized. MaxSize: {MaxSize}, MaxAge: {MaxAge}", 
            _maxQueueSize, _maxEventAge);
    }

    public int Count
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.Count;
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_queueLock)
            {
                return _queue.Count == 0;
            }
        }
    }

    public async Task EnqueueAsync(LogEvent logEvent, int priority = 0, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryEventQueue));
        ArgumentNullException.ThrowIfNull(logEvent);

        var queuedEvent = new QueuedEvent
        {
            LogEvent = logEvent,
            Priority = priority
        };

        var previousSize = Count;
        var success = false;

        lock (_queueLock)
        {
            if (_queue.Count >= _maxQueueSize)
            {
                _logger.LogWarning("Queue is full ({MaxSize} events). Dropping event {EventId}", 
                    _maxQueueSize, logEvent.EventId);
                return;
            }

            var priorityKey = new QueuedEventPriority(priority, queuedEvent.EnqueuedAt);
            _queue.Enqueue(queuedEvent, priorityKey);
            
            _maxSizeReached = Math.Max(_maxSizeReached, _queue.Count);
            Interlocked.Increment(ref _totalEnqueued);
            success = true;
        }

        if (success)
        {
            // Track enqueue time for rate calculation
            _recentEnqueueTimes.Enqueue(DateTimeOffset.UtcNow);
            CleanupOldMetrics(_recentEnqueueTimes);

            // Release semaphore to notify waiting dequeuers
            _dequeueSemaphore.Release();

            // Fire events
            var currentSize = Count;
            if (Math.Abs(currentSize - previousSize) > Math.Max(1, _maxQueueSize * 0.1))
            {
                QueueSizeChanged?.Invoke(this, new QueueSizeChangedEventArgs
                {
                    PreviousSize = previousSize,
                    CurrentSize = currentSize,
                    MaxCapacity = _maxQueueSize
                });
            }

            EventEnqueued?.Invoke(this, new EventQueuedEventArgs
            {
                QueuedEvent = queuedEvent,
                QueueSize = currentSize
            });

            _logger.LogDebug("Event {EventId} enqueued with priority {Priority}. Queue size: {QueueSize}", 
                logEvent.EventId, priority, currentSize);
        }
    }

    public async Task<QueuedEvent?> DequeueAsync(int timeoutMs = 1000, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryEventQueue));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            await _dequeueSemaphore.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null; // Timeout or cancellation
        }

        QueuedEvent? dequeuedEvent = null;
        var currentSize = 0;

        lock (_queueLock)
        {
            // Clean up expired events first
            CleanupExpiredEvents();

            if (_queue.TryDequeue(out var queuedEvent, out _))
            {
                queuedEvent.DequeuedAt = DateTimeOffset.UtcNow;
                queuedEvent.ProcessingStartedAt = DateTimeOffset.UtcNow;
                
                dequeuedEvent = queuedEvent;
                currentSize = _queue.Count;
                
                Interlocked.Increment(ref _totalDequeued);
                Interlocked.Increment(ref _eventsBeingProcessed);
            }
        }

        if (dequeuedEvent != null)
        {
            // Track dequeue time and wait time
            _recentDequeueTimes.Enqueue(DateTimeOffset.UtcNow);
            if (dequeuedEvent.WaitTime.HasValue)
            {
                _recentWaitTimes.Enqueue(dequeuedEvent.WaitTime.Value);
            }
            
            CleanupOldMetrics(_recentDequeueTimes);
            CleanupOldMetrics(_recentWaitTimes);

            EventDequeued?.Invoke(this, new EventDequeuedEventArgs
            {
                QueuedEvent = dequeuedEvent,
                QueueSize = currentSize,
                WaitTime = dequeuedEvent.WaitTime ?? TimeSpan.Zero
            });

            _logger.LogDebug("Event {EventId} dequeued after {WaitTime}ms. Queue size: {QueueSize}",
                dequeuedEvent.LogEvent.EventId, 
                dequeuedEvent.WaitTime?.TotalMilliseconds ?? 0,
                currentSize);
        }

        return dequeuedEvent;
    }

    public Task<QueuedEvent?> PeekAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryEventQueue));

        lock (_queueLock)
        {
            return Task.FromResult(_queue.TryPeek(out var queuedEvent, out _) ? queuedEvent : null);
        }
    }

    public EventQueueMetrics GetMetrics()
    {
        var currentSize = Count;
        var enqueueRate = CalculateRate(_recentEnqueueTimes);
        var dequeueRate = CalculateRate(_recentDequeueTimes);
        var avgWaitTime = CalculateAverageWaitTime();

        return new EventQueueMetrics
        {
            CurrentSize = currentSize,
            MaxSize = _maxSizeReached,
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalDequeued = Interlocked.Read(ref _totalDequeued),
            TotalDeadLettered = Interlocked.Read(ref _totalDeadLettered),
            AverageWaitTime = avgWaitTime,
            EnqueueRate = enqueueRate,
            DequeueRate = dequeueRate,
            EventsBeingProcessed = _eventsBeingProcessed,
            DeadLetterQueueSize = _deadLetterQueue.Count,
            UtilizationPercent = _maxQueueSize > 0 ? (double)currentSize / _maxQueueSize * 100 : 0
        };
    }

    public void Clear()
    {
        lock (_queueLock)
        {
            _queue.Clear();
            _logger.LogInformation("Event queue cleared");
        }
    }

    public IReadOnlyList<QueuedEvent> GetDeadLetterQueue()
    {
        return _deadLetterQueue.ToList().AsReadOnly();
    }

    public async Task MoveToDeadLetterQueueAsync(QueuedEvent queuedEvent, string reason)
    {
        ArgumentNullException.ThrowIfNull(queuedEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        queuedEvent.LastFailureReason = reason;
        _deadLetterQueue.Enqueue(queuedEvent);
        
        Interlocked.Increment(ref _totalDeadLettered);
        Interlocked.Decrement(ref _eventsBeingProcessed);

        _logger.LogWarning("Event {EventId} moved to dead letter queue. Reason: {Reason}, RetryCount: {RetryCount}",
            queuedEvent.LogEvent.EventId, reason, queuedEvent.RetryCount);

        // Limit dead letter queue size
        while (_deadLetterQueue.Count > 1000 && _deadLetterQueue.TryDequeue(out _))
        {
            // Remove oldest dead letter entries
        }
    }

    private void CleanupExpiredEvents()
    {
        var expiredEvents = new List<QueuedEvent>();
        var tempQueue = new PriorityQueue<QueuedEvent, QueuedEventPriority>();

        // Extract all events and filter out expired ones
        while (_queue.TryDequeue(out var queuedEvent, out var priority))
        {
            if (queuedEvent.IsExpired(_maxEventAge))
            {
                expiredEvents.Add(queuedEvent);
            }
            else
            {
                tempQueue.Enqueue(queuedEvent, priority);
            }
        }

        // Put non-expired events back
        while (tempQueue.TryDequeue(out var queuedEvent, out var priority))
        {
            _queue.Enqueue(queuedEvent, priority);
        }

        // Move expired events to dead letter queue
        foreach (var expiredEvent in expiredEvents)
        {
            _deadLetterQueue.Enqueue(expiredEvent);
            Interlocked.Increment(ref _totalDeadLettered);
        }

        if (expiredEvents.Count > 0)
        {
            _logger.LogWarning("Cleaned up {Count} expired events from queue", expiredEvents.Count);
        }
    }

    private double CalculateRate(ConcurrentQueue<DateTimeOffset> timestamps)
    {
        const int windowSeconds = 60;
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-windowSeconds);
        var count = 0;

        foreach (var timestamp in timestamps)
        {
            if (timestamp >= cutoff)
                count++;
        }

        return count / (double)windowSeconds;
    }

    private TimeSpan CalculateAverageWaitTime()
    {
        if (_recentWaitTimes.IsEmpty)
            return TimeSpan.Zero;

        var totalMs = 0.0;
        var count = 0;

        foreach (var waitTime in _recentWaitTimes)
        {
            totalMs += waitTime.TotalMilliseconds;
            count++;
        }

        return count > 0 ? TimeSpan.FromMilliseconds(totalMs / count) : TimeSpan.Zero;
    }

    private static void CleanupOldMetrics<T>(ConcurrentQueue<T> queue) where T : struct
    {
        const int maxItems = 1000;
        while (queue.Count > maxItems && queue.TryDequeue(out _))
        {
            // Remove oldest items
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dequeueSemaphore.Dispose();
        
        _logger.LogInformation("InMemoryEventQueue disposed. Final metrics: {Metrics}", GetMetrics());
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Priority key for queued events (higher priority processed first, then FIFO)
/// </summary>
public readonly struct QueuedEventPriority : IComparable<QueuedEventPriority>
{
    public int Priority { get; }
    public DateTimeOffset Timestamp { get; }

    public QueuedEventPriority(int priority, DateTimeOffset timestamp)
    {
        Priority = priority;
        Timestamp = timestamp;
    }

    public int CompareTo(QueuedEventPriority other)
    {
        // Higher priority first (reverse comparison)
        var priorityComparison = other.Priority.CompareTo(Priority);
        if (priorityComparison != 0)
            return priorityComparison;

        // Same priority: FIFO (earlier timestamp first)
        return Timestamp.CompareTo(other.Timestamp);
    }
}
