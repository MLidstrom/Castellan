# Castellan API Documentation

**Status**: ✅ **Production Ready**  
**Last Updated**: September 11, 2025
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

## 🆕 YARA Malware Detection API

### List YARA Rules
```http
GET /api/yara-rules?page=1&limit=10&category=Malware&enabled=true
```

**Query Parameters:**
- `page` - Page number (default: 1)
- `limit` - Items per page (default: 10, max: 100) 
- `category` - Filter by category: `Malware`, `Ransomware`, `Trojan`, etc.
- `tag` - Filter by tag
- `mitreTechnique` - Filter by MITRE ATT&CK technique (e.g., `T1059.001`)
- `enabled` - Filter by enabled status: `true`, `false`

**Response:**
```json
{
  "data": [
    {
      "id": "22d42700-9ca9-4ed9-9b84-869568d43024",
      "name": "Suspicious_PowerShell_Commands",
      "description": "Detects suspicious PowerShell command patterns",
      "category": "Malware",
      "author": "Security Team",
      "createdAt": "2025-09-09T14:00:00Z",
      "updatedAt": "2025-09-09T14:00:00Z",
      "isEnabled": true,
      "priority": 75,
      "threatLevel": "High",
      "hitCount": 0,
      "falsePositiveCount": 0,
      "averageExecutionTimeMs": 0.0,
      "mitreTechniques": ["T1059.001"],
      "tags": ["powershell", "suspicious"],
      "isValid": true,
      "source": "Custom"
    }
  ],
  "total": 1,
  "page": 1,
  "perPage": 10
}
```

### Create YARA Rule
```http
POST /api/yara-rules
Content-Type: application/json

{
  "name": "Ransomware_File_Extensions",
  "description": "Detects common ransomware file extensions",
  "ruleContent": "rule Ransomware_Extensions {\n  strings:\n    $ext1 = \".encrypted\"\n    $ext2 = \".locked\"\n  condition:\n    any of them\n}",
  "category": "Ransomware",
  "author": "Threat Intel Team",
  "isEnabled": true,
  "priority": 90,
  "threatLevel": "Critical",
  "mitreTechniques": ["T1486"],
  "tags": ["ransomware", "file-encryption"]
}
```

### Get YARA Rule Details
```http
GET /api/yara-rules/{ruleId}
```

### Update YARA Rule
```http
PUT /api/yara-rules/{ruleId}
Content-Type: application/json

{
  "description": "Updated description",
  "isEnabled": false,
  "threatLevel": "Medium"
}
```

### Delete YARA Rule
```http
DELETE /api/yara-rules/{ruleId}
```

### Test YARA Rule
```http
POST /api/yara-rules/test
Content-Type: application/json

{
  "ruleContent": "rule Test { strings: $a = \"malware\" condition: $a }",
  "testContent": "This content contains malware signatures"
}
```

**Response:**
```json
{
  "isValid": true,
  "matched": true,
  "matchedStrings": [
    {
      "identifier": "$a",
      "offset": 21,
      "value": "malware",
      "isHex": false
    }
  ],
  "executionTimeMs": 2.5
}
```

### Get Available Categories
```http
GET /api/yara-rules/categories
```

**Response:**
```json
{
  "data": [
    "Malware",
    "Ransomware", 
    "Trojan",
    "Backdoor",
    "Webshell",
    "Cryptominer",
    "Exploit",
    "Suspicious",
    "PUA",
    "Custom"
  ]
}
```

### Report False Positive
```http
POST /api/yara-rules/{ruleId}/false-positive
Content-Type: application/json

{
  "reporter": "analyst@company.com",
  "comment": "This rule triggered on legitimate software",
  "context": "Windows System File"
}
```

## 🔍 YARA Matches API

### Get YARA Matches
```http
GET /api/yara-rules/matches?securityEventId={eventId}&count=100
```

**Query Parameters:**
- `securityEventId` - Filter matches by security event ID (optional)
- `count` - Maximum number of matches to return (default: 100)

**Response:**
```json
{
  "data": [
    {
      "id": "match-uuid-123",
      "ruleId": "22d42700-9ca9-4ed9-9b84-869568d43024",
      "ruleName": "Suspicious_PowerShell_Commands",
      "matchTime": "2025-09-11T07:30:00Z",
      "targetFile": "C:\\temp\\suspicious.ps1",
      "targetHash": "a1b2c3d4e5f6...",
      "matchedStrings": [
        {
          "identifier": "$a",
          "offset": 42,
          "value": "Invoke-Expression",
          "isHex": false
        }
      ],
      "metadata": {
        "threat_level": "High",
        "category": "Malware"
      },
      "executionTimeMs": 15.2,
      "securityEventId": "evt-456"
    }
  ],
  "total": 1
}
```

## 📈 Timeline Visualization API

### Get Timeline Data
```http
GET /api/timeline?granularity=day&from=2025-09-04T00:00:00Z&to=2025-09-11T23:59:59Z
```

**Query Parameters:**
- `granularity` - Time aggregation granularity: `minute`, `hour`, `day`, `week`, `month` (default: `day`)
- `from` - Start time for timeline (ISO 8601 format)
- `to` - End time for timeline (ISO 8601 format)
- `eventTypes` - Filter by event types (comma-separated)
- `riskLevels` - Filter by risk levels (comma-separated)

**Response:**
```json
{
  "data": [
    {
      "timestamp": "2025-09-04T00:00:00.0000000",
      "count": 12
    },
    {
      "timestamp": "2025-09-05T00:00:00.0000000",
      "count": 28
    },
    {
      "timestamp": "2025-09-06T00:00:00.0000000",
      "count": 45
    }
  ],
  "total": 8
}
```

### Get Timeline Statistics
```http
GET /api/timeline/stats?startTime=2025-09-04T00:00:00Z&endTime=2025-09-11T23:59:59Z
```

**Query Parameters:**
- `startTime` - Start time for statistics (ISO 8601 format)
- `endTime` - End time for statistics (ISO 8601 format)

**Response:**
```json
{
  "totalEvents": 156,
  "eventsByRiskLevel": {
    "low": 89,
    "medium": 45,
    "high": 22
  },
  "eventsByType": {
    "ProcessCreation": 67,
    "Authentication": 34,
    "NetworkConnection": 28,
    "FileSystem": 27
  },
  "eventsByHour": {
    "08": 12,
    "09": 23,
    "10": 45,
    "11": 38
  },
  "eventsByDayOfWeek": {
    "Monday": 22,
    "Tuesday": 34,
    "Wednesday": 45,
    "Thursday": 33,
    "Friday": 22
  },
  "topMitreTechniques": ["T1059.001", "T1055", "T1087"],
  "topMachines": ["WORKSTATION-01", "SERVER-02"],
  "topUsers": ["admin", "service-account"],
  "averageRiskScore": 42.5,
  "highRiskEvents": 22,
  "criticalRiskEvents": 3
}
```

### Get Detailed Timeline Events
```http
GET /api/timeline/events/detailed?startTime=2025-09-11T00:00:00Z&endTime=2025-09-11T23:59:59Z&limit=50
```

**Query Parameters:**
- `startTime` - Start time for events (ISO 8601 format, required)
- `endTime` - End time for events (ISO 8601 format, required)
- `riskLevels` - Filter by risk levels (optional)
- `eventTypes` - Filter by event types (optional)
- `limit` - Maximum number of events to return (default: 50)

**Response:**
```json
{
  "events": [
    {
      "id": "evt-123",
      "eventType": "ProcessCreation",
      "timestamp": "2025-09-11T14:30:00Z",
      "riskLevel": "high",
      "summary": "Suspicious process execution detected",
      "mitreTechniques": ["T1059.001"],
      "confidence": 87,
      "machine": "WORKSTATION-01",
      "user": "admin"
    }
  ],
  "totalCount": 156,
  "startTime": "2025-09-11T00:00:00Z",
  "endTime": "2025-09-11T23:59:59Z"
}
```

### Get Timeline Heatmap
```http
GET /api/timeline/heatmap?granularity=hour&startTime=2025-09-04T00:00:00Z&endTime=2025-09-11T23:59:59Z
```

**Query Parameters:**
- `granularity` - Time granularity: `hour`, `day`, `week` (default: `hour`)
- `startTime` - Start time (ISO 8601 format)
- `endTime` - End time (ISO 8601 format)
- `riskLevels` - Filter by risk levels (optional)

**Response:**
```json
{
  "data": [
    {
      "timeBucket": "2025-09-04T08:00:00Z",
      "intensity": 0.75,
      "eventCount": 23
    },
    {
      "timeBucket": "2025-09-04T09:00:00Z", 
      "intensity": 1.0,
      "eventCount": 31
    }
  ]
}
```

### Get Timeline Metrics
```http
GET /api/timeline/metrics?startTime=2025-09-01T00:00:00Z&endTime=2025-09-30T23:59:59Z
```

**Query Parameters:**
- `startTime` - Start time for metrics analysis (ISO 8601 format)
- `endTime` - End time for metrics analysis (ISO 8601 format)

**Response:**
```json
{
  "trends": {
    "eventVelocity": {
      "current": 156,
      "previous": 134,
      "percentageChange": 16.4
    },
    "riskTrend": "increasing",
    "topGrowingThreats": ["T1059.001", "T1055"]
  },
  "anomalies": [
    {
      "timestamp": "2025-09-11T14:00:00Z",
      "score": 2.8,
      "eventCount": 89,
      "expectedCount": 23.5,
      "description": "Unusually high activity detected"
    }
  ],
  "patterns": {
    "peakHours": ["09:00-11:00", "14:00-16:00"],
    "quietDays": ["Sunday"],
    "seasonalTrends": "stable"
  }
}
```

## 💾 Export API

### Get Available Export Formats
```http
GET /api/export/formats
```

**Response:**
```json
{
  "data": [
    {
      "format": "csv",
      "name": "Comma-Separated Values",
      "description": "Standard CSV format for spreadsheet applications",
      "mimeType": "text/csv",
      "fileExtension": ".csv"
    },
    {
      "format": "json",
      "name": "JSON",
      "description": "Structured JSON format for programmatic access",
      "mimeType": "application/json",
      "fileExtension": ".json"
    },
    {
      "format": "pdf",
      "name": "Portable Document Format",
      "description": "Formatted PDF report for documentation",
      "mimeType": "application/pdf",
      "fileExtension": ".pdf"
    }
  ]
}
```

### Export Security Events
```http
POST /api/export/security-events
Content-Type: application/json

{
  "format": "csv",
  "filters": {
    "dateFrom": "2025-09-01T00:00:00Z",
    "dateTo": "2025-09-11T23:59:59Z",
    "riskLevel": ["high", "critical"],
    "eventTypes": ["ProcessCreation", "Authentication"]
  },
  "options": {
    "includeHeaders": true,
    "maxRecords": 10000,
    "fields": ["timestamp", "eventType", "riskLevel", "summary", "mitreTechniques"]
  }
}
```

**Query Parameters (Alternative GET request):**
```http
GET /api/export/security-events?format=csv&dateFrom=2025-09-01T00:00:00Z&dateTo=2025-09-11T23:59:59Z&riskLevel=high,critical
```

**Response (JSON format):**
```json
{
  "data": [
    {
      "id": "evt-123",
      "timestamp": "2025-09-11T14:30:00Z",
      "eventType": "ProcessCreation",
      "riskLevel": "high",
      "summary": "Suspicious process execution detected",
      "mitreTechniques": ["T1059.001"],
      "confidence": 87,
      "machine": "WORKSTATION-01",
      "user": "admin"
    }
  ],
  "exportInfo": {
    "format": "json",
    "totalRecords": 156,
    "exportedRecords": 156,
    "generatedAt": "2025-09-11T14:30:00Z",
    "filters": {
      "dateFrom": "2025-09-01T00:00:00Z",
      "dateTo": "2025-09-11T23:59:59Z"
    }
  }
}
```

**Response (CSV format):**
```csv
Id,Timestamp,EventType,RiskLevel,Summary,MitreTechniques,Confidence,Machine,User
evt-123,2025-09-11T14:30:00Z,ProcessCreation,high,"Suspicious process execution detected","T1059.001",87,WORKSTATION-01,admin
evt-124,2025-09-11T14:31:00Z,Authentication,critical,"Failed logon attempts detected","T1110.001",92,SERVER-01,service-account
```

**Response (PDF format):**
Returns a binary PDF file with formatted security event report including:
- Executive summary with key statistics
- Event timeline visualization
- Risk level distribution charts
- Top MITRE techniques table
- Detailed event listings with filtering applied

### Get Export Statistics
```http
GET /api/export/stats
```

**Response:**
```json
{
  "data": {
    "totalExports": 1247,
    "exportsByFormat": {
      "csv": 543,
      "json": 398,
      "pdf": 306
    },
    "exportsByTimeRange": {
      "last24Hours": 23,
      "lastWeek": 156,
      "lastMonth": 423
    },
    "averageExportSize": {
      "csv": "2.4MB",
      "json": "3.1MB",
      "pdf": "1.8MB"
    },
    "mostExportedEventTypes": [
      {
        "eventType": "ProcessCreation",
        "count": 1247
      },
      {
        "eventType": "Authentication", 
        "count": 892
      }
    ],
    "popularFilters": {
      "riskLevel": ["high", "critical"],
      "timeRange": "last7days"
    }
  }
}
```

### Background Export (Large Datasets)
For large exports that may take time to process:

```http
POST /api/export/security-events/async
Content-Type: application/json

{
  "format": "csv",
  "filters": {
    "dateFrom": "2025-01-01T00:00:00Z",
    "dateTo": "2025-12-31T23:59:59Z"
  },
  "options": {
    "maxRecords": 100000
  },
  "callbackUrl": "https://your-app.com/export-complete"
}
```

**Response:**
```json
{
  "exportId": "export-uuid-123",
  "status": "processing",
  "estimatedCompletion": "2025-09-11T14:45:00Z",
  "progress": {
    "recordsProcessed": 0,
    "totalRecords": 45000,
    "percentComplete": 0
  }
}
```

### Check Export Status
```http
GET /api/export/status/{exportId}
```

**Response:**
```json
{
  "exportId": "export-uuid-123",
  "status": "completed",
  "downloadUrl": "/api/export/download/export-uuid-123",
  "expiresAt": "2025-09-18T14:30:00Z",
  "fileSize": "15.2MB",
  "recordCount": 45000
}
```

### Download Export File
```http
GET /api/export/download/{exportId}
```

Returns the exported file with appropriate Content-Type and Content-Disposition headers.

## 🔍 YARA Scanning API

### Scan Content with YARA Rules
```http
POST /api/yara-rules/scan
Content-Type: application/json

{
  "content": "base64-encoded-file-content",
  "fileName": "suspicious_file.exe"
}
```

**Alternative - Scan by File Path:**
```json
{
  "filePath": "C:\\temp\\suspicious_file.exe",
  "fileName": "suspicious_file.exe"
}
```

**Response:**
```json
{
  "data": {
    "fileName": "suspicious_file.exe",
    "scanTime": "2025-09-11T07:30:00Z",
    "matchCount": 2,
    "matches": [
      {
        "ruleId": "rule-uuid-123",
        "ruleName": "Malware_Detection_Rule",
        "matchedStrings": [
          {
            "identifier": "$malware_sig",
            "offset": 1024,
            "value": "malicious_pattern",
            "isHex": false
          }
        ],
        "executionTimeMs": 8.5
      }
    ]
  }
}
```

### Get YARA Service Status
```http
GET /api/yara-rules/status
```

**Response:**
```json
{
  "isAvailable": true,
  "isHealthy": true,
  "error": null,
  "compiledRules": 45
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
|| Endpoint Category | Requests per Minute |
||------------------|-------------------|
|| Authentication | 60 |
|| Security Events | 1000 |
|| AI Analysis | 100 |
|| YARA Rules | 200 |
|| YARA Matches | 300 |
|| YARA Scanning | 50 |
|| YARA Testing | 50 |
|| Timeline Data | 500 |
|| Timeline Stats | 200 |
|| Export Operations | 100 |
|| Export Downloads | 50 |
|| Vector Search | 500 |
|| System Status | 200 |
|| Configuration | 30 |

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
