# CastellanAI v1.0.0 Release Notes

**Release Date**: October 23, 2025
**Release Type**: First Official Production Release
**Codename**: "Sentinel"

---

## Highlights

**CastellanAI v1.0.0** marks the **first official production release** of our AI-powered Windows security monitoring platform. This release represents the culmination of intensive development through v0.1.0 - v0.9.0, delivering a complete, enterprise-ready security monitoring solution with groundbreaking conversational AI capabilities.

### **What Makes v1.0.0 Special**

- **Conversational AI Security Analyst** - Natural language interface for threat hunting and investigation
- **Human-in-the-Loop Actions** - Action execution with 24-hour undo/rollback capability
- **Intelligent Citation Linking** - Click citations to view full security event details
- **Modern React Dashboard** - Tailwind CSS-based UI with real-time SignalR updates
- **Production-Ready Performance** - 12,000+ events/second, 30-70% cache hit rates
- **Enterprise Security** - BCrypt password hashing, JWT authentication, audit trails

---

## Major Features

### **Conversational AI Chat Interface** (v0.8.0 - v0.9.0)
**Location**: `http://localhost:3000/chat`

Transform security analysis with natural language conversations:

- **7 Intent Types**: Query, Investigate, Hunt, Compliance, Explain, Action, Conversational
- **RAG Context Retrieval**: Vector search + critical events + correlation patterns
- **Multi-Turn Conversations**: Database-backed conversation history with archiving
- **Smart Features**:
  - Citations with clickable links to security events
  - Suggested actions with confirmation dialogs
  - Follow-up question recommendations
  - Markdown rendering with syntax highlighting
  - Copy functionality for easy sharing
  - Conversation ratings and feedback

**Technical Implementation**:
- **Backend**: 9 REST API endpoints, IntentClassifier, ContextRetriever, ChatService, ConversationManager
- **Frontend**: ChatPage, ChatMessage, ChatInput, ConversationSidebar, SmartSuggestions, ThinkingIndicator
- **Database**: Conversations and ChatMessages tables with JSON serialization

### **Human-in-the-Loop Action Execution** (v0.9.0)
**Location**: Action buttons within chat responses

Execute security actions with full audit trail and rollback capability:

- **Action Types**: Block IP, Isolate Host, Quarantine File, Add to Watchlist, Create Ticket
- **Undo/Rollback**: 24-hour window for reversible actions
- **Action History**: Track all executed actions with before/after states
- **UI Components**:
  - Confirmation dialogs with impact warnings
  - Action History panel showing recent actions
  - Visual indicators for reversible vs non-reversible actions
- **Database**: ActionExecution table with status tracking and audit trail

### **Citation Linking** (v0.9.0)
**Feature**: Click citations in AI responses to view full event details

- Citations displayed under AI responses with event details
- Clickable citation cards navigate to `/security-events/{id}`
- Relevance scores shown as percentages
- ExternalLink icons for visual clarity

### **Shared Modal Components** (v1.0.0)
**Feature**: Consistent UX across Dashboard and Security Events pages

- Created `SecurityEventDetailModal.tsx` as shared component
- RecentActivity items on Dashboard now open modal dialog
- Same detailed view as Security Events page
- Includes all event fields (risk level, MITRE, scores, IPs, etc.)

---

## Core Platform Features

### **Security Event Processing**
- **24-Hour Rolling Window**: Automatic event cleanup for focused analysis
- **AI-Powered Detection**: LLM analysis with multi-model ensemble support
- **MITRE ATT&CK Mapping**: Automatic technique identification
- **Correlation Engine**: Background intelligence for pattern detection
- **Anomaly Detection**: ML.NET clustering with 8-feature analysis

### **Malware Detection** (v0.6.0)
- **70 Active malware Detection Rules**: Community-sourced signatures
- **Automatic Updates**: Daily refresh with configurable frequency
- **Database Consolidation**: Single `/data/castellan.db` for all components
- **Performance Optimization**: Database-level pagination (1-3s load times)

### **React Dashboard** (v0.7.0)
**Location**: `http://localhost:3000`

Modern security monitoring interface:

- **Pages**: Dashboard, Chat, Security Events, Timeline, MITRE ATT&CK, Malware Rules, Threat Scanner, System Status, Configuration
- **Real-Time Updates**: SignalR WebSocket integration
- **Dark Mode**: Full support throughout
- **Performance**: React Query caching with 30min memory retention, 24h localStorage persistence
- **Instant Page Loads**: <50ms with cached data

### **Notification System** (v0.7.0)
- **8 Default Templates**: 4 types × 2 platforms (Teams/Slack)
- **Rich Formatting**: Visual separators, emoji headers, organized sections
- **15+ Dynamic Tags**: DATE, HOST, USER, EVENT_TYPE, SEVERITY, etc.
- **Template Management UI**: Configuration → Notifications → Message Templates
- **REST API**: Full CRUD at `/api/notification-templates`

---

## Technical Improvements

### **AI Intelligence Upgrades** (v0.7.0 - v0.8.0)

**Week 1-2: Quick Wins**
1. **Embedding Cache**: 30-70% fewer API calls with LRU caching
2. **Polly Resilience**: Retry, circuit breaker, timeout patterns
3. **Strict JSON Validation**: 97%+ parse success with schema enforcement

**Week 3-4: Foundation**
4. **Hybrid Search**: Vector similarity (70%) + metadata scoring (30%)
5. **OpenTelemetry Tracing**: Distributed tracing with Jaeger/Zipkin support

**Week 5-6: Multi-Model Ensemble**
6. **LLM Factory Pattern**: Model-specific client creation
7. **Multi-Model Ensemble**: 20-30% accuracy improvement with voting strategies

**Week 7: Automated Evaluation**
8. **Evaluation Framework**: Automated quality assessment

**Week 8-10: Conversational AI**
9. **Chat Backend**: RAG, intent classification, 9 API endpoints
10. **Chat Frontend**: Complete UI with 5 components
11. **Chat Polish**: Action buttons, undo/rollback, citation linking

### **Performance Optimizations**

**Dashboard**:
- React Query snapshot caching (30min memory, 24h localStorage)
- Skeleton loading states with <50ms initial render
- Consolidated data architecture

**Database**:
- Connection pooling with EF Core PooledDbContextFactory
- SQLite WAL mode with 10MB cache, 5s busy timeout
- Database-level pagination for malware rules

**Worker**:
- Increased concurrent tasks (8→16)
- Consumer concurrency (4→8)
- Malware scanning concurrency (4→8)
- Semaphore timeout optimization (15s→10s)
- ImmediateDashboardBroadcast disabled (90-100% overhead reduction)

### **Security Enhancements**

- **BCrypt Password Hashing**: Secure password storage
- **JWT Token Blacklisting**: Server-side token invalidation
- **Refresh Token System**: Secure token rotation
- **Audit Trail**: Comprehensive logging for all actions
- **Admin User Configuration**: Environment-based setup

---

## Architecture

### **Backend Stack**
- .NET 8.0 ASP.NET Core Web API
- Entity Framework Core with SQLite
- SignalR for real-time communication
- Dependency Injection with service extensions

### **Frontend Stack**
- React 18+ with TypeScript
- Tailwind CSS 3+
- React Query for data fetching and caching
- React Router 6 for navigation
- Lucide React for icons
- Recharts for data visualization

### **AI/ML Stack**
- Qdrant vector database
- Ollama/OpenAI for embeddings and LLM
- ML.NET for clustering and anomaly detection
- Hybrid search with vector + metadata scoring

### **Data Storage**
- SQLite: `/data/castellan.db` (central database)
- JSON Files: Configuration persistence in `/data/` directory
- Qdrant: Vector embeddings for similarity search

---

## Migration Guide

### **From v0.9.0 to v1.0.0**

**Breaking Changes**: None
**New Features**: Shared modal components, comprehensive disclaimer

**Steps**:
1. No database migrations required
2. No configuration changes required
3. Update dashboard by running `npm install` and `npm run build`
4. Restart services: `.\scripts\stop.ps1` then `.\scripts\start.ps1`

### **Fresh Installation**

1. **Prerequisites**:
   - Windows 10/11 or Windows Server
   - .NET 8.0 SDK
   - Node.js 18+ with npm
   - Docker Desktop (for Qdrant)
   - Ollama (for local LLM) OR OpenAI API key

2. **Configuration**:
   - Copy `appsettings.template.json` → `appsettings.json`
   - Set `AUTHENTICATION__JWT__SECRETKEY` (64+ characters)
   - Set `AUTHENTICATION__ADMINUSER__USERNAME` and `PASSWORD`
   - Configure Ollama or OpenAI provider

3. **Start Services**:
   ```powershell
   .\scripts\start.ps1
   ```

4. **Access Dashboard**:
   - Dashboard: `http://localhost:3000`
   - AI Chat: `http://localhost:3000/chat`
   - Worker API: `http://localhost:5000`

---

## Known Issues & Limitations

### **Current Limitations**

1. **24-Hour Event Retention**: Security events older than 24 hours are automatically deleted
   - **Rationale**: AI pattern detection is optimized for 24-hour windows
   - **Workaround**: Export critical events before deletion
   - **Future**: Configurable retention periods in v1.1.0+

2. **Single-User Mode**: One admin user configured via environment variables
   - **Workaround**: Use single admin account for all analysts
   - **Future**: Multi-user RBAC in v1.1.0+

3. **Windows-Only**: Currently supports Windows Event Log monitoring only
   - **Future**: Multi-platform support in v1.2.0+

### **Known Issues**

1. **Large Conversation History**: Very long conversations (100+ messages) may experience slight lag
   - **Workaround**: Archive old conversations regularly
   - **Impact**: Minor UX degradation

2. **Qdrant Dependency**: Vector search requires Qdrant Docker container
   - **Workaround**: Ensure Docker is running before starting Worker
   - **Impact**: Service won't start without Qdrant

---

## What's Next (v1.1.0 Roadmap)

### **Planned Features** (Open Source)

1. **Export to PDF**: Export chat conversations as incident reports
2. **Advanced Performance**:
   - Streaming responses (token-by-token)
   - Intent classification caching
   - Virtual scrolling for long conversations
3. **Security Enhancements**:
   - Rate limiting (10 messages/minute)
   - Enhanced input validation
4. **UI/UX Improvements**:
   - Additional dashboard visualizations
   - Enhanced search and filtering
   - Accessibility improvements (WCAG 2.1)

### **Pro Version Features** (CastellanAI Pro)

For enterprise production deployments, **CastellanAI Pro** offers:
- **Multi-User RBAC**: Admin, Analyst, Viewer roles with granular permissions
- **Extended Retention**: Configurable event retention (30, 60, 90 days) with tiered storage
- **Compliance Reporting**: SOC2, PCI-DSS, HIPAA compliance frameworks
- **PostgreSQL Database**: Enterprise-scale database with time-series partitioning
- **Multi-Tenancy**: Tenant isolation and management
- **Professional Support**: SLA guarantees and dedicated support team

---

## Acknowledgments

**Development Team**: Exceptional progress through v0.1.0 - v0.9.0
**Open Source Community**: YARA rules, threat intelligence feeds
**AI Providers**: Ollama, OpenAI for LLM capabilities
**Framework Authors**: .NET, React, Tailwind CSS, and all dependencies

---

## License

CastellanAI is open source software. See LICENSE file for details.

---

## Disclaimer

This is an experimental security monitoring platform intended for research, education, and testing purposes. See README.md for complete disclaimer and production deployment guidelines.

For production deployments, consider **CastellanAI Pro** with professional support, SLA guarantees, and production-ready features.

---

## Support

- **Documentation**: See `/docs` directory
- **Issues**: Report at https://github.com/MLidstrom/Castellan/issues
- **Questions**: TROUBLESHOOTING.md

---

**CastellanAI v1.0.0 - "Sentinel" - First Official Production Release**
*October 23, 2025*
