using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Castellan.Worker.Models;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for YARA scanning service
/// </summary>
public interface IYaraScanService
{
    /// <summary>
    /// Scan a file using enabled YARA rules
    /// </summary>
    /// <param name="filePath">Path to the file to scan</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of YARA matches</returns>
    Task<IEnumerable<YaraMatch>> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scan byte array using enabled YARA rules
    /// </summary>
    /// <param name="bytes">Byte array to scan</param>
    /// <param name="fileName">Optional filename for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of YARA matches</returns>
    Task<IEnumerable<YaraMatch>> ScanBytesAsync(byte[] bytes, string? fileName = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scan a stream using enabled YARA rules
    /// </summary>
    /// <param name="stream">Stream to scan</param>
    /// <param name="fileName">Optional filename for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of YARA matches</returns>
    Task<IEnumerable<YaraMatch>> ScanStreamAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the number of compiled YARA rules
    /// </summary>
    int GetCompiledRuleCount();
    
    /// <summary>
    /// Refresh the compiled YARA rules (useful after rule updates)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RefreshRulesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if YARA scanning is available and healthy
    /// </summary>
    bool IsHealthy { get; }
    
    /// <summary>
    /// Get the last error message if any
    /// </summary>
    string? LastError { get; }
}
