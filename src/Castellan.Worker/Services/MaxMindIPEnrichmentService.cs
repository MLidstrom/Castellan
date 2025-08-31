using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;

namespace Castellan.Worker.Services;

/// <summary>
/// IP enrichment service using MaxMind GeoLite2 databases
/// </summary>
public class MaxMindIPEnrichmentService : IIPEnrichmentService, IDisposable
{
    private readonly ILogger<MaxMindIPEnrichmentService> _logger;
    private readonly IPEnrichmentOptions _options;
    private readonly IMemoryCache _cache;
    private DatabaseReader? _cityReader;
    private DatabaseReader? _asnReader;
    private DatabaseReader? _countryReader;
    private bool _disposed;

    public MaxMindIPEnrichmentService(
        ILogger<MaxMindIPEnrichmentService> logger,
        IOptions<IPEnrichmentOptions> options,
        IMemoryCache cache)
    {
        _logger = logger;
        _options = options.Value;
        _cache = cache;

        InitializeDatabases();
    }

    private void InitializeDatabases()
    {
        try
        {
            _logger.LogInformation("Initializing MaxMind databases...");
            _logger.LogInformation("City DB Path from config: {Path}", _options.MaxMindCityDbPath ?? "null");
            _logger.LogInformation("ASN DB Path from config: {Path}", _options.MaxMindASNDbPath ?? "null");
            _logger.LogInformation("Country DB Path from config: {Path}", _options.MaxMindCountryDbPath ?? "null");
            _logger.LogInformation("Current working directory: {Dir}", Directory.GetCurrentDirectory());
            
            // Initialize City database
            if (!string.IsNullOrEmpty(_options.MaxMindCityDbPath) && File.Exists(_options.MaxMindCityDbPath))
            {
                _cityReader = new DatabaseReader(_options.MaxMindCityDbPath);
                _logger.LogInformation("MaxMind City database loaded from {Path}", _options.MaxMindCityDbPath);
            }
            else
            {
                _logger.LogWarning("MaxMind City database not found at {Path}", _options.MaxMindCityDbPath ?? "null");
                if (!string.IsNullOrEmpty(_options.MaxMindCityDbPath))
                {
                    var fullPath = Path.GetFullPath(_options.MaxMindCityDbPath);
                    _logger.LogWarning("Full path would be: {FullPath}, Exists: {Exists}", fullPath, File.Exists(fullPath));
                }
            }

            // Initialize ASN database
            if (!string.IsNullOrEmpty(_options.MaxMindASNDbPath) && File.Exists(_options.MaxMindASNDbPath))
            {
                _asnReader = new DatabaseReader(_options.MaxMindASNDbPath);
                _logger.LogInformation("MaxMind ASN database loaded from {Path}", _options.MaxMindASNDbPath);
            }
            else
            {
                _logger.LogWarning("MaxMind ASN database not found at {Path}", _options.MaxMindASNDbPath ?? "null");
            }

            // Initialize Country database
            if (!string.IsNullOrEmpty(_options.MaxMindCountryDbPath) && File.Exists(_options.MaxMindCountryDbPath))
            {
                _countryReader = new DatabaseReader(_options.MaxMindCountryDbPath);
                _logger.LogInformation("MaxMind Country database loaded from {Path}", _options.MaxMindCountryDbPath);
            }
            else
            {
                _logger.LogWarning("MaxMind Country database not found at {Path}", _options.MaxMindCountryDbPath ?? "null");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MaxMind databases");
        }
    }

    public async Task<IPEnrichmentResult> EnrichAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MaxMindIPEnrichmentService));

        if (!_options.Enabled)
        {
            return IPEnrichmentResult.Failed(ipAddress, "IP enrichment is disabled");
        }

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return IPEnrichmentResult.Failed(ipAddress, "IP address is null or empty");
        }

        // Check cache first
        var cacheKey = $"ip_enrichment_{ipAddress}";
        if (_options.CacheMinutes > 0 && _cache.TryGetValue(cacheKey, out IPEnrichmentResult? cachedResult))
        {
            if (_options.EnableDebugLogging)
                _logger.LogDebug("Cache hit for IP {IPAddress}", ipAddress);
            return cachedResult!;
        }

        try
        {
            if (!IPAddress.TryParse(ipAddress, out var parsedIP))
            {
                return IPEnrichmentResult.Failed(ipAddress, "Invalid IP address format");
            }

            // Check if it's a private IP
            if (IsPrivateIP(parsedIP))
            {
                if (!_options.EnrichPrivateIPs)
                {
                    var privateResult = IPEnrichmentResult.Private(ipAddress);
                    CacheResult(cacheKey, privateResult);
                    return privateResult;
                }
            }

            var result = await EnrichIPAddressAsync(parsedIP, cancellationToken);
            CacheResult(cacheKey, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enrich IP address {IPAddress}", ipAddress);
            return IPEnrichmentResult.Failed(ipAddress, ex.Message);
        }
    }

    private async Task<IPEnrichmentResult> EnrichIPAddressAsync(IPAddress ipAddress, CancellationToken cancellationToken)
    {
        string? country = null;
        string? countryCode = null;
        string? city = null;
        double? latitude = null;
        double? longitude = null;
        int? asn = null;
        string? asnOrganization = null;

        // Get geographical information - try City database first, then Country database as fallback
        if (_cityReader != null)
        {
            try
            {
                var cityResponse = await Task.Run(() => _cityReader.City(ipAddress), cancellationToken);
                country = cityResponse.Country.Name;
                countryCode = cityResponse.Country.IsoCode;
                city = cityResponse.City.Name;
                latitude = (double?)cityResponse.Location.Latitude;
                longitude = (double?)cityResponse.Location.Longitude;

                if (_options.EnableDebugLogging)
                {
                    _logger.LogDebug("MaxMind City lookup for {IPAddress}: {Country}/{City}", 
                        ipAddress, country, city);
                }
            }
            catch (AddressNotFoundException)
            {
                // IP not found in City database - try Country database as fallback
                if (_countryReader != null)
                {
                    try
                    {
                        var countryResponse = await Task.Run(() => _countryReader.Country(ipAddress), cancellationToken);
                        country = countryResponse.Country.Name;
                        countryCode = countryResponse.Country.IsoCode;
                        
                        if (_options.EnableDebugLogging)
                        {
                            _logger.LogDebug("MaxMind Country lookup for {IPAddress}: {Country} (City database fallback)", 
                                ipAddress, country);
                        }
                    }
                    catch (AddressNotFoundException)
                    {
                        if (_options.EnableDebugLogging)
                            _logger.LogDebug("IP {IPAddress} not found in MaxMind City or Country databases", ipAddress);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error looking up country information for IP {IPAddress}", ipAddress);
                    }
                }
                else
                {
                    if (_options.EnableDebugLogging)
                        _logger.LogDebug("IP {IPAddress} not found in MaxMind City database", ipAddress);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error looking up city information for IP {IPAddress}", ipAddress);
            }
        }
        else if (_countryReader != null)
        {
            // If no City database, try Country database
            try
            {
                var countryResponse = await Task.Run(() => _countryReader.Country(ipAddress), cancellationToken);
                country = countryResponse.Country.Name;
                countryCode = countryResponse.Country.IsoCode;
                
                if (_options.EnableDebugLogging)
                {
                    _logger.LogDebug("MaxMind Country lookup for {IPAddress}: {Country}", 
                        ipAddress, country);
                }
            }
            catch (AddressNotFoundException)
            {
                if (_options.EnableDebugLogging)
                    _logger.LogDebug("IP {IPAddress} not found in MaxMind Country database", ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error looking up country information for IP {IPAddress}", ipAddress);
            }
        }

        // Get ASN information
        if (_asnReader != null)
        {
            try
            {
                var asnResponse = await Task.Run(() => _asnReader.Asn(ipAddress), cancellationToken);
                asn = (int?)asnResponse.AutonomousSystemNumber;
                asnOrganization = asnResponse.AutonomousSystemOrganization;

                if (_options.EnableDebugLogging)
                {
                    _logger.LogDebug("MaxMind ASN lookup for {IPAddress}: AS{ASN} {Organization}", 
                        ipAddress, asn, asnOrganization);
                }
            }
            catch (AddressNotFoundException)
            {
                // IP not found in database - this is normal for some IPs
                if (_options.EnableDebugLogging)
                    _logger.LogDebug("IP {IPAddress} not found in MaxMind ASN database", ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error looking up ASN information for IP {IPAddress}", ipAddress);
            }
        }

        // Determine risk factors
        var riskFactors = new List<string>();
        var isHighRisk = false;

        if (!string.IsNullOrEmpty(countryCode) && _options.HighRiskCountries.Contains(countryCode))
        {
            riskFactors.Add($"High-risk country: {country}");
            isHighRisk = true;
        }

        if (asn.HasValue && _options.HighRiskASNs.Contains(asn.Value))
        {
            riskFactors.Add($"High-risk ASN: AS{asn}");
            isHighRisk = true;
        }

        return IPEnrichmentResult.Success(
            ipAddress: ipAddress.ToString(),
            country: country,
            countryCode: countryCode,
            city: city,
            latitude: latitude,
            longitude: longitude,
            asn: asn,
            asnOrganization: asnOrganization,
            isHighRisk: isHighRisk,
            riskFactors: riskFactors,
            isPrivate: IsPrivateIP(ipAddress)
        );
    }

    public async Task<Dictionary<string, IPEnrichmentResult>> EnrichBatchAsync(IEnumerable<string> ipAddresses, CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentDictionary<string, IPEnrichmentResult>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2); // Limit concurrency

        var tasks = ipAddresses.Select(async ipAddress =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await EnrichAsync(ipAddress, cancellationToken);
                results.TryAdd(ipAddress, result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_options.Enabled)
            return false;

        try
        {
            // Test with a known public IP (Google DNS)
            var testResult = await EnrichAsync("8.8.8.8", cancellationToken);
            return testResult.IsEnriched;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPrivateIP(IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // IPv4 private ranges:
            // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 172 && (bytes[1] & 0xF0) == 16) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // IPv6 private ranges: fc00::/7, ::1/128
            return ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal || 
                   IPAddress.IsLoopback(ipAddress) || 
                   (bytes[0] & 0xFE) == 0xFC; // fc00::/7
        }

        return false;
    }

    private void CacheResult(string cacheKey, IPEnrichmentResult result)
    {
        if (_options.CacheMinutes > 0)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(_options.CacheMinutes / 2),
                Size = 1
            };
            
            _cache.Set(cacheKey, result, cacheOptions);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cityReader?.Dispose();
            _asnReader?.Dispose();
            _countryReader?.Dispose();
            _disposed = true;
        }
    }
}
