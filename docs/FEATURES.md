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
- **🆕 Real-time System Monitoring** - Live system health, performance metrics, and threat intelligence status via SignalR
- **✅ Enhanced Performance Dashboard** - Full-stack monitoring with 7 API endpoints, real-time charts, and configurable alerts
- **✅ Threat Intelligence Health Dashboard** - Comprehensive service health monitoring for VirusTotal, MalwareBazaar, and OTX
- **Security Event Correlation** - Pattern detection and event relationship analysis
- **Threat Pattern Recognition** - AI-powered identification of attack sequences
- **Performance Monitoring** - System health and security service status with real-time dashboards
- **Event Timeline** - Chronological security event tracking with live updates
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
- **✅ Configuration Management** - Complete threat intelligence settings management with persistent storage and validation
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
