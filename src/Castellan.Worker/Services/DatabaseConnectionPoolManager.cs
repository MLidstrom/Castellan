using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

public class DatabaseConnectionPoolManager : IDisposable
{
    private readonly IDbContextFactory<CastellanDbContext> _contextFactory;
    private readonly DatabaseConnectionPoolOptions _options;
    private readonly ILogger<DatabaseConnectionPoolManager> _logger;
    private readonly DatabaseConnectionPoolMetrics _metrics;
    private readonly System.Threading.Timer? _healthCheckTimer;
    private bool _disposed;

    public DatabaseConnectionPoolManager(
        IDbContextFactory<CastellanDbContext> contextFactory,
        IOptions<DatabaseConnectionPoolOptions> options,
        ILogger<DatabaseConnectionPoolManager> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = new DatabaseConnectionPoolMetrics
        {
            MaxPoolSize = _options.MaxPoolSize,
            DatabaseProvider = _options.Provider
        };

        if (_options.HealthCheck.Enabled)
        {
            _healthCheckTimer = new System.Threading.Timer(
                PerformHealthCheck,
                null,
                TimeSpan.Zero,
                _options.HealthCheck.Interval);
        }

        _logger.LogInformation(
            "DatabaseConnectionPoolManager initialized for {Provider} with MaxPoolSize: {MaxPoolSize}",
            _options.Provider, _options.MaxPoolSize);
    }

    public DatabaseConnectionPoolMetrics GetMetrics() => _metrics;

    public async Task<bool> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Database-agnostic health check query
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);

            _metrics.LastHealthCheck = DateTimeOffset.UtcNow;
            _metrics.HealthStatus = canConnect
                ? ConnectionPoolHealthStatus.Healthy
                : ConnectionPoolHealthStatus.Unhealthy;

            if (!canConnect)
            {
                _logger.LogWarning("Database health check failed - cannot connect");
            }

            return canConnect;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            _metrics.HealthStatus = ConnectionPoolHealthStatus.Unhealthy;
            _metrics.FailedConnectionAttempts++;
            return false;
        }
    }

    private async void PerformHealthCheck(object? state)
    {
        if (_disposed) return;
        await PerformHealthCheckAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _healthCheckTimer?.Dispose();
        _logger.LogInformation("DatabaseConnectionPoolManager disposed");
        GC.SuppressFinalize(this);
    }
}