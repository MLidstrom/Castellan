using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Castellan.Worker.Models;

namespace Castellan.Worker.Hubs;

/// <summary>
/// SignalR Hub for broadcasting real-time scan progress and system metrics to connected clients.
/// Provides live updates for threat scanning operations, system health, and performance metrics.
/// </summary>
public class ScanProgressHub : Hub
{
    private readonly ILogger<ScanProgressHub> _logger;

    public ScanProgressHub(ILogger<ScanProgressHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to ScanProgressHub: {ConnectionId}", Context.ConnectionId);
        
        // Check if user is authenticated (optional - allows anonymous for dashboard viewing)
        var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;
        _logger.LogInformation("Client {ConnectionId} authenticated: {IsAuthenticated}", Context.ConnectionId, isAuthenticated);
        
        // Add client to general progress updates group (allows anonymous viewers)
        await Groups.AddToGroupAsync(Context.ConnectionId, "ScanProgressUpdates");
        
        // Send initial connection confirmation
        await Clients.Caller.SendAsync("Connected", new { 
            message = "Connected to scan progress updates",
            connectionId = Context.ConnectionId,
            authenticated = isAuthenticated,
            timestamp = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from ScanProgressHub: {ConnectionId}", Context.ConnectionId);
        
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with exception: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a specific scan updates group for targeted updates
    /// </summary>
    /// <param name="scanId">The scan ID to subscribe to</param>
    public async Task JoinScanUpdates(string scanId)
    {
        var groupName = $"Scan_{scanId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Client {ConnectionId} joined scan updates for {ScanId}", Context.ConnectionId, scanId);
        
        await Clients.Caller.SendAsync("JoinedScanUpdates", new { 
            scanId,
            message = $"Subscribed to updates for scan {scanId}",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Leave a specific scan updates group
    /// </summary>
    /// <param name="scanId">The scan ID to unsubscribe from</param>
    public async Task LeaveScanUpdates(string scanId)
    {
        var groupName = $"Scan_{scanId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Client {ConnectionId} left scan updates for {ScanId}", Context.ConnectionId, scanId);
        
        await Clients.Caller.SendAsync("LeftScanUpdates", new { 
            scanId,
            message = $"Unsubscribed from updates for scan {scanId}",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Join system metrics updates group
    /// </summary>
    public async Task JoinSystemMetrics()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "SystemMetrics");
        
        _logger.LogInformation("Client {ConnectionId} joined system metrics updates", Context.ConnectionId);
        
        await Clients.Caller.SendAsync("JoinedSystemMetrics", new { 
            message = "Subscribed to system metrics updates",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Leave system metrics updates group
    /// </summary>
    public async Task LeaveSystemMetrics()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "SystemMetrics");
        
        _logger.LogInformation("Client {ConnectionId} left system metrics updates", Context.ConnectionId);
        
        await Clients.Caller.SendAsync("LeftSystemMetrics", new {
            message = "Unsubscribed from system metrics updates",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Join dashboard data updates group for consolidated dashboard data
    /// </summary>
    public async Task JoinDashboardUpdates()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "DashboardUpdates");

        _logger.LogInformation("Client {ConnectionId} joined dashboard data updates", Context.ConnectionId);

        await Clients.Caller.SendAsync("JoinedDashboardUpdates", new {
            message = "Subscribed to consolidated dashboard updates",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Leave dashboard data updates group
    /// </summary>
    public async Task LeaveDashboardUpdates()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "DashboardUpdates");

        _logger.LogInformation("Client {ConnectionId} left dashboard data updates", Context.ConnectionId);

        await Clients.Caller.SendAsync("LeftDashboardUpdates", new {
            message = "Unsubscribed from dashboard updates",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Request immediate dashboard data refresh for a specific time range
    /// </summary>
    /// <param name="timeRange">Time range for dashboard data (24h, 7d, 30d, 1h)</param>
    public async Task RequestDashboardData(string timeRange = "24h")
    {
        _logger.LogInformation("Client {ConnectionId} requested immediate dashboard data for time range: {TimeRange}",
            Context.ConnectionId, timeRange);

        // Note: The actual data fetching and sending will be handled by the DashboardDataBroadcastService
        // This method just logs the request and could trigger an immediate broadcast if needed
        await Clients.Caller.SendAsync("DashboardDataRequested", new {
            timeRange,
            message = $"Dashboard data requested for {timeRange}",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Join security events real-time updates group
    /// </summary>
    public async Task JoinSecurityEvents()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "SecurityEvents");

        _logger.LogInformation("Client {ConnectionId} joined security events real-time updates", Context.ConnectionId);

        await Clients.Caller.SendAsync("JoinedSecurityEvents", new {
            message = "Subscribed to real-time security events",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Leave security events real-time updates group
    /// </summary>
    public async Task LeaveSecurityEvents()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "SecurityEvents");

        _logger.LogInformation("Client {ConnectionId} left security events real-time updates", Context.ConnectionId);

        await Clients.Caller.SendAsync("LeftSecurityEvents", new {
            message = "Unsubscribed from security events",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Join correlation alerts group for real-time correlation notifications
    /// </summary>
    public async Task JoinCorrelationAlerts()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "CorrelationAlerts");

        _logger.LogInformation("Client {ConnectionId} joined correlation alerts", Context.ConnectionId);

        await Clients.Caller.SendAsync("JoinedCorrelationAlerts", new {
            message = "Subscribed to correlation alerts",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Leave correlation alerts group
    /// </summary>
    public async Task LeaveCorrelationAlerts()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "CorrelationAlerts");

        _logger.LogInformation("Client {ConnectionId} left correlation alerts", Context.ConnectionId);

        await Clients.Caller.SendAsync("LeftCorrelationAlerts", new {
            message = "Unsubscribed from correlation alerts",
            timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Service interface for broadcasting updates through SignalR hub
/// </summary>
public interface IScanProgressBroadcaster
{
    Task BroadcastScanProgress(ScanProgressUpdate update);
    Task BroadcastScanComplete(string scanId, object result);
    Task BroadcastScanError(string scanId, string error);
    Task BroadcastSystemMetrics(object metrics);
    Task BroadcastThreatIntelligenceStatus(object status);
    Task BroadcastSecurityEvent(object securityEvent);
    Task BroadcastCorrelationAlert(object correlationAlert);
    Task BroadcastMalwareMatch(object malwareMatch);
}

/// <summary>
/// Service for broadcasting scan progress and system metrics via SignalR
/// </summary>
public class ScanProgressBroadcaster : IScanProgressBroadcaster
{
    private readonly IHubContext<ScanProgressHub> _hubContext;
    private readonly ILogger<ScanProgressBroadcaster> _logger;

    public ScanProgressBroadcaster(IHubContext<ScanProgressHub> hubContext, ILogger<ScanProgressBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast scan progress updates to all subscribed clients
    /// </summary>
    /// <param name="update">The progress update to broadcast</param>
    public async Task BroadcastScanProgress(ScanProgressUpdate update)
    {
        try
        {
            var scanId = update.Progress.ScanId;
            
            // Send to general progress updates group
            await _hubContext.Clients.Group("ScanProgressUpdates").SendAsync("ScanProgressUpdate", update);
            
            // Send to specific scan subscribers
            if (!string.IsNullOrEmpty(scanId))
            {
                await _hubContext.Clients.Group($"Scan_{scanId}").SendAsync("ScanProgressUpdate", update);
            }

            _logger.LogDebug("Broadcasted scan progress update for {ScanId}: {PercentComplete}% complete", 
                scanId, update.Progress.PercentComplete);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting scan progress update");
        }
    }

    /// <summary>
    /// Broadcast scan completion notification
    /// </summary>
    /// <param name="scanId">The completed scan ID</param>
    /// <param name="result">The scan result</param>
    public async Task BroadcastScanComplete(string scanId, object result)
    {
        try
        {
            var notification = new
            {
                type = "scanComplete",
                scanId,
                result,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group("ScanProgressUpdates").SendAsync("ScanCompleted", notification);
            await _hubContext.Clients.Group($"Scan_{scanId}").SendAsync("ScanCompleted", notification);

            _logger.LogInformation("Broadcasted scan completion notification for {ScanId}", scanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting scan completion for {ScanId}", scanId);
        }
    }

    /// <summary>
    /// Broadcast scan error notification
    /// </summary>
    /// <param name="scanId">The failed scan ID</param>
    /// <param name="error">The error message</param>
    public async Task BroadcastScanError(string scanId, string error)
    {
        try
        {
            var notification = new
            {
                type = "scanError",
                scanId,
                error,
                timestamp = DateTime.UtcNow
            };

            await _hubContext.Clients.Group("ScanProgressUpdates").SendAsync("ScanError", notification);
            await _hubContext.Clients.Group($"Scan_{scanId}").SendAsync("ScanError", notification);

            _logger.LogWarning("Broadcasted scan error notification for {ScanId}: {Error}", scanId, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting scan error for {ScanId}", scanId);
        }
    }

    /// <summary>
    /// Broadcast system metrics updates
    /// </summary>
    /// <param name="metrics">The system metrics to broadcast</param>
    public async Task BroadcastSystemMetrics(object metrics)
    {
        try
        {
            await _hubContext.Clients.Group("SystemMetrics").SendAsync("SystemMetricsUpdate", metrics);

            _logger.LogDebug("Broadcasted system metrics update");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting system metrics");
        }
    }

    /// <summary>
    /// Broadcast threat intelligence service status updates
    /// </summary>
    /// <param name="status">The threat intelligence status to broadcast</param>
    public async Task BroadcastThreatIntelligenceStatus(object status)
    {
        try
        {
            await _hubContext.Clients.Group("SystemMetrics").SendAsync("ThreatIntelligenceStatus", status);

            _logger.LogDebug("Broadcasted threat intelligence status update");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting threat intelligence status");
        }
    }

    /// <summary>
    /// Broadcast new security event in real-time
    /// </summary>
    /// <param name="securityEvent">The security event to broadcast</param>
    public async Task BroadcastSecurityEvent(object securityEvent)
    {
        try
        {
            await _hubContext.Clients.Group("SecurityEvents").SendAsync("SecurityEventUpdate", securityEvent);

            _logger.LogDebug("Broadcasted security event update");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting security event");
        }
    }

    /// <summary>
    /// Broadcast correlation alert when patterns are detected
    /// </summary>
    /// <param name="correlationAlert">The correlation alert to broadcast</param>
    public async Task BroadcastCorrelationAlert(object correlationAlert)
    {
        try
        {
            await _hubContext.Clients.Group("CorrelationAlerts").SendAsync("CorrelationAlert", correlationAlert);

            // Also send to security events subscribers for critical correlations
            await _hubContext.Clients.Group("SecurityEvents").SendAsync("CorrelationAlert", correlationAlert);

            _logger.LogInformation("Broadcasted correlation alert");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting correlation alert");
        }
    }

    /// <summary>
    /// Broadcast YARA match detection in real-time
    /// </summary>
    /// <param name="malwareMatch">The YARA match to broadcast</param>
    public async Task BroadcastMalwareMatch(object malwareMatch)
    {
        try
        {
            await _hubContext.Clients.Group("SecurityEvents").SendAsync("MalwareMatchDetected", malwareMatch);

            _logger.LogInformation("Broadcasted YARA match detection");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting YARA match");
        }
    }
}
