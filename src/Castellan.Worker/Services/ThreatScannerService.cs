using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Models.ThreatIntelligence;
using Castellan.Worker.Services.Interfaces;

namespace Castellan.Worker.Services;

public class ThreatScannerService : IThreatScanner
{
    private readonly ILogger<ThreatScannerService> _logger;
    private readonly IOptionsMonitor<ThreatScanOptions> _optionsMonitor;
    private readonly IVirusTotalService? _virusTotalService;
    private readonly IMalwareBazaarService? _malwareBazaarService;
    private readonly IOtxService? _otxService;
    private readonly IThreatIntelligenceCacheService? _cacheService;
    private readonly IThreatScanHistoryRepository? _historyRepository;
    private readonly IThreatScanProgressStore _progressStore;
    private readonly ConcurrentQueue<ThreatScanResult> _scanHistory = new();
    private ThreatScanResult? _currentScan;
    private CancellationTokenSource? _currentScanCancellation;
    private readonly object _progressLock = new();
    
    // Progress tracking event
    public event EventHandler<ThreatScanProgress>? ProgressUpdated;

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

    public ThreatScannerService(
        ILogger<ThreatScannerService> logger,
        IOptionsMonitor<ThreatScanOptions> optionsMonitor,
        IThreatScanProgressStore progressStore,
        IVirusTotalService? virusTotalService = null,
        IMalwareBazaarService? malwareBazaarService = null,
        IOtxService? otxService = null,
        IThreatIntelligenceCacheService? cacheService = null,
        IThreatScanHistoryRepository? historyRepository = null)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _progressStore = progressStore;
        _virusTotalService = virusTotalService;
        _malwareBazaarService = malwareBazaarService;
        _otxService = otxService;
        _cacheService = cacheService;
        _historyRepository = historyRepository;

        // Ensure quarantine directory exists
        var options = _optionsMonitor.CurrentValue;
        if (options.QuarantineThreats && !string.IsNullOrEmpty(options.QuarantineDirectory))
        {
            Directory.CreateDirectory(options.QuarantineDirectory);
        }
    }

    public async Task<ThreatScanResult> PerformFullScanAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting full system threat scan");
        
        var scanResult = new ThreatScanResult
        {
            ScanType = ThreatScanType.FullScan,
            StartTime = DateTime.UtcNow,
            Status = ThreatScanStatus.Running,
            ScanId = Guid.NewGuid().ToString(),
            ScanPath = "Full System Scan"
        };

        _currentScan = scanResult;
        _currentScanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Initialize progress tracking
        var scanId = scanResult.ScanId!;
        UpdateProgress(scanId, ThreatScanStatus.Running, phase: "Initializing Full Scan");

        // Estimate total files to scan for progress calculation
        UpdateProgress(scanId, ThreatScanStatus.Running, phase: "Calculating scan scope");
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

        // Rough estimate of files to scan (this could be improved with actual counting)
        int totalEstimated = drives.Count() * 10000; // Rough estimate per drive

        try
        {

            foreach (var drive in drives)
            {
                if (_currentScanCancellation.Token.IsCancellationRequested)
                    break;

                _logger.LogInformation("Scanning drive: {DriveName}", drive.Name);
                UpdateProgress(scanId, ThreatScanStatus.Running,
                    scanResult.FilesScanned, scanResult.DirectoriesScanned, scanResult.ThreatsFound,
                    "", drive.Name, "Scanning Drive", totalEstimated);

                await ScanDirectoryRecursiveAsync(drive.RootDirectory.FullName, scanResult, _currentScanCancellation.Token, maxDepth: int.MaxValue, scanId: scanId, totalEstimated: totalEstimated);
            }

            scanResult.Status = scanResult.ThreatsFound > 0 ? ThreatScanStatus.CompletedWithThreats : ThreatScanStatus.Completed;
            UpdateProgress(scanId, scanResult.Status, scanResult.FilesScanned, scanResult.DirectoriesScanned,
                scanResult.ThreatsFound, "", "", "Completed", totalEstimated);
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

            // Clean up progress after some delay to allow final progress check
            _ = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ =>
            {
                _progressStore.RemoveProgress(scanId);
                _logger.LogDebug("Cleaned up progress for scan: {ScanId}", scanId);
            });

            // Persist to database if repository is available
            if (_historyRepository != null)
            {
                try
                {
                    await _historyRepository.CreateScanAsync(scanResult);
                    _logger.LogDebug("Saved scan result to database: {ScanId}", scanResult.ScanId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save scan result to database: {ScanId}", scanResult.ScanId);
                }
            }

            // Keep only last 50 scans in memory
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
            Status = ThreatScanStatus.Running,
            ScanId = Guid.NewGuid().ToString(),
            ScanPath = "Quick System Scan"
        };

        _currentScan = scanResult;
        _currentScanCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Initialize progress tracking
        var scanId = scanResult.ScanId!;
        UpdateProgress(scanId, ThreatScanStatus.Running, phase: "Initializing Quick Scan");
        
        try
        {
            // Estimate total files to scan for progress calculation
            UpdateProgress(scanId, ThreatScanStatus.Running, phase: "Calculating scan scope");
            int totalEstimated = await EstimateFilesToScanAsync(_highRiskDirectories, maxDepth: 3);
            
            // Quick scan focuses on high-risk directories
            foreach (var directory in _highRiskDirectories)
            {
                if (_currentScanCancellation.Token.IsCancellationRequested)
                    break;
                    
                if (Directory.Exists(directory))
                {
                    _logger.LogDebug("Quick scanning directory: {Directory}", directory);
                    UpdateProgress(scanId, ThreatScanStatus.Running, 
                        scanResult.FilesScanned, scanResult.DirectoriesScanned, scanResult.ThreatsFound,
                        "", directory, "Scanning", totalEstimated);
                    
                    await ScanDirectoryRecursiveAsync(directory, scanResult, _currentScanCancellation.Token, maxDepth: 3, scanId: scanId, totalEstimated: totalEstimated);
                }
            }

            scanResult.Status = scanResult.ThreatsFound > 0 ? ThreatScanStatus.CompletedWithThreats : ThreatScanStatus.Completed;
            UpdateProgress(scanId, scanResult.Status, scanResult.FilesScanned, scanResult.DirectoriesScanned, 
                scanResult.ThreatsFound, "", "", "Completed", totalEstimated);
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

            // Clean up progress after some delay to allow final progress check
            _ = Task.Delay(TimeSpan.FromSeconds(30)).ContinueWith(_ =>
            {
                _progressStore.RemoveProgress(scanId);
                _logger.LogDebug("Cleaned up progress for scan: {ScanId}", scanId);
            });

            // Persist to database if repository is available
            if (_historyRepository != null)
            {
                try
                {
                    await _historyRepository.CreateScanAsync(scanResult);
                    _logger.LogDebug("Saved scan result to database: {ScanId}", scanResult.ScanId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save scan result to database: {ScanId}", scanResult.ScanId);
                }
            }

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
            Status = ThreatScanStatus.Running,
            ScanId = Guid.NewGuid().ToString(),
            ScanPath = path
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
        if (_historyRepository != null)
        {
            try
            {
                return await _historyRepository.GetScanHistoryAsync(1, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get scan history from database, falling back to memory");
            }
        }

        // Fallback to in-memory history
        var history = _scanHistory.ToArray().TakeLast(count).Reverse();
        return await Task.FromResult(history);
    }
    
    public async Task<ThreatScanProgress?> GetScanProgressAsync()
    {
        return await Task.FromResult(_progressStore.GetCurrentProgress());
    }
    
    private void UpdateProgress(string scanId, ThreatScanStatus status, int filesScanned = 0,
        int directoriesScanned = 0, int threatsFound = 0, string currentFile = "",
        string currentDirectory = "", string phase = "", int totalEstimated = 0)
    {
        lock (_progressLock)
        {
            _logger.LogInformation("📊 UpdateProgress called for scanId: {ScanId}, phase: {Phase}, filesScanned: {FilesScanned}", scanId, phase, filesScanned);

            var currentProgress = _progressStore.GetProgress(scanId);
            if (currentProgress == null)
            {
                _logger.LogInformation("📊 Creating new progress for scanId: {ScanId}", scanId);
                currentProgress = new ThreatScanProgress
                {
                    ScanId = scanId,
                    StartTime = DateTime.UtcNow,
                    Status = status
                };
            }
            else
            {
                _logger.LogInformation("📊 Updating existing progress for scanId: {ScanId}", scanId);
            }

            currentProgress.Status = status;
            currentProgress.FilesScanned = filesScanned;
            currentProgress.DirectoriesScanned = directoriesScanned;
            currentProgress.ThreatsFound = threatsFound;
            currentProgress.CurrentFile = currentFile;
            currentProgress.CurrentDirectory = currentDirectory;
            currentProgress.TotalEstimatedFiles = totalEstimated;

            if (!string.IsNullOrEmpty(phase))
                currentProgress.ScanPhase = phase;

            // Calculate percentage if we have an estimate
            if (totalEstimated > 0)
            {
                currentProgress.PercentComplete = Math.Min(100.0, (double)filesScanned / totalEstimated * 100);

                // Estimate remaining time
                if (filesScanned > 0)
                {
                    var avgTimePerFile = currentProgress.ElapsedTime.TotalSeconds / filesScanned;
                    var remainingFiles = totalEstimated - filesScanned;
                    currentProgress.EstimatedTimeRemaining = TimeSpan.FromSeconds(avgTimePerFile * remainingFiles);
                }
            }

            // Store the updated progress
            _progressStore.SetProgress(scanId, currentProgress);
            _logger.LogInformation("📊 Progress stored for scanId: {ScanId}, status: {Status}, percent: {Percent}%",
                scanId, status, currentProgress.PercentComplete);
        }

        // Fire progress event with current progress
        var storedProgress = _progressStore.GetProgress(scanId);
        if (storedProgress != null)
        {
            ProgressUpdated?.Invoke(this, storedProgress);
        }
    }
    
    private async Task<int> EstimateFilesToScanAsync(string[] directories, int maxDepth = int.MaxValue)
    {
        int totalEstimate = 0;
        
        foreach (var directory in directories)
        {
            if (Directory.Exists(directory))
            {
                totalEstimate += await EstimateDirectoryFilesAsync(directory, maxDepth);
            }
        }
        
        return totalEstimate;
    }
    
    private async Task<int> EstimateDirectoryFilesAsync(string directoryPath, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth > maxDepth)
            return 0;
            
        try
        {
            // Skip excluded directories
            if (_optionsMonitor.CurrentValue.ExcludedDirectories.Any(excluded => 
                directoryPath.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
            {
                return 0;
            }
            
            var directory = new DirectoryInfo(directoryPath);
            if (!directory.Exists)
                return 0;
            
            int count = 0;
            
            // Count files that would be scanned
            var files = directory.GetFiles().Where(f => ShouldScanFile(f));
            count += files.Count();
            
            // Recursively count subdirectories
            if (currentDepth < maxDepth)
            {
                var subdirectories = directory.GetDirectories();
                foreach (var subdirectory in subdirectories)
                {
                    count += await EstimateDirectoryFilesAsync(subdirectory.FullName, maxDepth, currentDepth + 1);
                }
            }
            
            return count;
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
            return 0;
        }
        catch (Exception)
        {
            // Skip directories with errors
            return 0;
        }
    }

    private async Task ScanDirectoryRecursiveAsync(string directoryPath, ThreatScanResult scanResult, 
        CancellationToken cancellationToken, int currentDepth = 0, int maxDepth = int.MaxValue, 
        string scanId = "", int totalEstimated = 0)
    {
        if (cancellationToken.IsCancellationRequested || currentDepth > maxDepth)
            return;

        try
        {
            // Skip excluded directories
            if (_optionsMonitor.CurrentValue.ExcludedDirectories.Any(excluded => 
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
            
            var semaphore = new SemaphoreSlim(_optionsMonitor.CurrentValue.MaxConcurrentFiles, _optionsMonitor.CurrentValue.MaxConcurrentFiles);
            var scanTasks = files.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // Update progress with current file being scanned
                    if (!string.IsNullOrEmpty(scanId))
                    {
                        UpdateProgress(scanId, ThreatScanStatus.Running, scanResult.FilesScanned, 
                            scanResult.DirectoriesScanned, scanResult.ThreatsFound, 
                            file.Name, directoryPath, "Scanning", totalEstimated);
                    }
                    
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

                        _logger.LogInformation("Security finding: {ThreatName} in {FilePath} (Risk: {RiskLevel})", 
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
                        cancellationToken, currentDepth + 1, maxDepth, scanId, totalEstimated);
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
        if (file.Length > _optionsMonitor.CurrentValue.MaxFileSizeMB * 1024 * 1024)
            return false;

        // Skip excluded extensions
        if (_optionsMonitor.CurrentValue.ExcludedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
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
            // Calculate file hashes (MD5 for backward compatibility, SHA256 for threat intelligence)
            using var stream = file.OpenRead();
            using var md5 = MD5.Create();
            var md5HashBytes = await md5.ComputeHashAsync(stream, cancellationToken);
            result.FileHash = Convert.ToHexString(md5HashBytes).ToLowerInvariant();
            
            // Calculate SHA256 for threat intelligence services
            stream.Position = 0;
            using var sha256 = SHA256.Create();
            var sha256HashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            var sha256Hash = Convert.ToHexString(sha256HashBytes).ToLowerInvariant();

            // First check external threat intelligence (higher confidence)
            var threatIntelResult = await QueryThreatIntelligenceAsync(sha256Hash, cancellationToken);
            if (threatIntelResult?.IsKnownThreat == true)
            {
                result.ThreatType = MapThreatIntelligenceType(threatIntelResult);
                result.ThreatName = threatIntelResult.ThreatName;
                result.RiskLevel = threatIntelResult.RiskLevel;
                result.Confidence = threatIntelResult.ConfidenceScore;
                result.Description = $"Threat Intelligence: {threatIntelResult.Description}";
                _logger.LogInformation("External threat intelligence hit: {ThreatName} for {FilePath} (Confidence: {Confidence})",
                    result.ThreatName, file.FullName, result.Confidence);
                return result;
            }

            // Check against known local threat signatures (fallback)
            if (_threatSignatures.ContainsKey(result.FileHash))
            {
                var signature = _threatSignatures[result.FileHash];
                result.ThreatType = signature.Type;
                result.ThreatName = signature.Name;
                result.RiskLevel = signature.Risk;
                result.Confidence = 0.95f;
                result.Description = $"Known local signature: {signature.Name}";
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

    /// <summary>
    /// Query threat intelligence services for file hash information
    /// </summary>
    private async Task<ThreatIntelligenceResult?> QueryThreatIntelligenceAsync(string fileHash, CancellationToken cancellationToken)
    {
        try
        {
            // Query VirusTotal if available
            if (_virusTotalService != null)
            {
                var vtResult = await _virusTotalService.GetFileReportAsync(fileHash, cancellationToken);
                if (vtResult != null)
                {
                    _logger.LogDebug("VirusTotal result: {IsKnownThreat} threat for {Hash}", vtResult.IsKnownThreat, fileHash);
                    return vtResult;
                }
            }

            // Query MalwareBazaar if available
            if (_malwareBazaarService != null)
            {
                var mbResult = await _malwareBazaarService.GetHashInfoAsync(fileHash, cancellationToken);
                if (mbResult != null)
                {
                    _logger.LogDebug("MalwareBazaar result: {IsKnownThreat} threat for {Hash}", mbResult.IsKnownThreat, fileHash);
                    return mbResult;
                }
            }

            // Query AlienVault OTX if available
            if (_otxService != null)
            {
                var otxResult = await _otxService.GetHashReputationAsync(fileHash, cancellationToken);
                if (otxResult != null)
                {
                    _logger.LogDebug("AlienVault OTX result: {IsKnownThreat} threat for {Hash}", otxResult.IsKnownThreat, fileHash);
                    return otxResult;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying threat intelligence for hash: {Hash}", fileHash);
            return null;
        }
    }

    /// <summary>
    /// Map threat intelligence result to local threat type
    /// </summary>
    private ThreatType MapThreatIntelligenceType(ThreatIntelligenceResult threatIntel)
    {
        if (!threatIntel.IsKnownThreat)
            return ThreatType.Unknown;

        var threatName = threatIntel.ThreatName?.ToLowerInvariant() ?? "";

        return threatName switch
        {
            var name when name.Contains("trojan") => ThreatType.Trojan,
            var name when name.Contains("virus") => ThreatType.Virus,
            var name when name.Contains("backdoor") => ThreatType.Backdoor,
            var name when name.Contains("rootkit") => ThreatType.Rootkit,
            var name when name.Contains("spyware") => ThreatType.Spyware,
            var name when name.Contains("adware") => ThreatType.Adware,
            var name when name.Contains("ransomware") => ThreatType.Ransomware,
            var name when name.Contains("worm") => ThreatType.Worm,
            _ => ThreatType.Malware // Default to generic malware
        };
    }
}
