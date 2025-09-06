using Castellan.Core.Interfaces.Pipeline.Scaling;
using Castellan.Core.Models.Pipeline.Scaling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Castellan.Core.Services.Pipeline.Scaling
{
    public class PipelineAutoScaler : BackgroundService
    {
        private readonly IPipelineInstanceManager _instanceManager;
        private readonly IEventQueue _eventQueue;
        private readonly ILogger<PipelineAutoScaler> _logger;
        private readonly PipelineScalingOptions _options;
        private readonly ConcurrentDictionary<string, ScalingDecision> _recentDecisions;
        private readonly SemaphoreSlim _scalingLock;
        private DateTime _lastScalingEvaluation;
        private readonly Timer _metricsCollectionTimer;

        public PipelineAutoScaler(
            IPipelineInstanceManager instanceManager,
            IEventQueue eventQueue,
            ILogger<PipelineAutoScaler> logger,
            IOptions<PipelineScalingOptions> options)
        {
            _instanceManager = instanceManager;
            _eventQueue = eventQueue;
            _logger = logger;
            _options = options.Value;
            _recentDecisions = new ConcurrentDictionary<string, ScalingDecision>();
            _scalingLock = new SemaphoreSlim(1, 1);
            _lastScalingEvaluation = DateTime.UtcNow;
            
            // Initialize metrics collection timer
            _metricsCollectionTimer = new Timer(CollectScalingMetrics, null, TimeSpan.Zero, 
                TimeSpan.FromSeconds(_options.AutoScaling.EvaluationInterval));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Pipeline Auto Scaler started with policy: {Policy}", 
                _options.AutoScaling.ScalingPolicy.PolicyType);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EvaluateScalingAsync();
                    await Task.Delay(TimeSpan.FromSeconds(_options.AutoScaling.EvaluationInterval), stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error during auto-scaling evaluation");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // Brief delay before retry
                }
            }

            _logger.LogInformation("Pipeline Auto Scaler stopped");
        }

        private async Task EvaluateScalingAsync()
        {
            if (!_options.AutoScaling.Enabled)
            {
                return;
            }

            await _scalingLock.WaitAsync();
            try
            {
                var instances = await _instanceManager.GetInstancesAsync();
                var activeInstances = instances.Where(i => i.Status == InstanceStatus.Running).ToList();
                var queueMetrics = await _eventQueue.GetMetricsAsync();

                var scalingMetrics = await CollectScalingMetricsAsync(activeInstances, queueMetrics);
                var scalingDecision = await EvaluateScalingDecisionAsync(scalingMetrics, activeInstances.Count);

                if (scalingDecision.Action != ScalingAction.None)
                {
                    _logger.LogInformation("Auto-scaling decision: {Action} {Count} instance(s). Reason: {Reason}",
                        scalingDecision.Action, scalingDecision.InstanceCount, scalingDecision.Reason);

                    await ExecuteScalingDecisionAsync(scalingDecision);
                    RecordScalingDecision(scalingDecision);
                }

                _lastScalingEvaluation = DateTime.UtcNow;
            }
            finally
            {
                _scalingLock.Release();
            }
        }

        private async Task<ScalingMetrics> CollectScalingMetricsAsync(
            List<PipelineInstance> activeInstances, 
            EventQueueMetrics queueMetrics)
        {
            var metrics = new ScalingMetrics
            {
                Timestamp = DateTime.UtcNow,
                ActiveInstanceCount = activeInstances.Count,
                QueueDepth = queueMetrics.QueueDepth,
                HighPriorityQueueDepth = queueMetrics.HighPriorityCount,
                ProcessingRate = queueMetrics.ProcessingRate,
                ErrorRate = queueMetrics.ErrorRate
            };

            if (activeInstances.Any())
            {
                metrics.AverageCpuUsage = activeInstances.Average(i => i.Metrics.CpuUsagePercent);
                metrics.AverageMemoryUsage = activeInstances.Average(i => i.Metrics.MemoryUsagePercent);
                metrics.AverageResponseTime = TimeSpan.FromMilliseconds(
                    activeInstances.Average(i => i.Metrics.AverageResponseTime.TotalMilliseconds));
                metrics.TotalThroughput = activeInstances.Sum(i => i.Metrics.EventsProcessedPerSecond);
                
                // Calculate resource pressure
                metrics.CpuPressure = metrics.AverageCpuUsage / 100.0;
                metrics.MemoryPressure = metrics.AverageMemoryUsage / 100.0;
                metrics.QueuePressure = Math.Min(1.0, metrics.QueueDepth / 1000.0); // Normalize queue depth
            }

            return metrics;
        }

        private async Task<ScalingDecision> EvaluateScalingDecisionAsync(
            ScalingMetrics metrics, 
            int currentInstanceCount)
        {
            var policy = _options.AutoScaling.ScalingPolicy;
            var decision = new ScalingDecision
            {
                Timestamp = DateTime.UtcNow,
                CurrentInstanceCount = currentInstanceCount,
                Action = ScalingAction.None,
                InstanceCount = 0,
                Reason = "No scaling required",
                Metrics = metrics
            };

            // Check cooldown period
            if (IsInCooldownPeriod())
            {
                decision.Reason = "Scaling is in cooldown period";
                return decision;
            }

            // Evaluate scale-up conditions
            var scaleUpDecision = await EvaluateScaleUpAsync(metrics, currentInstanceCount, policy);
            if (scaleUpDecision.Action == ScalingAction.ScaleUp)
            {
                return scaleUpDecision;
            }

            // Evaluate scale-down conditions
            var scaleDownDecision = await EvaluateScaleDownAsync(metrics, currentInstanceCount, policy);
            if (scaleDownDecision.Action == ScalingAction.ScaleDown)
            {
                return scaleDownDecision;
            }

            return decision;
        }

        private async Task<ScalingDecision> EvaluateScaleUpAsync(
            ScalingMetrics metrics, 
            int currentInstanceCount, 
            ScalingPolicyOptions policy)
        {
            var decision = new ScalingDecision
            {
                Timestamp = DateTime.UtcNow,
                CurrentInstanceCount = currentInstanceCount,
                Action = ScalingAction.None,
                Metrics = metrics
            };

            var reasons = new List<string>();

            // Check maximum instance limit
            if (currentInstanceCount >= _options.InstanceLimits.MaxInstances)
            {
                decision.Reason = "Maximum instance limit reached";
                return decision;
            }

            // Evaluate based on policy type
            switch (policy.PolicyType)
            {
                case ScalingPolicyType.TargetTracking:
                    return await EvaluateTargetTrackingScaleUp(metrics, currentInstanceCount, policy, reasons);
                    
                case ScalingPolicyType.StepScaling:
                    return await EvaluateStepScalingScaleUp(metrics, currentInstanceCount, policy, reasons);
                    
                case ScalingPolicyType.PredictiveScaling:
                    return await EvaluatePredictiveScalingScaleUp(metrics, currentInstanceCount, policy, reasons);
                    
                default:
                    decision.Reason = "Unknown scaling policy type";
                    return decision;
            }
        }

        private async Task<ScalingDecision> EvaluateTargetTrackingScaleUp(
            ScalingMetrics metrics,
            int currentInstanceCount,
            ScalingPolicyOptions policy,
            List<string> reasons)
        {
            var decision = new ScalingDecision
            {
                Timestamp = DateTime.UtcNow,
                CurrentInstanceCount = currentInstanceCount,
                Action = ScalingAction.None,
                Metrics = metrics
            };

            // Check CPU threshold
            if (metrics.AverageCpuUsage > policy.TargetCpuUtilization)
            {
                reasons.Add($"CPU usage {metrics.AverageCpuUsage:F1}% > target {policy.TargetCpuUtilization}%");
            }

            // Check memory threshold
            if (metrics.AverageMemoryUsage > policy.TargetMemoryUtilization)
            {
                reasons.Add($"Memory usage {metrics.AverageMemoryUsage:F1}% > target {policy.TargetMemoryUtilization}%");
            }

            // Check queue depth
            if (metrics.QueueDepth > policy.TargetQueueDepth)
            {
                reasons.Add($"Queue depth {metrics.QueueDepth} > target {policy.TargetQueueDepth}");
            }

            // Check response time
            if (metrics.AverageResponseTime.TotalMilliseconds > policy.TargetResponseTime)
            {
                reasons.Add($"Response time {metrics.AverageResponseTime.TotalMilliseconds:F0}ms > target {policy.TargetResponseTime}ms");
            }

            // Scale up if any threshold is exceeded
            if (reasons.Any())
            {
                var scaleUpCount = CalculateScaleUpCount(metrics, currentInstanceCount, policy);
                decision.Action = ScalingAction.ScaleUp;
                decision.InstanceCount = scaleUpCount;
                decision.Reason = $"Scale up triggered: {string.Join(", ", reasons)}";
            }
            else
            {
                decision.Reason = "All metrics within target thresholds";
            }

            return decision;
        }

        private async Task<ScalingDecision> EvaluateStepScalingScaleUp(
            ScalingMetrics metrics,
            int currentInstanceCount,
            ScalingPolicyOptions policy,
            List<string> reasons)
        {
            // Step scaling based on severity of metric breaches
            var decision = new ScalingDecision
            {
                Timestamp = DateTime.UtcNow,
                CurrentInstanceCount = currentInstanceCount,
                Action = ScalingAction.None,
                Metrics = metrics
            };

            var maxBreach = 0.0;
            
            // Calculate breach severity for each metric
            var cpuBreach = Math.Max(0, (metrics.AverageCpuUsage - policy.TargetCpuUtilization) / policy.TargetCpuUtilization);
            var memoryBreach = Math.Max(0, (metrics.AverageMemoryUsage - policy.TargetMemoryUtilization) / policy.TargetMemoryUtilization);
            var queueBreach = Math.Max(0, (metrics.QueueDepth - policy.TargetQueueDepth) / (double)policy.TargetQueueDepth);
            
            maxBreach = Math.Max(Math.Max(cpuBreach, memoryBreach), queueBreach);

            if (maxBreach > 0)
            {
                // Determine step scaling amount based on breach severity
                int scaleUpCount;
                if (maxBreach > 0.5) // Severe breach (>50% over target)
                    scaleUpCount = Math.Min(policy.MaxScaleOutStep, _options.InstanceLimits.MaxInstances - currentInstanceCount);
                else if (maxBreach > 0.2) // Moderate breach (>20% over target)
                    scaleUpCount = Math.Min(Math.Max(2, policy.MaxScaleOutStep / 2), _options.InstanceLimits.MaxInstances - currentInstanceCount);
                else // Minor breach
                    scaleUpCount = 1;

                decision.Action = ScalingAction.ScaleUp;
                decision.InstanceCount = scaleUpCount;
                decision.Reason = $"Step scaling: {maxBreach:P1} breach, adding {scaleUpCount} instance(s)";
            }
            else
            {
                decision.Reason = "No metric breach detected for step scaling";
            }

            return decision;
        }

        private async Task<ScalingDecision> EvaluatePredictiveScalingScaleUp(
            ScalingMetrics metrics,
            int currentInstanceCount,
            ScalingPolicyOptions policy,
            List<string> reasons)
        {
            // Simplified predictive scaling - would integrate with ML models in production
            var decision = new ScalingDecision
            {
                Timestamp = DateTime.UtcNow,
                CurrentInstanceCount = currentInstanceCount,
                Action = ScalingAction.None,
                Metrics = metrics
            };

            // For now, use trend analysis based on recent metrics
            var recentMetrics = await GetRecentMetricsAsync(TimeSpan.FromMinutes(10));
            if (recentMetrics.Count < 3)
            {
                decision.Reason = "Insufficient historical data for predictive scaling";
                return decision;
            }

            // Calculate trends
            var queueTrend = CalculateTrend(recentMetrics.Select(m => (double)m.QueueDepth).ToList());
            var cpuTrend = CalculateTrend(recentMetrics.Select(m => m.AverageCpuUsage).ToList());

            // Predict need for scaling based on trends
            if (queueTrend > 0.1 && cpuTrend > 0.05) // Both queue and CPU are trending up
            {
                decision.Action = ScalingAction.ScaleUp;
                decision.InstanceCount = 1;
                decision.Reason = $"Predictive scaling: Queue trend {queueTrend:F3}, CPU trend {cpuTrend:F3}";
            }
            else
            {
                decision.Reason = "Predictive model indicates no scaling needed";
            }

            return decision;
        }

        private async Task<ScalingDecision> EvaluateScaleDownAsync(
            ScalingMetrics metrics,
            int currentInstanceCount,
            ScalingPolicyOptions policy)
        {
            var decision = new ScalingDecision
            {
                Timestamp = DateTime.UtcNow,
                CurrentInstanceCount = currentInstanceCount,
                Action = ScalingAction.None,
                Metrics = metrics
            };

            // Check minimum instance limit
            if (currentInstanceCount <= _options.InstanceLimits.MinInstances)
            {
                decision.Reason = "Minimum instance limit reached";
                return decision;
            }

            var reasons = new List<string>();

            // Check if resources are underutilized
            var cpuUtilization = metrics.AverageCpuUsage;
            var memoryUtilization = metrics.AverageMemoryUsage;
            var queueDepth = metrics.QueueDepth;

            var scaleDownCpuThreshold = policy.TargetCpuUtilization * 0.7; // 70% of target
            var scaleDownMemoryThreshold = policy.TargetMemoryUtilization * 0.7;
            var scaleDownQueueThreshold = policy.TargetQueueDepth * 0.5; // 50% of target

            if (cpuUtilization < scaleDownCpuThreshold)
            {
                reasons.Add($"CPU usage {cpuUtilization:F1}% < scale-down threshold {scaleDownCpuThreshold:F1}%");
            }

            if (memoryUtilization < scaleDownMemoryThreshold)
            {
                reasons.Add($"Memory usage {memoryUtilization:F1}% < scale-down threshold {scaleDownMemoryThreshold:F1}%");
            }

            if (queueDepth < scaleDownQueueThreshold)
            {
                reasons.Add($"Queue depth {queueDepth} < scale-down threshold {scaleDownQueueThreshold}");
            }

            // Scale down only if multiple conditions are met (conservative approach)
            if (reasons.Count >= 2)
            {
                var scaleDownCount = Math.Min(policy.MaxScaleInStep, currentInstanceCount - _options.InstanceLimits.MinInstances);
                decision.Action = ScalingAction.ScaleDown;
                decision.InstanceCount = scaleDownCount;
                decision.Reason = $"Scale down triggered: {string.Join(", ", reasons)}";
            }
            else
            {
                decision.Reason = "Insufficient conditions met for scale down";
            }

            return decision;
        }

        private async Task ExecuteScalingDecisionAsync(ScalingDecision decision)
        {
            try
            {
                switch (decision.Action)
                {
                    case ScalingAction.ScaleUp:
                        await ScaleUpAsync(decision.InstanceCount);
                        break;
                    case ScalingAction.ScaleDown:
                        await ScaleDownAsync(decision.InstanceCount);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scaling decision: {Action} {Count} instance(s)",
                    decision.Action, decision.InstanceCount);
                throw;
            }
        }

        private async Task ScaleUpAsync(int instanceCount)
        {
            _logger.LogInformation("Scaling up {InstanceCount} instance(s)", instanceCount);
            
            for (int i = 0; i < instanceCount; i++)
            {
                try
                {
                    var instanceId = await _instanceManager.CreateInstanceAsync();
                    await _instanceManager.StartInstanceAsync(instanceId);
                    
                    _logger.LogInformation("Successfully started new instance: {InstanceId}", instanceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start instance {InstanceNumber} of {TotalCount}",
                        i + 1, instanceCount);
                }
            }
        }

        private async Task ScaleDownAsync(int instanceCount)
        {
            _logger.LogInformation("Scaling down {InstanceCount} instance(s)", instanceCount);
            
            var instances = await _instanceManager.GetInstancesAsync();
            var candidatesForRemoval = instances
                .Where(i => i.Status == InstanceStatus.Running)
                .OrderBy(i => i.Metrics.EventsProcessedPerSecond) // Remove least busy instances first
                .Take(instanceCount)
                .ToList();

            foreach (var instance in candidatesForRemoval)
            {
                try
                {
                    await _instanceManager.StopInstanceAsync(instance.Id);
                    await _instanceManager.RemoveInstanceAsync(instance.Id);
                    
                    _logger.LogInformation("Successfully removed instance: {InstanceId}", instance.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove instance: {InstanceId}", instance.Id);
                }
            }
        }

        private int CalculateScaleUpCount(ScalingMetrics metrics, int currentInstanceCount, ScalingPolicyOptions policy)
        {
            // Calculate desired instance count based on current load
            var desiredInstances = currentInstanceCount;

            // Factor in queue depth
            if (metrics.QueueDepth > policy.TargetQueueDepth)
            {
                var queueFactor = metrics.QueueDepth / (double)policy.TargetQueueDepth;
                desiredInstances = Math.Max(desiredInstances, (int)Math.Ceiling(currentInstanceCount * queueFactor));
            }

            // Factor in CPU utilization
            if (metrics.AverageCpuUsage > policy.TargetCpuUtilization)
            {
                var cpuFactor = metrics.AverageCpuUsage / policy.TargetCpuUtilization;
                desiredInstances = Math.Max(desiredInstances, (int)Math.Ceiling(currentInstanceCount * cpuFactor));
            }

            var scaleUpCount = Math.Min(desiredInstances - currentInstanceCount, policy.MaxScaleOutStep);
            return Math.Max(1, Math.Min(scaleUpCount, _options.InstanceLimits.MaxInstances - currentInstanceCount));
        }

        private bool IsInCooldownPeriod()
        {
            var lastDecision = _recentDecisions.Values
                .OrderByDescending(d => d.Timestamp)
                .FirstOrDefault();

            if (lastDecision == null)
                return false;

            var cooldownPeriod = lastDecision.Action == ScalingAction.ScaleUp
                ? TimeSpan.FromSeconds(_options.AutoScaling.ScaleUpCooldown)
                : TimeSpan.FromSeconds(_options.AutoScaling.ScaleDownCooldown);

            return DateTime.UtcNow - lastDecision.Timestamp < cooldownPeriod;
        }

        private void RecordScalingDecision(ScalingDecision decision)
        {
            var key = $"{decision.Timestamp:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid():N}";
            _recentDecisions.TryAdd(key, decision);

            // Clean up old decisions (keep last 100)
            if (_recentDecisions.Count > 100)
            {
                var keysToRemove = _recentDecisions.Keys
                    .OrderBy(k => k)
                    .Take(_recentDecisions.Count - 100)
                    .ToList();

                foreach (var keyToRemove in keysToRemove)
                {
                    _recentDecisions.TryRemove(keyToRemove, out _);
                }
            }
        }

        private async Task<List<ScalingMetrics>> GetRecentMetricsAsync(TimeSpan timeSpan)
        {
            // This would retrieve historical metrics from a data store
            // For now, return empty list as placeholder
            return new List<ScalingMetrics>();
        }

        private double CalculateTrend(List<double> values)
        {
            if (values.Count < 2)
                return 0;

            // Simple linear trend calculation
            var n = values.Count;
            var sumX = (n * (n - 1)) / 2; // Sum of indices
            var sumY = values.Sum();
            var sumXY = values.Select((y, x) => x * y).Sum();
            var sumX2 = values.Select((_, x) => x * x).Sum();

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return slope;
        }

        private void CollectScalingMetrics(object state)
        {
            try
            {
                var decisionCount = _recentDecisions.Count;
                var lastEvaluationAge = DateTime.UtcNow - _lastScalingEvaluation;
                
                _logger.LogDebug("Auto-scaling metrics - Recent decisions: {DecisionCount}, " +
                    "Last evaluation: {LastEvaluationAge} seconds ago",
                    decisionCount, lastEvaluationAge.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting auto-scaling metrics");
            }
        }

        public override void Dispose()
        {
            _metricsCollectionTimer?.Dispose();
            _scalingLock?.Dispose();
            base.Dispose();
        }
    }

    public class ScalingMetrics
    {
        public DateTime Timestamp { get; set; }
        public int ActiveInstanceCount { get; set; }
        public int QueueDepth { get; set; }
        public int HighPriorityQueueDepth { get; set; }
        public double ProcessingRate { get; set; }
        public double ErrorRate { get; set; }
        public double AverageCpuUsage { get; set; }
        public double AverageMemoryUsage { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
        public double TotalThroughput { get; set; }
        public double CpuPressure { get; set; }
        public double MemoryPressure { get; set; }
        public double QueuePressure { get; set; }
    }

    public class ScalingDecision
    {
        public DateTime Timestamp { get; set; }
        public int CurrentInstanceCount { get; set; }
        public ScalingAction Action { get; set; }
        public int InstanceCount { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ScalingMetrics Metrics { get; set; } = new();
    }

    public enum ScalingAction
    {
        None,
        ScaleUp,
        ScaleDown
    }
}
