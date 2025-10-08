using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Collectors;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Castellan.Worker.Options;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for pipeline services
/// </summary>
public static class PipelineServiceExtensions
{
    /// <summary>
    /// Adds pipeline services including event collection, processing, and export
    /// </summary>
    public static IServiceCollection AddCastellanPipeline(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure pipeline options
        services.Configure<EvtxOptions>(configuration.GetSection("Ingest:Evtx"));
        services.Configure<PipelineOptions>(configuration.GetSection("Pipeline"));
        services.Configure<AutomatedResponseOptions>(configuration.GetSection("AutomatedResponse"));
        services.Configure<WindowsEventLogOptions>(configuration.GetSection("WindowsEventLog"));
        services.Configure<IgnorePatternOptions>(configuration.GetSection("IgnorePatterns"));

        // Register event collectors
        services.AddSingleton<ILogCollector, EvtxCollector>();

        // Register pipeline services
        services.AddSingleton<EventIgnorePatternService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<IAdvancedSearchService, AdvancedSearchService>();
        services.AddScoped<ISavedSearchService, SavedSearchService>();
        services.AddScoped<ISearchHistoryService, SearchHistoryService>();

        // Register background services
        services.AddHostedService<Pipeline>();
        services.AddHostedService<StartupOrchestratorService>();
        services.AddHostedService<MitreImportStartupService>();
        services.AddHostedService<DailyRefreshHostedService>();
        services.AddHostedService<ScheduledThreatScanService>();
        services.AddHostedService<CorrelationBackgroundService>();
        services.AddHostedService<WindowsEventLogWatcherService>();

        return services;
    }
}
