# Castellan Configuration Guide

**Status**: ✅ **Production Ready**  
**Last Updated**: September 6, 2025

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
```

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
    "JWT": {
      "SecretKey": "your-secure-secret-key-minimum-64-characters",
      "Issuer": "Castellan",
      "Audience": "CastellanUsers",
      "TokenExpirationHours": 1,
      "RefreshTokenExpirationDays": 7,
      "EnableTokenBlacklist": true
    },
    "AdminUser": {
      "Username": "admin"
    },
    "PasswordPolicy": {
      "MinimumLength": 12,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireDigits": true,
      "RequireSpecialCharacters": true,
      "ForbidCommonPasswords": true
    }
  }
}
```

Do not store AdminUser password in files. Set it via environment variable:
- PowerShell: `$env:AUTHENTICATION__ADMINUSER__PASSWORD = "<secure-password>"`

### Environment Variables
```powershell
# Required authentication settings
$env:AUTHENTICATION__JWT__SECRETKEY = "your-secure-jwt-secret-key-minimum-64-characters"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password"

# Optional JWT settings
$env:AUTHENTICATION__JWT__TOKENEXPIRATIONHOURS = "2"
$env:AUTHENTICATION__JWT__REFRESHTOKENEXPIRATIONDAYS = "30"
```

## 🧠 AI Provider Configuration

### Ollama Configuration (Recommended)
```json
{
  "LLM": {
    "Provider": "Ollama",
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1:8b-instruct-q8_0",
    "Temperature": 0.1,
    "MaxTokens": 2048,
    "RequestTimeout": "00:05:00"
  },
  "Embeddings": {
    "Provider": "Ollama", 
    "BaseUrl": "http://localhost:11434",
    "Model": "nomic-embed-text",
    "Dimensions": 768,
    "RequestTimeout": "00:02:00"
  }
}
```

### OpenAI Configuration
```json
{
  "LLM": {
    "Provider": "OpenAI",
    "ApiKey": "${OPENAI_API_KEY}",
    "Model": "gpt-4",
    "Temperature": 0.1,
    "MaxTokens": 2048,
    "RequestTimeout": "00:03:00"
  },
  "Embeddings": {
    "Provider": "OpenAI",
    "ApiKey": "${OPENAI_API_KEY}",
    "Model": "text-embedding-3-small",
    "Dimensions": 1536,
    "RequestTimeout": "00:01:00"
  }
}
```

### Environment Variables
```powershell
# Ollama settings
$env:LLM__PROVIDER = "Ollama"
$env:EMBEDDINGS__PROVIDER = "Ollama"
$env:LLM__BASEURL = "http://localhost:11434"
$env:EMBEDDINGS__BASEURL = "http://localhost:11434"

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
    "Host": "localhost",
    "Port": 6333,
    "UseHttps": false,
    "ApiKey": null,
    "CollectionName": "security_events",
    "Timeout": "00:00:30",
    "RetryAttempts": 3,
    "RetryDelay": "00:00:02"
  }
}
```

### Qdrant Cloud Configuration
```json
{
  "Qdrant": {
    "Host": "your-cluster.qdrant.tech",
    "Port": 6333,
    "UseHttps": true,
    "ApiKey": "${QDRANT_API_KEY}",
    "CollectionName": "security_events"
  }
}
```

### Environment Variables
```powershell
$env:QDRANT__HOST = "localhost"
$env:QDRANT__PORT = "6333"
$env:QDRANT__USEHTTPS = "false"
$env:QDRANT__APIKEY = "your-qdrant-api-key"  # For Qdrant Cloud
```

## 🔗 Connection Pool Configuration

### Qdrant Connection Pool
```json
{
  "ConnectionPools": {
    "Qdrant": {
      "Instances": [
        {
          "Host": "localhost",
          "Port": 6333,
          "Weight": 100,
          "UseHttps": false
        },
        {
          "Host": "qdrant-replica",
          "Port": 6333,
          "Weight": 80,
          "UseHttps": false
        }
      ],
      "MaxConnectionsPerInstance": 10,
      "ConnectionTimeout": "00:00:10",
      "RequestTimeout": "00:01:00",
      "EnableFailover": true,
      "MinHealthyInstances": 1
    },
    "HealthMonitoring": {
      "Enabled": true,
      "CheckInterval": "00:00:30",
      "CheckTimeout": "00:00:05",
      "ConsecutiveFailureThreshold": 3,
      "ConsecutiveSuccessThreshold": 2,
      "EnableAutoRecovery": true,
      "RecoveryInterval": "00:01:00"
    },
    "LoadBalancing": {
      "Algorithm": "WeightedRoundRobin",
      "EnableHealthAwareRouting": true,
      "PerformanceWindow": "00:05:00"
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
    "EnableVectorBatching": true,
    "VectorBatchSize": 50,
    "VectorBatchTimeoutMs": 5000,
    "VectorBatchProcessingTimeoutMs": 30000,
    "EnableSemaphoreThrottling": true,
    "MaxConcurrentTasks": 8,
    "SemaphoreTimeoutMs": 15000,
    "MemoryHighWaterMarkMB": 1024,
    "EventHistoryRetentionMinutes": 60,
    "EnableDetailedMetrics": true
  }
}
```

### Caching Configuration
```json
{
  "Caching": {
    "Enabled": true,
    "MaxCacheSizeMB": 512,
    "DefaultTTLMinutes": 60,
    "SimilarityThreshold": 0.95,
    "EnableLRUEviction": true,
    "MemoryPressureThreshold": 0.8,
    "CleanupIntervalMinutes": 15
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

## 🔔 Notification Configuration

### Desktop Notifications
```json
{
  "Notifications": {
    "Desktop": {
      "Enabled": true,
      "MinRiskLevel": "medium",
      "ShowIPEnrichment": true,
      "MaxNotificationsPerMinute": 10
    }
  }
}
```

### Teams/Slack Integration
```json
{
  "Notifications": {
    "Webhooks": [
      {
        "Name": "Security Team Channel",
        "Url": "https://outlook.office.com/webhook/...",
        "Platform": "teams",
        "Enabled": true,
        "SeverityFilter": ["critical", "high"],
        "RateLimiting": {
          "Critical": "00:00:00",
          "High": "00:05:00",
          "Medium": "00:15:00",
          "Low": "01:00:00"
        }
      }
    ]
  }
}
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
    "MaxMind": {
      "Enabled": true,
      "DatabasePath": "data/GeoLite2-City.mmdb",
      "ASNDatabasePath": "data/GeoLite2-ASN.mmdb",
      "CountryDatabasePath": "data/GeoLite2-Country.mmdb",
      "CacheSize": 10000,
      "CacheTTLMinutes": 1440
    },
    "RiskAssessment": {
      "Enabled": true,
      "HighRiskCountries": ["CN", "RU", "KP"],
      "HighRiskASNs": [4134, 4837],
      "TorExitNodeDetection": true
    }
  }
}
```

## ⚙️ System Configuration

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
