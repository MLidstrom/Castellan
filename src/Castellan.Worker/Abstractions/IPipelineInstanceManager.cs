using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for managing pipeline instances in a scaling architecture
/// </summary>
public interface IPipelineInstanceManager : IDisposable
{
    /// <summary>
    /// Register a new pipeline instance
    /// </summary>
    /// <param name="instance">Instance to register</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if registration was successful</returns>
    Task<bool> RegisterInstanceAsync(PipelineInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregister a pipeline instance
    /// </summary>
    /// <param name="instanceId">Instance ID to unregister</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if unregistration was successful</returns>
    Task<bool> UnregisterInstanceAsync(string instanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all registered pipeline instances
    /// </summary>
    /// <param name="healthyOnly">If true, return only healthy instances</param>
    /// <returns>Collection of pipeline instances</returns>
    IReadOnlyList<PipelineInstance> GetInstances(bool healthyOnly = true);

    /// <summary>
    /// Get a specific pipeline instance by ID
    /// </summary>
    /// <param name="instanceId">Instance ID to retrieve</param>
    /// <returns>Pipeline instance or null if not found</returns>
    PipelineInstance? GetInstance(string instanceId);

    /// <summary>
    /// Update instance health status
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <param name="healthResult">Health check result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateInstanceHealthAsync(string instanceId, HealthCheckResult healthResult, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update instance performance metrics
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <param name="metrics">Performance metrics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateInstanceMetricsAsync(string instanceId, InstancePerformanceMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get aggregated metrics for all instances
    /// </summary>
    /// <returns>Aggregated performance metrics</returns>
    AggregatedInstanceMetrics GetAggregatedMetrics();

    /// <summary>
    /// Send a command to a specific instance
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <param name="command">Command to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command response</returns>
    Task<InstanceCommandResponse> SendCommandAsync(string instanceId, InstanceCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast a command to all instances
    /// </summary>
    /// <param name="command">Command to broadcast</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of command responses</returns>
    Task<IReadOnlyList<InstanceCommandResponse>> BroadcastCommandAsync(InstanceCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when instance status changes
    /// </summary>
    event EventHandler<InstanceStatusChangedEventArgs>? InstanceStatusChanged;

    /// <summary>
    /// Event fired when instance is added
    /// </summary>
    event EventHandler<InstanceAddedEventArgs>? InstanceAdded;

    /// <summary>
    /// Event fired when instance is removed
    /// </summary>
    event EventHandler<InstanceRemovedEventArgs>? InstanceRemoved;

    /// <summary>
    /// Start health monitoring for all instances
    /// </summary>
    Task StartHealthMonitoringAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop health monitoring
    /// </summary>
    Task StopHealthMonitoringAsync();

    /// <summary>
    /// Get current health monitoring status
    /// </summary>
    bool IsHealthMonitoringActive { get; }
}
