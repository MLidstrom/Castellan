# Castellan Features

## 🔍 **Intelligent Log Analysis**
- **Real-time Windows Event Log Collection** - Monitors security, application, and system events
- **AI-Powered Threat Analysis** - LLM-based event classification with external threat intelligence
- **Vector Search** - Semantic similarity search using Qdrant vector database for correlation
- **Advanced Correlation** - M4 correlation engine with threat intelligence enrichment
- **🆕 File Threat Scanning** - Real-time malware detection with VirusTotal integration and local heuristics

## 🛡️ **Security Detection**
- **⚡ YARA Malware Detection** ✅ **PRODUCTION READY** - Complete signature-based malware detection system
  - **Native dnYara Integration** - Real malware scanning with dnYara 2.1.0 library
  - **React Admin Interface** - Complete web UI for rule management and match analysis
  - **Full CRUD Operations** - REST API and web interface for all rule operations
  - **Real-time Validation** - Native YARA rule compilation and syntax checking
  - **Performance Metrics** - Thread-safe scanning with execution time tracking
  - **Match History** - Complete audit trail with detailed forensic analysis
  - **MITRE ATT&CK Mapping** - Threat intelligence integration with visual indicators
  - **Advanced Filtering** - Category-based organization with color-coded threat levels
- **✅ Tier 1 Threat Intelligence** - Fully operational VirusTotal, MalwareBazaar, and AlienVault OTX integration for enhanced malware detection
- **IP Reputation & Geolocation** - MaxMind GeoLite2 databases for IP enrichment and threat correlation
- **MITRE ATT&CK Mapping** - Automatic threat technique classification with 800+ techniques
- **Anomaly Detection** - Machine learning-based behavioral analysis with vector similarity
- **Automated Response** - Real-time threat response with configurable actions and escalation

## 📊 **Monitoring & Analysis**
- **✅ Advanced Search & Filtering** ✅ **PRODUCTION READY** - Comprehensive security event search system
  - **Multi-Criteria Filtering** - Date ranges, risk levels, event types, MITRE ATT&CK techniques
  - **Full-Text Search** - High-performance SQLite FTS5 with exact match and fuzzy search options
  - **MITRE Technique Filtering** - 25+ common security techniques organized by tactic categories
  - **Numeric Range Filters** - Confidence, correlation, burst, and anomaly score filtering
  - **URL Synchronization** - Bookmarkable and shareable search states with persistent filters
  - **Real-time Results** - Search summaries with performance metrics and result counts
  - **Export Integration** - Direct CSV, JSON, XLSX export from filtered results
  - **Professional UI** - Responsive drawer interface with loading states and error handling
- **🆕 Real-time System Monitoring** - Live system health, performance metrics, and threat intelligence status via SignalR
- **✅ Enhanced Performance Dashboard** - Full-stack monitoring with 7 API endpoints, real-time charts, and configurable alerts
- **✅ Threat Intelligence Health Dashboard** - Comprehensive service health monitoring for VirusTotal, MalwareBazaar, and OTX
- **✅ Timeline Visualization** ✅ **PRODUCTION READY** - Interactive security event timeline analysis
  - **Granular Time Controls** - Minute, hour, day, week, month granularity selection
  - **Date Range Filtering** - Precise datetime selection with native browser controls
  - **Real-time Data Refresh** - Loading states with manual and automatic refresh options
  - **Summary Statistics** - Risk level breakdown and event type analysis
  - **Responsive Design** - Two-column layout with timeline chart and summary panels
  - **API Integration** - Timeline, events, heatmap, stats, and anomaly detection endpoints
- **Security Event Correlation** - Pattern detection and event relationship analysis
- **Threat Pattern Recognition** - AI-powered identification of attack sequences
- **Performance Monitoring** - System health and security service status with real-time dashboards
- **Persistent Storage** - 24-hour rolling window with automatic restart recovery
- **Application Data Management** - PostgreSQL database for enhanced performance, applications, MITRE ATT&CK techniques, and unified security event storage

## 🔔 **Notifications & Interface**
- **🆕 Teams/Slack Integration** - Real-time security alerts in Microsoft Teams and Slack channels
- **✅ Enhanced Performance Dashboard** - Full-featured performance monitoring with real-time metrics, multi-timeframe analytics (1h-7d), and interactive charts
- **✅ Threat Intelligence Health Dashboard** - Service status monitoring with API rate limiting, cache efficiency, and automated alerting
- **✅ React Admin Interface** - Complete management system with:
  - **Security Events Management** - List, view, edit security events with MITRE integration
  - **MITRE ATT&CK Techniques** - Browse and search 800+ techniques with statistics
  - **YARA Rules Management** - Full CRUD operations with validation and performance tracking
  - **YARA Matches Analysis** - Detection history with forensic details and correlation
  - **System Status Monitoring** - Component health with real-time indicators
  - **Notification Settings** - Teams/Slack configuration with test functionality
- **✅ Configuration Management** ✅ **PRODUCTION READY** - Complete threat intelligence settings with secure UI
  - **Three-Provider Interface** - VirusTotal, MalwareBazaar, AlienVault OTX configuration panels
  - **Secure API Key Management** - Password-type fields with show/hide functionality
  - **Real-time Validation** - Rate limits (1-1000/min), cache TTL (1-1440min) validation
  - **Persistent Storage** - File-based JSON storage with comprehensive error handling
  - **Security Compliance** - No plaintext passwords in repository, JWT authentication
- **✅ Data Export** ✅ **PRODUCTION READY** - Comprehensive data export system
  - **Multiple Formats** - CSV, JSON, PDF export with configurable field selection
  - **Background Processing** - Memory-efficient streaming for large datasets
  - **Export Filtering** - Apply security event filters to exported data
  - **Progress Tracking** - Real-time export status with download notifications
  - **Export Statistics** - Usage metrics and export history tracking
- **🆕 Real-time Web Dashboard** - Live system monitoring with SignalR-powered updates
- **Desktop Notifications** - Real-time security alerts
- **WebSocket Integration** - Real-time scan progress, system health, and threat intelligence status
- **Windows Native** - Optimized for Windows Event Log collection and analysis
- **Local Deployment** - No cloud dependencies, runs entirely on your local infrastructure

## 🔒 **Enterprise Security**
- **BCrypt Password Hashing** - Industry-standard password security with configurable work factors
- **JWT Token Management** - Secure refresh token rotation and server-side invalidation
- **Token Blacklisting** - Real-time token revocation with automatic cleanup
- **Password Complexity Validation** - Comprehensive password strength requirements
- **Audit Trail** - Complete authentication event logging for security monitoring
- **Configuration Validation** - Startup validation prevents deployment with invalid security settings
- **Error Handling** - Consistent security error responses with correlation tracking
