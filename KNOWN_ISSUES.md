# Known Issues and Workarounds

**Version**: v1.0.0
**Last Updated**: October 23, 2025

This document tracks known limitations, issues, and their workarounds for CastellanAI v1.0.0.

---

## Table of Contents

- [Current Limitations](#current-limitations)
- [Known Issues](#known-issues)
- [Performance Considerations](#performance-considerations)
- [Workarounds](#workarounds)
- [Future Improvements](#future-improvements)

---

## Current Limitations

### **1. 24-Hour Event Retention Window**

**Description**: Security events older than 24 hours are automatically deleted by `EventCleanupService`.

**Impact**:
- Historical analysis limited to 24-hour window
- Long-term trend analysis not available
- Event data cannot be recovered once deleted

**Rationale**: AI pattern detection algorithms are optimized for 24-hour windows for security event correlation.

**Workaround**:
- Export critical events to CSV/JSON before 24-hour window expires
- Use `/api/security-events/export` endpoint for data archival
- Set up scheduled exports for compliance requirements

**Status**: Design decision for open source version

**Future**: Extended retention periods (30, 60, 90 days) available in **CastellanAI Pro** with tiered storage approach

---

### **2. Single-User Admin Mode**

**Description**: Only one admin user configured via environment variables (`AUTHENTICATION__ADMINUSER__USERNAME` and `PASSWORD`).

**Impact**:
- No multi-user support
- No role-based access control (RBAC)
- All analysts share single admin account
- Cannot track individual user actions

**Workaround**:
- Use single admin account credentials
- Document user actions in external system
- Manually attribute actions in audit logs

**Status**: Current architecture limitation for open source version

**Future**: Full multi-user RBAC available in **CastellanAI Pro** with Admin, Analyst, and Viewer roles

---

### **3. Windows-Only Event Monitoring**

**Description**: Currently supports Windows Event Log monitoring only.

**Impact**:
- Linux/macOS system logs not supported
- Cannot monitor non-Windows infrastructure
- Limited to Windows security events

**Workaround**:
- Use dedicated Windows security monitoring for Windows hosts
- Integrate with existing SIEM for non-Windows logs
- Deploy CastellanAI on Windows Server for centralized Windows monitoring

**Status**: Platform-specific design

**Future**: Multi-platform support in v1.2.0+ (syslog, journald, cloud platform logs)

---

### **4. Local LLM Requirement (Ollama or OpenAI)**

**Description**: Requires either local Ollama installation with models or OpenAI API key.

**Impact**:
- Additional infrastructure requirement
- Ollama requires ~8-16GB RAM for models
- OpenAI incurs API costs
- Air-gapped environments require local Ollama

**Workaround**:
- Use Ollama for air-gapped/cost-sensitive deployments
- Use OpenAI for managed LLM service
- Pre-pull Ollama models: `nomic-embed-text`, `llama3.1:8b-instruct-q8_0`

**Status**: AI-powered analysis requirement

**Future**: Additional LLM provider support (Azure OpenAI, AWS Bedrock, Google Vertex AI)

---

### **5. Qdrant Vector Database Dependency**

**Description**: Vector search requires Qdrant Docker container running.

**Impact**:
- Docker Desktop installation required
- Additional resource consumption (~512MB-1GB RAM)
- Service won't start without Qdrant
- Additional infrastructure complexity

**Workaround**:
- Use provided Docker Compose configuration
- Ensure Docker is running before starting Worker: `docker ps`
- Start Qdrant manually if needed: `docker run -d --name qdrant -p 6333:6333 qdrant/qdrant`

**Status**: Architecture dependency for vector similarity search

**Future**: Alternative vector stores (Milvus, Weaviate, Chroma) in v1.2.0+

---

## Known Issues

### **1. Large Conversation History Performance**

**Severity**: Minor
**Component**: Chat Interface

**Description**: Very long conversations (100+ messages) may experience slight lag when loading or rendering.

**Impact**:
- Slower page load (1-3 seconds)
- Increased memory usage
- Occasional UI stuttering

**Workaround**:
- Archive old conversations regularly
- Use "New Conversation" button for fresh context
- Clear browser cache if experiencing performance degradation

**Root Cause**: Rendering 100+ messages with markdown, citations, and action buttons

**Status**: Performance optimization planned for v1.1.0 (virtual scrolling)

---

### **2. SignalR Reconnection Delay**

**Severity**: Minor
**Component**: Real-Time Updates

**Description**: After network interruption, SignalR may take 10-30 seconds to reconnect.

**Impact**:
- Temporary loss of real-time updates
- Dashboard metrics may be stale during reconnection
- Chat interface may not reflect latest messages

**Workaround**:
- Refresh browser page to force immediate reconnection
- Check SignalR status indicator (top-right of dashboard)
- Wait for automatic reconnection (typically <30 seconds)

**Root Cause**: Default SignalR reconnection backoff strategy

**Status**: Acceptable for current use case, configurable in future releases

---

### **3. Malware Rule Import Timeout**

**Severity**: Minor
**Component**: YARA Rule Management

**Description**: Importing very large YARA rule files (>1000 rules) may timeout.

**Impact**:
- Cannot import extremely large rule sets in single operation
- UI may show timeout error after 30 seconds

**Workaround**:
- Split large rule files into smaller chunks (<500 rules each)
- Use database-level import via `YaraImportTool`
- Increase timeout in `appsettings.json` under `RequestTimeout`

**Root Cause**: HTTP request timeout during rule parsing and validation

**Status**: Edge case, most rule sets are <200 rules

---

### **4. Dashboard Loading Spinner Overlap**

**Severity**: Cosmetic
**Component**: React Dashboard

**Description**: Skeleton loading animation may briefly overlap with actual content on very fast networks.

**Impact**:
- Minor visual glitch for <100ms
- Does not affect functionality

**Workaround**: None needed, visual-only issue

**Root Cause**: React Query state transition timing

**Status**: Low priority, cosmetic issue only

---

## Performance Considerations

### **High Event Volume (15,000+ EPS)**

**Description**: System designed for 12,000+ events/second sustained throughput.

**Recommendation**:
- Monitor system resources (CPU, RAM, disk I/O)
- Increase semaphore limits in `appsettings.json` if needed
- Consider horizontal scaling for >20,000 EPS
- Enable vector batching for better vector operation performance

**Configuration**:
```json
{
  "Pipeline": {
    "MaxConcurrentTasks": 16,
    "MaxConcurrentScans": 8,
    "ConsumerConcurrency": 8,
    "SemaphoreTimeoutMs": 10000,
    "SkipOnThrottleTimeout": true
  }
}
```

---

### **Database Size Growth**

**Description**: SQLite database grows with event volume, even with 24-hour retention.

**Recommendation**:
- Monitor `/data/castellan.db` file size
- Run `VACUUM` periodically to reclaim space
- Consider PostgreSQL migration for >1M events/day

**Workaround**:
```sql
-- SQLite VACUUM to reclaim space
VACUUM;
```

---

### **Ollama Model Loading Time**

**Description**: First LLM request after Ollama restart may take 10-30 seconds as model loads into memory.

**Impact**:
- First chat message or security analysis may be slow
- Subsequent requests are fast (<3 seconds)

**Workaround**:
- Keep Ollama running continuously
- Use Ollama warmup: `ollama run llama3.1:8b-instruct-q8_0 --verbose`
- Pre-load models at startup

---

## Workarounds

### **Export Security Events Before 24-Hour Deletion**

```powershell
# Export all events to JSON
Invoke-WebRequest -Uri "http://localhost:5000/api/security-events/export?format=json" `
  -Headers @{ Authorization = "Bearer $token" } `
  -OutFile "events_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"

# Export to CSV
Invoke-WebRequest -Uri "http://localhost:5000/api/security-events/export?format=csv" `
  -Headers @{ Authorization = "Bearer $token" } `
  -OutFile "events_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
```

---

### **Manual Conversation Archive**

```powershell
# Archive conversation via API
$conversationId = "conversation-guid"
Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5000/api/chat/conversations/$conversationId/archive" `
  -Headers @{ Authorization = "Bearer $token" }
```

---

### **Force Qdrant Restart**

```bash
# Stop Qdrant container
docker stop qdrant

# Remove container
docker rm qdrant

# Start fresh Qdrant instance
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

---

## Future Improvements

### **Open Source Roadmap**

#### **Planned for v1.1.0**

1. **Virtual Scrolling** for long conversations
2. **Rate Limiting** (10 messages/minute)
3. **Streaming Responses** (token-by-token)
4. **Export to PDF** (chat conversations as incident reports)
5. **Enhanced Input Validation**

#### **Planned for v1.2.0**

6. **Multi-Platform Support** (Linux, macOS, cloud logs)
7. **Alternative Vector Stores** (Milvus, Weaviate, Chroma)
8. **Additional LLM Providers** (Azure OpenAI, AWS Bedrock, Google Vertex AI)
9. **UI/UX Improvements** (accessibility, visualizations, search)

#### **Planned for v1.3.0**

10. **Cloud-Native Deployment** (Kubernetes, Docker Swarm)
11. **Enhanced Search and Filtering**
12. **Additional Integrations** (webhook support, custom notifications)

### **Pro Version Features** (CastellanAI Pro)

The following enterprise features are available in **CastellanAI Pro**:

- **Multi-User RBAC**: Admin, Analyst, Viewer roles with granular permissions
- **Extended Retention**: Configurable event retention (30, 60, 90 days) with tiered storage
- **Compliance Reporting**: SOC2, PCI-DSS, HIPAA, FedRAMP compliance frameworks
- **PostgreSQL Database**: Enterprise-scale database with time-series partitioning
- **Multi-Tenancy**: Tenant isolation and management
- **Enterprise Integrations**: SIEM, SOAR platforms with professional support
- **Professional Support**: SLA guarantees and dedicated support team

---

## Reporting Issues

If you encounter an issue not listed here:

1. **Check Documentation**: Review README.md, and docs/ directory
2. **Search Existing Issues**: Check if issue is already reported
3. **Gather Information**:
   - CastellanAI version
   - Environment (Windows version, .NET version, Node version)
   - Error messages and stack traces
   - Steps to reproduce
4. **Report Issue**: File detailed bug report with reproduction steps

---

**Last Updated**: October 23, 2025
**Version**: v1.0.0
**Status**: Production Release
