using Castellan.Pipeline.Services.ConnectionPools.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Castellan.Pipeline.Services.ConnectionPools;

/// <summary>
/// Health check for HTTP client pools.
/// </summary>
public sealed class HttpClientPoolHealthCheck : IHealthCheck
{
    private readonly IHttpClientPoolManager _httpClientPoolManager;

    public HttpClientPoolHealthCheck(IHttpClientPoolManager httpClientPoolManager)
    {
        _httpClientPoolManager = httpClientPoolManager ?? throw new ArgumentNullException(nameof(httpClientPoolManager));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthStatus = await _httpClientPoolManager.GetHealthStatusAsync();
            var metrics = _httpClientPoolManager.GetMetrics();

            var unhealthyPools = healthStatus
                .Where(kvp => !kvp.Value.IsHealthy)
                .ToList();

            var healthyPoolCount = healthStatus.Count - unhealthyPools.Count;
            var totalPools = healthStatus.Count;

            var data = new Dictionary<string, object>
            {
                ["TotalPools"] = totalPools,
                ["HealthyPools"] = healthyPoolCount,
                ["UnhealthyPools"] = unhealthyPools.Count,
                ["TotalConnections"] = metrics.TotalConnections,
                ["ActiveConnections"] = metrics.ActiveConnections,
                ["LastUpdated"] = metrics.LastUpdated
            };

            // Add individual pool status
            foreach (var poolHealth in healthStatus)
            {
                data[$"Pool_{poolHealth.Key}_IsHealthy"] = poolHealth.Value.IsHealthy;
                data[$"Pool_{poolHealth.Key}_Status"] = poolHealth.Value.Status;
                data[$"Pool_{poolHealth.Key}_ResponseTime"] = poolHealth.Value.ResponseTime ?? 0;
            }

            // Determine overall health
            if (totalPools == 0)
            {
                return HealthCheckResult.Unhealthy(
                    "No HTTP client pools configured",
                    data: data);
            }

            if (unhealthyPools.Count == totalPools)
            {
                var unhealthyMessages = string.Join(", ", 
                    unhealthyPools.Select(p => $"{p.Key}: {p.Value.Status}"));

                return HealthCheckResult.Unhealthy(
                    $"All HTTP client pools are unhealthy: {unhealthyMessages}",
                    data: data);
            }

            if (unhealthyPools.Count > 0)
            {
                var unhealthyMessages = string.Join(", ", 
                    unhealthyPools.Select(p => $"{p.Key}: {p.Value.Status}"));

                return HealthCheckResult.Degraded(
                    $"Some HTTP client pools are unhealthy ({unhealthyPools.Count}/{totalPools}): {unhealthyMessages}",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"All HTTP client pools are healthy ({healthyPoolCount}/{totalPools})",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"HTTP client pool health check failed: {ex.Message}",
                ex);
        }
    }
}

/// <summary>
/// Health check for Qdrant connection pools.
/// </summary>
public sealed class QdrantConnectionPoolHealthCheck : IHealthCheck
{
    private readonly IQdrantConnectionPool _qdrantConnectionPool;

    public QdrantConnectionPoolHealthCheck(IQdrantConnectionPool qdrantConnectionPool)
    {
        _qdrantConnectionPool = qdrantConnectionPool ?? throw new ArgumentNullException(nameof(qdrantConnectionPool));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthStatus = await _qdrantConnectionPool.GetHealthStatusAsync();
            var metrics = _qdrantConnectionPool.GetMetrics();

            var unhealthyInstances = healthStatus
                .Where(kvp => !kvp.Value.IsHealthy)
                .ToList();

            var healthyInstanceCount = healthStatus.Count - unhealthyInstances.Count;
            var totalInstances = healthStatus.Count;

            var data = new Dictionary<string, object>
            {
                ["TotalInstances"] = totalInstances,
                ["HealthyInstances"] = healthyInstanceCount,
                ["UnhealthyInstances"] = unhealthyInstances.Count,
                ["TotalConnections"] = metrics.TotalConnections,
                ["ActiveConnections"] = metrics.ActiveConnections,
                ["LastUpdated"] = metrics.LastUpdated
            };

            // Add individual instance status
            foreach (var instanceHealth in healthStatus)
            {
                data[$"Instance_{instanceHealth.Key}_IsHealthy"] = instanceHealth.Value.IsHealthy;
                data[$"Instance_{instanceHealth.Key}_Status"] = instanceHealth.Value.Status;
                data[$"Instance_{instanceHealth.Key}_ResponseTime"] = instanceHealth.Value.ResponseTime ?? 0;
            }

            // Add instance-specific metrics
            foreach (var instanceMetrics in metrics.InstanceMetrics)
            {
                var instanceId = instanceMetrics.Key;
                var instanceData = instanceMetrics.Value;
                
                data[$"Instance_{instanceId}_ActiveConnections"] = instanceData.ActiveConnections;
                data[$"Instance_{instanceId}_TotalConnections"] = instanceData.TotalConnections;
                data[$"Instance_{instanceId}_AvailableConnections"] = instanceData.AvailableConnections;
                data[$"Instance_{instanceId}_MaxPoolSize"] = instanceData.MaxPoolSize;
            }

            // Determine overall health
            if (totalInstances == 0)
            {
                return HealthCheckResult.Unhealthy(
                    "No Qdrant instances configured",
                    data: data);
            }

            if (unhealthyInstances.Count == totalInstances)
            {
                var unhealthyMessages = string.Join(", ", 
                    unhealthyInstances.Select(i => $"{i.Key}: {i.Value.Status}"));

                return HealthCheckResult.Unhealthy(
                    $"All Qdrant instances are unhealthy: {unhealthyMessages}",
                    data: data);
            }

            if (unhealthyInstances.Count > 0)
            {
                var unhealthyMessages = string.Join(", ", 
                    unhealthyInstances.Select(i => $"{i.Key}: {i.Value.Status}"));

                return HealthCheckResult.Degraded(
                    $"Some Qdrant instances are unhealthy ({unhealthyInstances.Count}/{totalInstances}): {unhealthyMessages}",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"All Qdrant instances are healthy ({healthyInstanceCount}/{totalInstances})",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Qdrant connection pool health check failed: {ex.Message}",
                ex);
        }
    }
}
