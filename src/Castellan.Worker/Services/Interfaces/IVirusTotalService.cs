using Castellan.Worker.Models.ThreatIntelligence;

namespace Castellan.Worker.Services.Interfaces;

/// <summary>
/// Interface for VirusTotal threat intelligence service
/// </summary>
public interface IVirusTotalService
{
    /// <summary>
    /// Query VirusTotal for file hash analysis
    /// </summary>
    /// <param name="fileHash">SHA256, SHA1, or MD5 hash of the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>VirusTotal analysis result</returns>
    Task<VirusTotalResult?> GetFileReportAsync(string fileHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit a file to VirusTotal for scanning (premium feature)
    /// </summary>
    /// <param name="filePath">Path to file to submit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scan ID for tracking submission</returns>
    Task<string?> SubmitFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get scan results by scan ID
    /// </summary>
    /// <param name="scanId">Scan ID from SubmitFileAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Scan results when available</returns>
    Task<VirusTotalResult?> GetScanResultAsync(string scanId, CancellationToken cancellationToken = default);

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
    /// Clear cached results for a specific hash
    /// </summary>
    /// <param name="fileHash">Hash to clear from cache</param>
    void ClearCache(string fileHash);

    /// <summary>
    /// Clear all cached results
    /// </summary>
    void ClearAllCache();
}

/// <summary>
/// Rate limit status information
/// </summary>
public class RateLimitStatus
{
    public int RequestsRemaining { get; set; }
    public int RequestsPerMinute { get; set; }
    public int RequestsPerDay { get; set; }
    public DateTime ResetTime { get; set; }
    public bool IsLimitExceeded { get; set; }
}
