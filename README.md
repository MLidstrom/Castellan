<p align="center">
    <picture>
        <source media="(prefers-color-scheme: dark)" srcset="assets/images/Castellan_light.png">
        <source media="(prefers-color-scheme: light)" srcset="assets/images/Castellan_dark.png">
        <img alt="Castellan logo" src="assets/images/Castellan.png">
    </picture>
</p>

<div align="center">

![GitHub Tag](https://img.shields.io/github/v/tag/MLidstrom/Castellan)
[![Open Source](https://img.shields.io/badge/Open%20Source-100%25-brightgreen.svg?logo=github)](#-open-source--enterprise-grade)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-Native-blue.svg?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![AI](https://img.shields.io/badge/AI-Powered-orange.svg)](https://openai.com/)
[![MITRE](https://img.shields.io/badge/MITRE-ATT%26CK-darkred.svg)](https://attack.mitre.org/)

**AI-Powered Windows Security Monitoring**

</div>

---

**Castellan** is a **100% open source**, enterprise-grade security monitoring platform that transforms Windows event logs into actionable security intelligence using AI-powered analysis, vector search, and real-time threat correlation.

🚀 **Enterprise Features**: 12K+ events/sec processing • Vector similarity search • Real-time Teams/Slack alerts • 800+ MITRE ATT&CK techniques • VirusTotal/MalwareBazaar integration • Complete YARA malware detection • Timeline visualization • Export capabilities • Threat intelligence configuration

⚡ **5-Minute Setup**: `.\scripts\start.ps1` → Open `http://localhost:8080` → Start monitoring

## 🔥 Key Features

- **🧠 AI-Powered Analysis** - LLM-based threat classification with vector similarity search
- **🛡️ Real-time Detection** - Live Windows Event Log monitoring with instant threat correlation  
- **📊 Enterprise Scale** - 12K+ events/sec processing with intelligent caching (30-50% boost)
- **🔔 Smart Notifications** - Rich Teams/Slack alerts with adaptive rate limiting
- **🎯 MITRE Integration** - Auto-updated 800+ ATT&CK techniques with threat mapping
- **🔍 Threat Intelligence** - VirusTotal, MalwareBazaar, AlienVault OTX with configuration UI
- **⚡ YARA Malware Detection** - Complete signature-based detection with React Admin UI
- **📋 Timeline Visualization** - Interactive security event timeline with granular analysis
- **📤 Data Export** - CSV, JSON, PDF export with filtering and background processing
- **📱 Real-time Dashboard** - React admin interface with SignalR live updates
- **🔒 Enterprise Security** - BCrypt passwords, JWT tokens, audit trails

## 🖼️ Screenshots

<p align="center">
  <img alt="Castellan Dashboard" src="assets/images/readme/dashboard.png" width="800" />
  <br><em>Real-time security monitoring dashboard with live threat intelligence</em>
</p>

<p align="center">
  <img alt="Teams Alert" src="assets/images/readme/teams-alert.png" width="600" />
  <br><em>Rich Microsoft Teams alert cards with actionable security context</em>
</p>

## 🚀 Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 
- [Docker](https://www.docker.com/get-started/) (for Qdrant)
- [Ollama](https://ollama.com/) or [OpenAI API key](https://platform.openai.com/api-keys)

### Installation

1. **Clone repository**
   ```powershell
   git clone https://github.com/MLidstrom/castellan.git
   cd castellan
   ```

2. **Configure authentication**
   ```powershell
   $env:AUTHENTICATION__JWT__SECRETKEY = "your-secure-jwt-secret-key-minimum-64-characters"
   $env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
   $env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password"
   ```

3. **Install AI models** (if using Ollama)
   ```powershell
   ollama pull nomic-embed-text
   ollama pull llama3.1:8b-instruct-q8_0
   ```

4. **Start services**
   ```powershell
   .\scripts\start.ps1
   ```

5. **Access dashboard**: Open `http://localhost:8080`

> **⚠️ Security Note**: See [Configuration Setup](docs/CONFIGURATION_SETUP.md) for detailed setup instructions.

## 🔥 Why Castellan?

### 🎆 **Production-Ready Enterprise Platform**
- **MIT Licensed** - 100% open source with complete transparency
- **5-Minute Setup** - From clone to monitoring in minutes
- **Enterprise Scale** - 12K+ events/sec with intelligent caching
- **AI-First** - Vector search + LLM analysis built-in
- **Zero Vendor Lock-in** - Fork, modify, deploy anywhere

### 🔄 **vs. Traditional SIEM Solutions**
| Feature | Castellan | Splunk/QRadar/ELK |
|---------|-----------|-------------------|
| **Deployment** | 5 minutes | Weeks to months |
| **AI/ML** | Built-in LLM + Vector | Add-on modules |
| **Customization** | Full source access | Vendor limited |
| **Cost** | Free + self-hosted | $$$$ + licensing |
| **Windows Focus** | Native optimization | Generic approach |

## 📚 Documentation

**[📖 Complete Documentation Index](docs/README.md)** - Master documentation hub with organized access to all guides, features, and technical references.

### Quick Reference
| Topic | Description |
|-------|-------------|
| **[🚀 Quick Start Guide](docs/GETTING_STARTED.md)** | Complete installation and setup instructions |
| **[🔥 Features](docs/FEATURES.md)** | Comprehensive feature overview and capabilities |
| **[🆕 YARA Detection](docs/YARA_DETECTION.md)** | Signature-based malware detection and rule management |
| **[🔧 Configuration](docs/CONFIGURATION_SETUP.md)** | Authentication, AI providers, threat intelligence setup |
| **[🔔 Notifications](docs/NOTIFICATIONS.md)** | Teams/Slack integration and alert configuration |
| **[🏢 Architecture](docs/ARCHITECTURE.md)** | System architecture, security, and observability |
| **[📈 Performance](docs/PERFORMANCE.md)** | Performance metrics, benchmarks, and optimization |
| **[🚑 Troubleshooting](docs/TROUBLESHOOTING.md)** | Common issues and solutions |

> **📚 For the complete documentation catalog** including API references, build guides, security features, integrations, and specialized guides, visit **[docs/README.md](docs/README.md)**.

## 🤝 Community & Support

- **[GitHub Issues](https://github.com/MLidstrom/Castellan/issues)** - Bug reports and feature requests
- **[GitHub Discussions](https://github.com/MLidstrom/Castellan/discussions)** - Community support and questions  
- **[Contributing Guide](CONTRIBUTING.md)** - How to contribute to the project
- **[Security Policy](SECURITY.md)** - Security practices and responsible disclosure

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

<div align="center">

**Castellan** - Your digital fortress guardian. 🏰🛡️

Built with ❤️ by the open source community

</div>
