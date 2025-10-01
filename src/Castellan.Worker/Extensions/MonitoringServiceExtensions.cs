using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;
using Castellan.Worker.Options;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for monitoring and performance services
/// </summary>
public static class MonitoringServiceExtensions
{
    /// <summary>
    /// Adds monitoring services including performance tracking, analytics, and system health
    /// </summary>
    public static IServiceCollection AddCastellanMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure monitoring options
        services.Configure<PerformanceMonitorOptions>(
            configuration.GetSection("PerformanceMonitoring"));

        // Register performance monitoring services
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitorService>();
        services.AddScoped<PerformanceMetricsService>();
        services.AddScoped<PerformanceAlertService>();
        services.AddSingleton<AnalyticsService>();

        // Register system health service
        services.AddSingleton<SystemHealthService>();

        return services;
    }
}
