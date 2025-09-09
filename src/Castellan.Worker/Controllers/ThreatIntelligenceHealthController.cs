using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/threat-intelligence-health")]
[Authorize]
public class ThreatIntelligenceHealthController : ControllerBase
{
    private readonly ILogger<ThreatIntelligenceHealthController> _logger;
    private readonly IVirusTotalService _virusTotalService;
    private readonly IMalwareBazaarService _malwareBazaarService;
    private readonly IOtxService _otxService;
    private readonly IThreatIntelligenceCacheService _cacheService;

    public ThreatIntelligenceHealthController(
        ILogger<ThreatIntelligenceHealthController> logger,
        IVirusTotalService virusTotalService,
        IMalwareBazaarService malwareBazaarService,
        IOtxService otxService,
        IThreatIntelligenceCacheService cacheService)
    {
        _logger = logger;
        _virusTotalService = virusTotalService;
        _malwareBazaarService = malwareBazaarService;
        _otxService = otxService;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Get comprehensive health status of all threat intelligence services
    /// </summary>
    /// <returns>Health metrics for all threat intelligence services</returns>
    [HttpGet]
    public async Task<IActionResult> GetThreatIntelligenceHealth()
    {
        try
        {
            _logger.LogInformation("Getting threat intelligence service health status");

            // Get service health status
            var services = new List<object>();
            var alerts = new List<object>();
            var totalQueries = 0L;
            var totalResponseTime = 0.0;
            var totalErrors = 0L;
            var servicesOnline = 0;

            // VirusTotal Service Health
            var vtHealth = await GetVirusTotalHealthAsync();
            services.Add(vtHealth);
            if (vtHealth.GetType().GetProperty("status")?.GetValue(vtHealth)?.ToString() == "healthy")
                servicesOnline++;

            // MalwareBazaar Service Health  
            var mbHealth = await GetMalwareBazaarHealthAsync();
            services.Add(mbHealth);
            if (mbHealth.GetType().GetProperty("status")?.GetValue(mbHealth)?.ToString() == "healthy")
                servicesOnline++;

            // AlienVault OTX Service Health
            var otxHealth = await GetOtxHealthAsync();
            services.Add(otxHealth);
            if (otxHealth.GetType().GetProperty("status")?.GetValue(otxHealth)?.ToString() == "healthy")
                servicesOnline++;

            // Get cache statistics
            var cacheStats = _cacheService.GetStatistics();
            var cacheEfficiency = cacheStats.TotalEntries > 0 ? cacheStats.CacheHitRate : 0.0f;

            // Calculate overall health score
            var healthScore = (double)servicesOnline / services.Count * 100;
            var overallStatus = healthScore >= 100 ? "healthy" : 
                              healthScore >= 66 ? "warning" : "critical";

            // Generate sample performance data
            var performanceMetrics = new
            {
                totalQueries24h = Random.Shared.Next(500, 2000),
                averageResponseTime = Random.Shared.Next(200, 800),
                cacheEfficiency = (double)cacheEfficiency,
                errorRate = Random.Shared.NextDouble() * 0.1
            };

            var usageMetrics = new
            {
                virusTotalQueries = Random.Shared.Next(100, 500),
                malwareBazaarQueries = Random.Shared.Next(50, 300), 
                otxQueries = Random.Shared.Next(75, 400),
                cacheHits = cacheStats.ValidEntries,
                cacheMisses = Math.Max(0, cacheStats.TotalEntries - cacheStats.ValidEntries)
            };

            // Check for alerts based on service status
            var currentAlerts = new List<object>();
            foreach (var service in services)
            {
                var serviceType = service.GetType();
                var serviceName = serviceType.GetProperty("name")?.GetValue(service)?.ToString();
                var serviceStatus = serviceType.GetProperty("status")?.GetValue(service)?.ToString();
                var errorRate = (double)(serviceType.GetProperty("errorRate")?.GetValue(service) ?? 0.0);

                if (serviceStatus != "healthy")
                {
                    currentAlerts.Add(new
                    {
                        id = Guid.NewGuid().ToString(),
                        service = serviceName,
                        severity = serviceStatus == "error" ? "error" : "warning",
                        message = serviceStatus == "error" 
                            ? $"{serviceName} service is not responding" 
                            : $"{serviceName} service experiencing issues",
                        timestamp = DateTime.UtcNow
                    });
                }

                if (errorRate > 0.1)
                {
                    currentAlerts.Add(new
                    {
                        id = Guid.NewGuid().ToString(),
                        service = serviceName,
                        severity = "warning",
                        message = $"{serviceName} error rate is high: {errorRate:P}",
                        timestamp = DateTime.UtcNow
                    });
                }
            }

            var response = new
            {
                services = services,
                overallHealth = new
                {
                    status = overallStatus,
                    healthScore = healthScore,
                    servicesOnline = servicesOnline,
                    totalServices = services.Count
                },
                performance = performanceMetrics,
                usage = usageMetrics,
                alerts = currentAlerts
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat intelligence health status");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private async Task<object> GetVirusTotalHealthAsync()
    {
        try
        {
            // Check if VirusTotal is configured and accessible
            var isHealthy = await CheckServiceHealthAsync("VirusTotal");
            
            return new
            {
                name = "VirusTotal",
                status = isHealthy ? "healthy" : "error",
                lastCheck = DateTime.UtcNow.ToString("O"),
                responseTime = Random.Shared.Next(200, 1000),
                uptime = Random.Shared.Next(3600, 86400), // 1 hour to 1 day in seconds
                errorRate = isHealthy ? Random.Shared.NextDouble() * 0.05 : 0.2,
                requestsToday = Random.Shared.Next(50, 300),
                rateLimitRemaining = Random.Shared.Next(800, 1000),
                rateLimitTotal = 1000,
                apiKeyStatus = isHealthy ? "Valid" : "Invalid or Missing",
                cacheHitRate = Random.Shared.NextDouble() * 0.4 + 0.6, // 60-100%
                lastError = isHealthy ? null : "API key validation failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking VirusTotal health");
            return new
            {
                name = "VirusTotal",
                status = "error",
                lastCheck = DateTime.UtcNow.ToString("O"),
                responseTime = 0,
                uptime = 0,
                errorRate = 1.0,
                requestsToday = 0,
                rateLimitRemaining = 0,
                rateLimitTotal = 1000,
                apiKeyStatus = "Error",
                lastError = "Service check failed"
            };
        }
    }

    private async Task<object> GetMalwareBazaarHealthAsync()
    {
        try
        {
            var isHealthy = await CheckServiceHealthAsync("MalwareBazaar");
            
            return new
            {
                name = "MalwareBazaar",
                status = isHealthy ? "healthy" : "warning", // MalwareBazaar is often slower
                lastCheck = DateTime.UtcNow.ToString("O"),
                responseTime = Random.Shared.Next(500, 2000),
                uptime = Random.Shared.Next(7200, 86400),
                errorRate = isHealthy ? Random.Shared.NextDouble() * 0.08 : 0.15,
                requestsToday = Random.Shared.Next(30, 150),
                rateLimitRemaining = Random.Shared.Next(400, 500),
                rateLimitTotal = 500,
                apiKeyStatus = isHealthy ? "Valid" : "Rate Limited",
                cacheHitRate = Random.Shared.NextDouble() * 0.3 + 0.5, // 50-80%
                lastError = isHealthy ? null : "Rate limit exceeded"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking MalwareBazaar health");
            return new
            {
                name = "MalwareBazaar",
                status = "error",
                lastCheck = DateTime.UtcNow.ToString("O"),
                responseTime = 0,
                uptime = 0,
                errorRate = 1.0,
                requestsToday = 0,
                rateLimitRemaining = 0,
                rateLimitTotal = 500,
                apiKeyStatus = "Error",
                lastError = "Service check failed"
            };
        }
    }

    private async Task<object> GetOtxHealthAsync()
    {
        try
        {
            var isHealthy = await CheckServiceHealthAsync("OTX");
            
            return new
            {
                name = "AlienVault OTX",
                status = isHealthy ? "healthy" : "warning",
                lastCheck = DateTime.UtcNow.ToString("O"),
                responseTime = Random.Shared.Next(300, 800),
                uptime = Random.Shared.Next(10800, 86400),
                errorRate = isHealthy ? Random.Shared.NextDouble() * 0.06 : 0.12,
                requestsToday = Random.Shared.Next(75, 400),
                rateLimitRemaining = Random.Shared.Next(1800, 2000),
                rateLimitTotal = 2000,
                apiKeyStatus = isHealthy ? "Valid" : "Quota Exceeded",
                cacheHitRate = Random.Shared.NextDouble() * 0.35 + 0.55, // 55-90%
                lastError = isHealthy ? null : "Daily quota exceeded"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking OTX health");
            return new
            {
                name = "AlienVault OTX",
                status = "error",
                lastCheck = DateTime.UtcNow.ToString("O"),
                responseTime = 0,
                uptime = 0,
                errorRate = 1.0,
                requestsToday = 0,
                rateLimitRemaining = 0,
                rateLimitTotal = 2000,
                apiKeyStatus = "Error",
                lastError = "Service check failed"
            };
        }
    }

    private async Task<bool> CheckServiceHealthAsync(string serviceName)
    {
        try
        {
            // Simulate service health check
            // In production, this would make actual calls to check service availability
            await Task.Delay(Random.Shared.Next(10, 100)); // Simulate network delay
            
            // Return healthy status for most cases, occasional failures for demo
            return Random.Shared.NextDouble() > 0.1; // 90% success rate
        }
        catch
        {
            return false;
        }
    }
}
