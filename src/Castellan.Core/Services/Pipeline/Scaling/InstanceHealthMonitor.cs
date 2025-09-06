using Castellan.Core.Interfaces.Pipeline.Scaling;
using Castellan.Core.Models.Pipeline.Scaling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Castellan.Core.Services.Pipeline.Scaling
{
    public class InstanceHealthMonitor : BackgroundService
    {
        private readonly IPipelineInstanceManager _instanceManager;
        private readonly ILogger<InstanceHealthMonitor> _logger;
        private readonly PipelineScalingOptions _options;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, InstanceHealthHistory> _healthHistory;
        private readonly ConcurrentDictionary<string, DateTime> _lastHealthCheck;
        private readonly Timer _metricsTimer;

        public InstanceHealthMonitor(
            IPipelineInstanceManager instanceManager,
            ILogger<InstanceHealthMonitor> logger,
            IOptions<PipelineScalingOptions> options,
            HttpClient httpClient)
        {
            _instanceManager = instanceManager;
            _logger = logger;
            _options = options.Value;
            _httpClient = httpClient;
            _healthHistory = new ConcurrentDictionary<string, InstanceHealthHistory>();
            _lastHealthCheck = new ConcurrentDictionary<string, DateTime>();
            
            // Initialize metrics collection timer
            _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, 
                TimeSpan.FromSeconds(_options.HealthMonitoring.MetricsCollectionInterval));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Instance Health Monitor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformHealthChecksAsync();
                    await Task.Delay(TimeSpan.FromSeconds(_options.HealthMonitoring.CheckInterval), stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Error during health monitoring cycle");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Brief delay before retry
                }
            }

            _logger.LogInformation("Instance Health Monitor stopped");
        }

        private async Task PerformHealthChecksAsync()
        {
            var instances = await _instanceManager.GetInstancesAsync();
            var healthCheckTasks = instances.Select(instance => CheckInstanceHealthAsync(instance));
            
            await Task.WhenAll(healthCheckTasks);
        }

        private async Task CheckInstanceHealthAsync(PipelineInstance instance)
        {
            try
            {
                var healthCheckResult = new HealthCheckResult
                {
                    InstanceId = instance.Id,
                    CheckTimestamp = DateTime.UtcNow,
                    IsHealthy = true,
                    ResponseTime = TimeSpan.Zero,
                    Details = new Dictionary<string, object>()
                };

                // Perform HTTP health check if endpoint is configured
                if (!string.IsNullOrEmpty(instance.HealthEndpoint))
                {
                    await PerformHttpHealthCheckAsync(instance, healthCheckResult);
                }

                // Check performance metrics
                CheckPerformanceMetrics(instance, healthCheckResult);

                // Update health history
                UpdateHealthHistory(instance.Id, healthCheckResult);

                // Determine overall health status
                var overallHealth = DetermineOverallHealth(instance.Id);
                
                // Update instance if health status changed
                if (instance.Health != overallHealth)
                {
                    await _instanceManager.UpdateHealthAsync(instance.Id, overallHealth);
                    
                    _logger.LogInformation("Instance {InstanceId} health changed from {OldHealth} to {NewHealth}",
                        instance.Id, instance.Health, overallHealth);
                    
                    // Fire health change event
                    await _instanceManager.FireHealthChangeEventAsync(instance.Id, instance.Health, overallHealth);
                }

                // Check alert thresholds
                await CheckAlertThresholdsAsync(instance, healthCheckResult);

                _lastHealthCheck[instance.Id] = DateTime.UtcNow;
                
                _logger.LogDebug("Health check completed for instance {InstanceId}: {Health} (Response: {ResponseTime}ms)",
                    instance.Id, overallHealth, healthCheckResult.ResponseTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check health for instance {InstanceId}", instance.Id);
                
                // Record failed health check
                var failedResult = new HealthCheckResult
                {
                    InstanceId = instance.Id,
                    CheckTimestamp = DateTime.UtcNow,
                    IsHealthy = false,
                    Error = ex.Message,
                    Details = new Dictionary<string, object> { ["exception"] = ex.GetType().Name }
                };
                
                UpdateHealthHistory(instance.Id, failedResult);
                var overallHealth = DetermineOverallHealth(instance.Id);
                
                if (instance.Health != overallHealth)
                {
                    await _instanceManager.UpdateHealthAsync(instance.Id, overallHealth);
                    await _instanceManager.FireHealthChangeEventAsync(instance.Id, instance.Health, overallHealth);
                }
            }
        }

        private async Task PerformHttpHealthCheckAsync(PipelineInstance instance, HealthCheckResult result)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.HealthMonitoring.Timeout));
                var response = await _httpClient.GetAsync(instance.HealthEndpoint, cts.Token);
                
                result.ResponseTime = DateTime.UtcNow - startTime;
                result.HttpStatusCode = (int)response.StatusCode;
                result.IsHealthy = response.IsSuccessStatusCode;
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var healthData = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                        if (healthData != null)
                        {
                            foreach (var kvp in healthData)
                            {
                                result.Details[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    catch
                    {
                        result.Details["raw_response"] = content;
                    }
                }
                else
                {
                    result.Error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
                }
            }
            catch (TaskCanceledException)
            {
                result.ResponseTime = DateTime.UtcNow - startTime;
                result.IsHealthy = false;
                result.Error = "Health check timeout";
            }
            catch (Exception ex)
            {
                result.ResponseTime = DateTime.UtcNow - startTime;
                result.IsHealthy = false;
                result.Error = ex.Message;
            }
        }

        private void CheckPerformanceMetrics(PipelineInstance instance, HealthCheckResult result)
        {
            var thresholds = _options.HealthMonitoring.AlertThresholds;
            
            // Check CPU usage
            if (instance.Metrics.CpuUsagePercent > thresholds.CpuUsageThreshold)
            {
                result.IsHealthy = false;
                result.Details["high_cpu"] = instance.Metrics.CpuUsagePercent;
            }
            
            // Check memory usage
            if (instance.Metrics.MemoryUsagePercent > thresholds.MemoryUsageThreshold)
            {
                result.IsHealthy = false;
                result.Details["high_memory"] = instance.Metrics.MemoryUsagePercent;
            }
            
            // Check error rate
            if (instance.Metrics.ErrorRate > thresholds.ErrorRateThreshold)
            {
                result.IsHealthy = false;
                result.Details["high_error_rate"] = instance.Metrics.ErrorRate;
            }
            
            // Check response time
            if (instance.Metrics.AverageResponseTime.TotalMilliseconds > thresholds.ResponseTimeThreshold)
            {
                result.IsHealthy = false;
                result.Details["high_response_time"] = instance.Metrics.AverageResponseTime.TotalMilliseconds;
            }
            
            // Check queue depth
            if (instance.Metrics.QueueDepth > thresholds.QueueDepthThreshold)
            {
                result.IsHealthy = false;
                result.Details["high_queue_depth"] = instance.Metrics.QueueDepth;
            }
            
            // Add current metrics to details
            result.Details["cpu_usage"] = instance.Metrics.CpuUsagePercent;
            result.Details["memory_usage"] = instance.Metrics.MemoryUsagePercent;
            result.Details["error_rate"] = instance.Metrics.ErrorRate;
            result.Details["response_time"] = instance.Metrics.AverageResponseTime.TotalMilliseconds;
            result.Details["queue_depth"] = instance.Metrics.QueueDepth;
        }

        private void UpdateHealthHistory(string instanceId, HealthCheckResult result)
        {
            var history = _healthHistory.GetOrAdd(instanceId, _ => new InstanceHealthHistory
            {
                InstanceId = instanceId,
                HealthChecks = new List<HealthCheckResult>(),
                ConsecutiveFailures = 0,
                ConsecutiveSuccesses = 0
            });

            lock (history)
            {
                // Add new result
                history.HealthChecks.Add(result);
                
                // Keep only recent history
                var cutoff = DateTime.UtcNow.AddMinutes(-_options.HealthMonitoring.HealthHistoryMinutes);
                history.HealthChecks = history.HealthChecks
                    .Where(hc => hc.CheckTimestamp >= cutoff)
                    .ToList();
                
                // Update consecutive counters
                if (result.IsHealthy)
                {
                    history.ConsecutiveSuccesses++;
                    history.ConsecutiveFailures = 0;
                }
                else
                {
                    history.ConsecutiveFailures++;
                    history.ConsecutiveSuccesses = 0;
                }
            }
        }

        private InstanceHealth DetermineOverallHealth(string instanceId)
        {
            if (!_healthHistory.TryGetValue(instanceId, out var history))
                return InstanceHealth.Unknown;

            lock (history)
            {
                // If we have consecutive failures beyond threshold, mark as unhealthy
                if (history.ConsecutiveFailures >= _options.HealthMonitoring.ConsecutiveFailureThreshold)
                    return InstanceHealth.Unhealthy;
                
                // If we have consecutive successes beyond threshold, mark as healthy
                if (history.ConsecutiveSuccesses >= _options.HealthMonitoring.ConsecutiveSuccessThreshold)
                    return InstanceHealth.Healthy;
                
                // Check recent health check results
                var recentChecks = history.HealthChecks
                    .Where(hc => hc.CheckTimestamp >= DateTime.UtcNow.AddMinutes(-5))
                    .ToList();
                
                if (!recentChecks.Any())
                    return InstanceHealth.Unknown;
                
                var healthyCount = recentChecks.Count(hc => hc.IsHealthy);
                var healthyRatio = (double)healthyCount / recentChecks.Count;
                
                // Determine health based on recent ratio
                if (healthyRatio >= 0.8)
                    return InstanceHealth.Healthy;
                else if (healthyRatio >= 0.5)
                    return InstanceHealth.Degraded;
                else
                    return InstanceHealth.Unhealthy;
            }
        }

        private async Task CheckAlertThresholdsAsync(PipelineInstance instance, HealthCheckResult result)
        {
            // This would integrate with alerting systems
            // For now, we'll just log critical issues
            
            if (!result.IsHealthy && result.Details.ContainsKey("high_cpu"))
            {
                _logger.LogWarning("ALERT: Instance {InstanceId} has high CPU usage: {CpuUsage}%",
                    instance.Id, result.Details["high_cpu"]);
            }
            
            if (!result.IsHealthy && result.Details.ContainsKey("high_memory"))
            {
                _logger.LogWarning("ALERT: Instance {InstanceId} has high memory usage: {MemoryUsage}%",
                    instance.Id, result.Details["high_memory"]);
            }
            
            if (!result.IsHealthy && result.Details.ContainsKey("high_error_rate"))
            {
                _logger.LogWarning("ALERT: Instance {InstanceId} has high error rate: {ErrorRate}",
                    instance.Id, result.Details["high_error_rate"]);
            }
            
            var history = _healthHistory[instance.Id];
            if (history.ConsecutiveFailures >= _options.HealthMonitoring.ConsecutiveFailureThreshold)
            {
                _logger.LogError("ALERT: Instance {InstanceId} has {ConsecutiveFailures} consecutive failures",
                    instance.Id, history.ConsecutiveFailures);
            }
        }

        private void CollectMetrics(object state)
        {
            try
            {
                // This method would be called periodically to collect and aggregate metrics
                // For now, it's a placeholder for metric collection logic
                
                var activeInstances = _instanceManager.GetInstancesAsync().Result
                    .Where(i => i.Status == InstanceStatus.Running)
                    .Count();
                
                _logger.LogDebug("Health monitoring metrics - Active instances: {ActiveInstances}, " +
                    "Monitored instances: {MonitoredInstances}",
                    activeInstances, _healthHistory.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting health monitoring metrics");
            }
        }

        public override void Dispose()
        {
            _metricsTimer?.Dispose();
            _httpClient?.Dispose();
            base.Dispose();
        }
    }

    internal class InstanceHealthHistory
    {
        public string InstanceId { get; set; } = string.Empty;
        public List<HealthCheckResult> HealthChecks { get; set; } = new();
        public int ConsecutiveFailures { get; set; }
        public int ConsecutiveSuccesses { get; set; }
    }

    internal class HealthCheckResult
    {
        public string InstanceId { get; set; } = string.Empty;
        public DateTime CheckTimestamp { get; set; }
        public bool IsHealthy { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public int? HttpStatusCode { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
    }
}
