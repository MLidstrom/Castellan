# Castellan Configuration Guide

**Status**: ✅ **Production Ready**  
**Last Updated**: September 16, 2025

## 🎯 Overview

This comprehensive guide covers all configuration options for Castellan, including security settings, performance tuning, AI providers, and operational parameters. Castellan provides extensive configurability while maintaining secure defaults.

## 🔧 Configuration Methods

### Sensitive data policy (no secrets in files)
- Do NOT store passwords, API keys, or tokens in JSON/YAML files or source control.
- Prefer environment variables for all sensitive values.
- Provide non-sensitive defaults in files; override via environment variables at runtime.

Example (PowerShell):
```powershell
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "<secure-password>"
$env:LLM__PROVIDER = "Ollama"
```
Note: Use .\\scripts\\start.ps1 to start Castellan after setting environment variables in your session.

### 1. Configuration Files
```powershell
# Primary configuration file
src/Castellan.Worker/appsettings.json

# Environment-specific overrides
src/Castellan.Worker/appsettings.Development.json
src/Castellan.Worker/appsettings.Production.json

# Runtime configuration (persisted via web UI)
src/Castellan.Worker/bin/Release/net8.0-windows/data/threat-intelligence-config.json
src/Castellan.Worker/bin/Release/net8.0-windows/data/notification-config.json
src/Castellan.Worker/bin/Release/net8.0-windows/data/ip-enrichment-config.json
```

**Runtime Configuration:**
- Configuration changes made through the web UI at `http://localhost:8080/#/configuration` are persisted to JSON files in the `data/` directory
- These files use camelCase naming convention for compatibility with JavaScript/TypeScript frontends
- Changes are automatically loaded when navigating between configuration tabs
- Runtime configuration overrides default settings from appsettings.json

### 2. Environment Variables
Environment variables override configuration file settings and follow the hierarchical naming pattern:
```powershell
$env:SECTION__SUBSECTION__PROPERTY = "value"
```

### 3. Command Line Arguments
```powershell
dotnet run --AUTHENTICATION__JWT__SECRETKEY="your-secret-key"
```

## 🔐 Authentication Configuration

### JWT Settings
```json
{
  "Authentication": {
    "Jwt": {
      "SecretKey": "",
      "Issuer": "castellan-security",
      "Audience": "castellan-admin",
      "ExpirationHours": 24
    },
    "AdminUser": {
      "Username": "",
      "Password": "",
      "Email": "admin@castellan.security",
      "FirstName": "Castellan",
      "LastName": "Administrator"
    }
  }
}
```

**Important Security Notes:**
- **SecretKey**: Must be set via environment variable `AUTHENTICATION__JWT__SECRETKEY` (minimum 64 characters)
- **Username**: Set via environment variable `AUTHENTICATION__ADMINUSER__USERNAME`
- **Password**: Set via environment variable `AUTHENTICATION__ADMINUSER__PASSWORD`
- Never store credentials directly in configuration files

Do not store AdminUser password in files. Set it via environment variable:
- PowerShell: `$env:AUTHENTICATION__ADMINUSER__PASSWORD = "<secure-password>"`

### Environment Variables
```powershell
# Required authentication settings
$env:AUTHENTICATION__JWT__SECRETKEY = "your-secure-jwt-secret-key-minimum-64-characters"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password"

# Optional JWT settings
$env:AUTHENTICATION__JWT__EXPIRATIONHOURS = "24"
```

## 🧠 AI Provider Configuration

### Ollama Configuration (Recommended)
```json
{
  "LLM": {
    "Provider": "Ollama",
    "Endpoint": "http://localhost:11434",
    "Model": "llama3.1:8b-instruct-q8_0",
    "OpenAIModel": "gpt-4o-mini",
    "OpenAIKey": ""
  },
  "Embeddings": {
    "Provider": "Ollama",
    "Endpoint": "http://localhost:11434",
    "Model": "nomic-embed-text",
    "OpenAIKey": ""
  }
}
```

### OpenAI Configuration
```json
{
  "LLM": {
    "Provider": "OpenAI",
    "Endpoint": "https://api.openai.com",
    "Model": "gpt-4o-mini",
    "OpenAIModel": "gpt-4o-mini",
    "OpenAIKey": ""
  },
  "Embeddings": {
    "Provider": "OpenAI",
    "Endpoint": "https://api.openai.com",
    "Model": "text-embedding-3-small",
    "OpenAIKey": ""
  }
}
```

**Security Note**: Set OpenAI API key via environment variable `OPENAI_API_KEY`

### Environment Variables
```powershell
# Ollama settings
$env:LLM__PROVIDER = "Ollama"
$env:EMBEDDINGS__PROVIDER = "Ollama"
$env:LLM__ENDPOINT = "http://localhost:11434"
$env:EMBEDDINGS__ENDPOINT = "http://localhost:11434"

# OpenAI settings (alternative)
$env:OPENAI_API_KEY = "your-openai-api-key"
$env:LLM__PROVIDER = "OpenAI"
$env:EMBEDDINGS__PROVIDER = "OpenAI"
```

## 🗄️ Qdrant Vector Database Configuration

### Basic Configuration
```json
{
  "Qdrant": {
    "UseCloud": false,
    "Host": "localhost",
    "Port": 6333,
    "Https": false,
    "ApiKey": "",
    "Collection": "log_events",
    "VectorSize": 768,
    "Distance": "Cosine"
  }
}
```

### Qdrant Cloud Configuration
```json
{
  "Qdrant": {
    "UseCloud": true,
    "Host": "your-cluster.qdrant.tech",
    "Port": 6333,
    "Https": true,
    "ApiKey": "",
    "Collection": "log_events",
    "VectorSize": 768,
    "Distance": "Cosine"
  }
}
```

**Security Note**: Set Qdrant API key via environment variable `QDRANT__APIKEY`

### Environment Variables
```powershell
$env:QDRANT__HOST = "localhost"
$env:QDRANT__PORT = "6333"
$env:QDRANT__HTTPS = "false"
$env:QDRANT__APIKEY = "your-qdrant-api-key"  # For Qdrant Cloud
$env:QDRANT__USECLOUD = "false"
```

## 🔗 Connection Pool Configuration

### Qdrant Connection Pool
```json
{
  "ConnectionPools": {
    "QdrantPool": {
      "Instances": [
        {
          "Host": "localhost",
          "Port": 6333,
          "Weight": 100,
          "UseHttps": false,
          "ApiKey": ""
        }
      ],
      "MaxConnectionsPerInstance": 50,
      "HealthCheckInterval": "00:00:30",
      "ConnectionTimeout": "00:00:10",
      "RequestTimeout": "00:01:00",
      "EnableFailover": true,
      "MinHealthyInstances": 1
    },
    "HttpClientPools": {
      "Pools": {
        "Default": {
          "MaxConnections": 100,
          "ConnectionTimeout": "00:00:30",
          "RequestTimeout": "00:02:00",
          "MaxRetries": 3,
          "CircuitBreakerThreshold": 5,
          "CircuitBreakerTimeout": "00:01:00",
          "EnableCompression": true
        },
        "LLM": {
          "MaxConnections": 20,
          "ConnectionTimeout": "00:01:00",
          "RequestTimeout": "00:05:00",
          "MaxRetries": 2,
          "CircuitBreakerThreshold": 3,
          "CircuitBreakerTimeout": "00:02:00"
        },
        "IPEnrichment": {
          "MaxConnections": 50,
          "ConnectionTimeout": "00:00:30",
          "RequestTimeout": "00:01:00",
          "MaxRetries": 3,
          "CircuitBreakerThreshold": 5,
          "CircuitBreakerTimeout": "00:01:00"
        }
      },
      "DefaultPool": "Default",
      "EnableAutoPoolCreation": true
    },
    "HealthMonitoring": {
      "Enabled": true,
      "CheckInterval": "00:00:30",
      "CheckTimeout": "00:00:05",
      "ConsecutiveFailureThreshold": 3,
      "ConsecutiveSuccessThreshold": 2,
      "EnableAutoRecovery": true,
      "RecoveryInterval": "00:01:00",
      "HealthHistoryRetention": "24:00:00"
    },
    "LoadBalancing": {
      "Algorithm": "WeightedRoundRobin",
      "EnableHealthAwareRouting": true,
      "PerformanceWindow": "00:05:00",
      "WeightAdjustment": {
        "ResponseTimeFactor": 0.4,
        "ErrorRateFactor": 0.3,
        "ConcurrencyFactor": 0.3,
        "MinimumWeightMultiplier": 0.1,
        "MaximumWeightMultiplier": 3.0
      },
      "StickySession": {
        "Enabled": false,
        "SessionDuration": "00:30:00",
        "MaxSessions": 10000
      }
    },
    "GlobalTimeouts": {
      "DefaultConnectionTimeout": "00:00:30",
      "DefaultRequestTimeout": "00:02:00",
      "MaxTimeout": "00:10:00",
      "DnsTimeout": "00:00:05"
    },
    "Metrics": {
      "Enabled": true,
      "CollectionInterval": "00:00:10",
      "RetentionPeriod": "24:00:00",
      "EnableDetailedMetrics": false,
      "MaxSamples": 10000
    }
  }
}
```

### Environment Variables
```powershell
# Connection pool settings
$env:CONNECTIONPOOLS__QDRANT__MAXCONNECTIONSPERINSTANCE = "10"
$env:CONNECTIONPOOLS__QDRANT__CONNECTIONTIMEOUT = "00:00:10"
$env:CONNECTIONPOOLS__QDRANT__REQUESTTIMEOUT = "00:01:00"
$env:CONNECTIONPOOLS__QDRANT__ENABLEFAILOVER = "true"

# Health monitoring
$env:CONNECTIONPOOLS__HEALTHMONITORING__ENABLED = "true"
$env:CONNECTIONPOOLS__HEALTHMONITORING__CHECKINTERVAL = "00:00:30"
$env:CONNECTIONPOOLS__HEALTHMONITORING__CONSECUTIVEFAILURETHRESHOLD = "3"

# Load balancing
$env:CONNECTIONPOOLS__LOADBALANCING__ALGORITHM = "WeightedRoundRobin"
$env:CONNECTIONPOOLS__LOADBALANCING__ENABLEHEALTHAWAREROUTING = "true"
```

## ⚡ Performance Configuration

### Pipeline Settings
```json
{
  "Pipeline": {
    "EnableParallelProcessing": true,
    "MaxConcurrency": 4,
    "ParallelOperationTimeoutMs": 30000,
    "EnableParallelVectorOperations": true,
    "BatchSize": 100,
    "ProcessingIntervalMs": 1000,
    "RetryAttempts": 3,
    "RetryDelayMs": 1000,
    "EnableSemaphoreThrottling": true,
    "MaxConcurrentTasks": 8,
    "SemaphoreTimeoutMs": 15000,
    "SkipOnThrottleTimeout": false,
    "EnableAdaptiveThrottling": false,
    "CpuThrottleThreshold": 80,
    "EventHistoryRetentionMinutes": 60,
    "MaxEventsPerCorrelationKey": 1000,
    "MemoryHighWaterMarkMB": 1024,
    "MemoryCleanupIntervalMinutes": 10,
    "EnableAggressiveGarbageCollection": false,
    "EnableMemoryPressureMonitoring": true,
    "MaxQueueDepth": 1000,
    "EnableQueueBackPressure": true,
    "DropOldestOnQueueFull": false,
    "EnableDetailedMetrics": true,
    "MetricsIntervalMs": 30000,
    "EnablePerformanceAlerts": true,
    "EnableVectorBatching": true,
    "VectorBatchSize": 100,
    "VectorBatchTimeoutMs": 5000,
    "VectorBatchProcessingTimeoutMs": 30000
  }
}
```

### Cache Configuration
```json
{
  "Cache": {
    "Enabled": true,
    "Provider": "Memory",
    "DefaultTtlMinutes": 30,
    "MaxMemoryMb": 512,
    "EnableMetrics": true,
    "Embedding": {
      "Enabled": true,
      "MaxEntries": 5000,
      "TtlMinutes": 60,
      "SimilarityThreshold": 0.95
    },
    "IpEnrichment": {
      "Enabled": true,
      "MaxEntries": 10000,
      "TtlMinutes": 240
    },
    "LlmResponse": {
      "Enabled": true,
      "MaxEntries": 2000,
      "TtlMinutes": 30,
      "HighConfidenceTtlMinutes": 60,
      "LowConfidenceTtlMinutes": 10
    },
    "VectorSearch": {
      "Enabled": true,
      "MaxEntries": 3000,
      "TtlMinutes": 15,
      "SimilarityThreshold": 0.90
    }
  }
}
```

### Environment Variables
```powershell
# Performance settings
$env:PIPELINE__ENABLEPARALLELPROCESSING = "true"
$env:PIPELINE__MAXCONCURRENCY = "4"
$env:PIPELINE__ENABLEVECTORBATCHING = "true"
$env:PIPELINE__VECTORBATCHSIZE = "50"
$env:PIPELINE__MAXCONCURRENTTASKS = "8"
$env:PIPELINE__ENABLEDETAILEDMETRICS = "true"

# Caching settings
$env:CACHING__ENABLED = "true"
$env:CACHING__MAXCACHESIZEMB = "512"
$env:CACHING__DEFAULTTTLMINUTES = "60"
$env:CACHING__SIMILARITYTHRESHOLD = "0.95"
```

## 📥 Event Ingestion Configuration

### Windows Event Log Collection
```json
{
  "Ingest": {
    "Evtx": {
      "Channels": ["Security", "System"],
      "XPath": "*[System[TimeCreated[timediff(@SystemTime) <= 30000]]]",
      "PollSeconds": 5
    }
  }
}
```

### Environment Variables
```powershell
$env:INGEST__EVTX__CHANNELS__0 = "Security"
$env:INGEST__EVTX__CHANNELS__1 = "System"
$env:INGEST__EVTX__POLLSECONDS = "5"
```

## 🚨 Alert Configuration

### Alert Settings
```json
{
  "Alerts": {
    "MinRiskLevel": "high",
    "EnableConsoleAlerts": true,
    "EnableFileLogging": true
  }
}
```

### Environment Variables
```powershell
$env:ALERTS__MINRISKLEVEL = "high"
$env:ALERTS__ENABLECONSOLEALERTS = "true"
$env:ALERTS__ENABLEFILELOGGING = "true"
```

## 🔔 Notification Configuration

### Desktop Notifications
```json
{
  "Notifications": {
    "EnableDesktopNotifications": true,
    "NotificationLevel": "high",
    "NotificationTimeout": 5000,
    "ShowEventDetails": true,
    "ShowIPEnrichment": true
  }
}
```

### Environment Variables
```powershell
$env:NOTIFICATIONS__ENABLEDESKTOPNOTIFICATIONS = "true"
$env:NOTIFICATIONS__NOTIFICATIONLEVEL = "high"
$env:NOTIFICATIONS__NOTIFICATIONTIMEOUT = "5000"
$env:NOTIFICATIONS__SHOWEVENTDETAILS = "true"
$env:NOTIFICATIONS__SHOWIPENRICHMENT = "true"
```

## 📊 Logging Configuration

### Structured Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Castellan": "Debug"
    },
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "TimestampFormat": "yyyy-MM-ddTHH:mm:ss.fffZ",
        "UseUtcTimestamp": true,
        "IncludeScopes": true
      }
    },
    "File": {
      "Enabled": true,
      "Path": "logs/castellan.log",
      "MaxFileSizeMB": 100,
      "MaxFiles": 10,
      "TimestampFormat": "yyyy-MM-ddTHH:mm:ss.fffZ"
    }
  }
}
```

### Correlation ID Configuration
```json
{
  "CorrelationId": {
    "Enabled": true,
    "HeaderName": "X-Correlation-ID",
    "IncludeInResponse": true,
    "LogPropertyName": "CorrelationId"
  }
}
```

## 🛡️ Security Configuration

### CORS Settings
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:8080",
      "https://your-domain.com"
    ],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "PATCH"],
    "AllowedHeaders": ["*"],
    "AllowCredentials": true
  }
}
```

### Rate Limiting
```json
{
  "RateLimiting": {
    "Enabled": true,
    "GlobalRules": {
      "RequestsPerMinute": 1000,
      "RequestsPerHour": 10000
    },
    "EndpointRules": {
      "/api/auth/*": {
        "RequestsPerMinute": 60
      },
      "/api/ai/*": {
        "RequestsPerMinute": 100
      }
    }
  }
}
```

## 🌐 Network Configuration

### ASP.NET Core Settings
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:5000"
      },
      "Https": {
        "Url": "https://localhost:5001",
        "Certificate": {
          "Path": "certificates/castellan.pfx",
          "Password": "${CERTIFICATE_PASSWORD}"
        }
      }
    }
  }
}
```

### Reverse Proxy Configuration
```json
{
  "ForwardedHeaders": {
    "Enabled": true,
    "ForwardedHeaders": ["XForwardedFor", "XForwardedProto"],
    "TrustedNetworks": ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"]
  }
}
```

## 🗃️ Database Configuration

### SQLite Settings
```json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionString": "Data Source=data/castellan.db",
    "EnableSensitiveDataLogging": false,
    "CommandTimeout": 30,
    "MaxRetries": 3,
    "EnableDetailedErrors": false
  }
}
```

### Connection Pool Settings
```json
{
  "Database": {
    "ConnectionPool": {
      "MaxPoolSize": 100,
      "MinPoolSize": 5,
      "ConnectionTimeout": 30,
      "PoolCleanupInterval": 300
    }
  }
}
```

## 🔍 IP Enrichment Configuration

### MaxMind GeoIP Settings
```json
{
  "IPEnrichment": {
    "Enabled": true,
    "Provider": "MaxMind",
    "MaxMindCityDbPath": "data/GeoLite2-City.mmdb",
    "MaxMindASNDbPath": "data/GeoLite2-ASN.mmdb",
    "MaxMindCountryDbPath": "data/GeoLite2-Country.mmdb",
    "CacheMinutes": 60,
    "MaxCacheEntries": 10000,
    "TimeoutMs": 5000,
    "EnrichPrivateIPs": false,
    "HighRiskCountries": ["CN", "RU", "KP", "IR", "SY", "BY"],
    "HighRiskASNs": [],
    "EnableDebugLogging": false
  }
}
```

### MaxMind Database Download Setup
To enable automatic MaxMind GeoLite2 database downloads:

1. **Sign up for MaxMind GeoLite2**: Visit [MaxMind's website](https://www.maxmind.com/en/geolite2/signup) and create a free account
2. **Generate License Key**: In your MaxMind account portal, generate a license key for GeoLite2 databases
3. **Configure Environment Variables**:
```powershell
# Set MaxMind credentials (never store in config files)
$env:MAXMIND_ACCOUNT_ID = "your-account-id"
$env:MAXMIND_LICENSE_KEY = "your-license-key"
```

4. **Manual Download**: Use the Configuration UI at `http://localhost:8080/#/configuration` → IP ENRICHMENT tab → "Download Databases Now" button
5. **Automatic Updates**: Databases automatically update weekly (Mondays at 2 AM) when configured

**Supported Databases**:
- **GeoLite2-City**: City-level IP geolocation
- **GeoLite2-Country**: Country-level IP geolocation
- **GeoLite2-ASN**: Autonomous System Number data

**Environment Variables**:
```powershell
$env:IPENRICHMENT__MAXMIND__ENABLED = "true"
$env:IPENRICHMENT__MAXMIND__ACCOUNTID = "your-account-id"
$env:IPENRICHMENT__MAXMIND__LICENSEKEY = "your-license-key"
$env:IPENRICHMENT__MAXMIND__AUTODOWNLOAD = "true"
```

## 🗃️ Security Event Retention Configuration

### Event Retention Settings
```json
{
  "SecurityEventRetention": {
    "RetentionDays": 30,
    "RetentionHours": 0,
    "MaxEventsInMemory": 10000,
    "CleanupIntervalMinutes": 60,
    "EnableTieredStorage": false,
    "HotStorageDays": 7,
    "WarmStorageDays": 30,
    "EnableCompression": true,
    "CompressionThresholdDays": 7
  }
}
```

### Environment Variables
```powershell
$env:SECURITYEVENTRETENTION__RETENTIONDAYS = "30"
$env:SECURITYEVENTRETENTION__MAXEVENTSINMEMORY = "10000"
$env:SECURITYEVENTRETENTION__CLEANUPINTERVALMINUTES = "60"
$env:SECURITYEVENTRETENTION__ENABLETIEREDSTORAGE = "false"
$env:SECURITYEVENTRETENTION__ENABLECOMPRESSION = "true"
```

## 🛡️ Malware Scanning Configuration

### YARA Detection Settings
```json
{
  "YaraScanning": {
    "Enabled": true,
    "MaxFileSizeMB": 100,
    "ScanTimeoutSeconds": 30,
    "MaxConcurrentScans": 4,
    "AutoScanSecurityEvents": true,
    "MinThreatLevel": "Medium",
    "Compilation": {
      "EnableFastMatching": true,
      "MaxMemoryMB": 256,
      "PrecompileOnStartup": true,
      "RefreshIntervalMinutes": 1440
    },
    "Performance": {
      "EnableMetrics": true,
      "SlowScanThresholdSeconds": 10,
      "ThreadsPerCore": 1,
      "StreamBufferSizeKB": 64
    }
  }
}
```

### Environment Variables
```powershell
$env:YARASCANNING__ENABLED = "true"
$env:YARASCANNING__MAXFILESIZEMB = "100"
$env:YARASCANNING__SCANTIMEOUTSECONDS = "30"
$env:YARASCANNING__MAXCONCURRENTSCANS = "4"
$env:YARASCANNING__AUTOSCANSECURITYEVENTS = "true"
$env:YARASCANNING__MINTHREATLEVEL = "Medium"
```

## 🎯 MITRE ATT&CK Configuration

### MITRE Integration Settings
```json
{
  "Mitre": {
    "AutoImportOnStartup": true,
    "RefreshIntervalDays": 1
  }
}
```

### Environment Variables
```powershell
$env:MITRE__AUTOIMPORTONSTARTUP = "true"
$env:MITRE__REFRESHINTERVALDAYS = "1"
```

## ⚙️ System Configuration

### Startup Configuration
```json
{
  "Startup": {
    "AutoStart": {
      "Enabled": true,
      "Qdrant": true,
      "ReactAdmin": true,
      "SystemTray": true
    }
  }
}
```

### Environment Variables
```powershell
$env:STARTUP__AUTOSTART__ENABLED = "true"
$env:STARTUP__AUTOSTART__QDRANT = "true"
$env:STARTUP__AUTOSTART__REACTADMIN = "true"
$env:STARTUP__AUTOSTART__SYSTEMTRAY = "true"
```

### Health Check Settings
```json
{
  "HealthChecks": {
    "Enabled": true,
    "CheckInterval": "00:00:30",
    "Timeout": "00:00:10",
    "Endpoints": {
      "Qdrant": "http://localhost:6333/health",
      "Ollama": "http://localhost:11434/api/version"
    }
  }
}
```

### Startup Configuration
```json
{
  "Startup": {
    "AutoStart": {
      "Enabled": true,
      "Services": ["Qdrant", "ReactAdmin"],
      "StartupTimeout": "00:02:00"
    },
    "Validation": {
      "ValidateOnStartup": true,
      "StrictValidation": true,
      "FailOnValidationError": true
    }
  }
}
```

## 📊 Metrics and Monitoring

### Performance Metrics
```json
{
  "Metrics": {
    "Enabled": true,
    "CollectionInterval": "00:00:10",
    "RetentionPeriod": "24:00:00",
    "ExportFormats": ["Prometheus", "JSON"],
    "EnableDetailedMetrics": false
  }
}
```

### Export Configuration
```json
{
  "Metrics": {
    "Prometheus": {
      "Enabled": true,
      "Endpoint": "/metrics",
      "Port": 9090
    },
    "StatsD": {
      "Enabled": false,
      "Host": "localhost",
      "Port": 8125,
      "Prefix": "castellan"
    }
  }
}
```

## 🔧 Configuration Validation

### Validation Rules
Castellan automatically validates configuration at startup:

```json
{
  "ConfigurationValidation": {
    "Enabled": true,
    "ValidateSecrets": true,
    "ValidateConnections": true,
    "ValidatePermissions": true,
    "FailOnValidationError": true,
    "ValidationTimeout": "00:01:00"
  }
}
```

### Manual Validation
```powershell
# Validate configuration without starting the service
dotnet run --validate-config-only

# Validate specific configuration section
dotnet run --validate-config="Authentication,Qdrant"
```

## 📋 Configuration Templates

### Development Template
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Castellan": "Trace"
    }
  },
  "Pipeline": {
    "EnableDetailedMetrics": true,
    "MaxConcurrency": 2
  },
  "Authentication": {
    "JWT": {
      "TokenExpirationHours": 8
    }
  }
}
```

### Production Template
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Pipeline": {
    "EnableDetailedMetrics": false,
    "MaxConcurrency": 8
  },
  "Authentication": {
    "JWT": {
      "TokenExpirationHours": 1
    }
  },
  "RateLimiting": {
    "Enabled": true
  }
}
```

## 🛠️ Configuration Management Best Practices

### Security Best Practices
1. **Never store secrets in configuration files**
2. **Use environment variables for sensitive data**
3. **Implement proper file permissions** (600 for config files)
4. **Regular secret rotation** (JWT keys, API keys)
5. **Configuration encryption** for production deployments

### Operational Best Practices
1. **Version control configuration templates** (without secrets)
2. **Automated configuration validation** in CI/CD
3. **Configuration drift detection**
4. **Environment-specific configurations**
5. **Configuration backup and recovery**

### Performance Tuning
1. **Monitor resource usage** and adjust limits accordingly
2. **Tune connection pool sizes** based on load
3. **Optimize cache settings** for your workload
4. **Adjust batch processing parameters** for throughput
5. **Configure appropriate timeouts** for your network

### Database Architecture Evolution
**Current Architecture (v1.0 Ready)**:
- **SQLite Database**: Production-ready with excellent performance for most use cases
- **Vector Search**: Qdrant integration for AI-powered threat analysis
- **JSON Storage**: Optimized event storage with efficient querying

**Optional Future Enhancement (Post-v1.0)**:
- **PostgreSQL Migration**: Optional upgrade for enhanced database performance and advanced JSON querying
- **Storage Consolidation**: Potential elimination of triple storage for unified data management
- **Advanced Partitioning**: Time-series partitioning for large-scale deployments
- **Status**: PostgreSQL is optional and not required for v1.0 production deployments

## 📞 Configuration Support

For configuration assistance:
- **Documentation**: Complete guides in `/docs` folder
- **GitHub Issues**: [Configuration problems](https://github.com/MLidstrom/Castellan/issues)
- **Community**: [GitHub Discussions](https://github.com/MLidstrom/Castellan/discussions)

---

**Castellan** - Comprehensive configuration management for enterprise security monitoring. 🏰🛡️
