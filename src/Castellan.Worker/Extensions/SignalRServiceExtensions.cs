using Microsoft.Extensions.DependencyInjection;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Hubs;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for SignalR and real-time services
/// </summary>
public static class SignalRServiceExtensions
{
    /// <summary>
    /// Adds SignalR services for real-time updates and dashboard broadcasting
    /// </summary>
    public static IServiceCollection AddCastellanSignalR(this IServiceCollection services)
    {
        // Add SignalR
        services.AddSignalR();

        // Add SignalR-related services
        services.AddSingleton<IScanProgressBroadcaster, ScanProgressBroadcaster>();
        services.AddSingleton<IEnhancedProgressTrackingService, EnhancedProgressTrackingService>();

        // Add dashboard data consolidation services
        services.AddScoped<IDashboardDataConsolidationService, DashboardDataConsolidationService>();
        services.AddSingleton<DashboardDataBroadcastService>();
        services.AddHostedService<DashboardDataBroadcastService>(provider =>
            provider.GetRequiredService<DashboardDataBroadcastService>());

        // Add system metrics background service for SignalR broadcasting
        services.AddHostedService<SystemMetricsBackgroundService>();

        return services;
    }
}
