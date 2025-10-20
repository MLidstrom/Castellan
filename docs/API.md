# Castellan API Documentation

**Status**: **Production Ready**
**Last Updated**: October 14, 2025
**API Version**: v1.4 - Threat Scanner Configuration & Dashboard Enhancements

## Overview

Castellan provides a comprehensive REST API for security monitoring, threat analysis, and system management. This documentation covers all available endpoints, authentication, and integration patterns.

## Authentication

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

## System Status API

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

## Security Events API
## Dashboard Consolidated Data

### Get Consolidated Dashboard Data
```http
GET /api/dashboarddata/consolidated?timeRange=24h
```

**Query Parameters:**
- `timeRange` — `1h`, `24h`, `7d`, `30d` (default: `24h`)

**Response (schema):**
```json
{
  "securityEvents": {
    "totalEvents": 693,
    "riskLevelCounts": {
      "CRITICAL": 5,
      "HIGH": 21,
      "MEDIUM": 48,
      "LOW": 12
    },
    "recentEvents": [ /* last N events (array) */ ],
    "lastEventTime": "2025-10-09T15:21:48.9342838Z"
  },
  "systemStatus": {
    "totalComponents": 8,
    "healthyComponents": 8,
    "components": [ /* component list */ ],
    "componentStatuses": { /* map */ }
  },
  "threatScanner": {
    "totalScans": 44,
    "activeScans": 0,
    "completedScans": 0,
    "threatsFound": 148150,
    "lastScanTime": "2025-10-09T12:09:35.2252803Z"
  },
  "lastUpdated": "2025-10-09T13:58:50.9723882Z",
  "timeRange": "24h"
}
```

**Frontend mapping (reference):**
- Open Events — `securityEvents.recentEvents` where `status ∈ {OPEN, INVESTIGATING}`
- Critical Threats — `securityEvents.riskLevelCounts.CRITICAL`
- Events/Week — `securityEvents.totalEvents` (until weekly metric is provided)
- System Status — `OPERATIONAL` if `healthyComponents === totalComponents`, else `DEGRADED`
- Threat Distribution — from `riskLevelCounts`, shown in order: Critical, High, Medium, Low, Unknown

### Trigger Broadcast (SignalR)
```http
POST /api/dashboarddata/broadcast
Authorization: Bearer {token}
```
Sends a consolidated update over SignalR to connected dashboards.

## Real-time (SignalR)

**Hub:** `/hubs/scan-progress`

**Events (client handlers):**
- `DashboardUpdate` — sends the consolidated payload above
- `SecurityEvent` — notifies of a new/updated security event
- `SystemStatusUpdate` — notifies when component health changes

**WebSocket verification:**
- `POST /hubs/scan-progress/negotiate` → 200
- A socket connects to `/hubs/scan-progress` with incoming frames


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

## Security Event Rules API

### List Security Event Rules
```http
GET /api/security-event-rules?enabled=true&page=1&limit=50&sort=priority&order=desc
Authorization: Bearer {token}
```

**Query Parameters:**
- `enabled` - Filter by enabled status: `true`, `false` (optional)
- `page` - Page number for pagination (optional)
- `limit` - Items per page (optional)
- `sort` - Sort field: `eventid`, `priority`, `risklevel`, `confidence` (optional)
- `order` - Sort order: `asc`, `desc` (optional)

**Response:**
```json
[
  {
    "id": 1,
    "eventId": 4625,
    "channel": "Security",
    "eventType": "AuthenticationFailure",
    "riskLevel": "high",
    "confidence": 85,
    "summary": "Multiple failed logon attempts detected",
    "mitreTechniques": ["T1110.001", "T1078"],
    "recommendedActions": [
      "Block source IP address",
      "Review account security policies",
      "Investigate source location"
    ],
    "isEnabled": true,
    "priority": 90,
    "description": "Detects brute force authentication attempts",
    "tags": ["authentication", "brute-force"],
    "createdAt": "2025-10-01T10:00:00Z",
    "updatedAt": "2025-10-08T14:30:00Z",
    "modifiedBy": "admin"
  }
]
```

**Response Headers:**
```
X-Total-Count: 45
Access-Control-Expose-Headers: X-Total-Count
```

### Get Security Event Rule by ID
```http
GET /api/security-event-rules/{id}
Authorization: Bearer {token}
```

**Response:**
```json
{
  "id": 1,
  "eventId": 4625,
  "channel": "Security",
  "eventType": "AuthenticationFailure",
  "riskLevel": "high",
  "confidence": 85,
  "summary": "Multiple failed logon attempts detected",
  "mitreTechniques": ["T1110.001"],
  "recommendedActions": ["Block source IP", "Review account security"],
  "isEnabled": true,
  "priority": 90,
  "description": "Detects brute force attempts",
  "tags": ["authentication"],
  "createdAt": "2025-10-01T10:00:00Z",
  "updatedAt": "2025-10-08T14:30:00Z",
  "modifiedBy": "admin"
}
```

### Get Rule by Event ID and Channel
```http
GET /api/security-event-rules/event/{eventId}/channel/{channel}
Authorization: Bearer {token}
```

**Example:**
```http
GET /api/security-event-rules/event/4625/channel/Security
```

### Create Security Event Rule (Admin Only)
```http
POST /api/security-event-rules
Authorization: Bearer {token}
Content-Type: application/json

{
  "eventId": 4688,
  "channel": "Security",
  "eventType": "ProcessCreation",
  "riskLevel": "high",
  "confidence": 90,
  "summary": "Suspicious PowerShell execution detected",
  "mitreTechniques": ["T1059.001"],
  "recommendedActions": [
    "Analyze PowerShell command",
    "Check process parent",
    "Scan workstation"
  ],
  "isEnabled": true,
  "priority": 85,
  "description": "Detects suspicious PowerShell command execution",
  "tags": ["powershell", "execution"]
}
```

**Response:**
```json
{
  "id": 12,
  "eventId": 4688,
  "channel": "Security",
  "eventType": "ProcessCreation",
  "riskLevel": "high",
  "confidence": 90,
  "summary": "Suspicious PowerShell execution detected",
  "mitreTechniques": ["T1059.001"],
  "recommendedActions": [
    "Analyze PowerShell command",
    "Check process parent",
    "Scan workstation"
  ],
  "isEnabled": true,
  "priority": 85,
  "description": "Detects suspicious PowerShell command execution",
  "tags": ["powershell", "execution"],
  "createdAt": "2025-10-09T14:30:00Z",
  "updatedAt": "2025-10-09T14:30:00Z",
  "modifiedBy": "admin"
}
```

### Update Security Event Rule (Admin Only)
```http
PUT /api/security-event-rules/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "confidence": 95,
  "priority": 100,
  "isEnabled": false,
  "description": "Updated description"
}
```

### Delete Security Event Rule (Admin Only)
```http
DELETE /api/security-event-rules/{id}
Authorization: Bearer {token}
```

**Response:** 204 No Content

### Refresh Rules Cache (Admin Only)
```http
POST /api/security-event-rules/refresh-cache
Authorization: Bearer {token}
```

**Response:**
```json
{
  "message": "Cache refreshed successfully"
}
```

**Features:**
- **Database-Backed Rules** - SQLite storage with EF Core and in-memory caching (15-minute TTL)
- **Automatic Event Enrichment** - Rules automatically enhance security events with context and risk assessments
- **Real-time Detection** - Used by SecurityEventDetector for live event analysis
- **Comprehensive Filtering** - Filter by enabled status, event ID, channel, and more
- **Role-Based Access** - Read access for all authenticated users, write access for admins only
- **Pagination Support** - Efficient handling of large rule sets with sorting and pagination
- **Cache Management** - 15-minute in-memory cache with manual refresh capability

## AI Analysis API

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

## Vector Search API

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

## Performance Metrics API

### Get Performance Metrics
```http
GET /api/performance/metrics?timeRange=1h
```

**Query Parameters:**
- `timeRange` - Time range: `1h`, `6h`, `24h`, `7d` (default: 1h)

**Response:**
```json
{
  "timeRange": "1h",
  "dataPoints": [
    {
      "timestamp": "2025-09-14T12:00:00Z",
      "eventsPerSecond": 12450,
      "avgProcessingTime": 0.082,
      "queueDepth": 15
    }
  ],
  "summary": {
    "avgEventsPerSecond": 12450,
    "peakEventsPerSecond": 15200,
    "avgProcessingTime": 0.082,
    "totalEventsProcessed": 45000
  },
  "lastUpdated": "2025-09-14T12:30:00Z"
}
```

### Get Performance Alerts
```http
GET /api/performance/alerts
```

**Response:**
```json
{
  "active": [
    {
      "id": "alert-123",
      "severity": "warning",
      "message": "High memory usage detected",
      "threshold": 80,
      "currentValue": 85,
      "timestamp": "2025-09-14T12:25:00Z"
    }
  ],
  "history": [],
  "summary": {
    "totalActive": 1,
    "criticalCount": 0,
    "warningCount": 1,
    "lastCheck": "2025-09-14T12:30:00Z"
  }
}
```

### Get Cache Statistics
```http
GET /api/performance/cache-stats
```

**Response:**
```json
{
  "hitRate": 0.78,
  "missRate": 0.22,
  "memoryUsage": "125MB",
  "totalRequests": 15420,
  "effectivenessRatio": 0.85,
  "cacheEntries": 2340,
  "averageResponseTime": 0.023,
  "lastUpdated": "2025-09-14T12:30:00Z"
}
```

### Get Database Performance
```http
GET /api/performance/database
```

**Response:**
```json
{
  "connectionPool": {
    "active": 5,
    "total": 20,
    "utilization": 0.25,
    "peakConnections": 12
  },
  "queryPerformance": {
    "avgResponseTime": 0.045,
    "slowQueries": 2,
    "totalQueries": 15420,
    "queriesPerSecond": 125.3
  },
  "qdrantMetrics": {
    "avgOperationTime": 0.023,
    "vectorCount": 2350000,
    "collectionStatus": "healthy",
    "batchOperationTime": 0.156
  },
  "lastUpdated": "2025-09-14T12:30:00Z"
}
```

### Get System Resources
```http
GET /api/performance/system-resources
```

**Response:**
```json
{
  "cpu": {
    "usage": 12.5,
    "cores": 8,
    "loadAverage": 1.2,
    "processUsage": 8.3
  },
  "memory": {
    "usage": 0.217,
    "total": "2048MB",
    "available": "1603MB",
    "processMemory": "445MB"
  },
  "disk": {
    "usage": 0.024,
    "total": "50GB",
    "available": "48.8GB",
    "readSpeed": "125MB/s",
    "writeSpeed": "95MB/s"
  },
  "lastUpdated": "2025-09-14T12:30:00Z"
}
```

## Database Connection Pool API

### Get Database Pool Health
```http
GET /api/database-pool/health
Authorization: Bearer {token}
```

**Response:**
```json
{
  "healthy": true,
  "timestamp": "2025-10-09T14:30:00Z",
  "status": "healthy"
}
```

### Get Database Pool Metrics
```http
GET /api/database-pool/metrics
Authorization: Bearer {token}
```

**Response:**
```json
{
  "data": {
    "activeConnections": 5,
    "idleConnections": 15,
    "totalConnections": 20,
    "maxPoolSize": 100,
    "poolUtilizationPercent": 20.0,
    "healthyConnections": 20,
    "failedConnections": 0,
    "averageWaitTimeMs": 2.5,
    "lastHealthCheck": "2025-10-09T14:30:00Z",
    "databaseProvider": "SQLite"
  }
}
```

### Get Connection Details
```http
GET /api/database-pool/connections
Authorization: Bearer {token}
```

**Response:**
```json
{
  "data": {
    "active": 5,
    "idle": 15,
    "total": 20,
    "maxPoolSize": 100,
    "utilizationPercent": 20.0,
    "provider": "SQLite"
  }
}
```

### Force Health Check (Admin Only)
```http
POST /api/database-pool/health-check
Authorization: Bearer {token}
```

**Response:**
```json
{
  "success": true,
  "healthy": true,
  "message": "Database connection pool is healthy"
}
```

**Features:**
- **EF Core PooledDbContextFactory** - Production-ready connection pooling with 5-100 configurable connections
- **SQLite Optimizations** - WAL mode, shared cache, 10MB cache size, 5s busy timeout
- **Automatic Health Monitoring** - Health checks every 60 seconds with metrics collection
- **PostgreSQL Ready** - Architecture supports seamless migration without code changes
- **Performance Metrics** - Tracks active/idle connections, utilization, failure counts, wait times

## Analytics API

### Get Historical Trends
```http
GET /api/analytics/trends?metric=TotalEvents&timeRange=7d&groupBy=day
```

**Query Parameters:**
- `metric` - Metric type: `TotalEvents`, `CriticalEvents`, `HighRiskEvents` (default: TotalEvents)
- `timeRange` - Time range: `7d`, `30d`, `90d` (default: 7d)
- `groupBy` - Grouping interval: `day`, `hour` (default: day)

**Response:**
```json
{
  "data": [
    {
      "timestamp": "2025-01-15T00:00:00Z",
      "value": 120
    },
    {
      "timestamp": "2025-01-16T00:00:00Z",
      "value": 145
    }
  ]
}
```

### Generate Forecast
```http
GET /api/analytics/forecast?metric=TotalEvents&forecastPeriod=7
```

**Query Parameters:**
- `metric` - Metric type: `TotalEvents`, `CriticalEvents`, `HighRiskEvents` (default: TotalEvents)
- `forecastPeriod` - Number of days to forecast: 1-30 (default: 7)

**Response:**
```json
{
  "data": {
    "historicalData": [
      {
        "timestamp": "2025-01-15T00:00:00Z",
        "value": 120
      }
    ],
    "forecastedData": [
      {
        "timestamp": "2025-01-16T00:00:00Z",
        "forecastValue": 150,
        "lowerBound": 130,
        "upperBound": 170
      }
    ]
  }
}
```

**Features:**
- **ML.NET Time Series Forecasting** - Uses Singular Spectrum Analysis (SSA) for accurate predictions
- **Confidence Intervals** - Statistical upper and lower bounds for forecast reliability
- **Historical Context** - 90 days of historical data used for model training
- **Anonymous Access** - No authentication required for analytics endpoints
- **Real-time Processing** - On-demand forecast generation with sub-second response times

## MITRE ATT&CK API

### Get MITRE Technique Count
```http
GET /api/mitre/count
```

**Response:**
```json
{
  "count": 823,
  "shouldImport": false,
  "lastUpdated": "2025-09-20T10:00:00Z"
}
```

### Import MITRE Techniques
```http
POST /api/mitre/import
```

**Response:**
```json
{
  "success": true,
  "message": "Successfully imported 823 new techniques and updated 0 existing techniques",
  "result": {
    "techniquesImported": 823,
    "techniquesUpdated": 0,
    "errors": []
  }
}
```

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

## Correlation Engine API

**Note**: The Correlation Engine operates as background intelligence and automatically enhances security events. These APIs provide access to correlation statistics and configuration, but the correlation analysis runs automatically without requiring dashboard interaction.

### Get Correlation Statistics
```http
GET /api/correlation/statistics
```

**Response:**
```json
{
  "totalEventsProcessed": 45320,
  "correlationsDetected": 127,
  "correlationsByType": {
    "TemporalBurst": 45,
    "BruteForce": 23,
    "LateralMovement": 34,
    "PrivilegeEscalation": 25
  },
  "averageConfidenceScore": 0.82,
  "averageProcessingTime": "00:00:00.0234",
  "lastUpdated": "2025-09-20T14:30:00Z",
  "topPatterns": [
    {
      "pattern": "Multiple failed authentications followed by success",
      "count": 23,
      "riskLevel": "high"
    }
  ]
}
```

### Get Correlation Rules
```http
GET /api/correlation/rules
```

**Response:**
```json
[
  {
    "id": "temporal-burst",
    "name": "Temporal Burst Detection",
    "description": "Detects multiple events from same source in short time",
    "type": 0,
    "patterns": [],
    "timeWindow": "00:05:00",
    "minEventCount": 5,
    "minConfidence": 0.7,
    "isEnabled": true,
    "requiredEventTypes": [],
    "parameters": {}
  },
  {
    "id": "brute-force",
    "name": "Brute Force Attack",
    "description": "Multiple failed authentications followed by success",
    "type": 1,
    "patterns": [],
    "timeWindow": "00:10:00",
    "minEventCount": 3,
    "minConfidence": 0.8,
    "isEnabled": true,
    "requiredEventTypes": ["AuthenticationFailure", "AuthenticationSuccess"],
    "parameters": {}
  },
  {
    "id": "lateral-movement",
    "name": "Lateral Movement Detection",
    "description": "Similar events across multiple machines",
    "type": 2,
    "patterns": [],
    "timeWindow": "00:30:00",
    "minEventCount": 3,
    "minConfidence": 0.75,
    "isEnabled": true,
    "requiredEventTypes": [],
    "parameters": {}
  },
  {
    "id": "privilege-escalation",
    "name": "Privilege Escalation",
    "description": "Events indicating privilege escalation attempts",
    "type": 3,
    "patterns": [],
    "timeWindow": "00:15:00",
    "minEventCount": 2,
    "minConfidence": 0.85,
    "isEnabled": true,
    "requiredEventTypes": ["PrivilegeEscalation", "ProcessCreation"],
    "parameters": {}
  }
]
```

### Update Correlation Rule
```http
PUT /api/correlation/rules/{ruleId}
Content-Type: application/json

{
  "name": "Updated Rule Name",
  "description": "Updated description",
  "isEnabled": false,
  "minConfidence": 0.9,
  "minEventCount": 5,
  "timeWindow": "00:15:00"
}
```

### Get Correlations
```http
GET /api/correlation/correlations?startTime=2025-09-20T00:00:00Z&endTime=2025-09-20T23:59:59Z&limit=50
```

**Query Parameters:**
- `startTime` - Start time for correlation filter (ISO 8601)
- `endTime` - End time for correlation filter (ISO 8601)
- `correlationType` - Filter by correlation type: `TemporalBurst`, `BruteForce`, `LateralMovement`, `PrivilegeEscalation`
- `minConfidence` - Minimum confidence score (0.0-1.0)
- `limit` - Maximum number of correlations to return (default: 50)

**Response:**
```json
{
  "data": [
    {
      "id": "corr-uuid-123",
      "correlationType": "BruteForce",
      "confidenceScore": 0.87,
      "riskLevel": "high",
      "pattern": "Multiple authentication failures followed by success",
      "eventIds": ["evt-456", "evt-457", "evt-458", "evt-459"],
      "mitreTechniques": ["T1110.001"],
      "detectedAt": "2025-09-20T14:25:00Z",
      "timeWindow": "00:10:00",
      "matchedRule": "brute-force",
      "metadata": {
        "sourceHost": "DC-01.contoso.com",
        "targetUser": "administrator",
        "failedAttempts": 8,
        "timeBetweenEvents": "00:01:30"
      }
    }
  ],
  "total": 1,
  "startTime": "2025-09-20T00:00:00Z",
  "endTime": "2025-09-20T23:59:59Z"
}
```

### Analyze Event for Correlations
```http
POST /api/correlation/analyze
Content-Type: application/json

{
  "eventId": "evt-789",
  "eventType": "AuthenticationSuccess",
  "timestamp": "2025-09-20T14:30:00Z",
  "sourceHost": "DC-01.contoso.com",
  "user": "administrator"
}
```

**Response:**
```json
{
  "hasCorrelation": true,
  "confidenceScore": 0.89,
  "explanation": "Detected brute force attack pattern: 8 failed authentication attempts followed by successful login",
  "matchedRules": ["Brute Force Attack"],
  "correlation": {
    "id": "corr-uuid-124",
    "correlationType": "BruteForce",
    "riskLevel": "high",
    "mitreTechniques": ["T1110.001"],
    "relatedEventIds": ["evt-780", "evt-781", "evt-782", "evt-783", "evt-784", "evt-785", "evt-786", "evt-787", "evt-789"]
  },
  "recommendations": [
    "Investigate source IP address",
    "Review account security policies",
    "Consider account lockout mechanisms",
    "Monitor for further suspicious activity"
  ]
}
```

### Detect Attack Chains
```http
POST /api/correlation/attack-chains
Content-Type: application/json

{
  "eventIds": ["evt-100", "evt-101", "evt-102"],
  "timeWindow": "00:30:00"
}
```

**Response:**
```json
{
  "attackChains": [
    {
      "id": "chain-uuid-456",
      "attackType": "Privilege Escalation",
      "confidenceScore": 0.92,
      "riskLevel": "high",
      "stages": [
        {
          "stageNumber": 1,
          "eventId": "evt-100",
          "description": "Initial authentication",
          "mitreTechnique": "T1078"
        },
        {
          "stageNumber": 2,
          "eventId": "evt-101",
          "description": "Privilege escalation attempt",
          "mitreTechnique": "T1068"
        },
        {
          "stageNumber": 3,
          "eventId": "evt-102",
          "description": "Suspicious process creation",
          "mitreTechnique": "T1059"
        }
      ],
      "detectedAt": "2025-09-20T14:35:00Z",
      "timeSpan": "00:05:30"
    }
  ],
  "totalChains": 1
}
```

### Train Correlation Models
```http
POST /api/correlation/train
Content-Type: application/json

{
  "confirmedCorrelations": [
    {
      "id": "corr-uuid-123",
      "correlationType": "BruteForce",
      "confidenceScore": 0.87,
      "isConfirmed": true,
      "feedback": "True positive - confirmed malicious activity"
    }
  ]
}
```

**Response:**
```json
{
  "success": true,
  "message": "Models retrained successfully with 1 confirmed correlations",
  "trainingStats": {
    "correlationsProcessed": 1,
    "modelAccuracyImprovement": 0.03,
    "trainingTime": "00:00:02.456"
  }
}
```

### Cleanup Old Correlations
```http
DELETE /api/correlation/cleanup?retentionPeriod=30
```

**Query Parameters:**
- `retentionPeriod` - Number of days to retain correlations (default: 30)

**Response:**
```json
{
  "success": true,
  "message": "Cleanup completed successfully",
  "correlationsRemoved": 145,
  "retentionPeriod": "30.00:00:00"
}
```

**Features:**
- **Background Intelligence** - Automatic correlation analysis without user intervention
- **Event Enhancement** - Security events automatically enriched with correlation context and indicators
- **ML.NET Integration** - K-means clustering with 8-feature vector analysis for pattern detection
- **Multiple Correlation Types** - Temporal bursts, brute force, lateral movement, privilege escalation
- **Risk Intelligence** - Automatic risk level upgrades based on correlation types
- **Smart Notifications** - Correlation-aware alerts with adaptive throttling
- **Performance Optimized** - Continuous background processing with efficient batch analysis

## Notifications API

### Get Notification Configuration
```http
GET /api/notifications/config
```

**Response:**
```json
{
  "teams": {
    "enabled": false,
    "webhookUrl": "",
    "notificationTypes": {
      "criticalEvents": true,
      "highRiskEvents": true,
      "yaraMatches": true,
      "systemAlerts": true
    },
    "rateLimitPerHour": 60
  },
  "slack": {
    "enabled": false,
    "webhookUrl": "",
    "channel": "#security",
    "notificationTypes": {
      "criticalEvents": true,
      "highRiskEvents": true,
      "yaraMatches": true,
      "systemAlerts": false
    },
    "rateLimitPerHour": 60
  }
}
```

### Update Notification Configuration
```http
PUT /api/notifications/config
Content-Type: application/json

{
  "teams": {
    "enabled": true,
    "webhookUrl": "https://outlook.office.com/webhook/...",
    "notificationTypes": {
      "criticalEvents": true,
      "highRiskEvents": true,
      "yaraMatches": false,
      "systemAlerts": false
    },
    "rateLimitPerHour": 30
  },
  "slack": {
    "enabled": false,
    "webhookUrl": "",
    "channel": "#security",
    "notificationTypes": {
      "criticalEvents": true,
      "highRiskEvents": true,
      "yaraMatches": true,
      "systemAlerts": false
    },
    "rateLimitPerHour": 60
  }
}
```

## Notification Templates API (v0.7.0)

**Version**: v0.7.0 (October 2025)

Customizable notification templates with dynamic tag/placeholder support for Teams and Slack integrations.

### List Notification Templates
```http
GET /api/notification-templates
Authorization: Bearer {token}
```

**Response:**
```json
{
  "data": [
    {
      "id": "template-uuid-123",
      "name": "SecurityEvent_Teams",
      "platform": "Teams",
      "type": "SecurityEvent",
      "templateContent": "🚨 {{BOLD:SECURITY ALERT}} - {{SEVERITY}} Severity\n\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n📋 {{BOLD:Event Details}}\n• {{BOLD:Event Type:}} {{EVENT_TYPE}}\n• {{BOLD:Event ID:}} {{EVENT_ID}}\n• {{BOLD:Timestamp:}} {{DATE}}\n\n🖥️  {{BOLD:Affected System}}\n• {{BOLD:Machine:}} {{HOST}}\n• {{BOLD:User Account:}} {{USER}}\n\n📊 {{BOLD:Risk Assessment}}\n{{SUMMARY}}\n\n🎯 {{BOLD:MITRE ATT&CK Framework}}\n{{MITRE_TECHNIQUES}}\n\n✅ {{BOLD:Recommended Response Actions}}\n{{RECOMMENDED_ACTIONS}}\n\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n{{LINK:{{DETAILS_URL}}|🔍 View Full Investigation Details}}\n\n⚡ Powered by CastellanAI Security Platform",
      "isEnabled": true,
      "createdAt": "2025-10-15T10:00:00Z",
      "updatedAt": "2025-10-15T10:00:00Z"
    }
  ],
  "total": 8
}
```

### Get Notification Template by ID
```http
GET /api/notification-templates/{id}
Authorization: Bearer {token}
```

**Response:**
```json
{
  "id": "template-uuid-123",
  "name": "SecurityEvent_Teams",
  "platform": "Teams",
  "type": "SecurityEvent",
  "templateContent": "🚨 {{BOLD:SECURITY ALERT}} - {{SEVERITY}} Severity...",
  "isEnabled": true,
  "createdAt": "2025-10-15T10:00:00Z",
  "updatedAt": "2025-10-15T10:00:00Z"
}
```

### Create Notification Template (Admin Only)
```http
POST /api/notification-templates
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "CustomTemplate_Teams",
  "platform": "Teams",
  "type": "SecurityEvent",
  "templateContent": "Custom template with {{DATE}} and {{HOST}} tags",
  "isEnabled": true
}
```

**Validation:**
- `platform`: Must be "Teams" or "Slack"
- `type`: Must be "SecurityEvent", "SystemAlert", "HealthWarning", or "PerformanceAlert"
- `templateContent`: Required, non-empty string with valid tag syntax

### Update Notification Template (Admin Only)
```http
PUT /api/notification-templates/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Updated Template Name",
  "templateContent": "Updated template content with {{BOLD:tags}}",
  "isEnabled": false
}
```

### Delete Notification Template (Admin Only)
```http
DELETE /api/notification-templates/{id}
Authorization: Bearer {token}
```

**Response:** 204 No Content

### Validate Template Syntax
```http
POST /api/notification-templates/validate
Authorization: Bearer {token}
Content-Type: application/json

{
  "templateContent": "Test template with {{DATE}} and {{HOST}} tags",
  "platform": "Teams"
}
```

**Response:**
```json
{
  "isValid": true,
  "errors": [],
  "warnings": [],
  "tags": ["DATE", "HOST"]
}
```

**Error Response (Invalid Template):**
```json
{
  "isValid": false,
  "errors": [
    "Unclosed tag at position 45: {{BOLD:text",
    "Unknown tag: {{INVALID_TAG}}"
  ],
  "warnings": [
    "Tag {{OPTIONAL_TAG}} may not have data in all scenarios"
  ],
  "tags": ["DATE", "HOST", "BOLD", "INVALID_TAG"]
}
```

### Preview Template with Sample Data
```http
POST /api/notification-templates/preview
Authorization: Bearer {token}
Content-Type: application/json

{
  "templateContent": "Alert: {{EVENT_TYPE}} on {{HOST}} at {{DATE}}",
  "platform": "Teams",
  "sampleData": {
    "EVENT_TYPE": "Unauthorized Access",
    "HOST": "SERVER-01",
    "DATE": "2025-10-15 14:30:00 UTC"
  }
}
```

**Response:**
```json
{
  "renderedContent": "Alert: Unauthorized Access on SERVER-01 at 2025-10-15 14:30:00 UTC",
  "platform": "Teams"
}
```

### Supported Dynamic Tags

Templates support 15+ dynamic tags for event data substitution:

**Event Data Tags:**
- `{{DATE}}` - Event date/time (formatted: yyyy-MM-dd HH:mm:ss UTC)
- `{{HOST}}` - Machine/hostname where event occurred
- `{{USER}}` - Username associated with the event
- `{{EVENT_ID}}` - Windows Event ID number
- `{{EVENT_TYPE}}` - Event type classification
- `{{SEVERITY}}` - Severity level (Critical, High, Medium, Low)
- `{{RISK_LEVEL}}` - AI-determined risk level
- `{{CONFIDENCE}}` - AI confidence score (0-100%)
- `{{CHANNEL}}` - Event log channel (Security, System, etc.)

**Analysis Tags:**
- `{{SUMMARY}}` - AI-generated event summary
- `{{MITRE_TECHNIQUES}}` - MITRE ATT&CK techniques (comma-separated)
- `{{RECOMMENDED_ACTIONS}}` - AI-recommended response actions (numbered list)
- `{{CORRELATION_SCORE}}` - Correlation score (if event is correlated)

**Networking Tags:**
- `{{IP_ADDRESS}}` - Source IP address (if available)
- `{{DETAILS_URL}}` - Deep link to event details in dashboard

**Formatting Tags:**
- `{{BOLD:text}}` - Bold text (platform-specific formatting)
- `{{LINK:url|text}}` - Hyperlink formatting

**Features:**
- **8 Default Templates** - 4 template types × 2 platforms with rich formatting
- **Dynamic Tag System** - 15+ supported tags for event data substitution
- **Template Validation** - Real-time syntax validation with error messages
- **Live Preview** - Preview templates with sample data before saving
- **Platform-Specific Formatting** - Automatic formatting conversion for Teams/Slack
- **Automatic Initialization** - Templates created automatically on first Worker startup
- **File-Based Persistence** - Templates stored in JSON format at `data/notification-templates.json`
- **Visual Organization** - Rich formatting with visual separators, emoji headers, and professional branding

## Malware Detection API

### List Malware Detection Rules
```http
GET /api/malware-rules?page=1&limit=10&category=Malware&enabled=true
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

### Create Malware Detection Rule
```http
POST /api/malware-rules
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

### Get Malware Detection Rule Details
```http
GET /api/malware-rules/{ruleId}
```

### Update Malware Detection Rule
```http
PUT /api/malware-rules/{ruleId}
Content-Type: application/json

{
  "description": "Updated description",
  "isEnabled": false,
  "threatLevel": "Medium"
}
```

### Delete Malware Detection Rule
```http
DELETE /api/malware-rules/{ruleId}
```

### Test Malware Detection Rule
```http
POST /api/malware-rules/test
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
GET /api/malware-rules/categories
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
POST /api/malware-rules/{ruleId}/false-positive
Content-Type: application/json

{
  "reporter": "analyst@company.com",
  "comment": "This rule triggered on legitimate software",
  "context": "Windows System File"
}
```

## YARA Configuration API

### Get YARA Configuration
```http
GET /api/yara-configuration
```

**Response:**
```json
{
  "autoUpdate": {
    "enabled": false,
    "updateFrequencyDays": 7,
    "lastUpdate": null,
    "nextUpdate": null
  },
  "sources": {
    "enabled": true,
    "urls": [
      "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/APT_APT1.yar",
      "https://raw.githubusercontent.com/Neo23x0/signature-base/master/yara/apt_cobalt_strike.yar",
      "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_Zeus.yar",
      "https://raw.githubusercontent.com/Neo23x0/signature-base/master/yara/general_clamav_signature_set.yar",
      "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_Ransomware.yar",
      "https://raw.githubusercontent.com/YARAHQ/malware-rules/main/malware/TrickBot.yar"
    ],
    "maxRulesPerSource": 50
  },
  "rules": {
    "enabledByDefault": true,
    "autoValidation": true,
    "performanceThresholdMs": 1000
  },
  "import": {
    "lastImportDate": "2025-09-15T10:30:00Z",
    "totalRules": 70,
    "enabledRules": 70,
    "failedRules": 0
  },
  "createdAt": "2025-09-15T10:00:00Z",
  "updatedAt": "2025-09-15T10:30:00Z"
}
```

### Update YARA Configuration
```http
PUT /api/yara-configuration
Content-Type: application/json

{
  "autoUpdate": {
    "enabled": true,
    "updateFrequencyDays": 14
  },
  "sources": {
    "enabled": true,
    "urls": [
      "https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/APT_APT1.yar",
      "https://raw.githubusercontent.com/custom/rules/malware/Custom_Rules.yar"
    ],
    "maxRulesPerSource": 100
  },
  "rules": {
    "enabledByDefault": true,
    "autoValidation": true,
    "performanceThresholdMs": 2000
  }
}
```

**Validation Rules:**
- `updateFrequencyDays`: 1-365 days
- `maxRulesPerSource`: 1-1000 rules
- `performanceThresholdMs`: 100-10000 milliseconds
- `urls`: Array of valid HTTP/HTTPS URLs pointing to malware detection rule files

### Get YARA Import Statistics
```http
GET /api/yara-configuration/stats
```

**Response:**
```json
{
  "totalRules": 70,
  "enabledRules": 70,
  "disabledRules": 0,
  "failedRules": 0,
  "lastImportDate": "2025-09-15T10:30:00Z",
  "sources": [
    {
      "name": "GitHub YARA-Rules",
      "rulesCount": 20,
      "lastUpdate": "2025-09-15T10:30:00Z"
    },
    {
      "name": "Neo23x0 Signature-Base",
      "rulesCount": 25,
      "lastUpdate": "2025-09-15T10:30:00Z"
    },
    {
      "name": "YARAHQ Community",
      "rulesCount": 25,
      "lastUpdate": "2025-09-15T10:30:00Z"
    }
  ]
}
```

### Trigger Manual Malware Detection Rules Import
```http
POST /api/yara-configuration/import
```

**Response:**
```json
{
  "success": true,
  "message": "Successfully imported 15 new malware detection rules (Total: 85)",
  "imported": 15,
  "totalRules": 85,
  "enabledRules": 85,
  "failedRules": 0,
  "importDate": "2025-09-15T14:30:00Z"
}
```

**Features:**
- **Real Import Processing** - Executes actual YARA import tool with configurable rule limits
- **Source URL Management** - Dynamic configuration of malware detection rule source URLs
- **Auto-Update Configuration** - Configurable scheduling for automatic rule updates
- **Import Statistics** - Detailed tracking of rule counts and import success/failure
- **Performance Monitoring** - Rule execution time thresholds and optimization
- **Database Integration** - SQLite storage with automatic rule compilation and refresh

### Check Auto-Update Status
```http
GET /api/yara-configuration/check-update
```

**Response:**
```json
{
  "updateNeeded": true,
  "reason": "Last update was 8.5 days ago (threshold: 7 days)",
  "nextUpdate": "2025-09-22T10:30:00Z",
  "lastUpdate": "2025-09-14T10:30:00Z",
  "updateFrequencyDays": 7,
  "autoUpdateEnabled": true
}
```

## YARA Matches API

### Get YARA Matches
```http
GET /api/malware-rules/matches?securityEventId={eventId}&count=100
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

## Timeline Visualization API

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

## Export API

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

## Threat Scanner Configuration API

### Get Threat Scanner Configuration
```http
GET /api/scheduledscan/config
Authorization: Bearer {token}
```

**Response:**
```json
{
  "data": {
    "enabled": true,
    "scanInterval": "1.00:00:00",
    "defaultScanType": "Quick",
    "quarantine": {
      "enabled": false,
      "directory": "C:\\Castellan\\Quarantine"
    },
    "performance": {
      "maxConcurrentFiles": 10,
      "maxFileSizeMB": 100,
      "notificationThreshold": 10
    },
    "exclusions": {
      "directories": [
        "C:\\Windows\\System32",
        "C:\\Program Files"
      ],
      "extensions": [
        ".dll",
        ".sys",
        ".exe"
      ]
    }
  }
}
```

### Update Threat Scanner Configuration
```http
PUT /api/scheduledscan/config
Authorization: Bearer {token}
Content-Type: application/json

{
  "enabled": true,
  "scanInterval": "2.12:00:00",
  "defaultScanType": "Full",
  "quarantine": {
    "enabled": true,
    "directory": "D:\\Quarantine"
  },
  "performance": {
    "maxConcurrentFiles": 20,
    "maxFileSizeMB": 200,
    "notificationThreshold": 5
  },
  "exclusions": {
    "directories": [
      "C:\\Windows\\System32",
      "C:\\Program Files",
      "C:\\ProgramData"
    ],
    "extensions": [
      ".dll",
      ".sys"
    ]
  }
}
```

**Validation Rules:**
- `scanInterval`: TimeSpan format "d.hh:mm:ss" (e.g., "1.00:00:00" = 1 day, "0.12:00:00" = 12 hours)
- `defaultScanType`: "Quick" or "Full"
- `maxConcurrentFiles`: 1-100 files
- `maxFileSizeMB`: 1-1000 MB
- `notificationThreshold`: 1-100 threats
- `directories`: Array of valid Windows paths
- `extensions`: Array of file extensions (auto-prepends dot if missing)

**Response:**
```json
{
  "data": {
    "enabled": true,
    "scanInterval": "2.12:00:00",
    "defaultScanType": "Full",
    // ... updated configuration
  }
}
```

### Get Threat Scanner Status
```http
GET /api/scheduledscan/status
Authorization: Bearer {token}
```

**Response:**
```json
{
  "data": {
    "enabled": true,
    "lastScanTime": "2025-10-14T10:30:00Z",
    "nextScanTime": "2025-10-15T10:30:00Z",
    "currentStatus": "Idle",
    "scheduledScanType": "Quick"
  }
}
```

**Status Values:**
- `Idle` - Scanner ready, no active scan
- `Scanning` - Scan currently in progress
- `Paused` - Scanner temporarily paused
- `Error` - Scanner encountered an error

**Features:**
- **Scheduled Scanning** - Configurable scan intervals (days/hours) with automatic execution
- **Scan Type Selection** - Choose between Quick Scan (high-risk locations) and Full Scan (all drives)
- **Quarantine Management** - Enable/disable quarantine with configurable directory for suspicious files
- **Performance Tuning** - Control concurrent files, file size limits, and notification thresholds
- **Scan Exclusions** - Directory and file extension exclusion management for optimization
- **Real-time Status** - Monitor scheduler status with last/next scan times and current operation
- **TimeSpan Format** - .NET format "d.hh:mm:ss" for flexible interval configuration

## Malware Scanning API

### Scan Content with Malware Detection Rules
```http
POST /api/malware-rules/scan
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
GET /api/malware-rules/status
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

## Reporting API

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

## Advanced Search APIs

### Search History API

#### Get Search History
```http
GET /api/search-history?limit=20
```

**Query Parameters:**
- `limit` - Maximum number of history entries to return (default: 20, max: 100)

**Response:**
```json
{
  "data": [
    {
      "id": "hist-uuid-123",
      "searchCriteria": {
        "fullTextQuery": "powershell suspicious",
        "riskLevels": ["high", "critical"],
        "startDate": "2025-09-10T00:00:00Z",
        "endDate": "2025-09-16T23:59:59Z",
        "mitreTechniques": ["T1059.001"]
      },
      "resultCount": 45,
      "executionTimeMs": 234,
      "timestamp": "2025-09-15T10:30:00Z",
      "userId": "user-123"
    }
  ]
}
```

#### Record Search in History
```http
POST /api/search-history
Content-Type: application/json

{
  "searchCriteria": {
    "fullTextQuery": "authentication failure",
    "riskLevels": ["high"],
    "eventTypes": ["Authentication"]
  },
  "resultCount": 12,
  "executionTimeMs": 156
}
```

#### Clear Search History
```http
DELETE /api/search-history
```

### Saved Searches API

#### Get Saved Searches
```http
GET /api/saved-searches
```

**Response:**
```json
{
  "data": [
    {
      "id": "search-uuid-456",
      "name": "Critical Security Events",
      "description": "High-priority security events requiring immediate attention",
      "searchCriteria": {
        "riskLevels": ["critical"],
        "eventTypes": ["Authentication", "ProcessCreation"],
        "mitreTechniques": ["T1110", "T1059"]
      },
      "isShared": false,
      "createdAt": "2025-09-10T14:00:00Z",
      "updatedAt": "2025-09-15T09:00:00Z",
      "lastUsed": "2025-09-15T16:30:00Z",
      "useCount": 23,
      "userId": "user-123"
    }
  ]
}
```

#### Create Saved Search
```http
POST /api/saved-searches
Content-Type: application/json

{
  "name": "PowerShell Threats",
  "description": "Suspicious PowerShell activity detection",
  "searchCriteria": {
    "fullTextQuery": "powershell",
    "riskLevels": ["high", "critical"],
    "mitreTechniques": ["T1059.001"],
    "minConfidence": 70
  },
  "isShared": false
}
```

#### Get Saved Search Details
```http
GET /api/saved-searches/{searchId}
```

#### Update Saved Search
```http
PUT /api/saved-searches/{searchId}
Content-Type: application/json

{
  "name": "Updated Search Name",
  "description": "Updated description",
  "searchCriteria": {
    "fullTextQuery": "updated query",
    "riskLevels": ["critical"]
  }
}
```

#### Delete Saved Search
```http
DELETE /api/saved-searches/{searchId}
```

#### Execute Saved Search
```http
POST /api/saved-searches/{searchId}/execute
Content-Type: application/json

{
  "page": 1,
  "limit": 50
}
```

**Response:**
```json
{
  "searchResults": {
    "data": [
      {
        "id": 1,
        "eventType": "ProcessCreation",
        "riskLevel": "high",
        "summary": "Suspicious PowerShell execution detected"
      }
    ],
    "total": 45,
    "page": 1,
    "perPage": 50
  },
  "executionTime": 187,
  "searchCriteria": {
    "fullTextQuery": "powershell",
    "riskLevels": ["high", "critical"]
  }
}
```

## IP Enrichment Configuration API

### Get IP Enrichment Configuration
```http
GET /api/settings/ip-enrichment
```

**Response:**
```json
{
  "data": {
    "id": "ip-enrichment",
    "maxMind": {
      "enabled": true,
      "cityDbPath": "data/GeoLite2-City.mmdb",
      "asnDbPath": "data/GeoLite2-ASN.mmdb",
      "countryDbPath": "data/GeoLite2-Country.mmdb",
      "accountId": "your-account-id",
      "licenseKey": "your-license-key",
      "autoDownload": true,
      "lastUpdate": "2025-09-15T02:00:00Z",
      "nextUpdate": "2025-09-22T02:00:00Z"
    },
    "ipInfo": {
      "enabled": false,
      "apiKey": "",
      "rateLimitPerMinute": 1000,
      "cacheEnabled": true,
      "cacheTtlMinutes": 1440
    }
  }
}
```

### Update IP Enrichment Configuration
```http
PUT /api/settings/ip-enrichment
Content-Type: application/json

{
  "maxMind": {
    "enabled": true,
    "accountId": "your-maxmind-account-id",
    "licenseKey": "your-maxmind-license-key",
    "autoDownload": true
  },
  "ipInfo": {
    "enabled": true,
    "apiKey": "your-ipinfo-api-key",
    "rateLimitPerMinute": 1000,
    "cacheEnabled": true,
    "cacheTtlMinutes": 1440
  }
}
```

### Download MaxMind Databases
```http
POST /api/settings/ip-enrichment/download-databases
Content-Type: application/json

{
  "accountId": "your-maxmind-account-id",
  "licenseKey": "your-maxmind-license-key"
}
```

**Response:**
```json
{
  "success": true,
  "message": "MaxMind databases downloaded successfully",
  "downloadedDatabases": [
    {
      "name": "GeoLite2-City",
      "size": "68.5MB",
      "lastModified": "2025-09-15T02:00:00Z",
      "path": "data/GeoLite2-City.mmdb"
    },
    {
      "name": "GeoLite2-ASN",
      "size": "15.2MB",
      "lastModified": "2025-09-15T02:00:00Z",
      "path": "data/GeoLite2-ASN.mmdb"
    },
    {
      "name": "GeoLite2-Country",
      "size": "6.8MB",
      "lastModified": "2025-09-15T02:00:00Z",
      "path": "data/GeoLite2-Country.mmdb"
    }
  ],
  "nextScheduledUpdate": "2025-09-22T02:00:00Z"
}
```

**Error Response:**
```json
{
  "success": false,
  "message": "Failed to download databases",
  "errors": [
    "Authentication failed - check Account ID and License Key",
    "Network timeout occurred"
  ]
}
```

## Threat Intelligence Configuration API

### Get Threat Intelligence Configuration
```http
GET /api/settings/threat-intelligence
```

**Response:**
```json
{
  "data": {
    "id": "threat-intelligence",
    "virusTotal": {
      "enabled": true,
      "apiKey": "your-api-key",
      "rateLimitPerMinute": 4,
      "cacheEnabled": true,
      "cacheTtlMinutes": 60
    },
    "malwareBazaar": {
      "enabled": true,
      "rateLimitPerMinute": 10,
      "cacheEnabled": true,
      "cacheTtlMinutes": 30
    },
    "alienVaultOtx": {
      "enabled": false,
      "apiKey": "",
      "rateLimitPerMinute": 10,
      "cacheEnabled": true,
      "cacheTtlMinutes": 60
    }
  }
}
```

### Update Threat Intelligence Configuration
```http
PUT /api/settings/threat-intelligence
Content-Type: application/json

{
  "id": "threat-intelligence",
  "virusTotal": {
    "enabled": true,
    "apiKey": "your-virustotal-api-key",
    "rateLimitPerMinute": 4,
    "cacheEnabled": true,
    "cacheTtlMinutes": 90
  },
  "malwareBazaar": {
    "enabled": true,
    "rateLimitPerMinute": 15,
    "cacheEnabled": true,
    "cacheTtlMinutes": 45
  },
  "alienVaultOtx": {
    "enabled": true,
    "apiKey": "your-otx-api-key",
    "rateLimitPerMinute": 10,
    "cacheEnabled": true,
    "cacheTtlMinutes": 60
  }
}
```

**Validation Rules:**
- `rateLimitPerMinute`: 1-1000 for VirusTotal and AlienVault OTX, 1-60 for MalwareBazaar
- `cacheTtlMinutes`: 1-1440 minutes (1 minute to 24 hours)
- API keys required when provider is enabled (except MalwareBazaar)

**Response:**
```json
{
  "data": {
    "id": "threat-intelligence",
    "virusTotal": {
      "enabled": true,
      "apiKey": "your-virustotal-api-key",
      "rateLimitPerMinute": 4,
      "cacheEnabled": true,
      "cacheTtlMinutes": 90
    }
    // ... updated configuration
  }
}
```

**Error Response (Validation Failed):**
```json
{
  "message": "Validation failed",
  "errors": [
    "VirusTotal rate limit must be between 1 and 1000 requests per minute",
    "VirusTotal cache TTL must be between 1 and 1440 minutes"
  ]
}
```

## Configuration API

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

## Batch Operations API

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

## Dashboard Data Consolidation API

### Get Consolidated Dashboard Data
```http
GET /api/dashboarddata/consolidated?timeRange=24h
```

**Query Parameters:**
- `timeRange` - Time range for data aggregation: `1h`, `24h`, `7d`, `30d` (default: `24h`)

**Response:**
```json
{
  "data": {
    "securityEvents": {
      "totalEvents": 1786,
      "riskLevelCounts": {
        "critical": 23,
        "high": 156,
        "medium": 847,
        "low": 760
      },
      "recentEvents": [
        {
          "id": "evt-123",
          "eventType": "AuthenticationFailure",
          "timestamp": "2025-09-24T14:30:00Z",
          "riskLevel": "critical",
          "source": "Security",
          "machine": "WORKSTATION-01"
        }
      ],
      "lastEventTime": "2025-09-24T14:30:00Z"
    },
    "systemStatus": {
      "totalComponents": 8,
      "healthyComponents": 8,
      "components": [
        {
          "component": "Qdrant Vector Database",
          "status": "Healthy",
          "responseTime": 5,
          "lastCheck": "2025-09-24T14:30:00Z"
        }
      ],
      "componentStatuses": {
        "Qdrant Vector Database": "Healthy",
        "SecurityEventDetector": "Healthy"
      }
    },
    "compliance": {
      "totalReports": 5,
      "averageScore": 85.4,
      "passingReports": 4,
      "failingReports": 1,
      "recentReports": [
        {
          "id": "comp-123",
          "title": "Windows Security Baseline",
          "score": 92.5,
          "generated": "2025-09-24T12:30:00Z",
          "status": "Passed"
        }
      ]
    },
    "threatScanner": {
      "totalScans": 45,
      "activeScans": 2,
      "completedScans": 43,
      "threatsFound": 12,
      "lastScanTime": "2025-09-24T14:25:00Z",
      "recentScans": [
        {
          "id": "scan-123",
          "scanType": "FullSystem",
          "timestamp": "2025-09-24T14:25:00Z",
          "status": "Completed",
          "filesScanned": 15420,
          "threatsFound": 3
        }
      ]
    },
    "timeRange": "24h",
    "lastUpdated": "2025-09-24T14:30:00Z"
  }
}
```

**Key Features:**
- **Single API Call**: Replaces 4+ separate REST API calls
- **80%+ Performance Improvement**: Consolidated data fetching with parallel processing
- **Caching**: 30-second cache duration for optimal performance
- **SignalR Integration**: Real-time updates via WebSocket when available
- **Automatic Fallback**: Works as REST API when SignalR unavailable

### Consolidated Dashboard Data Schema

The consolidated dashboard data follows a hierarchical schema optimized for minimal payload size:

#### Root Object: `ConsolidatedDashboardData`
```typescript
{
  securityEvents: SecurityEventsSummary;
  systemStatus: SystemStatusSummary;
  threatScanner: ThreatScannerSummary;
  lastUpdated: string;         // ISO 8601 timestamp (UTC)
  timeRange: string;           // "1h" | "24h" | "7d" | "30d"
}
```

#### `SecurityEventsSummary`
```typescript
{
  totalEvents: number;         // Total events in time range
  riskLevelCounts: {
    critical: number;
    high: number;
    medium: number;
    low: number;
  };
  recentEvents: SecurityEventBasic[];  // Latest 10 events
  lastEventTime: string;       // ISO 8601 timestamp of most recent event
}
```

#### `SecurityEventBasic`
```typescript
{
  id: string;                  // Event UUID
  eventType: string;           // "AuthenticationFailure", "ProcessCreation", etc.
  timestamp: string;           // ISO 8601 timestamp (UTC)
  riskLevel: "critical" | "high" | "medium" | "low";
  source: string;              // Event log source ("Security", "System", etc.)
  machine: string;             // Source machine hostname
}
```

#### `SystemStatusSummary`
```typescript
{
  totalComponents: number;     // Total monitored components
  healthyComponents: number;   // Count of healthy components
  components: ComponentHealthBasic[];
  componentStatuses: {
    [componentName: string]: "Healthy" | "Degraded" | "Unhealthy";
  };
}
```

#### `ComponentHealthBasic`
```typescript
{
  component: string;           // Component name (e.g., "Qdrant Vector Database")
  status: "Healthy" | "Degraded" | "Unhealthy";
  responseTime: number;        // Response time in milliseconds
  lastCheck: string;           // ISO 8601 timestamp (UTC)
}
```

#### `ThreatScannerSummary`
```typescript
{
  totalScans: number;          // All-time scan count
  activeScans: number;         // Currently running scans
  completedScans: number;      // Successfully completed scans
  threatsFound: number;        // Total threats detected across all scans
  lastScanTime: string;        // ISO 8601 timestamp of most recent scan
  recentScans: ThreatScanBasic[];  // Latest 5 scans
}
```

#### `ThreatScanBasic`
```typescript
{
  id: string;                  // Scan UUID
  scanType: "FullSystem" | "QuickScan" | "CustomPath";
  timestamp: string;           // ISO 8601 timestamp (UTC)
  status: "Running" | "Completed" | "Failed" | "Cancelled";
  filesScanned: number;
  threatsFound: number;
}
```

### Get Individual Dashboard Components

The consolidated endpoint returns all data at once, but individual components can be fetched separately:

#### Get Security Events Summary Only
```http
GET /api/dashboarddata/security-events?timeRange=24h
Authorization: Bearer {token}
```

**Response:**
```json
{
  "totalEvents": 1786,
  "riskLevelCounts": {
    "critical": 23,
    "high": 156,
    "medium": 847,
    "low": 760
  },
  "recentEvents": [
    {
      "id": "evt-123",
      "eventType": "AuthenticationFailure",
      "timestamp": "2025-10-09T14:30:00Z",
      "riskLevel": "critical",
      "source": "Security",
      "machine": "WORKSTATION-01"
    }
  ],
  "lastEventTime": "2025-10-09T14:30:00Z"
}
```

#### Get System Status Summary Only
```http
GET /api/dashboarddata/system-status
Authorization: Bearer {token}
```

**Response:**
```json
{
  "totalComponents": 8,
  "healthyComponents": 8,
  "components": [
    {
      "component": "Qdrant Vector Database",
      "status": "Healthy",
      "responseTime": 5,
      "lastCheck": "2025-10-09T14:30:00Z"
    },
    {
      "component": "SecurityEventDetector",
      "status": "Healthy",
      "responseTime": 3,
      "lastCheck": "2025-10-09T14:30:00Z"
    }
  ],
  "componentStatuses": {
    "Qdrant Vector Database": "Healthy",
    "SecurityEventDetector": "Healthy",
    "CorrelationEngine": "Healthy",
    "YaraScanService": "Healthy"
  }
}
```

#### Get Threat Scanner Summary Only
```http
GET /api/dashboarddata/threat-scanner
Authorization: Bearer {token}
```

**Response:**
```json
{
  "totalScans": 45,
  "activeScans": 2,
  "completedScans": 43,
  "threatsFound": 12,
  "lastScanTime": "2025-10-09T14:25:00Z",
  "recentScans": [
    {
      "id": "scan-123",
      "scanType": "FullSystem",
      "timestamp": "2025-10-09T14:25:00Z",
      "status": "Completed",
      "filesScanned": 15420,
      "threatsFound": 3
    },
    {
      "id": "scan-124",
      "scanType": "QuickScan",
      "timestamp": "2025-10-09T14:20:00Z",
      "status": "Completed",
      "filesScanned": 8234,
      "threatsFound": 1
    }
  ]
}
```

### Dashboard Cache Management

#### Get Cache Status
```http
GET /api/dashboarddata/cache-status
Authorization: Bearer {token}
```

**Response:**
```json
{
  "cacheEnabled": true,
  "cacheDurationSeconds": 30,
  "lastUpdate": "2025-10-09T14:30:00Z",
  "message": "Dashboard data cache is active"
}
```

#### Refresh Dashboard Data (Force Cache Invalidation)
```http
POST /api/dashboarddata/refresh
Authorization: Bearer {token}
```

**Response:**
```json
{
  "message": "Dashboard data refresh triggered",
  "timestamp": "2025-10-09T14:30:00Z"
}
```

**Use Cases:**
- **Manual Refresh**: Force immediate cache invalidation and data update
- **Post-Configuration**: Refresh after changing system settings
- **Testing**: Verify real-time data updates
- **Development**: Clear stale cached data during debugging

**Note**: This endpoint triggers both cache invalidation and immediate SignalR broadcast to all connected clients.

### Trigger Dashboard Data Broadcast
```http
POST /api/dashboarddata/broadcast
```

**Response:**
```json
{
  "success": true,
  "message": "Dashboard data broadcast triggered successfully",
  "timestamp": "2025-09-24T14:30:00Z"
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

## Error Responses

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

## Rate Limiting

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
| Advanced Search APIs | 500 |
| Search History | 200 |
| Saved Searches | 200 |
| AI Analysis | 100 |
| Malware Detection Rules | 200 |
| YARA Matches | 300 |
| Malware Scanning | 50 |
| YARA Testing | 50 |
| Timeline Data | 500 |
| Timeline Stats | 200 |
| Export Operations | 100 |
| Export Downloads | 50 |
| Vector Search | 500 |
| System Status | 200 |
| Configuration | 30 |
| IP Enrichment Config | 50 |
| Threat Intelligence Config | 50 |

## Support and Integration

### SDK and Libraries
- **.NET Client**: Available via NuGet package
- **JavaScript/TypeScript**: NPM package available
- **Python**: PyPI package for integration
- **PowerShell Module**: Gallery module for automation

## 🏛️ Compliance API

### Compliance Reports
```http
GET /api/compliance-reports
Authorization: Bearer {token}
```

**Response:**
```json
{
  "data": [
    {
      "id": "comp-123",
      "framework": "HIPAA",
      "reportType": "assessment",
      "overallScore": 85.4,
      "controlsPassed": 14,
      "controlsFailed": 3,
      "generatedAt": "2025-09-29T10:30:00Z"
    }
  ]
}
```

### Compliance Posture (Phase 4 - NEW)
```http
GET /api/compliance-posture/summary
Authorization: Bearer {token}
```

**Response:**
```json
{
  "overallPosture": "Good",
  "averageScore": 82.5,
  "frameworks": [
    {
      "framework": "HIPAA",
      "score": 85.4,
      "trend": "improving",
      "lastAssessed": "2025-09-29T10:30:00Z"
    }
  ]
}
```

### Background Report Generation (Phase 4 Week 3 - NEW)
```http
POST /api/background-compliance-reports/queue
Authorization: Bearer {token}
Content-Type: application/json

{
  "framework": "HIPAA",
  "format": "Pdf",
  "audience": "Executive"
}
```

**Response:**
```json
{
  "jobId": "job-abc123",
  "status": "Queued",
  "estimatedCompletion": "2025-09-29T10:35:00Z"
}
```

### Compliance Performance Monitoring (Phase 4 Week 3 - NEW)
```http
GET /api/compliance-performance/metrics
Authorization: Bearer {token}
```

**Response:**
```json
{
  "data": {
    "totalReportsGenerated": 156,
    "averageReportGenerationTime": "00:00:02.340",
    "averagePdfGenerationTime": "00:00:01.875",
    "cacheHitRate": 0.72,
    "reportsByFramework": {
      "HIPAA": 45,
      "SOX": 38,
      "PCI-DSS": 32
    }
  },
  "summary": {
    "total_reports": 156,
    "cache_hit_rate": "72.00%",
    "avg_report_time_ms": 2340,
    "most_popular_framework": "HIPAA"
  }
}
```

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

**Castellan** - Comprehensive REST API for enterprise security monitoring and threat intelligence. 🏰
