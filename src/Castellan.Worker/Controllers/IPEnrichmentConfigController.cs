using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Castellan.Worker.Models;
using System.Text.Json;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/settings/ip-enrichment")]
[Authorize]
public class IPEnrichmentConfigController : ControllerBase
{
    private readonly ILogger<IPEnrichmentConfigController> _logger;
    private readonly IOptionsMonitor<IPEnrichmentOptions> _ipEnrichmentOptions;
    private readonly string _configFilePath;

    public IPEnrichmentConfigController(
        ILogger<IPEnrichmentConfigController> logger,
        IOptionsMonitor<IPEnrichmentOptions> ipEnrichmentOptions)
    {
        _logger = logger;
        _ipEnrichmentOptions = ipEnrichmentOptions;
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "ip-enrichment-config.json");

        // Ensure data directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath)!);
    }

    [HttpGet]
    public async Task<IActionResult> GetConfiguration()
    {
        try
        {
            _logger.LogInformation("Getting IP enrichment configuration");

            IPEnrichmentConfigDto config;

            // Try to load from file first
            if (System.IO.File.Exists(_configFilePath))
            {
                var json = await System.IO.File.ReadAllTextAsync(_configFilePath);
                config = JsonSerializer.Deserialize<IPEnrichmentConfigDto>(json) ?? GetDefaultConfiguration();
            }
            else
            {
                // Use default configuration from appsettings
                config = GetDefaultConfiguration();
            }

            return Ok(new { data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP enrichment configuration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateConfiguration([FromBody] IPEnrichmentConfigDto config)
    {
        try
        {
            _logger.LogInformation("Updating IP enrichment configuration");

            if (config == null)
            {
                return BadRequest(new { message = "Configuration data is required" });
            }

            // Add ID if not present
            if (string.IsNullOrEmpty(config.Id))
            {
                config.Id = "ip-enrichment";
            }

            // Validate configuration
            var validationResult = ValidateConfiguration(config);
            if (validationResult != null)
            {
                return BadRequest(validationResult);
            }

            // Save to file
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(_configFilePath, json);

            _logger.LogInformation("IP enrichment configuration updated successfully");

            return Ok(new { data = config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP enrichment configuration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost("download-databases")]
    public async Task<IActionResult> DownloadMaxMindDatabases([FromBody] DownloadDatabasesRequest request)
    {
        try
        {
            _logger.LogInformation("Starting MaxMind database download");

            if (string.IsNullOrEmpty(request.LicenseKey) || string.IsNullOrEmpty(request.AccountId))
            {
                return BadRequest(new { message = "License key and account ID are required for database download" });
            }

            var downloadedDatabases = new List<string>();
            var failedDownloads = new List<string>();

            // Ensure data directory exists
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            Directory.CreateDirectory(dataDirectory);

            // MaxMind database download URLs (permalinks)
            var databases = new Dictionary<string, string>
            {
                { "GeoLite2-City", "https://download.maxmind.com/geoip/databases/GeoLite2-City/download?suffix=tar.gz" },
                { "GeoLite2-Country", "https://download.maxmind.com/geoip/databases/GeoLite2-Country/download?suffix=tar.gz" },
                { "GeoLite2-ASN", "https://download.maxmind.com/geoip/databases/GeoLite2-ASN/download?suffix=tar.gz" }
            };

            using var httpClient = new HttpClient();

            // Set up Basic Authentication
            var authString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{request.AccountId}:{request.LicenseKey}"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

            // Set timeout for downloads
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            foreach (var database in databases)
            {
                try
                {
                    _logger.LogInformation($"Downloading {database.Key} from MaxMind");

                    var response = await httpClient.GetAsync(database.Value);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsByteArrayAsync();

                        // Save compressed file temporarily
                        var tempFile = Path.Combine(dataDirectory, $"{database.Key}.tar.gz");
                        await System.IO.File.WriteAllBytesAsync(tempFile, content);

                        // Extract .mmdb file from tar.gz
                        var mmdbPath = ExtractMmdbFromTarGz(tempFile, dataDirectory, database.Key);

                        if (!string.IsNullOrEmpty(mmdbPath) && System.IO.File.Exists(mmdbPath))
                        {
                            downloadedDatabases.Add(database.Key);
                            _logger.LogInformation($"Successfully downloaded and extracted {database.Key} to {mmdbPath}");
                        }
                        else
                        {
                            failedDownloads.Add($"{database.Key} (extraction failed)");
                            _logger.LogError($"Failed to extract {database.Key}");
                        }

                        // Clean up temporary file
                        if (System.IO.File.Exists(tempFile))
                        {
                            System.IO.File.Delete(tempFile);
                        }
                    }
                    else
                    {
                        failedDownloads.Add($"{database.Key} (HTTP {response.StatusCode})");
                        _logger.LogError($"Failed to download {database.Key}: HTTP {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
                catch (Exception ex)
                {
                    failedDownloads.Add($"{database.Key} ({ex.Message})");
                    _logger.LogError(ex, $"Error downloading {database.Key}");
                }
            }

            var result = new DownloadDatabasesResponse
            {
                Success = downloadedDatabases.Count > 0,
                Message = downloadedDatabases.Count > 0
                    ? $"Successfully downloaded {downloadedDatabases.Count} database(s). {(failedDownloads.Count > 0 ? $"{failedDownloads.Count} failed." : "")}"
                    : "All database downloads failed. Please check your credentials and try again.",
                DownloadedDatabases = downloadedDatabases,
                FailedDatabases = failedDownloads,
                DownloadTime = DateTime.UtcNow
            };

            return Ok(new { data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading MaxMind databases");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private IPEnrichmentConfigDto GetDefaultConfiguration()
    {
        var options = _ipEnrichmentOptions.CurrentValue;

        return new IPEnrichmentConfigDto
        {
            Id = "ip-enrichment",
            Enabled = options.Enabled,
            Provider = options.Provider,
            MaxMind = new MaxMindConfigDto
            {
                LicenseKey = "",
                AccountId = "",
                AutoUpdate = false,
                UpdateFrequencyDays = 7,
                LastUpdate = null,
                DatabasePaths = new DatabasePathsDto
                {
                    City = options.MaxMindCityDbPath ?? "data/GeoLite2-City.mmdb",
                    Asn = options.MaxMindASNDbPath ?? "data/GeoLite2-ASN.mmdb",
                    Country = options.MaxMindCountryDbPath ?? "data/GeoLite2-Country.mmdb"
                }
            },
            IpInfo = new IpInfoConfigDto
            {
                ApiKey = options.IPInfoApiKey ?? ""
            },
            Enrichment = new EnrichmentSettingsDto
            {
                CacheMinutes = options.CacheMinutes,
                MaxCacheEntries = options.MaxCacheEntries,
                TimeoutMs = options.TimeoutMs,
                EnrichPrivateIPs = options.EnrichPrivateIPs
            },
            HighRiskCountries = options.HighRiskCountries,
            HighRiskASNs = options.HighRiskASNs,
            EnableDebugLogging = options.EnableDebugLogging
        };
    }

    private object? ValidateConfiguration(IPEnrichmentConfigDto config)
    {
        var errors = new List<string>();

        // Validate provider
        var validProviders = new[] { "MaxMind", "IPInfo", "Disabled" };
        if (!validProviders.Contains(config.Provider))
        {
            errors.Add($"Provider must be one of: {string.Join(", ", validProviders)}");
        }

        // Validate MaxMind configuration if MaxMind provider is selected
        if (config.Provider == "MaxMind" && config.MaxMind != null)
        {
            if (config.MaxMind.UpdateFrequencyDays < 1 || config.MaxMind.UpdateFrequencyDays > 365)
            {
                errors.Add("Update frequency must be between 1 and 365 days");
            }
        }

        // Validate enrichment settings
        if (config.Enrichment != null)
        {
            if (config.Enrichment.CacheMinutes < 0 || config.Enrichment.CacheMinutes > 10080) // Max 1 week
            {
                errors.Add("Cache minutes must be between 0 and 10080 (1 week)");
            }

            if (config.Enrichment.MaxCacheEntries < 100 || config.Enrichment.MaxCacheEntries > 1000000)
            {
                errors.Add("Max cache entries must be between 100 and 1,000,000");
            }

            if (config.Enrichment.TimeoutMs < 1000 || config.Enrichment.TimeoutMs > 60000)
            {
                errors.Add("Timeout must be between 1,000ms and 60,000ms");
            }
        }

        // Validate high-risk countries (should be 2-letter ISO codes)
        if (config.HighRiskCountries != null)
        {
            foreach (var country in config.HighRiskCountries)
            {
                if (string.IsNullOrEmpty(country) || country.Length != 2 || !country.All(char.IsUpper))
                {
                    errors.Add($"Invalid country code: {country}. Must be 2-letter uppercase ISO code.");
                    break; // Only show first error to avoid spam
                }
            }
        }

        return errors.Any() ? new { message = "Validation failed", errors } : null;
    }

    private string? ExtractMmdbFromTarGz(string tarGzPath, string extractDirectory, string databaseName)
    {
        try
        {
            using var fileStream = System.IO.File.OpenRead(tarGzPath);
            using var gzipStream = new GZipInputStream(fileStream);
            using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, System.Text.Encoding.UTF8);

            var targetPath = Path.Combine(extractDirectory, $"{databaseName}.mmdb");

            // Extract the archive to a temporary directory first
            var tempExtractDir = Path.Combine(extractDirectory, "temp_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                tarArchive.ExtractContents(tempExtractDir);

                // Find the .mmdb file in the extracted contents
                var mmdbFiles = Directory.GetFiles(tempExtractDir, "*.mmdb", SearchOption.AllDirectories);

                if (mmdbFiles.Length > 0)
                {
                    // Move the first .mmdb file found to the target location
                    System.IO.File.Move(mmdbFiles[0], targetPath, true);
                    return targetPath;
                }

                _logger.LogWarning($"No .mmdb file found in {databaseName} archive");
                return null;
            }
            finally
            {
                // Clean up temporary directory
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting {databaseName} from tar.gz");
            return null;
        }
    }
}

// DTOs for API responses
public class IPEnrichmentConfigDto
{
    public string Id { get; set; } = "ip-enrichment";
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "MaxMind";
    public MaxMindConfigDto MaxMind { get; set; } = new();
    public IpInfoConfigDto IpInfo { get; set; } = new();
    public EnrichmentSettingsDto Enrichment { get; set; } = new();
    public List<string> HighRiskCountries { get; set; } = new();
    public List<int> HighRiskASNs { get; set; } = new();
    public bool EnableDebugLogging { get; set; } = false;
}

public class MaxMindConfigDto
{
    public string LicenseKey { get; set; } = "";
    public string AccountId { get; set; } = "";
    public bool AutoUpdate { get; set; } = false;
    public int UpdateFrequencyDays { get; set; } = 7;
    public DateTime? LastUpdate { get; set; }
    public DatabasePathsDto DatabasePaths { get; set; } = new();
}

public class DatabasePathsDto
{
    public string City { get; set; } = "data/GeoLite2-City.mmdb";
    public string Asn { get; set; } = "data/GeoLite2-ASN.mmdb";
    public string Country { get; set; } = "data/GeoLite2-Country.mmdb";
}

public class IpInfoConfigDto
{
    public string ApiKey { get; set; } = "";
}

public class EnrichmentSettingsDto
{
    public int CacheMinutes { get; set; } = 60;
    public int MaxCacheEntries { get; set; } = 10000;
    public int TimeoutMs { get; set; } = 5000;
    public bool EnrichPrivateIPs { get; set; } = false;
}

public class DownloadDatabasesRequest
{
    public string LicenseKey { get; set; } = "";
    public string AccountId { get; set; } = "";
}

public class DownloadDatabasesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public List<string> DownloadedDatabases { get; set; } = new();
    public List<string> FailedDatabases { get; set; } = new();
    public DateTime DownloadTime { get; set; }
}