# Castellan Features

## 🔍 **Intelligent Log Analysis**
- **Real-time Windows Event Log Collection** - Monitors security, application, and system events
- **AI-Powered Threat Analysis** - LLM-based event classification with external threat intelligence
- **Vector Search** - Semantic similarity search using Qdrant vector database for correlation
- **Advanced Correlation** - M4 correlation engine with threat intelligence enrichment
- **🆕 File Threat Scanning** - Real-time malware detection with VirusTotal integration and local heuristics

## 🛡️ **Security Detection**
- **🆕 YARA Malware Detection** - Signature-based malware detection with comprehensive rule management
  - Complete YARA rule management system with REST API
  - File-based rule storage with versioning and rollback support
  - Performance metrics and false positive tracking
  - MITRE ATT&CK technique mapping for threat intelligence
  - Category-based organization (Malware, Ransomware, Trojan, etc.)
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
- **✅ Configuration Management** - Complete threat intelligence settings management with persistent storage and validation
- **🆕 Real-time Web Dashboard** - Live system monitoring with SignalR-powered updates
- **Desktop Notifications** - Real-time security alerts
- **Web Admin Interface** - React-based management dashboard with live metrics and comprehensive dashboards
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
