using System.Text.Json.Serialization;

namespace Castellan.Worker.Models.ThreatIntelligence;

// Base threat intelligence result
public class ThreatIntelligenceResult
{
    public string Source { get; set; } = string.Empty;
    public bool IsKnownThreat { get; set; }
    public string ThreatName { get; set; } = string.Empty;
    public ThreatRiskLevel RiskLevel { get; set; } = ThreatRiskLevel.Low;
    public float ConfidenceScore { get; set; } = 0.0f;
    public string Description { get; set; } = string.Empty;
    public DateTime QueryTime { get; set; } = DateTime.UtcNow;
    public bool FromCache { get; set; } = false;
}

// VirusTotal Models
public class VirusTotalResponse
{
    [JsonPropertyName("response_code")]
    public int ResponseCode { get; set; }

    [JsonPropertyName("verbose_msg")]
    public string VerboseMessage { get; set; } = string.Empty;

    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    [JsonPropertyName("scan_id")]
    public string ScanId { get; set; } = string.Empty;

    [JsonPropertyName("md5")]
    public string MD5 { get; set; } = string.Empty;

    [JsonPropertyName("sha1")]
    public string SHA1 { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string SHA256 { get; set; } = string.Empty;

    [JsonPropertyName("scan_date")]
    public string ScanDate { get; set; } = string.Empty;

    [JsonPropertyName("positives")]
    public int PositiveScans { get; set; }

    [JsonPropertyName("total")]
    public int TotalScans { get; set; }

    [JsonPropertyName("permalink")]
    public string Permalink { get; set; } = string.Empty;

    [JsonPropertyName("scans")]
    public Dictionary<string, VirusTotalScanResult> Scans { get; set; } = new();
}

public class VirusTotalScanResult
{
    [JsonPropertyName("detected")]
    public bool Detected { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("update")]
    public string Update { get; set; } = string.Empty;
}

public class VirusTotalResult : ThreatIntelligenceResult
{
    public int PositiveScans { get; set; }
    public int TotalScans { get; set; }
    public string Permalink { get; set; } = string.Empty;
    public List<string> DetectedBy { get; set; } = new();
}

// MalwareBazaar Models
public class MalwareBazaarResponse
{
    [JsonPropertyName("query_status")]
    public string QueryStatus { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<MalwareBazaarData> Data { get; set; } = new();
}

public class MalwareBazaarData
{
    [JsonPropertyName("sha256_hash")]
    public string SHA256Hash { get; set; } = string.Empty;

    [JsonPropertyName("md5_hash")]
    public string MD5Hash { get; set; } = string.Empty;

    [JsonPropertyName("sha1_hash")]
    public string SHA1Hash { get; set; } = string.Empty;

    [JsonPropertyName("first_seen")]
    public string FirstSeen { get; set; } = string.Empty;

    [JsonPropertyName("last_seen")]
    public string LastSeen { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("file_size")]
    public long FileSize { get; set; }

    [JsonPropertyName("file_type")]
    public string FileType { get; set; } = string.Empty;

    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;

    [JsonPropertyName("clamav")]
    public string ClamAV { get; set; } = string.Empty;

    [JsonPropertyName("vtpercent")]
    public int VTPercent { get; set; }

    [JsonPropertyName("imphash")]
    public string ImpHash { get; set; } = string.Empty;

    [JsonPropertyName("ssdeep")]
    public string SSDeep { get; set; } = string.Empty;

    [JsonPropertyName("tlsh")]
    public string TLSH { get; set; } = string.Empty;
}

public class MalwareBazaarResult : ThreatIntelligenceResult
{
    public string FirstSeen { get; set; } = string.Empty;
    public string LastSeen { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string ClamAV { get; set; } = string.Empty;
    public int VTPercent { get; set; }
}

// AlienVault OTX Models
public class OTXResponse
{
    [JsonPropertyName("indicator")]
    public string Indicator { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("type_title")]
    public string TypeTitle { get; set; } = string.Empty;

    [JsonPropertyName("base_indicator")]
    public OTXBaseIndicator BaseIndicator { get; set; } = new();

    [JsonPropertyName("pulse_info")]
    public OTXPulseInfo PulseInfo { get; set; } = new();

    [JsonPropertyName("false_positive")]
    public List<object> FalsePositive { get; set; } = new();

    [JsonPropertyName("validation")]
    public List<object> Validation { get; set; } = new();

    [JsonPropertyName("sections")]
    public List<string> Sections { get; set; } = new();
}

public class OTXBaseIndicator
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("indicator")]
    public string Indicator { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("access_type")]
    public string AccessType { get; set; } = string.Empty;

    [JsonPropertyName("access_reason")]
    public string AccessReason { get; set; } = string.Empty;
}

public class OTXPulseInfo
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("pulses")]
    public List<OTXPulse> Pulses { get; set; } = new();

    [JsonPropertyName("references")]
    public List<string> References { get; set; } = new();

    [JsonPropertyName("related")]
    public OTXRelated Related { get; set; } = new();
}

public class OTXPulse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("author_name")]
    public string AuthorName { get; set; } = string.Empty;

    [JsonPropertyName("modified")]
    public string Modified { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("malware_families")]
    public List<string> MalwareFamilies { get; set; } = new();

    [JsonPropertyName("attack_ids")]
    public List<string> AttackIds { get; set; } = new();

    [JsonPropertyName("industries")]
    public List<string> Industries { get; set; } = new();

    [JsonPropertyName("TLP")]
    public string TLP { get; set; } = string.Empty;
}

public class OTXRelated
{
    [JsonPropertyName("alienvault")]
    public OTXAlienVaultData AlienVault { get; set; } = new();

    [JsonPropertyName("other")]
    public OTXOtherData Other { get; set; } = new();
}

public class OTXAlienVaultData
{
    [JsonPropertyName("malware_families")]
    public List<string> MalwareFamilies { get; set; } = new();

    [JsonPropertyName("industries")]
    public List<string> Industries { get; set; } = new();
}

public class OTXOtherData
{
    [JsonPropertyName("malware_families")]
    public List<string> MalwareFamilies { get; set; } = new();

    [JsonPropertyName("industries")]
    public List<string> Industries { get; set; } = new();
}

public class OTXResult : ThreatIntelligenceResult
{
    public int PulseCount { get; set; }
    public List<string> MalwareFamilies { get; set; } = new();
    public List<string> AttackIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string AuthorName { get; set; } = string.Empty;
}

// Configuration Models
public class ThreatIntelligenceOptions
{
    public bool Enabled { get; set; } = true;
    public VirusTotalOptions VirusTotal { get; set; } = new();
    public MalwareBazaarOptions MalwareBazaar { get; set; } = new();
    public AlienVaultOTXOptions AlienVaultOTX { get; set; } = new();
    public CachingOptions Caching { get; set; } = new();
    public FallbackBehaviorOptions FallbackBehavior { get; set; } = new();
}

public class VirusTotalOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://www.virustotal.com/vtapi/v2/";
    public RateLimitOptions RateLimit { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryAttempts { get; set; } = 3;
    public int CacheExpiryHours { get; set; } = 24;
}

public class MalwareBazaarOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://mb-api.abuse.ch/api/v1/";
    public RateLimitOptions RateLimit { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 15;
    public int RetryAttempts { get; set; } = 3;
    public int CacheExpiryHours { get; set; } = 12;
}

public class AlienVaultOTXOptions
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://otx.alienvault.com/api/v1/";
    public RateLimitOptions RateLimit { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 20;
    public int RetryAttempts { get; set; } = 3;
    public int CacheExpiryHours { get; set; } = 6;
}

public class RateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 10;
    public int RequestsPerDay { get; set; } = 1000;
    public int RequestsPerMonth { get; set; } = 10000;
}

public class CachingOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxCacheSize { get; set; } = 10000;
    public int DefaultCacheExpiryHours { get; set; } = 12;
}

public class FallbackBehaviorOptions
{
    public bool ContinueOnApiFailure { get; set; } = true;
    public int MaxConcurrentApiCalls { get; set; } = 3;
    public bool CircuitBreakerEnabled { get; set; } = true;
    public int CircuitBreakerThreshold { get; set; } = 5;
}

// Cache entry model
public class ThreatIntelligenceCacheEntry
{
    public string Hash { get; set; } = string.Empty;
    public ThreatIntelligenceResult Result { get; set; } = new();
    public DateTime ExpiryTime { get; set; }
    public string Source { get; set; } = string.Empty;
}
