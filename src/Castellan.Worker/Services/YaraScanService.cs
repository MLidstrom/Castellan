using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using dnYara;

namespace Castellan.Worker.Services;

/// <summary>
/// YARA scanning service that provides malware detection capabilities
/// </summary>
public class YaraScanService : BackgroundService, IYaraScanService
{
    private readonly ILogger<YaraScanService> _logger;
    private readonly IYaraRuleStore _ruleStore;
    private readonly IOptionsMonitor<YaraScanningOptions> _options;
    private readonly SemaphoreSlim _compilationLock;
    private readonly SemaphoreSlim _scanLock;
    private readonly System.Threading.Timer _ruleRefreshTimer;
    
    private int _compiledRuleCount;
    private string? _lastError;
    private bool _isHealthy;
    private DateTime _lastRuleRefresh;
    
    // dnYara context and compiled rules
    private YaraContext? _yaraContext;
    private dnYara.CompiledRules? _compiledRules;
    private readonly object _yaraLock = new object();
    
    // Performance metrics
    private long _totalScans;
    private long _totalMatches;
    private long _totalScanTimeMs;
    private readonly object _metricsLock = new object();

    public YaraScanService(
        ILogger<YaraScanService> logger,
        IYaraRuleStore ruleStore,
        IOptionsMonitor<YaraScanningOptions> options)
    {
        _logger = logger;
        _ruleStore = ruleStore;
        _options = options;
        _compilationLock = new SemaphoreSlim(1, 1);
        _scanLock = new SemaphoreSlim(_options.CurrentValue.MaxConcurrentScans, _options.CurrentValue.MaxConcurrentScans);
        
        // Initialize YARA
        InitializeYara();
        
        // Setup rule refresh timer
        var refreshInterval = TimeSpan.FromMinutes(_options.CurrentValue.Compilation.RefreshIntervalMinutes);
        _ruleRefreshTimer = new System.Threading.Timer(async _ => await RefreshRulesAsync(), null, refreshInterval, refreshInterval);
        
        _logger.LogInformation("YARA Scan Service initialized");
    }

    public bool IsHealthy => _isHealthy;
    public string? LastError => _lastError;
    public int GetCompiledRuleCount() => _compiledRuleCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("YARA Scan Service background task started");
        
        // Initial rule compilation
        if (_options.CurrentValue.Compilation.PrecompileOnStartup)
        {
            await RefreshRulesAsync(stoppingToken);
        }
        
        // Keep the service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("YARA Scan Service background task stopping");
        }
    }

    private void InitializeYara()
    {
        try
        {
            // Initialize dnYara context - this creates the native YARA context
            _yaraContext = new YaraContext();
            
            _isHealthy = true;
            _lastError = null;
            _logger.LogInformation("YARA scanning service initialized with dnYara");
        }
        catch (Exception ex)
        {
            _lastError = $"Failed to initialize YARA service: {ex.Message}";
            _isHealthy = false;
            _logger.LogError(ex, "Failed to initialize YARA service");
        }
    }

    public async Task RefreshRulesAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogDebug("YARA scanning is disabled, skipping rule refresh");
            return;
        }

        await _compilationLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Refreshing YARA rules...");
            var stopwatch = Stopwatch.StartNew();
            
            // Get enabled rules
            var rules = await _ruleStore.GetEnabledRulesAsync();
            var rulesList = rules.ToList();
            
            if (!rulesList.Any())
            {
                _logger.LogWarning("No enabled YARA rules found");
                _compiledRules = null;
                _compiledRuleCount = 0;
                return;
            }
            
            // Compile all rules together using dnYara
            var validRules = new List<YaraRule>();
            var compiledCount = 0;
            
            lock (_yaraLock)
            {
                try
                {
                    // Dispose old compiled rules
                    _compiledRules?.Dispose();
                    _compiledRules = null;
                    
                    // Create new compiler
                    using var compiler = new dnYara.Compiler();
                    
                    // Add and validate each rule
                    foreach (var rule in rulesList)
                    {
                        try
                        {
                            // Try to add rule to compiler - this validates syntax
                            compiler.AddRuleString(rule.RuleContent);
                            
                            // If successful, mark as valid
                            rule.IsValid = true;
                            rule.ValidationError = null;
                            rule.LastValidated = DateTime.UtcNow;
                            validRules.Add(rule);
                            compiledCount++;
                            
                            _logger.LogDebug("Validated rule: {RuleName}", rule.Name);
                        }
                        catch (Exception ex)
                        {
                            // Rule validation failed
                            rule.IsValid = false;
                            rule.ValidationError = ex.Message;
                            rule.LastValidated = DateTime.UtcNow;
                            // Note: We'll update invalid rules after the lock
                            _logger.LogWarning(ex, "YARA rule validation failed: {RuleName} - {Error}", rule.Name, ex.Message);
                        }
                    }
                    
                    // Compile all valid rules
                    if (validRules.Any())
                    {
                        _compiledRules = compiler.Compile();
                        _logger.LogDebug("Successfully compiled {Count} YARA rules", validRules.Count);
                    }
                }
                catch (Exception ex)
                {
                    _lastError = $"YARA compilation error: {ex.Message}";
                    _isHealthy = false;
                    _logger.LogError(ex, "Failed to compile YARA rules");
                    return;
                }
            }
            
            // Update invalid rules after the lock (to avoid await in lock)
            var invalidRules = rulesList.Where(r => !r.IsValid).ToList();
            foreach (var invalidRule in invalidRules)
            {
                try
                {
                    await _ruleStore.UpdateRuleAsync(invalidRule);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update invalid rule in store: {RuleName}", invalidRule.Name);
                }
            }
            
            if (compiledCount == 0)
            {
                _lastError = "No valid YARA rules could be processed";
                _isHealthy = false;
                _logger.LogError("No valid YARA rules could be processed");
                return;
            }
            
            _compiledRuleCount = compiledCount;
            _lastRuleRefresh = DateTime.UtcNow;
            _isHealthy = true;
            _lastError = null;
            
            stopwatch.Stop();
            _logger.LogInformation("YARA rules processed: {CompiledCount}/{TotalCount} rules validated in {ElapsedMs}ms", 
                compiledCount, rulesList.Count, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _compilationLock.Release();
        }
    }

    public async Task<IEnumerable<YaraMatch>> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }
        
        var fileInfo = new FileInfo(filePath);
        var maxSizeBytes = (long)_options.CurrentValue.MaxFileSizeMB * 1024 * 1024;
        
        if (fileInfo.Length > maxSizeBytes)
        {
            _logger.LogWarning("File {FilePath} is too large ({FileSize} bytes > {MaxSize} bytes), skipping scan", 
                filePath, fileInfo.Length, maxSizeBytes);
            return Enumerable.Empty<YaraMatch>();
        }
        
        using var fileStream = File.OpenRead(filePath);
        return await ScanStreamAsync(fileStream, filePath, cancellationToken);
    }

    public async Task<IEnumerable<YaraMatch>> ScanBytesAsync(byte[] bytes, string? fileName = null, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream(bytes);
        return await ScanStreamAsync(memoryStream, fileName, cancellationToken);
    }

    public async Task<IEnumerable<YaraMatch>> ScanStreamAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled || !_isHealthy)
        {
            return Enumerable.Empty<YaraMatch>();
        }
        
        await _scanLock.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var matches = new List<YaraMatch>();
            
            // Read stream into memory for scanning
            byte[] buffer;
            if (stream is MemoryStream ms)
            {
                buffer = ms.ToArray();
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, cancellationToken);
                buffer = memoryStream.ToArray();
            }
            
            if (buffer.Length == 0)
            {
                return matches;
            }
            
            // Calculate file hash for match record
            var fileHash = CalculateHash(buffer);

            // Get compiled rules reference (Sprint 3 Phase 4: Minimize lock scope for concurrent scanning)
            dnYara.CompiledRules? compiledRules;
            lock (_yaraLock)
            {
                compiledRules = _compiledRules;
            }

            // Perform real YARA scanning OUTSIDE the lock for true concurrency
            if (compiledRules != null)
            {
                try
                {
                    // Create scanner and scan the buffer
                    var scanner = new dnYara.Scanner();

                    // Sprint 3 Phase 4: Use ScanMemory for binary scanning (avoids UTF-8 conversion overhead)
                    // Try binary scanning first, fall back to string scanning if not available
                    IEnumerable<object> scanResults;
                    try
                    {
                        // Attempt to use ScanMemory for binary data (more efficient)
                        var scanMemoryMethod = scanner.GetType().GetMethod("ScanMemory");
                        if (scanMemoryMethod != null)
                        {
                            scanResults = (IEnumerable<object>)scanMemoryMethod.Invoke(scanner, new object[] { buffer, compiledRules })!;
                        }
                        else
                        {
                            // Fallback to string scanning
                            var content = Encoding.UTF8.GetString(buffer);
                            scanResults = scanner.ScanString(content, compiledRules);
                        }
                    }
                    catch
                    {
                        // If reflection fails, use string scanning as fallback
                        var content = Encoding.UTF8.GetString(buffer);
                        scanResults = scanner.ScanString(content, compiledRules);
                    }

                    // Process scan results
                    var scanResultsList = new List<(object result, string? fileName, string fileHash, long elapsedMs)>();
                    foreach (var result in scanResults)
                    {
                        scanResultsList.Add((result, fileName, fileHash, stopwatch.ElapsedMilliseconds));
                    }

                    // Create match objects from scan results
                    foreach (var (result, fName, fHash, elapsed) in scanResultsList)
                    {
                        var yaraMatch = CreateYaraMatchFromScanResult(result, fName, fHash, elapsed);
                        if (yaraMatch != null)
                        {
                            matches.Add(yaraMatch);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during YARA scan: {FileName} - {Error}", fileName ?? "stream", ex.Message);
                }
            }
            
            // Process matches outside the lock (requires async calls)
            foreach (var match in matches.ToList())
            {
                try
                {
                    // Save match and update rule metrics
                    await _ruleStore.SaveMatchAsync(match);
                    var ruleId = GetRuleIdByName(match.RuleName);
                    if (ruleId != null)
                    {
                        await _ruleStore.UpdateRuleMetricsAsync(ruleId, true, match.ExecutionTimeMs);
                    }
                    
                    _logger.LogInformation("YARA match found: Rule {RuleName} matched in {FileName}", 
                        match.RuleName, fileName ?? "stream");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save YARA match: {RuleName}", match.RuleName);
                }
            }
            
            stopwatch.Stop();
            
            // Update performance metrics
            lock (_metricsLock)
            {
                _totalScans++;
                _totalMatches += matches.Count;
                _totalScanTimeMs += stopwatch.ElapsedMilliseconds;
            }
            
            _logger.LogDebug("YARA scan completed: {FileName} - {MatchCount} matches in {ElapsedMs}ms", 
                fileName ?? "stream", matches.Count, stopwatch.ElapsedMilliseconds);
            
            // Log slow scans
            if (stopwatch.ElapsedMilliseconds > _options.CurrentValue.Performance.SlowScanThresholdSeconds * 1000)
            {
                _logger.LogWarning("Slow YARA scan detected: {FileName} took {ElapsedMs}ms", 
                    fileName ?? "stream", stopwatch.ElapsedMilliseconds);
            }
            
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during YARA scan of {FileName}", fileName ?? "stream");
            return Enumerable.Empty<YaraMatch>();
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private string CalculateHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash);
    }

    private string? GetRuleIdByName(string ruleName)
    {
        // This is a simple implementation - in production you might want to cache this mapping
        try
        {
            var rules = _ruleStore.GetAllRulesAsync().Result;
            return rules.FirstOrDefault(r => r.Name == ruleName)?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get rule ID for rule name: {RuleName}", ruleName);
            return null;
        }
    }

    /// <summary>
    /// Get performance metrics
    /// </summary>
    public (long TotalScans, long TotalMatches, double AverageScanTimeMs) GetPerformanceMetrics()
    {
        lock (_metricsLock)
        {
            var avgScanTime = _totalScans > 0 ? (double)_totalScanTimeMs / _totalScans : 0;
            return (_totalScans, _totalMatches, avgScanTime);
        }
    }

    private YaraMatch? CreateYaraMatchFromScanResult(object scanResult, string? fileName, string fileHash, long executionTimeMs)
    {
        try
        {
            // Since we don't have detailed info about the ScanResult type structure from our exploration,
            // we'll use reflection to extract what we can
            var resultType = scanResult.GetType();
            
            // Try to get rule name and matched strings from the scan result
            string? ruleName = null;
            var matchedStrings = new List<YaraMatchString>();
            
            // Use reflection to get rule name and match details
            var ruleProperty = resultType.GetProperty("Rule") ?? resultType.GetProperty("RuleName");
            if (ruleProperty != null)
            {
                var ruleValue = ruleProperty.GetValue(scanResult);
                if (ruleValue != null)
                {
                    // If it's a Rule object, try to get its name
                    var ruleType = ruleValue.GetType();
                    var nameProperty = ruleType.GetProperty("Name") ?? ruleType.GetProperty("Identifier");
                    if (nameProperty != null)
                    {
                        ruleName = nameProperty.GetValue(ruleValue)?.ToString();
                    }
                    else
                    {
                        ruleName = ruleValue.ToString();
                    }
                }
            }
            
            // Try to get matches
            var matchesProperty = resultType.GetProperty("Matches") ?? resultType.GetProperty("MatchingStrings");
            if (matchesProperty != null)
            {
                var matchesValue = matchesProperty.GetValue(scanResult);
                if (matchesValue is System.Collections.IEnumerable enumerable)
                {
                    foreach (var match in enumerable)
                    {
                        if (match != null)
                        {
                            var matchType = match.GetType();
                            var identifierProp = matchType.GetProperty("Identifier") ?? matchType.GetProperty("Name");
                            var offsetProp = matchType.GetProperty("Offset") ?? matchType.GetProperty("Position");
                            var valueProp = matchType.GetProperty("Value") ?? matchType.GetProperty("Data");
                            
                            var identifier = identifierProp?.GetValue(match)?.ToString() ?? "$unknown";
                            var offset = (int)(offsetProp?.GetValue(match) ?? 0);
                            var value = valueProp?.GetValue(match)?.ToString() ?? "";
                            
                            matchedStrings.Add(new YaraMatchString
                            {
                                Identifier = identifier,
                                Offset = offset,
                                Value = value,
                                IsHex = false
                            });
                        }
                    }
                }
            }
            
            if (string.IsNullOrEmpty(ruleName))
            {
                _logger.LogWarning("Could not extract rule name from scan result");
                return null;
            }
            
            var ruleId = GetRuleIdByName(ruleName);
            if (ruleId == null)
            {
                _logger.LogWarning("Could not find rule ID for rule name: {RuleName}", ruleName);
                return null;
            }
            
            return new YaraMatch
            {
                Id = Guid.NewGuid().ToString(),
                RuleId = ruleId,
                RuleName = ruleName,
                MatchTime = DateTime.UtcNow,
                TargetFile = fileName ?? "stream",
                TargetHash = fileHash,
                ExecutionTimeMs = executionTimeMs,
                MatchedStrings = matchedStrings,
                Metadata = new Dictionary<string, string>
                {
                    { "scanner", "dnYara" },
                    { "scan_type", fileName != null ? "file" : "stream" }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create YaraMatch from scan result");
            return null;
        }
    }

    public override void Dispose()
    {
        _ruleRefreshTimer?.Dispose();
        _compilationLock?.Dispose();
        _scanLock?.Dispose();
        
        // Clean up YARA resources
        lock (_yaraLock)
        {
            _compiledRules?.Dispose();
            _compiledRules = null;
            _yaraContext?.Dispose();
            _yaraContext = null;
        }
        
        base.Dispose();
    }
}
