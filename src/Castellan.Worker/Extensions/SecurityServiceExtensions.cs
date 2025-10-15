using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Hubs;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;
using Castellan.Worker.Configuration;
using Castellan.Worker.Options;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for security services
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// Adds security services including threat detection, YARA scanning, and correlation
    /// </summary>
    public static IServiceCollection AddCastellanSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure security options
        services.Configure<AlertOptions>(configuration.GetSection("Alerts"));
        services.Configure<Castellan.Worker.Configuration.CorrelationOptions>(
            configuration.GetSection("Correlation"));
        services.Configure<ThreatScanOptions>(configuration.GetSection("ThreatScan"));
        services.Configure<MalwareScanningOptions>(
            configuration.GetSection(MalwareScanningOptions.SectionName));

        // Register security event rule store
        services.AddSingleton<ISecurityEventRuleStore, SecurityEventRuleStore>();

        // Register core security services
        services.AddSingleton<SecurityEventDetector>();
        services.AddSingleton<RulesEngine>();
        services.AddSingleton<IAutomatedResponseService, AutomatedResponseService>();

        // Register security event store with SignalR broadcasting
        services.AddScoped<DatabaseSecurityEventStore>();
        services.AddScoped<ISecurityEventStore>(provider =>
        {
            var baseStore = provider.GetRequiredService<DatabaseSecurityEventStore>();
            var broadcaster = provider.GetRequiredService<IScanProgressBroadcaster>();
            var logger = provider.GetRequiredService<ILogger<SignalRSecurityEventStore>>();
            return new SignalRSecurityEventStore(baseStore, broadcaster, logger);
        });

        // Register correlation engine
        services.AddSingleton<ICorrelationEngine, CorrelationEngine>();

        // Register YARA scanning services
        services.AddSingleton<DatabaseMalwareRuleStore>();
        services.AddSingleton<IMalwareRuleStore>(sp =>
            sp.GetRequiredService<DatabaseMalwareRuleStore>());
        services.AddSingleton<IMalwareScanService, MalwareScanService>();
        services.AddHostedService<MalwareScanService>(provider =>
            (MalwareScanService)provider.GetRequiredService<IMalwareScanService>());

        // Register threat scanning services
        services.AddScoped<IThreatScanner, ThreatScannerService>();
        services.AddScoped<IThreatScanHistoryRepository, ThreatScanHistoryRepository>();
        services.AddSingleton<IThreatScanConfigurationService, ThreatScanConfigurationService>();
        services.AddSingleton<IThreatScanProgressStore, ThreatScanProgressStore>();

        return services;
    }
}
