# Castellan API Documentation

**Status**: ✅ **Production Ready**  
**Last Updated**: September 6, 2025  
**API Version**: v1

## 🎯 Overview

Castellan provides a comprehensive REST API for security monitoring, threat analysis, and system management. This documentation covers all available endpoints, authentication, and integration patterns.

## 🔐 Authentication

### JWT Token Authentication
All API endpoints require JWT authentication with proper token management.

```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "your-secure-password"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-09-06T08:30:00Z",
  "tokenType": "Bearer"
}
```

### Token Usage
```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Token Refresh
```http
POST /api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

## 📊 System Status API

### Get System Status
```http
GET /api/system-status
```

**Response:**
```json
{
  "data": [
    {
      "id": "1",
      "component": "Qdrant Vector Database",
      "status": "Healthy",
      "isHealthy": true,
      "lastCheck": "2025-09-06T07:14:01.6007424Z",
      "responseTime": 5,
      "uptime": "99.9%",
      "details": "Connected on localhost:6333, vector operations nominal",
      "errorCount": 0,
      "warningCount": 0
    },
    {
      "id": "8",
      "component": "Qdrant Connection Pool",
      "status": "Healthy",
      "isHealthy": true,
      "lastCheck": "2025-09-06T07:14:01.6460605Z",
      "responseTime": 0,
      "uptime": "100%",
      "details": "2/2 instances healthy, Active connections: 0, Pool utilization: 0,0%",
      "errorCount": 0,
      "warningCount": 0
    }
  ],
  "total": 8,
  "page": 1,
  "perPage": 10
}
```

### Get Specific Component Status
```http
GET /api/system-status/{componentId}
```

## 🚨 Security Events API

### List Security Events
```http
GET /api/security-events?page=1&limit=50&riskLevel=high
```

**Query Parameters:**
- `page` - Page number (default: 1)
- `limit` - Items per page (default: 50, max: 100)
- `riskLevel` - Filter by risk level: `critical`, `high`, `medium`, `low`
- `eventType` - Filter by event type
- `dateFrom` - Start date filter (ISO 8601)
- `dateTo` - End date filter (ISO 8601)
- `sourceIP` - Filter by source IP address
- `user` - Filter by username
- `computer` - Filter by computer name

**Response:**
```json
{
  "data": [
    {
      "id": 1,
      "eventId": 4625,
      "eventType": "AuthenticationFailure",
      "riskLevel": "critical",
      "correlationScore": 0.95,
      "confidenceScore": 92,
      "sourceIP": "203.0.113.45",
      "destinationIP": "192.168.1.100",
      "user": "SYSTEM\\administrator",
      "computer": "WIN-SERVER01",
      "process": "lsass.exe",
      "commandLine": null,
      "parentProcess": "services.exe",
      "mitreTechniques": "T1110, T1110.001",
      "detectionMethod": "Deterministic",
      "summary": "Multiple failed logon attempts detected from external IP",
      "recommendedActions": "Block source IP, investigate source location, review account security",
      "ipEnrichment": {
        "country": "Russia",
        "city": "Moscow",
        "asn": "AS15169",
        "organization": "Google LLC",
        "isHighRisk": true
      },
      "timestamp": "2024-01-15T10:30:00Z",
      "createdAt": "2024-01-15T10:30:05Z",
      "notes": ""
    }
  ],
  "total": 1,
  "page": 1,
  "perPage": 50,
  "totalPages": 1
}
```

### Get Security Event Details
```http
GET /api/security-events/{eventId}
```

### Update Security Event
```http
PATCH /api/security-events/{eventId}
Content-Type: application/json

{
  "notes": "Investigated - confirmed as malicious activity",
  "status": "investigated"
}
```

## 🧠 AI Analysis API

### Analyze Event with AI
```http
POST /api/ai/analyze
Content-Type: application/json

{
  "eventData": {
    "eventId": 4688,
    "processName": "powershell.exe",
    "commandLine": "powershell.exe -ExecutionPolicy Bypass -EncodedCommand SGVsbG8gV29ybGQ=",
    "user": "DOMAIN\\user",
    "computer": "WORKSTATION01"
  },
  "analysisType": "threat_classification"
}
```

**Response:**
```json
{
  "analysisId": "uuid-analysis-id",
  "riskLevel": "high",
  "confidence": 87,
  "threatClassification": "Suspicious PowerShell Execution",
  "mitreTechniques": ["T1059.001", "T1027"],
  "recommendedActions": [
    "Analyze encoded command content",
    "Check user's recent activity",
    "Scan workstation for malware"
  ],
  "reasoning": "Detected PowerShell execution with bypassed execution policy and base64-encoded command, which is commonly used in malicious scripts.",
  "processingTime": 2.3
}
```

### Get AI Analysis History
```http
GET /api/ai/analysis-history?limit=20&analysisType=threat_classification
```

## 🔍 Vector Search API

### Semantic Search
```http
POST /api/vector/search
Content-Type: application/json

{
  "query": "suspicious powershell activity with encoded commands",
  "limit": 10,
  "similarityThreshold": 0.7
}
```

**Response:**
```json
{
  "results": [
    {
      "eventId": 1234,
      "similarity": 0.89,
      "event": {
        "eventType": "ProcessCreation",
        "process": "powershell.exe",
        "commandLine": "powershell.exe -EncodedCommand ...",
        "riskLevel": "high"
      }
    }
  ],
  "totalResults": 1,
  "queryTime": 0.045
}
```

### Vector Statistics
```http
GET /api/vector/stats
```

**Response:**
```json
{
  "totalVectors": 2350000,
  "indexSize": "1.2GB",
  "avgQueryTime": "0.023s",
  "lastUpdate": "2025-09-06T07:14:01Z"
}
```

## 📈 Performance Metrics API

### Get Performance Metrics
```http
GET /api/metrics/performance
```

**Response:**
```json
{
  "eventProcessing": {
    "eventsPerSecond": 12450,
    "avgProcessingTime": 0.082,
    "queueDepth": 15
  },
  "ai_analysis": {
    "avgAnalysisTime": 3.2,
    "analysesPerMinute": 185,
    "cacheHitRate": 0.67
  },
  "vectorOperations": {
    "avgSearchTime": 0.023,
    "avgInsertTime": 0.045,
    "cacheHitRate": 0.78
  },
  "connectionPool": {
    "activeConnections": 0,
    "poolUtilization": 0.0,
    "healthyInstances": 2,
    "totalInstances": 2
  }
}
```

### Get Resource Usage
```http
GET /api/metrics/resources
```

**Response:**
```json
{
  "memory": {
    "used": "445MB",
    "available": "1603MB",
    "utilization": 0.217
  },
  "cpu": {
    "usage": 12.5,
    "cores": 8,
    "threads": 16
  },
  "disk": {
    "used": "1.2GB",
    "available": "48.8GB",
    "utilization": 0.024
  }
}
```

## 🎯 MITRE ATT&CK API

### Get MITRE Techniques
```http
GET /api/mitre/techniques?category=execution&limit=50
```

**Query Parameters:**
- `category` - Filter by MITRE category
- `technique` - Specific technique ID (e.g., T1059.001)
- `search` - Text search in technique names/descriptions
- `limit` - Number of results (default: 50)

**Response:**
```json
{
  "techniques": [
    {
      "id": "T1059.001",
      "name": "PowerShell",
      "description": "Adversaries may abuse PowerShell commands and scripts for execution.",
      "category": "Execution",
      "subcategory": "Command and Scripting Interpreter",
      "platforms": ["Windows"],
      "dataSourceName": "Process",
      "lastUpdated": "2025-09-01T00:00:00Z"
    }
  ],
  "total": 1,
  "page": 1,
  "totalPages": 1
}
```

### Get Events by MITRE Technique
```http
GET /api/mitre/techniques/{techniqueId}/events
```

## 🔔 Notifications API

### List Notification Settings
```http
GET /api/notifications/settings
```

### Create Notification Webhook
```http
POST /api/notifications/webhooks
Content-Type: application/json

{
  "name": "Teams Security Channel",
  "url": "https://outlook.office.com/webhook/...",
  "platform": "teams",
  "severityFilter": ["critical", "high"],
  "enabled": true
}
```

### Test Webhook
```http
POST /api/notifications/webhooks/{webhookId}/test
```

### Update Webhook
```http
PATCH /api/notifications/webhooks/{webhookId}
Content-Type: application/json

{
  "enabled": false,
  "severityFilter": ["critical"]
}
```

## 📊 Reporting API

### Generate Security Report
```http
POST /api/reports/security
Content-Type: application/json

{
  "reportType": "summary",
  "dateRange": {
    "start": "2025-09-01T00:00:00Z",
    "end": "2025-09-06T23:59:59Z"
  },
  "includeCharts": true,
  "format": "json"
}
```

**Response:**
```json
{
  "reportId": "report-uuid",
  "generatedAt": "2025-09-06T08:00:00Z",
  "summary": {
    "totalEvents": 15420,
    "criticalAlerts": 12,
    "highRiskEvents": 89,
    "detectionRate": 0.94
  },
  "topThreats": [
    {
      "technique": "T1110.001",
      "name": "Password Spraying",
      "count": 45,
      "trend": "increasing"
    }
  ]
}
```

### Export Report
```http
GET /api/reports/{reportId}/export?format=pdf
```

## 🔧 Configuration API

### Get Configuration
```http
GET /api/config/settings
```

### Update Configuration
```http
PATCH /api/config/settings
Content-Type: application/json

{
  "pipeline": {
    "enableParallelProcessing": true,
    "maxConcurrency": 6
  },
  "connectionPools": {
    "qdrant": {
      "maxConnectionsPerInstance": 15
    }
  }
}
```

### Validate Configuration
```http
POST /api/config/validate
Content-Type: application/json

{
  "authentication": {
    "jwt": {
      "secretKey": "new-secret-key-value"
    }
  }
}
```

## 🚀 Batch Operations API

### Batch Event Analysis
```http
POST /api/batch/analyze
Content-Type: application/json

{
  "events": [
    {
      "eventId": 4688,
      "processName": "cmd.exe",
      "commandLine": "cmd.exe /c whoami"
    },
    {
      "eventId": 4689,
      "processName": "powershell.exe", 
      "commandLine": "Get-Process"
    }
  ],
  "analysisTypes": ["threat_classification", "mitre_mapping"]
}
```

### Batch Vector Operations
```http
POST /api/batch/vector/upsert
Content-Type: application/json

{
  "vectors": [
    {
      "id": "event-1234",
      "vector": [0.1, 0.2, 0.3, ...],
      "metadata": {
        "eventType": "ProcessCreation",
        "riskLevel": "medium"
      }
    }
  ]
}
```

## 📡 WebSocket API

### Real-time Event Stream
```javascript
const ws = new WebSocket('ws://localhost:5000/ws/events');

ws.onmessage = function(event) {
  const securityEvent = JSON.parse(event.data);
  console.log('New security event:', securityEvent);
};

// Send filter subscription
ws.send(JSON.stringify({
  type: 'subscribe',
  filters: {
    riskLevel: ['critical', 'high'],
    eventTypes: ['AuthenticationFailure', 'ProcessCreation']
  }
}));
```

### System Status Updates
```javascript
const ws = new WebSocket('ws://localhost:5000/ws/system-status');

ws.onmessage = function(event) {
  const statusUpdate = JSON.parse(event.data);
  console.log('System status update:', statusUpdate);
};
```

## 📋 Error Responses

### Standard Error Format
```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid request parameters",
    "details": {
      "field": "riskLevel",
      "issue": "Must be one of: critical, high, medium, low"
    },
    "correlationId": "COR-20250906-abc123",
    "timestamp": "2025-09-06T08:00:00Z"
  }
}
```

### Common Error Codes
| Code | HTTP Status | Description |
|------|-------------|-------------|
| `UNAUTHORIZED` | 401 | Invalid or expired token |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource not found |
| `VALIDATION_ERROR` | 400 | Invalid request data |
| `RATE_LIMITED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Server error |

## 🔄 Rate Limiting

### Rate Limit Headers
```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 995  
X-RateLimit-Reset: 1693958400
```

### Rate Limits by Endpoint
| Endpoint Category | Requests per Minute |
|------------------|-------------------|
| Authentication | 60 |
| Security Events | 1000 |
| AI Analysis | 100 |
| Vector Search | 500 |
| System Status | 200 |
| Configuration | 30 |

## 📞 Support and Integration

### SDK and Libraries
- **.NET Client**: Available via NuGet package
- **JavaScript/TypeScript**: NPM package available
- **Python**: PyPI package for integration
- **PowerShell Module**: Gallery module for automation

### Integration Examples
- **SIEM Integration**: Splunk, QRadar, Sentinel connectors
- **Ticketing Systems**: ServiceNow, Jira integration
- **Chat Platforms**: Teams, Slack webhooks
- **Monitoring Tools**: Grafana, Prometheus exports

### Support Channels
- **API Issues**: [GitHub Issues](https://github.com/MLidstrom/Castellan/issues)
- **Integration Help**: [GitHub Discussions](https://github.com/MLidstrom/Castellan/discussions)
- **Documentation**: Complete guides in `/docs` folder

---

**Castellan** - Comprehensive REST API for enterprise security monitoring and threat intelligence. 🏰🛡️
