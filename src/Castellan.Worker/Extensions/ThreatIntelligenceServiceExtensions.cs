using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Models.ThreatIntelligence;
using Castellan.Worker.Services;
using Castellan.Worker.Services.Interfaces;
using Castellan.Worker.Configuration;
using Castellan.Worker.Options;

namespace Castellan.Worker.Extensions;

/// <summary>
/// Service collection extensions for threat intelligence services
/// </summary>
public static class ThreatIntelligenceServiceExtensions
{
    /// <summary>
    /// Adds threat intelligence services including VirusTotal, MalwareBazaar, and OTX
    /// </summary>
    public static IServiceCollection AddCastellanThreatIntelligence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure threat intelligence options
        services.Configure<ThreatIntelligenceOptions>(
            configuration.GetSection("ThreatIntelligence"));

        // Register threat intelligence cache service
        services.AddSingleton<IThreatIntelligenceCacheService, ThreatIntelligenceCacheService>();

        // Register VirusTotal service with HttpClient
        services.AddHttpClient<IVirusTotalService, VirusTotalService>();

        // Register MalwareBazaar service with HttpClient
        services.AddHttpClient<IMalwareBazaarService, MalwareBazaarService>();

        // Register AlienVault OTX service with HttpClient
        services.AddHttpClient<IOtxService, OtxService>();

        // Register IP enrichment service
        services.Configure<IPEnrichmentOptions>(configuration.GetSection("IPEnrichment"));
        services.AddMemoryCache(); // For IP enrichment caching

        var ipEnrichmentProvider = configuration["IPEnrichment:Provider"] ?? "MaxMind";
        if (ipEnrichmentProvider.Equals("MaxMind", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IIPEnrichmentService, MaxMindIPEnrichmentService>();
        else
            services.AddSingleton<IIPEnrichmentService, MaxMindIPEnrichmentService>(); // Default

        return services;
    }
}
