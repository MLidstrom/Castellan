# Castellan YARA Integration Guide (v0.4.0)

## Overview

The Castellan YARA integration provides advanced malware detection capabilities using the YARA rule engine. This integration includes rule management, real-time scanning, and comprehensive analytics through both API endpoints and a React Admin dashboard.

## Architecture

### Components

1. **YaraScanService** - Background service for YARA rule compilation and scanning
2. **FileBasedYaraRuleStore** - Persistent storage for YARA rules
3. **YaraRulesController** - RESTful API endpoints for rule management
4. **YaraDashboardWidget** - React Admin UI component for monitoring
5. **YaraSummaryCard** - Dashboard summary component

### Technology Stack

- **Backend**: ASP.NET Core 8.0 with dnYara library
- **Frontend**: React Admin with Material-UI components
- **Storage**: File-based JSON storage with SQLite metadata
- **Authentication**: JWT Bearer tokens
- **Real-time Updates**: SignalR integration

## Configuration

### appsettings.json Configuration

```json
{
  "YaraScanning": {
    "Enabled": true,
    "MaxFileSizeMB": 100,
    "ScanTimeoutSeconds": 30,
    "MaxConcurrentScans": 4,
    "AutoScanSecurityEvents": true,
    "MinThreatLevel": "Medium",
    "Compilation": {
      "EnableFastMatching": true,
      "MaxMemoryMB": 256,
      "PrecompileOnStartup": true,
      "RefreshIntervalMinutes": 60
    },
    "Performance": {
      "EnableMetrics": true,
      "SlowScanThresholdSeconds": 10,
      "ThreadsPerCore": 1,
      "StreamBufferSizeKB": 64
    }
  }
}
```

### Configuration Parameters

| Parameter | Description | Default | Security Impact |
|-----------|-------------|---------|-----------------|
| `Enabled` | Enable YARA scanning service | `true` | Service availability |
| `MaxFileSizeMB` | Maximum file size for scanning | `100` | DoS prevention |
| `ScanTimeoutSeconds` | Timeout for individual scans | `30` | Resource management |
| `MaxConcurrentScans` | Maximum concurrent scans | `4` | Resource limits |
| `AutoScanSecurityEvents` | Auto-scan security events | `true` | Automated protection |
| `MinThreatLevel` | Minimum threat level to process | `Medium` | Noise reduction |
| `EnableFastMatching` | Enable YARA fast matching | `true` | Performance optimization |
| `MaxMemoryMB` | Maximum memory for YARA | `256` | Memory limits |
| `PrecompileOnStartup` | Compile rules on startup | `true` | Startup performance |
| `RefreshIntervalMinutes` | Rule refresh frequency | `60` | Rule freshness |

## API Reference

### Authentication

All API endpoints require JWT authentication:

```http
Authorization: Bearer <your-jwt-token>
```

### Endpoints

#### 1. List Rules

```http
GET /api/yara-rules
```

**Query Parameters:**
- `category` (string): Filter by category
- `tag` (string): Filter by tag
- `mitreTechnique` (string): Filter by MITRE technique
- `enabled` (boolean): Filter by enabled status
- `page` (int): Page number (default: 1)
- `perPage` (int): Items per page (default: 10)

**Response:**
```json
{
  "data": [
    {
      "id": "rule-uuid",
      "name": "Rule_Name",
      "description": "Rule description",
      "ruleContent": "rule Rule_Name { ... }",
      "category": "Malware",
      "author": "Security Team",
      "createdAt": "2025-09-11T15:00:00Z",
      "updatedAt": "2025-09-11T15:00:00Z",
      "isEnabled": true,
      "priority": 50,
      "threatLevel": "High",
      "hitCount": 10,
      "falsePositiveCount": 1,
      "averageExecutionTimeMs": 5.2,
      "mitreTechniques": ["T1059.001"],
      "tags": ["powershell", "suspicious"],
      "isValid": true,
      "validationError": null,
      "source": "Custom"
    }
  ],
  "total": 25,
  "page": 1,
  "perPage": 10
}
```

#### 2. Get Rule by ID

```http
GET /api/yara-rules/{id}
```

**Response:**
```json
{
  "data": { /* Rule object */ }
}
```

#### 3. Create Rule

```http
POST /api/yara-rules
```

**Request Body:**
```json
{
  "name": "Custom_Rule_Name",
  "description": "Rule description",
  "ruleContent": "rule Custom_Rule_Name {\n  meta:\n    description = \"Custom rule\"\n  strings:\n    $a = \"suspicious_string\"\n  condition:\n    $a\n}",
  "category": "Custom",
  "threatLevel": "Medium",
  "tags": ["custom", "test"],
  "mitreTechniques": ["T1059"],
  "isEnabled": true,
  "priority": 50
}
```

**Response:**
```json
{
  "data": { /* Created rule object */ }
}
```

#### 4. Update Rule

```http
PUT /api/yara-rules/{id}
```

**Request Body:** Same as create rule

#### 5. Delete Rule

```http
DELETE /api/yara-rules/{id}
```

**Response:**
```json
{
  "message": "Rule deleted successfully"
}
```

#### 6. Scan Content

```http
POST /api/yara-rules/scan
```

**Request Body (File Path):**
```json
{
  "filePath": "C:\\path\\to\\file.exe"
}
```

**Request Body (Base64 Content):**
```json
{
  "content": "SGVsbG8gV29ybGQ=" // Base64 encoded content
}
```

**Response:**
```json
{
  "data": {
    "fileName": "file.exe",
    "scanTime": "2025-09-11T15:30:00Z",
    "matchCount": 2,
    "matches": [
      {
        "ruleId": "rule-uuid-1",
        "ruleName": "Suspicious_PowerShell",
        "matchedStrings": ["Invoke-Expression"],
        "offset": 150,
        "length": 17,
        "threatLevel": "High",
        "confidence": 0.95
      }
    ]
  }
}
```

#### 7. Get Service Status

```http
GET /api/yara-rules/status
```

**Response:**
```json
{
  "isAvailable": true,
  "isHealthy": true,
  "error": null,
  "compiledRules": 25
}
```

#### 8. Get Rule Categories

```http
GET /api/yara-rules/categories
```

**Response:**
```json
{
  "data": [
    { "name": "Malware", "count": 15 },
    { "name": "Ransomware", "count": 8 },
    { "name": "Trojan", "count": 12 }
  ]
}
```

#### 9. Test Rule Syntax

```http
POST /api/yara-rules/test
```

**Request Body:**
```json
{
  "ruleContent": "rule Test_Rule { condition: true }"
}
```

**Response:**
```json
{
  "isValid": true,
  "errors": []
}
```

## Frontend Integration

### Dashboard Components

#### YaraDashboardWidget

The main YARA dashboard widget displays:
- Rule statistics (total, enabled, valid)
- Health status and compilation metrics
- Performance indicators
- Recent matches and alerts
- Top performing rules

**Usage in Dashboard:**
```tsx
import { YaraDashboardWidget } from './YaraDashboardWidget';

// In dashboard component
<YaraDashboardWidget />
```

#### YaraSummaryCard

A compact summary card for KPI display:
```tsx
import { YaraSummaryCard } from './YaraSummaryCard';

// In dashboard component
<YaraSummaryCard />
```

### YARA Rules Management Page

The React Admin resource for managing YARA rules provides:
- Rule listing with filters and search
- Rule creation and editing forms
- Bulk operations (enable/disable/delete)
- Import/export functionality
- Rule validation and testing

**Resource Configuration:**
```tsx
import { YaraRulesList, YaraRulesEdit, YaraRulesCreate, YaraRulesShow } from './resources/YaraRules';

// In App.tsx
<Resource 
  name="yara-rules" 
  list={YaraRulesList} 
  edit={YaraRulesEdit} 
  create={YaraRulesCreate} 
  show={YaraRulesShow} 
/>
```

## Usage Examples

### Creating a Simple Detection Rule

```yara
rule Suspicious_PowerShell_Download {
    meta:
        description = "Detects suspicious PowerShell download patterns"
        author = "Security Team"
        date = "2025-09-11"
        threat_level = "high"
        
    strings:
        $download1 = "DownloadString" nocase
        $download2 = "WebClient" nocase
        $exec1 = "Invoke-Expression" nocase
        $exec2 = "IEX" nocase
        
    condition:
        any of ($download*) and any of ($exec*)
}
```

### Scanning a File via API

```bash
curl -X POST "http://localhost:5000/api/yara-rules/scan" \
  -H "Authorization: Bearer your-jwt-token" \
  -H "Content-Type: application/json" \
  -d '{
    "filePath": "C:\\suspicious\\file.exe"
  }'
```

### Bulk Operations

```bash
# Bulk enable rules
curl -X POST "http://localhost:5000/api/yara-rules/bulk" \
  -H "Authorization: Bearer your-jwt-token" \
  -H "Content-Type: application/json" \
  -d '{
    "ruleIds": ["uuid1", "uuid2", "uuid3"],
    "operation": "enable"
  }'
```

## Security Considerations

### Authentication and Authorization

- All API endpoints require valid JWT authentication
- JWT tokens expire after 24 hours (configurable)
- Invalid tokens return 401 Unauthorized
- Malformed requests return 400 Bad Request

### Input Validation

- YARA rule syntax validation before storage
- File size limits (100MB default) to prevent DoS
- Scan timeouts (30s default) to prevent resource exhaustion
- SQL injection protection on all query parameters

### Resource Management

- Maximum concurrent scans limited (4 default)
- Memory limits for YARA compilation (256MB default)
- Rule compilation timeout protection
- Automatic cleanup of temporary scan files

### File Access Security

- File path validation (when implemented)
- Sandboxed scanning environment recommended
- Read-only access to scan targets
- Temporary file cleanup after scanning

## Performance Optimization

### Rule Compilation

- Rules are precompiled on service startup
- Background refresh every 60 minutes
- Fast matching enabled for performance
- Invalid rules excluded from compilation

### Scanning Performance

- Concurrent scanning with semaphore limits
- Stream-based processing for large files
- Configurable buffer sizes (64KB default)
- Performance metrics collection

### Caching Strategy

- Compiled rules cached in memory
- Scan results cached for duplicate content
- Rule metadata cached with TTL
- Database query result caching

## Troubleshooting

### Common Issues

#### 1. Rules Not Compiling

**Symptoms:** New rules not appearing in scans
**Solution:** Check rule syntax and restart service

```bash
# Check service status
curl -H "Authorization: Bearer token" http://localhost:5000/api/yara-rules/status

# Test rule syntax
curl -X POST -H "Authorization: Bearer token" \
  -d '{"ruleContent":"rule Test { condition: true }"}' \
  http://localhost:5000/api/yara-rules/test
```

#### 2. Scan Timeouts

**Symptoms:** Large files cause scan failures
**Solution:** Increase scan timeout or reduce file size

```json
{
  "YaraScanning": {
    "ScanTimeoutSeconds": 60,
    "MaxFileSizeMB": 50
  }
}
```

#### 3. Memory Issues

**Symptoms:** Service crashes during rule compilation
**Solution:** Increase memory limits or reduce rule count

```json
{
  "YaraScanning": {
    "Compilation": {
      "MaxMemoryMB": 512
    }
  }
}
```

#### 4. High False Positives

**Symptoms:** Too many irrelevant matches
**Solution:** Increase threat level threshold

```json
{
  "YaraScanning": {
    "MinThreatLevel": "High"
  }
}
```

### Logging and Monitoring

#### Key Log Messages

- `YARA Scan Service initialized` - Service startup
- `Refreshing YARA rules...` - Rule compilation start
- `Successfully compiled N YARA rules` - Compilation success
- `YARA rule validation failed` - Rule syntax error
- `YARA compilation error` - Compilation failure

#### Health Check Endpoints

```bash
# Service health
curl -H "Authorization: Bearer token" http://localhost:5000/api/yara-rules/status

# System health  
curl -H "Authorization: Bearer token" http://localhost:5000/api/yara-rules/health
```

#### Performance Metrics

Monitor these metrics in production:
- Rule compilation time
- Scan execution time
- False positive rate
- Memory usage
- Concurrent scan count

## Deployment

### Prerequisites

- .NET 8.0 Runtime
- dnYara library dependencies
- Sufficient memory (512MB+ recommended)
- File system write access for rule storage

### Environment Variables

```bash
# Optional: Override default settings
YARASCAN__ENABLED=true
YARASCAN__MAXFILESSIZEMB=100
YARASCAN__SCANTIMEOUTSECONDS=30
YARASCAN__MAXCONCURRENTSCANS=4
```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY . /app
WORKDIR /app
EXPOSE 5000
ENTRYPOINT ["dotnet", "Castellan.Worker.dll"]
```

### Production Checklist

- [ ] Configure resource limits appropriately
- [ ] Set up log monitoring and alerting
- [ ] Implement rule backup and versioning
- [ ] Test rule compilation performance
- [ ] Validate file access permissions
- [ ] Configure authentication secrets
- [ ] Set up health monitoring
- [ ] Plan rule update procedures

## Support and Maintenance

### Regular Tasks

1. **Rule Updates** - Review and update rules monthly
2. **Performance Review** - Monitor scan times weekly
3. **False Positive Analysis** - Review matches weekly
4. **Log Analysis** - Check error logs daily
5. **Health Monitoring** - Verify service status hourly

### Backup Procedures

```bash
# Backup rule store
cp -r /app/data/yara-rules /backup/yara-rules-$(date +%Y%m%d)

# Export rules via API
curl -H "Authorization: Bearer token" \
  "http://localhost:5000/api/yara-rules/export?format=json" \
  > rules-backup-$(date +%Y%m%d).json
```

### Version Compatibility

| Castellan Version | YARA Version | dnYara Version | Status |
|-------------------|--------------|----------------|---------|
| 0.4.0 | 4.2+ | Latest | ✅ Active |
| 0.3.x | N/A | N/A | ❌ Not supported |

---

*This documentation is for Castellan v0.4.0 YARA integration. For the latest updates, check the project repository.*
