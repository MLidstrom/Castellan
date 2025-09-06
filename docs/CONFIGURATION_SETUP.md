# Configuration Setup Guide

## ⚠️ Important: Initial Configuration Required

This project requires configuration before first use. The repository includes a template configuration file but **does NOT include actual credentials for security reasons**.

## Quick Setup

### 1. Create Your Configuration File

Copy the template configuration to create your actual configuration file:

```powershell
# Navigate to the Worker directory
cd src\Castellan.Worker

# Copy the template to create your configuration
Copy-Item appsettings.template.json appsettings.json
```

### 2. Configure Authentication (REQUIRED)

Edit `src\Castellan.Worker\appsettings.json` and update the authentication section:

```json
"Authentication": {
  "Jwt": {
    "SecretKey": "YOUR_SECURE_64_CHARACTER_KEY_HERE_REPLACE_THIS_IMMEDIATELY",
    "Issuer": "castellan-security",
    "Audience": "castellan-admin",
    "ExpirationHours": 24
  },
  "AdminUser": {
    "Username": "your-admin-username",
    "Password": "your-secure-password",
    "Email": "admin@yourdomain.com",
    "FirstName": "Your",
    "LastName": "Name"
  }
}
```

**Security Requirements:**
- JWT SecretKey MUST be at least 64 characters long
- Use a strong, unique password
- Never commit real credentials to version control

### 3. Configure AI Providers (Optional)

#### For Local AI (Ollama) - Default
No additional configuration needed if using Ollama locally.

#### For OpenAI
Update the configuration with your OpenAI API key:

```json
"Embeddings": {
  "Provider": "OpenAI",
  "OpenAIKey": "sk-your-openai-api-key-here"
},
"LLM": {
  "Provider": "OpenAI",
  "OpenAIModel": "gpt-4o-mini",
  "OpenAIKey": "sk-your-openai-api-key-here"
}
```

### 4. Configure Qdrant (Optional)

#### For Qdrant Cloud
If using Qdrant Cloud instead of local Docker:

```json
"Qdrant": {
  "UseCloud": true,
  "Host": "your-cluster.qdrant.io",
  "Port": 6333,
  "Https": true,
  "ApiKey": "your-qdrant-api-key"
}
```

### 5. Configure MITRE ATT&CK Import (Optional)

Castellan automatically imports MITRE ATT&CK data by default. You can customize this behavior:

```json
"Mitre": {
  "AutoImportOnStartup": true,      // Enable/disable automatic import
  "RefreshIntervalDays": 30         // How often to refresh data (days)
}
```

**Default Behavior:**
- ✅ Automatically imports 800+ MITRE techniques on first startup
- ✅ Refreshes data every 30 days from official sources
- ✅ Requires internet connectivity for import
- ✅ No additional configuration needed

**To disable auto-import:**
```json
"Mitre": {
  "AutoImportOnStartup": false
}
```

### 6. Configure Pipeline Performance (Optional)

Castellan includes parallel processing optimizations for improved performance:

```json
"Pipeline": {
  "EnableParallelProcessing": true,         // Enable parallel processing (default: true)
  "MaxConcurrency": 4,                      // Max concurrent operations (default: 4)
  "ParallelOperationTimeoutMs": 30000,      // Timeout for parallel ops (default: 30s)
  "EnableParallelVectorOperations": true,   // Parallel vector ops (default: true)
  "EnableVectorBatching": true,             // Enable vector batch processing (default: true)
  "VectorBatchSize": 100,                   // Vectors per batch (default: 100)
  "VectorBatchTimeoutMs": 5000              // Batch flush timeout (default: 5s)
}
```

**Performance Benefits:**
- ✅ 20% improvement in event processing speed with parallel processing
- ✅ 3-5x improvement in vector operations with batch processing
- ✅ Better CPU utilization across multiple cores
- ✅ Reduced latency for independent operations
- ✅ Automatic fallback to sequential processing on errors

### 7. Configure Connection Pools (Optional - Phase 2A)

Castellan includes enterprise-grade connection pooling for 15-25% I/O optimization:

```json
"ConnectionPools": {
  "QdrantPool": {
    "Instances": [
      {
        "Host": "localhost",
        "Port": 6333,
        "Weight": 100,
        "UseHttps": false
      },
      {
        "Host": "qdrant-replica",          // Optional: Add multiple instances
        "Port": 6333,
        "Weight": 80,
        "UseHttps": false
      }
    ],
    "MaxConnectionsPerInstance": 10,        // Connections per instance (default: 10)
    "ConnectionTimeout": "00:00:10",       // Connection timeout (default: 10s)
    "RequestTimeout": "00:01:00",          // Request timeout (default: 1min)
    "EnableFailover": true,                 // Auto failover (default: true)
    "MinHealthyInstances": 1                // Min required healthy instances (default: 1)
  },
  "HealthMonitoring": {
    "Enabled": true,                        // Enable health monitoring (default: true)
    "CheckInterval": "00:00:30",           // Health check interval (default: 30s)
    "ConsecutiveFailureThreshold": 3,      // Failures before unhealthy (default: 3)
    "ConsecutiveSuccessThreshold": 2,      // Successes to recover (default: 2)
    "EnableAutoRecovery": true              // Auto recovery (default: true)
  },
  "LoadBalancing": {
    "Algorithm": "WeightedRoundRobin",      // Load balancing algorithm
    "EnableHealthAwareRouting": true       // Health-aware routing (default: true)
  }
}
```

**Connection Pool Benefits:**
- ✅ 15-25% I/O performance improvement through connection reuse
- ✅ Automatic health monitoring and failover
- ✅ Load balancing across multiple Qdrant instances
- ✅ Intelligent connection lifecycle management
- ✅ Reduced connection establishment overhead

## Alternative: Environment Variables

You can also use environment variables instead of modifying appsettings.json:

```powershell
# Authentication (REQUIRED)
$env:AUTHENTICATION__JWT__SECRETKEY = "your-64-character-secure-key"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password"

# OpenAI (if using)
$env:OPENAI_API_KEY = "sk-your-openai-api-key"
$env:EMBEDDINGS__PROVIDER = "OpenAI"
$env:LLM__PROVIDER = "OpenAI"

# Qdrant Cloud (if using)
$env:QDRANT__USECLOUD = "true"
$env:QDRANT__HOST = "your-cluster.qdrant.io"
$env:QDRANT__HTTPS = "true"
$env:QDRANT__APIKEY = "your-api-key"

# Pipeline Performance (optional)
$env:PIPELINE__ENABLEPARALLELPROCESSING = "true"
$env:PIPELINE__MAXCONCURRENCY = "4"
$env:PIPELINE__PARALLELOPERATIONTIMEOUTMS = "30000"
$env:PIPELINE__ENABLEPARALLELVECTOROPERATIONS = "true"
$env:PIPELINE__ENABLEVECTORBATCHING = "true"
$env:PIPELINE__VECTORBATCHSIZE = "100"
$env:PIPELINE__VECTORBATCHTIMEOUTMS = "5000"

# Connection Pool Settings (Phase 2A - optional)
$env:CONNECTIONPOOLS__QDRANT__MAXCONNECTIONSPERINSTANCE = "10"
$env:CONNECTIONPOOLS__QDRANT__CONNECTIONTIMEOUT = "00:00:10"
$env:CONNECTIONPOOLS__QDRANT__REQUESTTIMEOUT = "00:01:00"
$env:CONNECTIONPOOLS__QDRANT__ENABLEFAILOVER = "true"
$env:CONNECTIONPOOLS__QDRANT__MINHEALTHYINSTANCES = "1"
$env:CONNECTIONPOOLS__HEALTHMONITORING__ENABLED = "true"
$env:CONNECTIONPOOLS__HEALTHMONITORING__CHECKINTERVAL = "00:00:30"
$env:CONNECTIONPOOLS__HEALTHMONITORING__CONSECUTIVEFAILURETHRESHOLD = "3"
$env:CONNECTIONPOOLS__LOADBALANCING__ALGORITHM = "WeightedRoundRobin"
```

## Security Best Practices

1. **Never Commit Credentials**: The `appsettings.json` file is now gitignored to prevent accidental credential commits
2. **Use Strong Passwords**: Generate secure passwords using a password manager
3. **Rotate Keys Regularly**: Change JWT secret keys and passwords periodically
4. **Secure Storage**: Store production credentials in secure vaults or environment variables
5. **Unique Per Environment**: Use different credentials for development, staging, and production

## Configuration File Locations

- **Template**: `src\Castellan.Worker\appsettings.template.json` (tracked in git)
- **Actual Config**: `src\Castellan.Worker\appsettings.json` (gitignored)
- **Environment-Specific**: 
  - `appsettings.Development.json` (gitignored)
  - `appsettings.Production.json` (gitignored)

## Troubleshooting

### "Authentication failed" Error
- Ensure JWT SecretKey is at least 64 characters
- Verify username and password match what you configured
- Check environment variables aren't overriding your settings

### "Cannot find appsettings.json"
- Make sure you copied `appsettings.template.json` to `appsettings.json`
- Verify you're in the correct directory (`src\Castellan.Worker`)

### OpenAI Connection Issues
- Verify your API key is valid
- Check you have credits/quota available
- Ensure Provider is set to "OpenAI" for both Embeddings and LLM

## Next Steps

After configuration:
1. Start the services: `.\scripts\start.ps1`
2. Access the web interface: http://localhost:8080
3. Login with your configured credentials
4. Refer to [GETTING_STARTED.md](GETTING_STARTED.md) for usage instructions