# Castellan Build Guide

**Status**: ✅ **Production Ready**  
**Last Updated**: September 6, 2025

## 🎯 Overview

This guide provides comprehensive instructions for building Castellan from source code, including all dependencies, configuration requirements, and deployment options.

## 📋 Prerequisites

### Required Software
- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** or later
- **[Docker Desktop](https://www.docker.com/get-started/)** (for Qdrant vector database)
- **[Node.js 18+](https://nodejs.org/)** (for React Admin interface)
- **[PowerShell 5.1+](https://docs.microsoft.com/powershell/)** (Windows native)

### AI Provider (Choose One)
- **[Ollama](https://ollama.com/)** (Recommended - Local AI)
- **[OpenAI API Key](https://platform.openai.com/api-keys)** (Cloud AI)

### Optional Components
- **[MaxMind GeoLite2 Databases](https://www.maxmind.com/en/geolite2/signup)** (IP enrichment)
- **[Git](https://git-scm.com/)** (for source code management)

## 🔧 Build Process

### 1. Clone Repository
```powershell
git clone https://github.com/MLidstrom/castellan.git
cd castellan
```

### 2. Verify Prerequisites
```powershell
# Check .NET SDK version
dotnet --version

# Check Docker status
docker --version

# Check Node.js version
node --version

# Check PowerShell version
$PSVersionTable.PSVersion
```

### 3. Build Core Application
```powershell
# Restore NuGet packages
dotnet restore

# Build in Release mode
dotnet build -c Release

# Verify build success
dotnet build -c Release --no-restore --verbosity minimal
```

### 4. Build React Admin Interface
```powershell
# Navigate to admin interface
cd castellan-admin

# Install dependencies
npm install

# Build production version
npm run build

# Return to root
cd ..
```

### 5. Build Docker Images (Optional)
```powershell
# Build Castellan worker image
docker build -t castellan-worker -f src/Castellan.Worker/Dockerfile .

# Build React admin image
docker build -t castellan-admin -f castellan-admin/Dockerfile ./castellan-admin
```

## ⚙️ Configuration

### 1. Application Configuration
```powershell
# Copy configuration template
cd src/Castellan.Worker
Copy-Item appsettings.template.json appsettings.json

# Edit configuration file
notepad appsettings.json
```

### 2. Environment Variables
```powershell
# Required authentication settings
$env:AUTHENTICATION__JWT__SECRETKEY = "your-secure-jwt-secret-key-minimum-64-characters"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password"

# AI Provider settings (Ollama recommended)
$env:EMBEDDINGS__PROVIDER = "Ollama"
$env:LLM__PROVIDER = "Ollama"

# Or OpenAI (if preferred)
# $env:OPENAI_API_KEY = "your-openai-api-key"
# $env:EMBEDDINGS__PROVIDER = "OpenAI"
# $env:LLM__PROVIDER = "OpenAI"
```

## 🚀 Deployment Options

### Option 1: Automated Deployment (Recommended)
```powershell
# Use automated startup script
.\scripts\start.ps1

# This will:
# 1. Start Qdrant vector database
# 2. Build and start Castellan Worker
# 3. Start React Admin interface
# 4. Verify all services are healthy
```

### Option 2: Manual Deployment
```powershell
# 1. Start Qdrant database
docker run -d --name qdrant -p 6333:6333 qdrant/qdrant

# 2. Start Castellan Worker
cd src/Castellan.Worker
dotnet run -c Release

# 3. Start React Admin (new terminal)
cd castellan-admin
npm start

# 4. Verify services
# - Qdrant: http://localhost:6333
# - Worker API: http://localhost:5000
# - Admin Interface: http://localhost:8080
```

### Option 3: Docker Compose Deployment
```yaml
# docker-compose.yml
version: '3.8'
services:
  qdrant:
    image: qdrant/qdrant:latest
    ports:
      - "6333:6333"
    volumes:
      - ./qdrant-data:/qdrant/storage
  
  castellan-worker:
    build:
      context: .
      dockerfile: src/Castellan.Worker/Dockerfile
    ports:
      - "5000:5000"
    depends_on:
      - qdrant
    environment:
      - QDRANT__HOST=qdrant
      - QDRANT__PORT=6333
  
  castellan-admin:
    build:
      context: ./castellan-admin
    ports:
      - "8080:80"
    depends_on:
      - castellan-worker
```

```powershell
# Deploy with Docker Compose
docker-compose up -d
```

## 🧪 Testing and Validation

### 1. Run Unit Tests
```powershell
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test category
dotnet test --filter Category=Unit
```

### 2. Integration Testing
```powershell
# Test connection to Qdrant
curl http://localhost:6333

# Test Worker API health
curl http://localhost:5000/health

# Test system status
curl http://localhost:5000/api/system-status
```

### 3. Performance Validation
```powershell
# Check performance metrics
curl http://localhost:5000/api/system-status | jq '.data[] | select(.component=="Performance Monitor")'

# Verify connection pool status
curl http://localhost:5000/api/system-status | jq '.data[] | select(.component=="Qdrant Connection Pool")'
```

## 📊 Build Optimization

### Performance Builds
```powershell
# Optimized release build
dotnet publish -c Release -r win-x64 --self-contained false

# AOT (Ahead-of-Time) compilation
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishAot=true
```

### Size Optimization
```powershell
# Trimmed deployment
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishTrimmed=true

# Single file deployment
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## 🛠️ Troubleshooting

### Common Build Issues

#### .NET SDK Version Mismatch
```powershell
# Check global.json requirements
cat global.json

# Install correct SDK version
# Download from: https://dotnet.microsoft.com/download/dotnet
```

#### NuGet Package Restore Failures
```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore with verbose output
dotnet restore --verbosity detailed
```

#### Docker Build Issues
```powershell
# Clean Docker build cache
docker builder prune

# Build with no cache
docker build --no-cache -t castellan-worker .
```

#### Node.js Build Issues
```powershell
# Clear npm cache
npm cache clean --force

# Delete node_modules and reinstall
Remove-Item -Recurse -Force node_modules
Remove-Item package-lock.json
npm install
```

### Performance Issues
```powershell
# Check system resources
Get-Process dotnet | Select-Object CPU, WorkingSet

# Monitor build performance
Measure-Command { dotnet build -c Release }
```

## 📋 Build Verification Checklist

### ✅ Build Success Indicators
- [ ] .NET build completes without errors
- [ ] All unit tests pass
- [ ] React admin builds successfully
- [ ] Docker images build (if using containers)
- [ ] Configuration files are properly templated
- [ ] All required services start successfully

### ✅ Deployment Verification
- [ ] Qdrant database is accessible on port 6333
- [ ] Castellan Worker API responds on port 5000
- [ ] React Admin interface loads on port 8080
- [ ] System status API returns healthy status for all components
- [ ] Connection pool shows healthy status
- [ ] AI/ML services are properly configured and responding

### ✅ Performance Validation
- [ ] Event processing performs at expected rates (10K+ events/sec)
- [ ] Memory usage is within expected bounds (<500MB)
- [ ] Connection pool shows optimal utilization
- [ ] AI analysis completes within expected timeframes (<4 seconds)

## 🎯 Next Steps

After successful build and deployment:

1. **Configure Authentication**: Set up secure admin credentials
2. **Set Up AI Models**: Install and configure Ollama models or OpenAI API
3. **Configure Notifications**: Set up Teams/Slack webhooks for alerts
4. **Customize Detection Rules**: Adjust security detection parameters
5. **Monitor Performance**: Use built-in performance dashboards

## 📞 Support

If you encounter build issues:

- **GitHub Issues**: [Report build problems](https://github.com/MLidstrom/Castellan/issues)
- **Discussions**: [Community support](https://github.com/MLidstrom/Castellan/discussions)
- **Documentation**: Check other guides in the `/docs` folder

---

**Castellan** - Production-ready security monitoring with enterprise-grade build and deployment capabilities. 🏰🛡️
