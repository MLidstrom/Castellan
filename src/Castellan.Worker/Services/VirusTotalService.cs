using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Castellan.Worker.Models.ThreatIntelligence;
using Castellan.Worker.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

/// <summary>
/// VirusTotal threat intelligence service implementation
/// </summary>
public class VirusTotalService : IVirusTotalService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VirusTotalService> _logger;
    private readonly IThreatIntelligenceCacheService _cacheService;
    private readonly VirusTotalOptions _options;
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private readonly object _rateLimitLock = new();
    
    // Rate limiting tracking
    private DateTime _lastRequestTime = DateTime.MinValue;
    private int _requestsInCurrentMinute = 0;
    private DateTime _currentMinuteStart = DateTime.UtcNow;
    private int _dailyRequestCount = 0;
    private DateTime _dailyCountResetTime = DateTime.UtcNow.Date.AddDays(1);

    private const string SOURCE_NAME = "VirusTotal";

    public VirusTotalService(
        HttpClient httpClient,
        IOptions<ThreatIntelligenceOptions> options,
        IThreatIntelligenceCacheService cacheService,
        ILogger<VirusTotalService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _options = options.Value.VirusTotal;
        
        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Castellan-Security-Scanner/1.0");

        // Initialize rate limiting
        _rateLimitSemaphore = new SemaphoreSlim(_options.RateLimit.RequestsPerMinute, _options.RateLimit.RequestsPerMinute);
        
        _logger.LogInformation("VirusTotal service initialized with base URL: {BaseUrl}", _options.BaseUrl);
    }

    public async Task<VirusTotalResult?> GetFileReportAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("VirusTotal service is disabled");
            return null;
        }

        if (string.IsNullOrWhiteSpace(fileHash))
        {
            _logger.LogWarning("File hash is null or empty");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("VirusTotal API key is not configured");
            return null;
        }

        // Normalize hash to uppercase
        var normalizedHash = fileHash.Trim().ToUpperInvariant();
        
        // Check cache first
        var cachedResult = _cacheService.Get<VirusTotalResult>(normalizedHash, SOURCE_NAME);
        if (cachedResult != null)
        {
            _logger.LogDebug("Retrieved VirusTotal result from cache for hash: {Hash}", normalizedHash);
            return cachedResult;
        }

        // Apply rate limiting
        if (!await WaitForRateLimitAsync(cancellationToken))
        {
            _logger.LogWarning("Rate limit exceeded for VirusTotal API");
            return null;
        }

        try
        {
            _logger.LogDebug("Querying VirusTotal for hash: {Hash}", normalizedHash);

            var requestUrl = $"file/report?apikey={_options.ApiKey}&resource={normalizedHash}";
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response == null)
            {
                _logger.LogError("Failed to get response from VirusTotal after retries");
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("VirusTotal API returned error status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, jsonContent);
                return null;
            }

            var vtResponse = JsonSerializer.Deserialize<VirusTotalResponse>(jsonContent);
            if (vtResponse == null)
            {
                _logger.LogError("Failed to deserialize VirusTotal response");
                return null;
            }

            var result = MapToResult(vtResponse, normalizedHash);
            
            // Cache the result
            if (result != null)
            {
                _cacheService.Set(normalizedHash, result, _options.CacheExpiryHours);
                _logger.LogDebug("Cached VirusTotal result for hash: {Hash}", normalizedHash);
            }

            return result;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError("VirusTotal request timed out for hash: {Hash}", normalizedHash);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for VirusTotal query: {Hash}", normalizedHash);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse VirusTotal response for hash: {Hash}", normalizedHash);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error querying VirusTotal for hash: {Hash}", normalizedHash);
            return null;
        }
    }

    public async Task<string?> SubmitFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return null;

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File does not exist: {FilePath}", filePath);
            return null;
        }

        // Check file size (VirusTotal has limits)
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > 32 * 1024 * 1024) // 32MB limit for public API
        {
            _logger.LogWarning("File too large for VirusTotal upload: {Size} bytes", fileInfo.Length);
            return null;
        }

        if (!await WaitForRateLimitAsync(cancellationToken))
        {
            _logger.LogWarning("Rate limit exceeded for VirusTotal file submission");
            return null;
        }

        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(_options.ApiKey), "apikey");

            using var fileStream = File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await RetryHttpRequest(
                () => _httpClient.PostAsync("file/scan", content, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var vtResponse = JsonSerializer.Deserialize<VirusTotalResponse>(jsonContent);
                
                _logger.LogInformation("Successfully submitted file to VirusTotal: {FilePath}, ScanId: {ScanId}", 
                    filePath, vtResponse?.ScanId);
                
                return vtResponse?.ScanId;
            }

            _logger.LogError("Failed to submit file to VirusTotal: {StatusCode}", response?.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting file to VirusTotal: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<VirusTotalResult?> GetScanResultAsync(string scanId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(scanId))
            return null;

        if (!await WaitForRateLimitAsync(cancellationToken))
            return null;

        try
        {
            var requestUrl = $"file/report?apikey={_options.ApiKey}&resource={scanId}";
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var vtResponse = JsonSerializer.Deserialize<VirusTotalResponse>(jsonContent);
                return vtResponse != null ? MapToResult(vtResponse, scanId) : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan result from VirusTotal: {ScanId}", scanId);
            return null;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return false;

        try
        {
            // Use a known clean file hash for testing
            var testHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"; // Empty file SHA256
            var result = await GetFileReportAsync(testHash, cancellationToken);
            return result != null; // If we get any result, the service is working
        }
        catch
        {
            return false;
        }
    }

    public async Task<RateLimitStatus> GetRateLimitStatusAsync()
    {
        lock (_rateLimitLock)
        {
            UpdateRateLimitCounters();
            
            return new RateLimitStatus
            {
                RequestsRemaining = Math.Max(0, _options.RateLimit.RequestsPerMinute - _requestsInCurrentMinute),
                RequestsPerMinute = _options.RateLimit.RequestsPerMinute,
                RequestsPerDay = _options.RateLimit.RequestsPerDay,
                ResetTime = _currentMinuteStart.AddMinutes(1),
                IsLimitExceeded = _requestsInCurrentMinute >= _options.RateLimit.RequestsPerMinute ||
                                 _dailyRequestCount >= _options.RateLimit.RequestsPerDay
            };
        }
    }

    public void ClearCache(string fileHash)
    {
        _cacheService.Remove(fileHash, SOURCE_NAME);
        _logger.LogDebug("Cleared VirusTotal cache for hash: {Hash}", fileHash);
    }

    public void ClearAllCache()
    {
        _cacheService.Clear();
        _logger.LogDebug("Cleared all VirusTotal cache entries");
    }

    private async Task<bool> WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        lock (_rateLimitLock)
        {
            UpdateRateLimitCounters();
            
            if (_dailyRequestCount >= _options.RateLimit.RequestsPerDay)
            {
                _logger.LogWarning("Daily rate limit exceeded: {Count}/{Limit}", 
                    _dailyRequestCount, _options.RateLimit.RequestsPerDay);
                return false;
            }

            if (_requestsInCurrentMinute >= _options.RateLimit.RequestsPerMinute)
            {
                var waitTime = _currentMinuteStart.AddMinutes(1) - DateTime.UtcNow;
                if (waitTime.TotalMilliseconds > 0)
                {
                    _logger.LogDebug("Rate limit reached, waiting {Seconds} seconds", waitTime.TotalSeconds);
                    // Don't wait here in the lock, return false to indicate rate limit exceeded
                    return false;
                }
            }

            // Increment counters
            _requestsInCurrentMinute++;
            _dailyRequestCount++;
            _lastRequestTime = DateTime.UtcNow;
        }

        // Wait for semaphore outside the lock
        return await _rateLimitSemaphore.WaitAsync(1000, cancellationToken);
    }

    private void UpdateRateLimitCounters()
    {
        var now = DateTime.UtcNow;
        
        // Reset minute counter if needed
        if (now >= _currentMinuteStart.AddMinutes(1))
        {
            _requestsInCurrentMinute = 0;
            _currentMinuteStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        }

        // Reset daily counter if needed
        if (now >= _dailyCountResetTime)
        {
            _dailyRequestCount = 0;
            _dailyCountResetTime = now.Date.AddDays(1);
        }
    }

    private async Task<HttpResponseMessage?> RetryHttpRequest(
        Func<Task<HttpResponseMessage>> requestFunc, 
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < _options.RetryAttempts; attempt++)
        {
            try
            {
                var response = await requestFunc();
                
                // Don't retry on client errors (4xx), only on server errors (5xx) or network issues
                if (response.IsSuccessStatusCode || 
                    ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500))
                {
                    return response;
                }

                if (attempt < _options.RetryAttempts - 1)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    _logger.LogWarning("Request failed with status {StatusCode}, retrying in {Delay} seconds (attempt {Attempt}/{Total})", 
                        response.StatusCode, delay.TotalSeconds, attempt + 1, _options.RetryAttempts);
                    
                    await Task.Delay(delay, cancellationToken);
                }

                response.Dispose();
            }
            catch (Exception ex) when (attempt < _options.RetryAttempts - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Request failed, retrying in {Delay} seconds (attempt {Attempt}/{Total})", 
                    delay.TotalSeconds, attempt + 1, _options.RetryAttempts);
                
                await Task.Delay(delay, cancellationToken);
            }
        }

        return null;
    }

    private VirusTotalResult? MapToResult(VirusTotalResponse vtResponse, string hash)
    {
        if (vtResponse.ResponseCode == -1)
        {
            _logger.LogDebug("Hash not found in VirusTotal: {Hash}", hash);
            return null;
        }

        if (vtResponse.ResponseCode == 0)
        {
            _logger.LogDebug("Hash queued for analysis in VirusTotal: {Hash}", hash);
            return null; // Scan is still pending
        }

        var detectedBy = vtResponse.Scans?
            .Where(s => s.Value.Detected)
            .Select(s => s.Key)
            .ToList() ?? new List<string>();

        var riskLevel = CalculateRiskLevel(vtResponse.PositiveScans, vtResponse.TotalScans);
        var confidenceScore = CalculateConfidenceScore(vtResponse.PositiveScans, vtResponse.TotalScans);

        return new VirusTotalResult
        {
            Source = SOURCE_NAME,
            IsKnownThreat = vtResponse.PositiveScans > 0,
            ThreatName = GetThreatName(vtResponse.Scans),
            RiskLevel = riskLevel,
            ConfidenceScore = confidenceScore,
            Description = $"Detected by {vtResponse.PositiveScans}/{vtResponse.TotalScans} engines",
            QueryTime = DateTime.UtcNow,
            PositiveScans = vtResponse.PositiveScans,
            TotalScans = vtResponse.TotalScans,
            Permalink = vtResponse.Permalink,
            DetectedBy = detectedBy
        };
    }

    private ThreatRiskLevel CalculateRiskLevel(int positiveScans, int totalScans)
    {
        if (totalScans == 0) return ThreatRiskLevel.Low;
        
        var detectionRate = (double)positiveScans / totalScans;
        
        return detectionRate switch
        {
            >= 0.5 => ThreatRiskLevel.Critical,
            >= 0.3 => ThreatRiskLevel.High,
            >= 0.1 => ThreatRiskLevel.Medium,
            > 0 => ThreatRiskLevel.Low,
            _ => ThreatRiskLevel.Low
        };
    }

    private float CalculateConfidenceScore(int positiveScans, int totalScans)
    {
        if (totalScans == 0) return 0.0f;
        
        var detectionRate = (float)positiveScans / totalScans;
        var scanCoverage = Math.Min(totalScans / 70.0f, 1.0f); // VirusTotal typically has ~70 engines
        
        return detectionRate * scanCoverage;
    }

    private string GetThreatName(Dictionary<string, VirusTotalScanResult>? scans)
    {
        if (scans == null || !scans.Any(s => s.Value.Detected))
            return string.Empty;

        // Get the most common threat name
        var threatNames = scans
            .Where(s => s.Value.Detected && !string.IsNullOrWhiteSpace(s.Value.Result))
            .Select(s => s.Value.Result)
            .GroupBy(name => name)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        return threatNames?.Key ?? "Malware";
    }

    public void Dispose()
    {
        _rateLimitSemaphore?.Dispose();
        // Note: HttpClient is managed by DI container, don't dispose it here
    }
}
