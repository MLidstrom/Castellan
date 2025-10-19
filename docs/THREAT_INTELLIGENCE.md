# Threat Intelligence Integration

Castellan now features **Tier 1 Threat Intelligence Integration** with external knowledge bases to significantly enhance malware detection capabilities beyond local heuristics.

## Overview


The threat intelligence system integrates with multiple external services to provide real-time malware detection and threat correlation. When scanning files, Castellan:

1. **Calculates file hashes** (MD5 and SHA256)
2. **Queries external threat intelligence** services for known threats (parallel processing)
3. **Falls back to local heuristics** if external services are unavailable
4. **Caches results** to improve performance and reduce API usage
5. **Provides detailed threat analysis** with confidence scores and risk levels

**Verified Integration**: All three Tier 1 services (VirusTotal, MalwareBazaar, AlienVault OTX) are successfully initialized and operational in the production system.

## Supported Services

### **Tier 1 Services (Implemented)**

#### **VirusTotal**
- **Purpose**: Comprehensive malware detection with 70+ antivirus engines
- **Features**: File hash reputation, malware family identification, detection confidence
- **API Key**: Required (free tier: 1,000 requests/day)
- **Get API Key**: [https://www.virustotal.com/gui/my-apikey](https://www.virustotal.com/gui/my-apikey)

#### **MalwareBazaar** (Abuse.ch)
- **Purpose**: Known malware sample database
- **Features**: Malware family classification, ClamAV signatures, first/last seen dates
- **API Key**: Not required (free public service)
- **Documentation**: [https://bazaar.abuse.ch/api/](https://bazaar.abuse.ch/api/)

#### **AlienVault OTX** (Open Threat Exchange)
- **Purpose**: Community-driven threat intelligence
- **Features**: Threat indicators, pulse information, malware families
- **API Key**: Required (free registration)
- **Get API Key**: [https://otx.alienvault.com/api](https://otx.alienvault.com/api)

### **Tier 2 Services (Planned)**
- YARA Engine Integration
- Microsoft Defender Threat Intelligence
- MISP Platform Integration

### **Tier 3 Services (Future)**
- CrowdStrike Falcon X
- Premium VirusTotal features
- Custom AI/ML models

## Configuration

### Environment Variables (Recommended)
```powershell
# Enable threat intelligence
$env:THREATINTELLIGENCE__ENABLED = "true"

# VirusTotal configuration
$env:THREATINTELLIGENCE__VIRUSTOTAL__ENABLED = "true"
$env:THREATINTELLIGENCE__VIRUSTOTAL__APIKEY = "your-virustotal-api-key"

# MalwareBazaar configuration (no API key needed)
$env:THREATINTELLIGENCE__MALWAREBAZAAR__ENABLED = "true"

# AlienVault OTX configuration  
$env:THREATINTELLIGENCE__ALIENVAULTOTX__ENABLED = "true"
$env:THREATINTELLIGENCE__ALIENVAULTOTX__APIKEY = "your-otx-api-key"
```

### appsettings.json Configuration
```json
{
  "ThreatIntelligence": {
    "Enabled": true,
    "VirusTotal": {
      "Enabled": true,
      "ApiKey": "your-virustotal-api-key",
      "BaseUrl": "https://www.virustotal.com/vtapi/v2/",
      "RateLimit": {
        "RequestsPerMinute": 4,
        "RequestsPerDay": 1000
      },
      "TimeoutSeconds": 30,
      "RetryAttempts": 3,
      "CacheExpiryHours": 24
    },
    "MalwareBazaar": {
      "Enabled": true,
      "BaseUrl": "https://mb-api.abuse.ch/api/v1/",
      "RateLimit": {
        "RequestsPerMinute": 10,
        "RequestsPerDay": 10000
      },
      "TimeoutSeconds": 15,
      "RetryAttempts": 3,
      "CacheExpiryHours": 12
    },
    "AlienVaultOTX": {
      "Enabled": true,
      "ApiKey": "your-otx-api-key", 
      "BaseUrl": "https://otx.alienvault.com/api/v1/",
      "RateLimit": {
        "RequestsPerMinute": 10,
        "RequestsPerDay": 1000
      },
      "TimeoutSeconds": 20,
      "RetryAttempts": 3,
      "CacheExpiryHours": 6
    },
    "Caching": {
      "Enabled": true,
      "MaxCacheSize": 10000,
      "DefaultCacheExpiryHours": 12
    },
    "FallbackBehavior": {
      "ContinueOnApiFailure": true,
      "MaxConcurrentApiCalls": 3,
      "CircuitBreakerEnabled": true
    }
  }
}
```

## Quick Setup

### 1. Get API Keys (Optional but Recommended)

#### VirusTotal API Key
1. Create free account at [VirusTotal](https://www.virustotal.com/)
2. Go to [My API Key](https://www.virustotal.com/gui/my-apikey)
3. Copy your API key

#### AlienVault OTX API Key
1. Create free account at [OTX](https://otx.alienvault.com/)
2. Go to [API Settings](https://otx.alienvault.com/api)
3. Copy your API key

### 2. Configure Castellan
```powershell
# Set environment variables
$env:THREATINTELLIGENCE__VIRUSTOTAL__APIKEY = "your-virustotal-api-key"
$env:THREATINTELLIGENCE__ALIENVAULTOTX__APIKEY = "your-otx-api-key"

# Restart Castellan
.\scripts\stop.ps1
.\scripts\start.ps1
```

### 3. Verify Integration
Check the logs for threat intelligence status:
```powershell
Get-Content src\Castellan.Worker\logs\*.log | Select-String "VirusTotal|MalwareBazaar|OTX" | Select-Object -Last 10
```

Look for successful initialization messages:
- `VirusTotal service initialized with base URL: https://www.virustotal.com/vtapi/v2/`
- `MalwareBazaar service initialized with base URL: https://mb-api.abuse.ch/api/v1/`
- `AlienVault OTX service initialized with base URL: https://otx.alienvault.com/api/v1/`
- `VirusTotal API key is not configured` (if not set - expected fallback behavior)

Alternatively, check the system status API:
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/system-status" | ConvertTo-Json -Depth 3
```
Look for "Threat Scanner" component showing as "Healthy".

## Performance & Features

### **Intelligent Query Strategy**
1. **Cache Check**: Check local cache first (instant response)
2. **Parallel Queries**: Query all enabled services simultaneously  
3. **Priority Results**: VirusTotal → MalwareBazaar → OTX → Local Heuristics
4. **Fallback Logic**: Graceful degradation if services are unavailable

### **Performance Optimizations**
- **Smart Caching**: Results cached with configurable TTL per service
- **Rate Limiting**: Respects API limits to prevent quota exhaustion
- **Connection Pooling**: Efficient HTTP connection reuse
- **Circuit Breaker**: Automatic failover on service failures
- **Exponential Backoff**: Intelligent retry logic for failed requests

### **Monitoring & Metrics**
- API response times logged for performance monitoring
- Cache hit rates and effectiveness tracking
- Rate limit status and quota usage monitoring
- Service health checks and availability metrics

## API Integration

### Threat Scanner Endpoints
The existing threat scanner APIs now include threat intelligence:

```bash
# Start async scan with threat intelligence
POST /api/threat-scanner/quick-scan?async=true

# Monitor progress (includes threat intel status)
GET /api/threat-scanner/progress

# View detailed results with threat intelligence data
GET /api/threat-scanner/last-result
```

### Sample Response with Threat Intelligence
```json
{
  "data": {
    "id": "scan-123",
    "status": "CompletedWithFindings",
    "filesScanned": 4799,
    "threatsFound": 1263,
    "threatDetails": [
      {
        "filePath": "C:\\temp\\malware.exe",
        "threatName": "Trojan.GenKryptik",
        "description": "Threat Intelligence: Detected by 45/70 engines",
        "riskLevel": "Critical",
        "confidence": 0.89,
        "threatType": "Trojan",
        "source": "VirusTotal"
      }
    ]
  }
}
```

## Security & Privacy

### Data Protection
- **No File Upload**: Only file hashes are sent to external services
- **Local Processing**: File content never leaves your system
- **Configurable Services**: Enable/disable services individually
- **Cache Control**: Configurable cache retention and cleanup

### Fallback Safety
- **Graceful Degradation**: System continues functioning without external services
- **No Dependencies**: Local heuristics always available as fallback
- **Error Isolation**: Service failures don't affect core scanning functionality

## Expected Results

### Detection Improvements
- **Reduced False Positives**: External validation reduces false alarms
- **Higher Confidence**: Multi-source validation increases accuracy
- **Malware Families**: Detailed threat classification and naming
- **Threat Attribution**: Link threats to known malware campaigns

### Performance Impact
- **First Query**: ~2-5 seconds (network dependent)
- **Cached Queries**: <100ms (local cache hit)
- **Parallel Processing**: Multiple services queried simultaneously
- **Background Processing**: Doesn't block other system operations

## Troubleshooting

### Common Issues

#### "VirusTotal API key is not configured"
- **Cause**: No API key set in configuration
- **Solution**: Set `THREATINTELLIGENCE__VIRUSTOTAL__APIKEY` environment variable
- **Impact**: Falls back to local heuristics (no functional impact)

#### "Rate limit exceeded"
- **Cause**: Too many API requests in short time
- **Solution**: Requests are automatically throttled and retried
- **Prevention**: Adjust rate limits in configuration if needed

#### "Request timeout" 
- **Cause**: Network connectivity issues or service downtime
- **Solution**: System automatically retries with exponential backoff
- **Fallback**: Local heuristics used if all retries fail

### Monitoring Commands
```powershell
# Check service health
Invoke-RestMethod -Uri "http://localhost:5000/api/threat-scanner/status" -Headers $headers

# View cache statistics (planned feature)
# Invoke-RestMethod -Uri "http://localhost:5000/api/threat-intelligence/cache/stats" -Headers $headers

# Check configuration
Get-Content C:\Users\matsl\Castellan\src\Castellan.Worker\appsettings.json | Select-String "ThreatIntelligence" -A 20
```

## API Reference

### ThreatIntelligenceResult
```csharp
public class ThreatIntelligenceResult
{
    public string Source { get; set; }           // "VirusTotal", "MalwareBazaar", "OTX"
    public bool IsKnownThreat { get; set; }      // True if threat detected
    public string ThreatName { get; set; }       // Malware family/name
    public ThreatRiskLevel RiskLevel { get; set; } // Low, Medium, High, Critical
    public float ConfidenceScore { get; set; }    // 0.0-1.0 confidence
    public string Description { get; set; }       // Human readable description
    public DateTime QueryTime { get; set; }       // When queried
    public bool FromCache { get; set; }           // True if from cache
}
```

### Service Health Check
```csharp
public interface IVirusTotalService
{
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    Task<RateLimitStatus> GetRateLimitStatusAsync();
    void ClearCache(string fileHash);
    void ClearAllCache();
}
```

---

## Summary

The Tier 1 Threat Intelligence Integration transforms Castellan from a local heuristics-based scanner into a **comprehensive threat detection platform** with:

- **Multi-source validation** from industry-leading threat intelligence providers
- **Intelligent caching** for optimal performance
- **Graceful fallback** ensuring reliability
- **Real-time monitoring** of service health and performance
- **Zero-impact integration** - existing functionality unchanged

This enhancement positions Castellan as an **enterprise-grade security solution** capable of detecting sophisticated threats while maintaining the performance and reliability expected from production systems.
