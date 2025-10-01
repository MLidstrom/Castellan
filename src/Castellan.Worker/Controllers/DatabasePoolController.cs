using Castellan.Worker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/database-pool")]
[Authorize]
public class DatabasePoolController : ControllerBase
{
    private readonly DatabaseConnectionPoolManager _poolManager;
    private readonly ILogger<DatabasePoolController> _logger;

    public DatabasePoolController(
        DatabaseConnectionPoolManager poolManager,
        ILogger<DatabasePoolController> logger)
    {
        _poolManager = poolManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current health status of the database connection pool
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var isHealthy = await _poolManager.PerformHealthCheckAsync();

        return Ok(new
        {
            healthy = isHealthy,
            timestamp = DateTimeOffset.UtcNow,
            status = isHealthy ? "healthy" : "unhealthy"
        });
    }

    /// <summary>
    /// Gets detailed metrics about the database connection pool
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        var metrics = _poolManager.GetMetrics();

        return Ok(new { data = metrics });
    }

    /// <summary>
    /// Gets detailed information about current database connections
    /// </summary>
    [HttpGet("connections")]
    public IActionResult GetConnectionDetails()
    {
        var metrics = _poolManager.GetMetrics();

        return Ok(new
        {
            data = new
            {
                active = metrics.ActiveConnections,
                idle = metrics.IdleConnections,
                total = metrics.TotalConnections,
                maxPoolSize = metrics.MaxPoolSize,
                utilizationPercent = metrics.PoolUtilizationPercent,
                provider = metrics.DatabaseProvider
            }
        });
    }

    /// <summary>
    /// Forces a health check of the database connection pool (Admin only)
    /// </summary>
    [HttpPost("health-check")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ForceHealthCheck()
    {
        _logger.LogInformation("Manual health check requested");

        var isHealthy = await _poolManager.PerformHealthCheckAsync();

        return Ok(new
        {
            success = true,
            healthy = isHealthy,
            message = isHealthy
                ? "Database connection pool is healthy"
                : "Database connection pool is unhealthy"
        });
    }
}