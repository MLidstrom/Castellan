using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Service for enriching IP addresses with geographical and network information
/// </summary>
public interface IIPEnrichmentService
{
    /// <summary>
    /// Enriches an IP address with geographical and network information
    /// </summary>
    /// <param name="ipAddress">The IP address to enrich</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enrichment result containing geographical and network data</returns>
    Task<IPEnrichmentResult> EnrichAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enriches multiple IP addresses in batch
    /// </summary>
    /// <param name="ipAddresses">Collection of IP addresses to enrich</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping IP addresses to their enrichment results</returns>
    Task<Dictionary<string, IPEnrichmentResult>> EnrichBatchAsync(IEnumerable<string> ipAddresses, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the IP enrichment service is available and working
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the service is healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
