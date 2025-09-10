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
    
    private List<YaraRule>? _compiledRules;
    private int _compiledRuleCount;
    private string? _lastError;
    private bool _isHealthy;
    private DateTime _lastRuleRefresh;
    
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
            // TODO: Initialize actual YARA context once dnYara API is properly integrated
            _isHealthy = true;
            _lastError = null;
            _logger.LogInformation("YARA scanning service initialized (placeholder implementation)");
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
            
            // TODO: Implement actual YARA rule compilation once dnYara API is integrated
            // For now, just store the rules for basic validation
            var compiledCount = 0;
            var validRules = new List<YaraRule>();
            
            foreach (var rule in rulesList)
            {
                try
                {
                    // Basic validation (this will be replaced with actual YARA compilation)
                    var (isValid, error) = ValidateYaraRuleBasic(rule.RuleContent);
                    if (isValid)
                    {
                        validRules.Add(rule);
                        compiledCount++;
                        _logger.LogDebug("Validated rule: {RuleName}", rule.Name);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to validate YARA rule {RuleName}: {Error}", rule.Name, error);
                        rule.IsValid = false;
                        rule.ValidationError = error;
                        rule.LastValidated = DateTime.UtcNow;
                        await _ruleStore.UpdateRuleAsync(rule);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process YARA rule: {RuleName}", rule.Name);
                }
            }
            
            if (compiledCount == 0)
            {
                _lastError = "No valid YARA rules could be processed";
                _isHealthy = false;
                _logger.LogError("No valid YARA rules could be processed");
                return;
            }
            
            _compiledRules = validRules;
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
        if (!_options.CurrentValue.Enabled || !_isHealthy || _compiledRules == null)
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
            
            // TODO: Perform actual YARA scan once dnYara API is properly integrated
            // For now, simulate scan results for testing
            if (_compiledRules?.Any() == true)
            {
                // Simulate a simple pattern match for demonstration
                var content = Encoding.UTF8.GetString(buffer);
                foreach (var rule in _compiledRules)
                {
                    // Very basic simulation - check if any rule content keywords are found
                    if (content.Contains("malware", StringComparison.OrdinalIgnoreCase) || 
                        content.Contains("virus", StringComparison.OrdinalIgnoreCase))
                    {
                        var yaraMatch = new YaraMatch
                        {
                            Id = Guid.NewGuid().ToString(),
                            RuleId = rule.Id,
                            RuleName = rule.Name,
                            MatchTime = DateTime.UtcNow,
                            TargetFile = fileName ?? "stream",
                            TargetHash = fileHash,
                            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                            MatchedStrings = new List<YaraMatchString>
                            {
                                new YaraMatchString
                                {
                                    Identifier = "$test",
                                    Offset = 0,
                                    Value = "simulated match",
                                    IsHex = false
                                }
                            },
                            Metadata = new Dictionary<string, string> { { "author", rule.Author } }
                        };
                        
                        matches.Add(yaraMatch);
                        
                        // Save match and update rule metrics
                        await _ruleStore.SaveMatchAsync(yaraMatch);
                        await _ruleStore.UpdateRuleMetricsAsync(rule.Id, true, yaraMatch.ExecutionTimeMs);
                    }
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

    private (bool IsValid, string? Error) ValidateYaraRuleBasic(string ruleContent)
    {
        // Basic YARA syntax validation - will be replaced with actual YARA compiler validation
        if (string.IsNullOrWhiteSpace(ruleContent))
            return (false, "Rule content cannot be empty");
        
        var lowerContent = ruleContent.ToLowerInvariant();
        
        if (!lowerContent.Contains("rule "))
            return (false, "Invalid YARA rule: missing 'rule' keyword");
        
        if (!ruleContent.Contains("{") || !ruleContent.Contains("}"))
            return (false, "Invalid YARA rule: missing braces");
        
        if (!lowerContent.Contains("condition:"))
            return (false, "Invalid YARA rule: missing 'condition' section");
        
        return (true, null);
    }

    public override void Dispose()
    {
        _ruleRefreshTimer?.Dispose();
        _compilationLock?.Dispose();
        _scanLock?.Dispose();
        
        base.Dispose();
    }
}
