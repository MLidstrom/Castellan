namespace Castellan.Worker.Models;

/// <summary>
/// Configuration options for IP enrichment services
/// </summary>
public class IPEnrichmentOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "IPEnrichment";

    /// <summary>
    /// Whether IP enrichment is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Provider to use for IP enrichment ("MaxMind", "IPInfo", "Disabled")
    /// </summary>
    public string Provider { get; set; } = "MaxMind";

    /// <summary>
    /// Path to MaxMind GeoLite2 City database file
    /// </summary>
    public string? MaxMindCityDbPath { get; set; }

    /// <summary>
    /// Path to MaxMind GeoLite2 ASN database file
    /// </summary>
    public string? MaxMindASNDbPath { get; set; }

    /// <summary>
    /// Path to MaxMind GeoLite2 Country database file
    /// </summary>
    public string? MaxMindCountryDbPath { get; set; }

    /// <summary>
    /// API key for IPInfo service (if using IPInfo provider)
    /// </summary>
    public string? IPInfoApiKey { get; set; }

    /// <summary>
    /// Cache enrichment results for this many minutes (0 = disabled)
    /// </summary>
    public int CacheMinutes { get; set; } = 60;

    /// <summary>
    /// Maximum number of entries in the enrichment cache
    /// </summary>
    public int MaxCacheEntries { get; set; } = 10000;

    /// <summary>
    /// Timeout for external API calls in milliseconds
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Whether to enrich private IP addresses (usually not useful)
    /// </summary>
    public bool EnrichPrivateIPs { get; set; } = false;

    /// <summary>
    /// Countries considered high-risk for security purposes
    /// </summary>
    public List<string> HighRiskCountries { get; set; } = new()
    {
        // Common high-risk countries for security monitoring
        // This is configurable and should be adjusted per organization
        "CN", "RU", "KP", "IR", "SY"
    };

    /// <summary>
    /// ASNs considered high-risk (known hosting providers used by attackers)
    /// </summary>
    public List<int> HighRiskASNs { get; set; } = new();

    /// <summary>
    /// Enable debug logging for IP enrichment
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
}
