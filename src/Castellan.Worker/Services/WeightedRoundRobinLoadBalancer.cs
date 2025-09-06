using System.Collections.Concurrent;
using System.Diagnostics;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

/// <summary>
/// Weighted round-robin load balancer with adaptive weight adjustment
/// </summary>
public class WeightedRoundRobinLoadBalancer : ILoadBalancer
{
    private readonly ILogger<WeightedRoundRobinLoadBalancer> _logger;
    private readonly LoadBalancingOptions _options;
    private readonly ConcurrentDictionary<string, InstanceLoadState> _instanceStates = new();
    private readonly ConcurrentQueue<ProcessingResult> _recentResults = new();
    private readonly object _selectionLock = new();
    
    // Metrics tracking
    private long _totalDecisions;
    private long _noInstanceAvailable;
    private readonly ConcurrentQueue<TimeSpan> _recentDecisionTimes = new();
    private int _currentIndex = 0;

    // Events
    public event EventHandler<LoadBalancingDecisionEventArgs>? DecisionMade;
    public event EventHandler<InstanceWeightUpdatedEventArgs>? WeightUpdated;

    private bool _disposed;

    public WeightedRoundRobinLoadBalancer(
        ILogger<WeightedRoundRobinLoadBalancer> logger, 
        IOptions<PipelineScalingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value?.LoadBalancing ?? throw new ArgumentNullException(nameof(options));
        
        _logger.LogInformation("WeightedRoundRobinLoadBalancer initialized with strategy: {Strategy}", 
            _options.Strategy);
    }

    public PipelineInstance? SelectInstance(QueuedEvent queuedEvent, IReadOnlyList<PipelineInstance> availableInstances)
    {
        var stopwatch = Stopwatch.StartNew();
        PipelineInstance? selectedInstance = null;
        string selectionReason = string.Empty;

        try
        {
            if (availableInstances == null || availableInstances.Count == 0)
            {
                Interlocked.Increment(ref _noInstanceAvailable);
                selectionReason = "No instances available";
                return null;
            }

            // Handle affinity if specified
            if (!string.IsNullOrEmpty(queuedEvent.AffinityInstanceId))
            {
                var affinityInstance = availableInstances.FirstOrDefault(i => i.Id == queuedEvent.AffinityInstanceId);
                if (affinityInstance != null)
                {
                    selectedInstance = affinityInstance;
                    selectionReason = $"Affinity to instance {queuedEvent.AffinityInstanceId}";
                    goto SelectionComplete;
                }
            }

            // Weighted round-robin selection
            lock (_selectionLock)
            {
                selectedInstance = SelectByWeightedRoundRobin(availableInstances, out selectionReason);
            }

            SelectionComplete:
            Interlocked.Increment(ref _totalDecisions);
            
            // Track decision time
            stopwatch.Stop();
            _recentDecisionTimes.Enqueue(stopwatch.Elapsed);
            CleanupOldMetrics();

            _logger.LogDebug("Load balancer selected instance {InstanceId} for event {EventId}. Reason: {Reason}",
                selectedInstance?.Id ?? "none", queuedEvent.LogEvent.EventId, selectionReason);

            return selectedInstance;
        }
        finally
        {
            // Fire decision event
            DecisionMade?.Invoke(this, new LoadBalancingDecisionEventArgs
            {
                QueuedEvent = queuedEvent,
                SelectedInstance = selectedInstance,
                AvailableInstances = availableInstances,
                DecisionTime = stopwatch.Elapsed,
                SelectionReason = selectionReason
            });
        }
    }

    private PipelineInstance? SelectByWeightedRoundRobin(IReadOnlyList<PipelineInstance> instances, out string reason)
    {
        reason = "Weighted round-robin selection";

        // Ensure instance states are updated
        foreach (var instance in instances)
        {
            _instanceStates.TryAdd(instance.Id, new InstanceLoadState(instance.Id));
        }

        // Calculate total weight
        var totalWeight = instances.Sum(i => i.LoadWeight);
        if (totalWeight <= 0)
        {
            reason = "All instances have zero weight, using simple round-robin";
            return SimpleRoundRobinSelection(instances);
        }

        // Weighted selection using accumulated weights
        var random = new Random().NextDouble() * totalWeight;
        var currentWeight = 0.0;

        foreach (var instance in instances)
        {
            currentWeight += instance.LoadWeight;
            if (random <= currentWeight)
            {
                var state = _instanceStates[instance.Id];
                state.LastSelected = DateTimeOffset.UtcNow;
                state.SelectionCount++;
                
                reason = $"Weighted selection (weight: {instance.LoadWeight:F2}, total: {totalWeight:F2})";
                return instance;
            }
        }

        // Fallback to last instance (should not happen)
        return instances.LastOrDefault();
    }

    private PipelineInstance SimpleRoundRobinSelection(IReadOnlyList<PipelineInstance> instances)
    {
        var index = Interlocked.Increment(ref _currentIndex) % instances.Count;
        return instances[index];
    }

    public async Task UpdateInstanceMetricsAsync(string instanceId, InstancePerformanceMetrics metrics)
    {
        if (!_instanceStates.TryGetValue(instanceId, out var state))
        {
            state = new InstanceLoadState(instanceId);
            _instanceStates.TryAdd(instanceId, state);
        }

        var previousWeight = state.Weight;
        
        // Update metrics and recalculate weight
        state.LastMetrics = metrics;
        state.LastMetricsUpdate = DateTimeOffset.UtcNow;
        
        // Calculate new weight based on performance
        var newWeight = CalculateInstanceWeight(metrics);
        state.Weight = newWeight;

        if (Math.Abs(newWeight - previousWeight) > 0.1)
        {
            _logger.LogDebug("Instance {InstanceId} weight updated from {OldWeight:F2} to {NewWeight:F2}",
                instanceId, previousWeight, newWeight);

            WeightUpdated?.Invoke(this, new InstanceWeightUpdatedEventArgs
            {
                InstanceId = instanceId,
                PreviousWeight = previousWeight,
                NewWeight = newWeight,
                Reason = "Metrics update"
            });
        }
    }

    public async Task RecordProcessingResultAsync(string instanceId, string eventId, TimeSpan processingTime, bool success)
    {
        var result = new ProcessingResult
        {
            InstanceId = instanceId,
            EventId = eventId,
            ProcessingTime = processingTime,
            Success = success,
            Timestamp = DateTimeOffset.UtcNow
        };

        _recentResults.Enqueue(result);
        CleanupOldResults();

        // Update instance state
        if (_instanceStates.TryGetValue(instanceId, out var state))
        {
            state.RecordProcessingResult(processingTime, success);
        }

        _logger.LogTrace("Recorded processing result for instance {InstanceId}: {Success} in {Duration}ms",
            instanceId, success, processingTime.TotalMilliseconds);
    }

    public LoadBalancingMetrics GetMetrics()
    {
        var decisionTimeMs = _recentDecisionTimes.IsEmpty 
            ? 0.0 
            : _recentDecisionTimes.Average(dt => dt.TotalMilliseconds);

        var loadDistribution = GetLoadDistribution();
        var loadVariance = CalculateLoadVariance(loadDistribution);
        
        var effectivenessScore = CalculateEffectivenessScore();
        var avgUtilization = _instanceStates.Values
            .Where(s => s.LastMetrics != null)
            .Average(s => s.LastMetrics!.CpuUtilizationPercent);

        return new LoadBalancingMetrics
        {
            TotalDecisions = _totalDecisions,
            NoInstanceAvailable = _noInstanceAvailable,
            AverageDecisionTimeMs = decisionTimeMs,
            LoadVariance = loadVariance,
            EffectivenessScore = effectivenessScore,
            TrackedInstances = _instanceStates.Count,
            AverageUtilization = avgUtilization
        };
    }

    public IReadOnlyDictionary<string, double> GetLoadDistribution()
    {
        var totalSelections = _instanceStates.Values.Sum(s => s.SelectionCount);
        if (totalSelections == 0)
            return new Dictionary<string, double>();

        return _instanceStates.ToDictionary(
            kvp => kvp.Key,
            kvp => (double)kvp.Value.SelectionCount / totalSelections * 100.0
        );
    }

    public void RefreshInstanceWeights(IReadOnlyList<PipelineInstance> instances)
    {
        var updatedCount = 0;

        foreach (var instance in instances)
        {
            if (_instanceStates.TryGetValue(instance.Id, out var state) && state.LastMetrics != null)
            {
                var oldWeight = state.Weight;
                var newWeight = CalculateInstanceWeight(state.LastMetrics);
                
                if (Math.Abs(newWeight - oldWeight) > 0.05)
                {
                    state.Weight = newWeight;
                    updatedCount++;
                    
                    WeightUpdated?.Invoke(this, new InstanceWeightUpdatedEventArgs
                    {
                        InstanceId = instance.Id,
                        PreviousWeight = oldWeight,
                        NewWeight = newWeight,
                        Reason = "Manual refresh"
                    });
                }
            }
        }

        _logger.LogInformation("Refreshed weights for {Count} instances", updatedCount);
    }

    private double CalculateInstanceWeight(InstancePerformanceMetrics metrics)
    {
        // Weight based on multiple factors
        var cpuFactor = Math.Max(0.1, 1.0 - metrics.CpuUtilizationPercent / 100.0);
        var memoryFactor = Math.Max(0.1, 1.0 - metrics.MemoryUtilizationPercent / 100.0);
        var responseTimeFactor = Math.Max(0.1, 1.0 - Math.Min(1.0, metrics.AverageResponseTimeMs / 2000.0));
        var errorFactor = Math.Max(0.1, 1.0 - metrics.ErrorRatePercent / 100.0);
        var queueFactor = metrics.QueueLength > 0 
            ? Math.Max(0.1, 1.0 - Math.Min(1.0, metrics.QueueLength / 1000.0))
            : 1.0;

        // Weighted average with emphasis on CPU and response time
        var weight = (cpuFactor * 0.3 + 
                     memoryFactor * 0.2 + 
                     responseTimeFactor * 0.3 + 
                     errorFactor * 0.15 + 
                     queueFactor * 0.05);

        return Math.Max(0.1, Math.Min(2.0, weight)); // Clamp between 0.1 and 2.0
    }

    private double CalculateLoadVariance(IReadOnlyDictionary<string, double> distribution)
    {
        if (distribution.Count < 2)
            return 0.0;

        var mean = distribution.Values.Average();
        var variance = distribution.Values.Sum(load => Math.Pow(load - mean, 2)) / distribution.Count;
        
        return Math.Sqrt(variance) / 100.0; // Normalize to 0-1 scale
    }

    private double CalculateEffectivenessScore()
    {
        var recentWindow = TimeSpan.FromMinutes(5);
        var cutoff = DateTimeOffset.UtcNow - recentWindow;
        
        var recentResults = _recentResults
            .Where(r => r.Timestamp >= cutoff)
            .ToList();

        if (!recentResults.Any())
            return 1.0;

        var successRate = recentResults.Count(r => r.Success) / (double)recentResults.Count;
        var avgResponseTime = recentResults.Average(r => r.ProcessingTime.TotalMilliseconds);
        var responseTimeScore = Math.Max(0.0, 1.0 - avgResponseTime / 5000.0); // 5s baseline

        return (successRate + responseTimeScore) / 2.0;
    }

    private void CleanupOldMetrics()
    {
        // Keep only recent decision times
        while (_recentDecisionTimes.Count > 1000 && _recentDecisionTimes.TryDequeue(out _))
        {
            // Remove oldest entries
        }
    }

    private void CleanupOldResults()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        while (_recentResults.TryPeek(out var result) && result.Timestamp < cutoff)
        {
            _recentResults.TryDequeue(out _);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        _logger.LogInformation("WeightedRoundRobinLoadBalancer disposed. Final metrics: {Metrics}", 
            GetMetrics().EffectivenessScore);
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Internal state tracking for each instance
/// </summary>
internal class InstanceLoadState
{
    public string InstanceId { get; }
    public long SelectionCount { get; set; }
    public DateTimeOffset LastSelected { get; set; }
    public double Weight { get; set; } = 1.0;
    public InstancePerformanceMetrics? LastMetrics { get; set; }
    public DateTimeOffset LastMetricsUpdate { get; set; }
    
    // Processing statistics
    private readonly Queue<TimeSpan> _recentProcessingTimes = new();
    private readonly Queue<bool> _recentResults = new();

    public InstanceLoadState(string instanceId)
    {
        InstanceId = instanceId;
        LastSelected = DateTimeOffset.UtcNow;
    }

    public void RecordProcessingResult(TimeSpan processingTime, bool success)
    {
        _recentProcessingTimes.Enqueue(processingTime);
        _recentResults.Enqueue(success);

        // Keep only recent results (last 100)
        while (_recentProcessingTimes.Count > 100)
        {
            _recentProcessingTimes.Dequeue();
            _recentResults.Dequeue();
        }
    }

    public TimeSpan AverageProcessingTime => _recentProcessingTimes.Count > 0 
        ? TimeSpan.FromTicks((long)_recentProcessingTimes.Average(t => t.Ticks))
        : TimeSpan.Zero;

    public double SuccessRate => _recentResults.Count > 0 
        ? _recentResults.Count(r => r) / (double)_recentResults.Count
        : 1.0;
}

/// <summary>
/// Processing result record for tracking
/// </summary>
internal record ProcessingResult
{
    public required string InstanceId { get; init; }
    public required string EventId { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public bool Success { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
