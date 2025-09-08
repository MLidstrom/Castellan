using System.Net.Http.Headers;
using System.Text.Json;
using Castellan.Worker.Models.ThreatIntelligence;
using Castellan.Worker.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

/// <summary>
/// AlienVault Open Threat Exchange (OTX) threat intelligence service implementation
/// </summary>
public class OtxService : IOtxService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OtxService> _logger;
    private readonly IThreatIntelligenceCacheService _cacheService;
    private readonly AlienVaultOTXOptions _options;
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private readonly object _rateLimitLock = new();
    
    // Rate limiting tracking
    private DateTime _lastRequestTime = DateTime.MinValue;
    private int _requestsInCurrentMinute = 0;
    private DateTime _currentMinuteStart = DateTime.UtcNow;
    private int _dailyRequestCount = 0;
    private DateTime _dailyCountResetTime = DateTime.UtcNow.Date.AddDays(1);

    private const string SOURCE_NAME = "AlienVault OTX";

    public OtxService(
        HttpClient httpClient,
        IOptions<ThreatIntelligenceOptions> options,
        IThreatIntelligenceCacheService cacheService,
        ILogger<OtxService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheService = cacheService;
        _options = options.Value.AlienVaultOTX;
        
        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Castellan-Security-Scanner/1.0");
        
        // Add API key to headers if available
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-OTX-API-KEY", _options.ApiKey);
        }

        // Initialize rate limiting
        _rateLimitSemaphore = new SemaphoreSlim(_options.RateLimit.RequestsPerMinute, _options.RateLimit.RequestsPerMinute);
        
        _logger.LogInformation("AlienVault OTX service initialized with base URL: {BaseUrl}", _options.BaseUrl);
    }

    public async Task<OTXResult?> GetHashReputationAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("AlienVault OTX service is disabled");
            return null;
        }

        if (string.IsNullOrWhiteSpace(fileHash))
        {
            _logger.LogWarning("File hash is null or empty");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("AlienVault OTX API key is not configured");
            return null;
        }

        // Normalize hash to lowercase
        var normalizedHash = fileHash.Trim().ToLowerInvariant();
        
        // Check cache first
        var cachedResult = _cacheService.Get<OTXResult>(normalizedHash, SOURCE_NAME);
        if (cachedResult != null)
        {
            _logger.LogDebug("Retrieved OTX result from cache for hash: {Hash}", normalizedHash);
            return cachedResult;
        }

        // Apply rate limiting
        if (!await WaitForRateLimitAsync(cancellationToken))
        {
            _logger.LogWarning("Rate limit exceeded for AlienVault OTX API");
            return null;
        }

        try
        {
            _logger.LogDebug("Querying AlienVault OTX for hash: {Hash}", normalizedHash);

            // OTX uses different endpoints based on hash type
            var hashType = GetHashType(normalizedHash);
            var requestUrl = $"indicators/file/{normalizedHash}/general";

            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response == null)
            {
                _logger.LogError("Failed to get response from AlienVault OTX after retries");
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("AlienVault OTX API returned error status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, jsonContent);
                return null;
            }

            var otxResponse = JsonSerializer.Deserialize<OTXResponse>(jsonContent);
            if (otxResponse == null)
            {
                _logger.LogError("Failed to deserialize AlienVault OTX response");
                return null;
            }

            var result = MapToResult(otxResponse, normalizedHash);
            
            // Cache the result
            if (result != null)
            {
                _cacheService.Set(normalizedHash, result, _options.CacheExpiryHours);
                _logger.LogDebug("Cached AlienVault OTX result for hash: {Hash}", normalizedHash);
            }

            return result;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError("AlienVault OTX request timed out for hash: {Hash}", normalizedHash);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for AlienVault OTX query: {Hash}", normalizedHash);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AlienVault OTX response for hash: {Hash}", normalizedHash);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error querying AlienVault OTX for hash: {Hash}", normalizedHash);
            return null;
        }
    }

    public async Task<OTXResult?> GetIPReputationAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(ipAddress))
            return null;

        if (!await WaitForRateLimitAsync(cancellationToken))
            return null;

        try
        {
            var requestUrl = $"indicators/IPv4/{ipAddress}/general";
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var otxResponse = JsonSerializer.Deserialize<OTXResponse>(jsonContent);
                return otxResponse != null ? MapToResult(otxResponse, ipAddress) : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying AlienVault OTX for IP: {IP}", ipAddress);
            return null;
        }
    }

    public async Task<OTXResult?> GetDomainReputationAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(domain))
            return null;

        if (!await WaitForRateLimitAsync(cancellationToken))
            return null;

        try
        {
            var requestUrl = $"indicators/domain/{domain}/general";
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var otxResponse = JsonSerializer.Deserialize<OTXResponse>(jsonContent);
                return otxResponse != null ? MapToResult(otxResponse, domain) : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying AlienVault OTX for domain: {Domain}", domain);
            return null;
        }
    }

    public async Task<OTXResult?> GetURLReputationAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(url))
            return null;

        if (!await WaitForRateLimitAsync(cancellationToken))
            return null;

        try
        {
            // URL encode the URL parameter
            var encodedUrl = Uri.EscapeDataString(url);
            var requestUrl = $"indicators/url/{encodedUrl}/general";
            
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var otxResponse = JsonSerializer.Deserialize<OTXResponse>(jsonContent);
                return otxResponse != null ? MapToResult(otxResponse, url) : null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying AlienVault OTX for URL: {URL}", url);
            return null;
        }
    }

    public async Task<List<OTXPulse>> GetPulsesAsync(string indicator, string indicatorType, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return new List<OTXPulse>();

        if (!await WaitForRateLimitAsync(cancellationToken))
            return new List<OTXPulse>();

        try
        {
            var requestUrl = $"indicators/{indicatorType}/{indicator}/";
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var otxResponse = JsonSerializer.Deserialize<OTXResponse>(jsonContent);
                return otxResponse?.PulseInfo?.Pulses ?? new List<OTXPulse>();
            }

            return new List<OTXPulse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pulses from AlienVault OTX for {Type}: {Indicator}", indicatorType, indicator);
            return new List<OTXPulse>();
        }
    }

    public async Task<List<OTXPulse>> SearchByMalwareFamilyAsync(string malwareFamily, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(malwareFamily))
            return new List<OTXPulse>();

        if (!await WaitForRateLimitAsync(cancellationToken))
            return new List<OTXPulse>();

        try
        {
            var requestUrl = $"search/pulses/?q={Uri.EscapeDataString(malwareFamily)}&limit={limit}";
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                // Parse search results - this would need to be adapted based on actual OTX search response format
                return new List<OTXPulse>(); // Placeholder
            }

            return new List<OTXPulse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching AlienVault OTX by malware family: {Family}", malwareFamily);
            return new List<OTXPulse>();
        }
    }

    public async Task<List<OTXPulse>> GetRecentPulsesAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return new List<OTXPulse>();

        if (!await WaitForRateLimitAsync(cancellationToken))
            return new List<OTXPulse>();

        try
        {
            var requestUrl = $"pulses/subscribed?limit={limit}";
            var response = await RetryHttpRequest(
                () => _httpClient.GetAsync(requestUrl, cancellationToken),
                cancellationToken);

            if (response?.IsSuccessStatusCode == true)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                // Parse recent pulses - this would need to be adapted based on actual OTX response format
                return new List<OTXPulse>(); // Placeholder
            }

            return new List<OTXPulse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent pulses from AlienVault OTX");
            return new List<OTXPulse>();
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
            return false;

        try
        {
            // Test with a simple request to check if the service is responding
            var requestUrl = "pulses/subscribed?limit=1";
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            return response.IsSuccessStatusCode;
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

    public void ClearCache(string indicator)
    {
        _cacheService.Remove(indicator, SOURCE_NAME);
        _logger.LogDebug("Cleared AlienVault OTX cache for indicator: {Indicator}", indicator);
    }

    public void ClearAllCache()
    {
        _cacheService.Clear();
        _logger.LogDebug("Cleared all AlienVault OTX cache entries");
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

    private string GetHashType(string hash)
    {
        return hash.Length switch
        {
            32 => "md5",
            40 => "sha1",
            64 => "sha256",
            _ => "file" // Default fallback
        };
    }

    private OTXResult? MapToResult(OTXResponse otxResponse, string indicator)
    {
        if (otxResponse?.PulseInfo == null)
        {
            _logger.LogDebug("No pulse info found in OTX for indicator: {Indicator}", indicator);
            return null;
        }

        var pulseCount = otxResponse.PulseInfo.Count;
        var isKnownThreat = pulseCount > 0;
        
        var malwareFamilies = new List<string>();
        var attackIds = new List<string>();
        var tags = new List<string>();
        var authorName = "";

        if (otxResponse.PulseInfo.Pulses?.Any() == true)
        {
            var pulse = otxResponse.PulseInfo.Pulses.First();
            malwareFamilies.AddRange(pulse.MalwareFamilies ?? new List<string>());
            attackIds.AddRange(pulse.AttackIds ?? new List<string>());
            tags.AddRange(pulse.Tags ?? new List<string>());
            authorName = pulse.AuthorName ?? "";
        }

        var riskLevel = CalculateRiskLevel(pulseCount, malwareFamilies);
        var confidenceScore = CalculateConfidenceScore(pulseCount, malwareFamilies);

        return new OTXResult
        {
            Source = SOURCE_NAME,
            IsKnownThreat = isKnownThreat,
            ThreatName = GetThreatName(malwareFamilies, tags),
            RiskLevel = riskLevel,
            ConfidenceScore = confidenceScore,
            Description = $"OTX: {pulseCount} pulses, {malwareFamilies.Count} malware families",
            QueryTime = DateTime.UtcNow,
            PulseCount = pulseCount,
            MalwareFamilies = malwareFamilies,
            AttackIds = attackIds,
            Tags = tags,
            AuthorName = authorName
        };
    }

    private ThreatRiskLevel CalculateRiskLevel(int pulseCount, List<string> malwareFamilies)
    {
        if (malwareFamilies.Any())
        {
            // If we have known malware families, risk is at least high
            return pulseCount > 5 ? ThreatRiskLevel.Critical : ThreatRiskLevel.High;
        }

        return pulseCount switch
        {
            > 10 => ThreatRiskLevel.High,
            > 3 => ThreatRiskLevel.Medium,
            > 0 => ThreatRiskLevel.Low,
            _ => ThreatRiskLevel.Low
        };
    }

    private float CalculateConfidenceScore(int pulseCount, List<string> malwareFamilies)
    {
        // Base confidence on pulse count and malware family presence
        var baseScore = Math.Min(1.0f, pulseCount / 10.0f);
        
        // Boost confidence if we have malware families
        if (malwareFamilies.Any())
            baseScore = Math.Min(1.0f, baseScore + 0.3f);

        return baseScore;
    }

    private string GetThreatName(List<string> malwareFamilies, List<string> tags)
    {
        if (malwareFamilies.Any())
            return string.Join(", ", malwareFamilies.Take(2));
            
        if (tags.Any())
            return string.Join(", ", tags.Take(2));
            
        return "Suspicious Indicator";
    }

    public void Dispose()
    {
        _rateLimitSemaphore?.Dispose();
    }
}
