namespace Castellan.Worker.Models;

/// <summary>
/// Represents the result of IP address enrichment with geographical and network information
/// </summary>
public class IPEnrichmentResult
{
    /// <summary>
    /// The IP address that was enriched
    /// </summary>
    public string IPAddress { get; init; } = string.Empty;

    /// <summary>
    /// Country name (e.g., "United States")
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// ISO country code (e.g., "US")
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>
    /// City name (e.g., "New York")
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// Latitude coordinate
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude coordinate
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// ASN (Autonomous System Number)
    /// </summary>
    public int? ASN { get; init; }

    /// <summary>
    /// ASN organization name (e.g., "Cloudflare, Inc.")
    /// </summary>
    public string? ASNOrganization { get; init; }

    /// <summary>
    /// Whether this IP is considered high-risk
    /// </summary>
    public bool IsHighRisk { get; init; }

    /// <summary>
    /// Risk factors identified for this IP
    /// </summary>
    public List<string> RiskFactors { get; init; } = new();

    /// <summary>
    /// Whether the IP is from a private/internal network
    /// </summary>
    public bool IsPrivate { get; init; }

    /// <summary>
    /// Whether enrichment was successful
    /// </summary>
    public bool IsEnriched { get; init; }

    /// <summary>
    /// Error message if enrichment failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful enrichment result
    /// </summary>
    public static IPEnrichmentResult Success(string ipAddress, string? country = null, string? countryCode = null, 
        string? city = null, double? latitude = null, double? longitude = null, int? asn = null, 
        string? asnOrganization = null, bool isHighRisk = false, List<string>? riskFactors = null, bool isPrivate = false)
    {
        return new IPEnrichmentResult
        {
            IPAddress = ipAddress,
            Country = country,
            CountryCode = countryCode,
            City = city,
            Latitude = latitude,
            Longitude = longitude,
            ASN = asn,
            ASNOrganization = asnOrganization,
            IsHighRisk = isHighRisk,
            RiskFactors = riskFactors ?? new List<string>(),
            IsPrivate = isPrivate,
            IsEnriched = true
        };
    }

    /// <summary>
    /// Creates a failed enrichment result
    /// </summary>
    public static IPEnrichmentResult Failed(string ipAddress, string error)
    {
        return new IPEnrichmentResult
        {
            IPAddress = ipAddress,
            IsEnriched = false,
            Error = error
        };
    }

    /// <summary>
    /// Creates a result for a private IP address
    /// </summary>
    public static IPEnrichmentResult Private(string ipAddress)
    {
        return new IPEnrichmentResult
        {
            IPAddress = ipAddress,
            IsPrivate = true,
            IsEnriched = true,
            Country = "Private Network",
            RiskFactors = new List<string>()
        };
    }
}
