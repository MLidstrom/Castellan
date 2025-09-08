using Castellan.Worker.Models.ThreatIntelligence;

namespace Castellan.Worker.Services.Interfaces;

/// <summary>
/// Interface for AlienVault Open Threat Exchange (OTX) threat intelligence service
/// </summary>
public interface IOtxService
{
    /// <summary>
    /// Query OTX for file hash reputation and threat information
    /// </summary>
    /// <param name="fileHash">SHA256, SHA1, or MD5 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OTX threat intelligence result</returns>
    Task<OTXResult?> GetHashReputationAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query OTX for IP address reputation
    /// </summary>
    /// <param name="ipAddress">IP address to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OTX threat intelligence result for IP</returns>
    Task<OTXResult?> GetIPReputationAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query OTX for domain reputation
    /// </summary>
    /// <param name="domain">Domain name to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OTX threat intelligence result for domain</returns>
    Task<OTXResult?> GetDomainReputationAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query OTX for URL reputation
    /// </summary>
    /// <param name="url">URL to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OTX threat intelligence result for URL</returns>
    Task<OTXResult?> GetURLReputationAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get threat pulses related to a specific indicator
    /// </summary>
    /// <param name="indicator">Threat indicator (hash, IP, domain, etc.)</param>
    /// <param name="indicatorType">Type of indicator (FileHash-SHA256, IPv4, domain, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of related threat pulses</returns>
    Task<List<OTXPulse>> GetPulsesAsync(string indicator, string indicatorType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search OTX pulses by malware family
    /// </summary>
    /// <param name="malwareFamily">Malware family name</param>
    /// <param name="limit">Maximum number of results to return (default: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pulses related to the malware family</returns>
    Task<List<OTXPulse>> SearchByMalwareFamilyAsync(string malwareFamily, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent threat pulses from OTX
    /// </summary>
    /// <param name="limit">Maximum number of recent pulses to return (default: 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recent threat pulses</returns>
    Task<List<OTXPulse>> GetRecentPulsesAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check service health and API key validity
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if service is healthy and API key is valid</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current rate limit status
    /// </summary>
    /// <returns>Rate limit information</returns>
    Task<RateLimitStatus> GetRateLimitStatusAsync();

    /// <summary>
    /// Clear cached results for a specific indicator
    /// </summary>
    /// <param name="indicator">Indicator to clear from cache</param>
    void ClearCache(string indicator);

    /// <summary>
    /// Clear all cached results
    /// </summary>
    void ClearAllCache();
}
