using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

public class ThreatScannerService : IThreatScanner
{
    private readonly ILogger<ThreatScannerService> _logger;
    private readonly ThreatScanOptions _options;
    private readonly ConcurrentQueue<ThreatScanResult> _scanHistory = new();
    private ThreatScanResult? _currentScan;
    private CancellationTokenSource? _currentScanCancellation;

    // Known threat signatures (simplified implementation)
    private readonly Dictionary<string, (ThreatType Type, string Name, ThreatRiskLevel Risk)> _threatSignatures = new()
    {
        // Common malware file hashes (examples - in production, use threat intelligence feeds)
        { "d41d8cd98f00b204e9800998ecf8427e", (ThreatType.SuspiciousFile, "Empty File", ThreatRiskLevel.Low) },
        // Suspicious file patterns
        { "EICAR", (ThreatType.Virus, "EICAR Test File", ThreatRiskLevel.Medium) },
        // PowerShell encoded patterns
        { "powershell", (ThreatType.SuspiciousScript, "PowerShell Script", ThreatRiskLevel.Medium) },
        { "invoke-expression", (ThreatType.SuspiciousScript, "PowerShell Invoke-Expression", ThreatRiskLevel.High) },
        { "downloadstring", (ThreatType.SuspiciousScript, "PowerShell Download", ThreatRiskLevel.High) },
        // Common backdoor patterns
        { "netcat", (ThreatType.Backdoor, "Netcat Binary", ThreatRiskLevel.High) },
        { "psexec", (ThreatType.SuspiciousBehavior, "PSExec Tool", ThreatRiskLevel.Medium) }
    };

    // High-risk directories to prioritize in scans
    private readonly string[] _highRiskDirectories = 
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers"),
        Path.GetTempPath(),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"C:\Windows\System32\Tasks",
        @"C:\Windows\System32\WindowsPowerShell\v1.0"
    };

    // Suspicious file extensions
    private readonly string[] _suspiciousExtensions = 
    {
        ".exe", ".dll", ".scr", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jar", 
        ".com", ".pif", ".sys", ".drv", ".tmp"
    };

    public ThreatScannerService(ILogger<ThreatScannerService> logger, ThreatScanOptions options)
    {
        _logger = logger;
        _options = options;
        
        // Ensure quarantine directory exists
        if (_options.QuarantineThreats && !string.IsNullOrEmpty(_options.QuarantineDirectory))
        {
            Directory.CreateDirectory(_options.QuarantineDirectory);
        }
    }

    public async Task<ThreatScanResult> PerformFullScanAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting full system threat scan");
        
        var scanResult = new ThreatScanResult
        {
            ScanType = ThreatScanType.FullScan,
            StartTime = DateTime.UtcNow,
            Status = ThreatScanStatus.Running
        };

        _currentScan = scanResult;
        _currentScanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
            
            foreach (var drive in drives)
            {
                if (_currentScanCancellation.Token.IsCancellationRequested)
                    break;
                    
                _logger.LogInformation("Scanning drive: {DriveName}", drive.Name);
                await ScanDirectoryRecursiveAsync(drive.RootDirectory.FullName, scanResult, _currentScanCancellation.Token);
            }

            scanResult.Status = scanResult.ThreatsFound > 0 ? ThreatScanStatus.CompletedWithThreats : ThreatScanStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            scanResult.Status = ThreatScanStatus.Cancelled;
            _logger.LogInformation("Full scan was cancelled");
        }
        catch (Exception ex)
        {
            scanResult.Status = ThreatScanStatus.Failed;
            scanResult.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error during full scan");
        }
        finally
        {
            scanResult.EndTime = DateTime.UtcNow;
            _scanHistory.Enqueue(scanResult);
            
            // Keep only last 50 scans
            while (_scanHistory.Count > 50)
            {
                _scanHistory.TryDequeue(out _);
            }
            
            _currentScan = null;
            _currentScanCancellation?.Dispose();
            _currentScanCancellation = null;
        }

        _logger.LogInformation("Full scan completed. Files: {FilesScanned}, Threats: {ThreatsFound}", 
            scanResult.FilesScanned, scanResult.ThreatsFound);

        return scanResult;
    }

    public async Task<ThreatScanResult> PerformQuickScanAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting quick threat scan");
        
        var scanResult = new ThreatScanResult
        {
            ScanType = ThreatScanType.QuickScan,
            StartTime = DateTime.UtcNow,
            Status = ThreatScanStatus.Running
        };

        _currentScan = scanResult;
        _currentScanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Quick scan focuses on high-risk directories
            foreach (var directory in _highRiskDirectories)
            {
                if (_currentScanCancellation.Token.IsCancellationRequested)
                    break;
                    
                if (Directory.Exists(directory))
                {
                    _logger.LogDebug("Quick scanning directory: {Directory}", directory);
                    await ScanDirectoryRecursiveAsync(directory, scanResult, _currentScanCancellation.Token, maxDepth: 3);
                }
            }

            scanResult.Status = scanResult.ThreatsFound > 0 ? ThreatScanStatus.CompletedWithThreats : ThreatScanStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            scanResult.Status = ThreatScanStatus.Cancelled;
            _logger.LogInformation("Quick scan was cancelled");
        }
        catch (Exception ex)
        {
            scanResult.Status = ThreatScanStatus.Failed;
            scanResult.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error during quick scan");
        }
        finally
        {
            scanResult.EndTime = DateTime.UtcNow;
            _scanHistory.Enqueue(scanResult);
            
            _currentScan = null;
            _currentScanCancellation?.Dispose();
            _currentScanCancellation = null;
        }

        _logger.LogInformation("Quick scan completed. Files: {FilesScanned}, Threats: {ThreatsFound}", 
            scanResult.FilesScanned, scanResult.ThreatsFound);

        return scanResult;
    }

    public async Task<ThreatScanResult> ScanDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting directory scan: {Path}", path);
        
        var scanResult = new ThreatScanResult
        {
            ScanType = ThreatScanType.DirectoryScan,
            StartTime = DateTime.UtcNow,
            Status = ThreatScanStatus.Running
        };

        try
        {
            await ScanDirectoryRecursiveAsync(path, scanResult, cancellationToken);
            scanResult.Status = scanResult.ThreatsFound > 0 ? ThreatScanStatus.CompletedWithThreats : ThreatScanStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            scanResult.Status = ThreatScanStatus.Cancelled;
        }
        catch (Exception ex)
        {
            scanResult.Status = ThreatScanStatus.Failed;
            scanResult.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error scanning directory: {Path}", path);
        }
        finally
        {
            scanResult.EndTime = DateTime.UtcNow;
        }

        return scanResult;
    }

    public async Task<FileThreatResult> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Scanning file: {FilePath}", filePath);
        
        try
        {
            if (!File.Exists(filePath))
            {
                return new FileThreatResult
                {
                    FilePath = filePath,
                    ThreatType = ThreatType.Unknown,
                    Description = "File not found",
                    RiskLevel = ThreatRiskLevel.Low
                };
            }

            var fileInfo = new FileInfo(filePath);
            var threat = await AnalyzeFileAsync(fileInfo, cancellationToken);
            return threat;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning file: {FilePath}", filePath);
            return new FileThreatResult
            {
                FilePath = filePath,
                ThreatType = ThreatType.Unknown,
                Description = $"Scan error: {ex.Message}",
                RiskLevel = ThreatRiskLevel.Low
            };
        }
    }

    public async Task<ThreatScanStatus> GetScanStatusAsync()
    {
        return await Task.FromResult(_currentScan?.Status ?? ThreatScanStatus.NotStarted);
    }

    public async Task CancelScanAsync()
    {
        if (_currentScanCancellation != null)
        {
            _currentScanCancellation.Cancel();
            _logger.LogInformation("Scan cancellation requested");
        }
        await Task.CompletedTask;
    }

    public async Task<ThreatScanResult?> GetLastScanResultAsync()
    {
        return await Task.FromResult(_scanHistory.LastOrDefault());
    }

    public async Task<IEnumerable<ThreatScanResult>> GetScanHistoryAsync(int count = 10)
    {
        var history = _scanHistory.ToArray().TakeLast(count).Reverse();
        return await Task.FromResult(history);
    }

    private async Task ScanDirectoryRecursiveAsync(string directoryPath, ThreatScanResult scanResult, 
        CancellationToken cancellationToken, int currentDepth = 0, int maxDepth = int.MaxValue)
    {
        if (cancellationToken.IsCancellationRequested || currentDepth > maxDepth)
            return;

        try
        {
            // Skip excluded directories
            if (_options.ExcludedDirectories.Any(excluded => 
                directoryPath.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var directory = new DirectoryInfo(directoryPath);
            if (!directory.Exists)
                return;

            scanResult.DirectoriesScanned++;

            // Scan files in current directory
            var files = directory.GetFiles().Where(f => ShouldScanFile(f));
            
            var semaphore = new SemaphoreSlim(_options.MaxConcurrentFiles, _options.MaxConcurrentFiles);
            var scanTasks = files.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var threat = await AnalyzeFileAsync(file, cancellationToken);
                    if (threat.ThreatType != ThreatType.Unknown)
                    {
                        scanResult.ThreatDetails.Add(threat);
                        
                        // Update counters
                        scanResult.ThreatsFound++;
                        switch (threat.ThreatType)
                        {
                            case ThreatType.Malware:
                            case ThreatType.Virus:
                            case ThreatType.Trojan:
                            case ThreatType.Ransomware:
                            case ThreatType.Worm:
                                scanResult.MalwareDetected++;
                                break;
                            case ThreatType.Backdoor:
                            case ThreatType.Rootkit:
                                scanResult.BackdoorsDetected++;
                                break;
                            default:
                                scanResult.SuspiciousFiles++;
                                break;
                        }

                        _logger.LogWarning("Threat detected: {ThreatName} in {FilePath} (Risk: {RiskLevel})", 
                            threat.ThreatName, threat.FilePath, threat.RiskLevel);
                    }
                    
                    scanResult.FilesScanned++;
                    scanResult.BytesScanned += file.Length;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(scanTasks);

            // Recursively scan subdirectories
            if (currentDepth < maxDepth)
            {
                var subdirectories = directory.GetDirectories();
                foreach (var subdirectory in subdirectories)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                        
                    await ScanDirectoryRecursiveAsync(subdirectory.FullName, scanResult, 
                        cancellationToken, currentDepth + 1, maxDepth);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogDebug("Access denied to directory: {Directory}", directoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning directory: {Directory}", directoryPath);
        }
    }

    private bool ShouldScanFile(FileInfo file)
    {
        // Skip files that are too large
        if (file.Length > _options.MaxFileSizeMB * 1024 * 1024)
            return false;

        // Skip excluded extensions
        if (_options.ExcludedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            return false;

        // Focus on suspicious extensions for efficiency
        return _suspiciousExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase) || 
               file.Extension == string.Empty; // Files without extensions
    }

    private async Task<FileThreatResult> AnalyzeFileAsync(FileInfo file, CancellationToken cancellationToken)
    {
        var result = new FileThreatResult
        {
            FilePath = file.FullName,
            FileSize = file.Length,
            ThreatType = ThreatType.Unknown,
            RiskLevel = ThreatRiskLevel.Low
        };

        try
        {
            // Calculate file hash
            using var stream = file.OpenRead();
            using var md5 = MD5.Create();
            var hashBytes = await md5.ComputeHashAsync(stream, cancellationToken);
            result.FileHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            // Check against known threat signatures
            if (_threatSignatures.ContainsKey(result.FileHash))
            {
                var signature = _threatSignatures[result.FileHash];
                result.ThreatType = signature.Type;
                result.ThreatName = signature.Name;
                result.RiskLevel = signature.Risk;
                result.Confidence = 0.95f;
                result.Description = $"Known threat signature: {signature.Name}";
                return result;
            }

            // Content-based analysis
            stream.Position = 0;
            var buffer = new byte[Math.Min(8192, file.Length)]; // Read first 8KB
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            var content = Encoding.UTF8.GetString(buffer, 0, bytesRead).ToLowerInvariant();

            // Check for suspicious patterns
            foreach (var signature in _threatSignatures)
            {
                if (content.Contains(signature.Key, StringComparison.OrdinalIgnoreCase))
                {
                    result.ThreatType = signature.Value.Type;
                    result.ThreatName = signature.Value.Name;
                    result.RiskLevel = signature.Value.Risk;
                    result.Confidence = 0.75f;
                    result.Description = $"Suspicious content pattern: {signature.Key}";
                    
                    // Add MITRE techniques based on threat type
                    result.MitreTechniques = GetMitreTechniques(result.ThreatType);
                    break;
                }
            }

            // Additional behavioral analysis
            if (result.ThreatType == ThreatType.Unknown)
            {
                result = PerformBehavioralAnalysis(file, result, content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error analyzing file: {FilePath}", file.FullName);
        }

        return result;
    }

    private FileThreatResult PerformBehavioralAnalysis(FileInfo file, FileThreatResult result, string content)
    {
        // Check for suspicious file locations
        if (file.DirectoryName?.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase) == true ||
            file.DirectoryName?.Contains(@"\AppData\Local\Temp\", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (_suspiciousExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            {
                result.ThreatType = ThreatType.SuspiciousFile;
                result.ThreatName = "Suspicious File in Temp Directory";
                result.RiskLevel = ThreatRiskLevel.Medium;
                result.Confidence = 0.6f;
                result.Description = "Executable file in temporary directory";
                result.MitreTechniques = new[] { "T1055", "T1059" };
            }
        }

        // Check for suspicious file names
        var suspiciousNames = new[] { "svchost", "csrss", "winlogon", "explorer" };
        if (suspiciousNames.Any(name => file.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)) &&
            !file.DirectoryName?.StartsWith(@"C:\Windows\System32", StringComparison.OrdinalIgnoreCase) == true)
        {
            result.ThreatType = ThreatType.SuspiciousBehavior;
            result.ThreatName = "Suspicious System Process Name";
            result.RiskLevel = ThreatRiskLevel.High;
            result.Confidence = 0.8f;
            result.Description = "File mimicking system process name outside system directory";
            result.MitreTechniques = new[] { "T1036", "T1055" };
        }

        return result;
    }

    private string[] GetMitreTechniques(ThreatType threatType)
    {
        return threatType switch
        {
            ThreatType.Backdoor => new[] { "T1505", "T1078" },
            ThreatType.Rootkit => new[] { "T1014", "T1055" },
            ThreatType.SuspiciousScript => new[] { "T1059", "T1086" },
            ThreatType.Ransomware => new[] { "T1486", "T1490" },
            ThreatType.Spyware => new[] { "T1056", "T1113" },
            ThreatType.Trojan => new[] { "T1055", "T1105" },
            _ => Array.Empty<string>()
        };
    }
}