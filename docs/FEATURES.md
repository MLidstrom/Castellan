# Castellan Features

## 🔍 **Intelligent Log Analysis**
- **Real-time Windows Event Log Collection** - Monitors security, application, and system events
- **AI-Powered Threat Analysis** - LLM-based event classification with external threat intelligence
- **Vector Search** - Semantic similarity search using Qdrant vector database for correlation
- **Advanced Correlation** - M4 correlation engine with threat intelligence enrichment
- **File Threat Scanning** - Real-time malware detection with VirusTotal integration and local heuristics

## 🛡️ **Security Detection**
- **Anomaly Detection** - Machine learning-based behavioral analysis with vector similarity
- **Automated Response** - Real-time threat response with configurable actions and escalation
- **MITRE ATT&CK Mapping** - Automatic threat technique classification with 800+ techniques
- **YARA Real-time Malware Detection** - Complete signature-based malware detection system with dnYara 2.1.0 library
- **YARA Configuration Management** - Advanced rule source management with editable URL configuration
  - **Editable Rule Sources** - Dynamic add/remove functionality for YARA rule source URLs
  - **Source URL Management** - Text field interface for configuring malware signature sources
  - **Auto-Update Configuration** - Configurable frequency and automatic rule import scheduling
  - **Real Import Processing** - Actual rule import execution with accurate result reporting
  - **Source Statistics** - Active source count display and import success/failure tracking
  - **Minimum Source Validation** - Prevents removal of all sources with UI restrictions
- **React Admin Interface** - Complete web UI for rule management and match analysis
- **Full CRUD Operations** - REST API and web interface for all rule operations
- **Performance Metrics** - Thread-safe scanning with execution time tracking
- **Match History** - Complete audit trail with detailed forensic analysis
- **Advanced Filtering** - Category-based organization with color-coded threat levels
- **Tier 1 Threat Intelligence** - Fully operational VirusTotal, MalwareBazaar, and AlienVault OTX integration for enhanced malware detection
- **IP Reputation & Geolocation** - MaxMind GeoLite2 database integration with automated downloads, real IP geolocation, ASN data, and secure HTTP Basic Authentication

## 📊 **Monitoring & Analysis**
- **Trend Analysis & Forecasting** - AI-powered predictive analytics with ML.NET time series forecasting
  - **Historical Trend Analysis** - Security event volume trends with configurable time ranges (7d, 30d, 90d)
  - **ML.NET Forecasting** - Singular Spectrum Analysis (SSA) for accurate 7-30 day predictions
  - **Confidence Intervals** - Statistical bounds for forecast reliability assessment
  - **Interactive Visualizations** - Two-panel responsive UI showing both historical data and AI predictions
  - **Professional UI** - Material-UI components with color-coded sections and branded ML.NET chips
  - **Real-time Data Display** - Live historical event counts with formatted dates and prediction ranges
  - **Real-time API** - Dedicated analytics endpoints for trend data and forecast generation
  - **Customizable Metrics** - Support for various event types and aggregation methods
  - **Anomaly Detection** - Statistical identification of significant event volume deviations
- **Advanced Search & Filtering** - Comprehensive security event search system with complete v0.5.0 implementation
  - **Enhanced Search Interface** - Advanced search drawer with collapsible sections and intuitive controls
  - **Multi-Criteria Filtering** - Date ranges, risk levels, event types, MITRE ATT&CK techniques, machines, users, sources
  - **Full-Text Search** - High-performance SQLite FTS5 with exact match and fuzzy search options
  - **MITRE Technique Filtering** - 25+ common security techniques organized by tactic categories with multi-select
  - **Numeric Range Filters** - Confidence, correlation, burst, and anomaly score filtering with dual sliders
  - **URL Synchronization** - Bookmarkable and shareable search states with persistent filters and real-time URL updates
  - **Search History** - Recently used search queries with quick access and one-click reapplication
  - **Saved Searches** - Bookmark frequently used search configurations with custom names and descriptions
  - **Search Management** - Full CRUD operations for saved searches with backend persistence
  - **Real-time Results** - Search summaries with performance metrics, result counts, and loading states
  - **Export Integration** - Direct CSV, JSON, XLSX export from filtered results with applied search criteria
  - **Professional UI** - Responsive Material-UI drawer interface with accordion sections and error handling
- **Real-time System Monitoring** - Live system health, performance metrics, and threat intelligence status via SignalR
- **Enhanced Performance Dashboard** - Full-stack monitoring with 7 API endpoints, real-time charts, and configurable alerts
- **Threat Intelligence Health Dashboard** - Comprehensive service health monitoring for VirusTotal, MalwareBazaar, and OTX
- **Timeline Visualization** - Interactive security event timeline analysis
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
- **Application Data Management** - SQLite database with FTS5 full-text search for enhanced performance, MITRE ATT&CK techniques, and unified security event storage

## 🔔 **Notifications & Interface**
- **Teams/Slack Integration** - Real-time security alerts in Microsoft Teams and Slack channels
- **Enhanced Performance Dashboard** - Full-featured performance monitoring with real-time metrics, multi-timeframe analytics (1h-7d), and interactive charts
- **Threat Intelligence Health Dashboard** - Service status monitoring with API rate limiting, cache efficiency, and automated alerting
- **React Admin Interface** - Complete management system with:
  - **Security Events Management** - List, view, edit security events with MITRE integration
  - **MITRE ATT&CK Techniques** - Browse and search 800+ techniques with statistics
  - **YARA Rules Management** - Full CRUD operations with validation and performance tracking
  - **YARA Matches Analysis** - Detection history with forensic details and correlation
  - **System Status Monitoring** - Component health with real-time indicators
  - **Notification Configuration** - Teams/Slack webhook integration under Configuration tab
- **Configuration Management** - Centralized configuration system with tabbed interface
  - **Threat Intelligence Tab** - VirusTotal, MalwareBazaar, AlienVault OTX configuration panels
  - **IP Enrichment Tab** - MaxMind GeoLite2 configuration with automated database downloads
  - **YARA Configuration Tab** - Advanced YARA rule source management with editable URLs and auto-update settings
  - **Notifications Tab** - Teams and Slack webhook configuration with notification type controls
  - **Secure API Key Management** - Password-type fields with show/hide functionality for sensitive credentials
  - **Real-time Validation** - Rate limits (1-1000/min), cache TTL (1-1440min) validation with immediate feedback
  - **Persistent Storage** - Backend API storage with comprehensive error handling and secure credential storage
  - **Security Compliance** - No plaintext passwords in repository, JWT authentication, environment variable support
- **Data Export** - Comprehensive data export system
  - **Multiple Formats** - CSV, JSON, PDF export with configurable field selection
  - **Background Processing** - Memory-efficient streaming for large datasets
  - **Export Filtering** - Apply security event filters to exported data
  - **Progress Tracking** - Real-time export status with download notifications
  - **Export Statistics** - Usage metrics and export history tracking
- **Real-time Web Dashboard** - Live system monitoring with SignalR-powered updates
- **Desktop Notifications** - Real-time security alerts
- **WebSocket Integration** - Real-time scan progress, system health, and threat intelligence status
- **Windows Native** - Optimized for Windows Event Log collection and analysis
- **Local Deployment** - No cloud dependencies, runs entirely on your local infrastructure

## 🔌 **Comprehensive REST API**
- **Complete API Coverage** - 19+ controllers covering all system functionality
- **Authentication API** - Login, refresh, logout, and token validation endpoints
- **Security Events API** - Full CRUD operations with advanced search and filtering
- **Analytics API** - Trend analysis and forecasting endpoints with ML.NET predictions
- **Advanced Search APIs** - Dedicated endpoints for search history and saved searches management
- **System Monitoring APIs** - Performance metrics, system status, and health check endpoints
- **Configuration APIs** - Threat intelligence, notifications, and IP enrichment configuration
- **Export APIs** - Multi-format data export with background processing status
- **Timeline API** - Historical analysis with heatmaps, statistics, and anomaly detection
- **YARA Management APIs** - Complete malware detection rule and match management
- **YARA Configuration API** - Advanced rule source management and auto-update configuration
- **MITRE ATT&CK API** - 800+ technique browsing and security event mapping
- **Threat Intelligence APIs** - Health monitoring and configuration for multiple providers
- **IP Enrichment API** - MaxMind database management and geolocation services

## 🔒 **Enterprise Security**
- **BCrypt Password Hashing** - Industry-standard password security with configurable work factors
- **JWT Token Management** - Secure refresh token rotation and server-side invalidation
- **Token Blacklisting** - Real-time token revocation with automatic cleanup
- **Password Complexity Validation** - Comprehensive password strength requirements
- **Audit Trail** - Complete authentication event logging for security monitoring
- **Configuration Validation** - Startup validation prevents deployment with invalid security settings
- **Error Handling** - Consistent security error responses with correlation tracking
