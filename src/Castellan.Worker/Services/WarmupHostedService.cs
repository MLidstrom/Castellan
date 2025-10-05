using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using System.Diagnostics;

namespace Castellan.Worker.Services;

/// <summary>
/// Hosted service that warms critical paths on application startup to reduce cold-start latency.
/// Primes EF Core pool, API endpoints, and optionally SignalR/Qdrant connections.
/// </summary>
public class WarmupHostedService : IHostedService
{
    private readonly ILogger<WarmupHostedService> _logger;
    private readonly WarmupOptions _options;
    private readonly IDbContextFactory<CastellanDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;

    public WarmupHostedService(
        ILogger<WarmupHostedService> logger,
        IOptions<WarmupOptions> options,
        IDbContextFactory<CastellanDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Warmup is disabled via configuration");
            return;
        }

        _logger.LogInformation("Starting warmup sequence after {InitialDelay}s delay", _options.InitialDelaySeconds);

        try
        {
            // Wait initial delay before starting warmup
            await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), cancellationToken);

            var sw = Stopwatch.StartNew();

            // Create linked timeout cancellation token
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var token = linkedCts.Token;

            _logger.LogInformation("Beginning warmup sequence (timeout: {Timeout}s)", _options.TimeoutSeconds);

            // 1. Prime EF Core connection pool
            await PrimeDbContextPoolAsync(token);

            // 2. Warm configured endpoints
            await WarmEndpointsAsync(token);

            // 3. Warm SignalR (optional)
            if (_options.SignalR?.Enabled == true)
            {
                await WarmSignalRAsync(token);
            }

            // 4. Warm Qdrant (optional)
            if (_options.Qdrant?.Enabled == true)
            {
                await WarmQdrantAsync(token);
            }

            sw.Stop();
            _logger.LogInformation("Warmup completed successfully in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Warmup cancelled or timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Warmup failed with exception");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task PrimeDbContextPoolAsync(CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Priming EF Core connection pool");

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(token);

            // Execute a minimal query to prime the pool and JIT compile queries
            _ = await dbContext.SecurityEvents
                .AsNoTracking()
                .Take(1)
                .ToListAsync(token);

            sw.Stop();
            _logger.LogInformation("EF Core pool primed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Failed to prime EF Core pool after {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
    }

    private async Task WarmEndpointsAsync(CancellationToken token)
    {
        if (_options.WarmEndpoints == null)
        {
            return;
        }

        var client = _httpClientFactory.CreateClient("WarmupClient");
        var baseUrl = "http://localhost:5000/api";

        // Warm system status
        if (_options.WarmEndpoints.SystemStatus)
        {
            await TryGetAsync(client, $"{baseUrl}/system-status", "SystemStatus", token);
        }

        // Warm consolidated dashboard
        if (_options.WarmEndpoints.DashboardConsolidated)
        {
            await TryGetAsync(client, $"{baseUrl}/dashboarddata/consolidated?timeRange=24h", "DashboardConsolidated", token);
        }

        // Warm database pool health
        if (_options.WarmEndpoints.DatabasePool)
        {
            await TryGetAsync(client, $"{baseUrl}/database-pool/health", "DatabasePoolHealth", token);
            await TryGetAsync(client, $"{baseUrl}/database-pool/metrics", "DatabasePoolMetrics", token);
        }

        // Warm security event rules
        if (_options.WarmEndpoints.SecurityEventRules)
        {
            await TryGetAsync(client, $"{baseUrl}/security-event-rules?enabled=true", "SecurityEventRules", token);
        }

        // Warm YARA summary
        if (_options.WarmEndpoints.YaraSummary)
        {
            await TryGetAsync(client, $"{baseUrl}/yara/summary", "YaraSummary", token);
        }

        // Warm threat scanner progress (optional)
        if (_options.WarmEndpoints.ThreatScannerProgress)
        {
            await TryGetAsync(client, $"{baseUrl}/threat-scanner/progress", "ThreatScannerProgress", token);
        }
    }

    private async Task TryGetAsync(HttpClient client, string url, string endpointName, CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Warming endpoint: {EndpointName} ({Url})", endpointName, url);

            var response = await client.GetAsync(url, token);

            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Warmed {EndpointName} in {ElapsedMs}ms (status: {StatusCode})",
                    endpointName, sw.ElapsedMilliseconds, (int)response.StatusCode);
            }
            else
            {
                _logger.LogWarning("Failed to warm {EndpointName} after {ElapsedMs}ms (status: {StatusCode})",
                    endpointName, sw.ElapsedMilliseconds, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Exception warming {EndpointName} after {ElapsedMs}ms", endpointName, sw.ElapsedMilliseconds);
        }
    }

    private Task WarmSignalRAsync(CancellationToken token)
    {
        // TODO: Implement SignalR warmup
        // This would connect to the SignalR hub briefly to warm the connection
        _logger.LogDebug("SignalR warmup not yet implemented");
        return Task.CompletedTask;
    }

    private Task WarmQdrantAsync(CancellationToken token)
    {
        // TODO: Implement Qdrant warmup
        // This would execute a minimal vector search to warm the Qdrant connection
        _logger.LogDebug("Qdrant warmup not yet implemented");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for the warmup service
/// </summary>
public class WarmupOptions
{
    public bool Enabled { get; set; } = true;
    public int InitialDelaySeconds { get; set; } = 15;
    public int TimeoutSeconds { get; set; } = 5;
    public WarmEndpointsOptions? WarmEndpoints { get; set; }
    public SignalRWarmupOptions? SignalR { get; set; }
    public QdrantWarmupOptions? Qdrant { get; set; }
}

public class WarmEndpointsOptions
{
    public bool SystemStatus { get; set; } = true;
    public bool DashboardConsolidated { get; set; } = true;
    public bool DatabasePool { get; set; } = true;
    public bool SecurityEventRules { get; set; } = true;
    public bool YaraSummary { get; set; } = true;
    public bool ThreatScannerProgress { get; set; } = false;
}

public class SignalRWarmupOptions
{
    public bool Enabled { get; set; } = false;
    public bool DashboardGroup { get; set; } = true;
}

public class QdrantWarmupOptions
{
    public bool Enabled { get; set; } = false;
    public string Collection { get; set; } = "windows-events";
    public int K { get; set; } = 1;
}
