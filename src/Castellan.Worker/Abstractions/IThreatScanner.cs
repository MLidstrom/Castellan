using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

public interface IThreatScanner
{
    /// <summary>
    /// Performs a full system threat scan
    /// </summary>
    Task<ThreatScanResult> PerformFullScanAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs a quick threat scan of high-risk areas
    /// </summary>
    Task<ThreatScanResult> PerformQuickScanAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scans a specific directory for threats
    /// </summary>
    Task<ThreatScanResult> ScanDirectoryAsync(string path, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scans a specific file for threats
    /// </summary>
    Task<FileThreatResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current scan status
    /// </summary>
    Task<ThreatScanStatus> GetScanStatusAsync();
    
    /// <summary>
    /// Cancels any running scan
    /// </summary>
    Task CancelScanAsync();
    
    /// <summary>
    /// Gets the last scan results
    /// </summary>
    Task<ThreatScanResult?> GetLastScanResultAsync();
    
    /// <summary>
    /// Gets threat scan history
    /// </summary>
    Task<IEnumerable<ThreatScanResult>> GetScanHistoryAsync(int count = 10);
}