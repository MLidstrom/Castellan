# Startup Configuration Guide

This guide explains how to configure and customize the automatic startup behavior of Castellan services.

## Overview

Castellan includes an automatic startup orchestrator (`StartupOrchestratorService.cs`) built into the Worker service that manages all required services from a single entry point. The orchestration logic is implemented entirely in C# code, not in PowerShell scripts. When you start the Worker service, it automatically launches:

- **Qdrant** - Vector database (Docker container)
- **Tailwind Dashboard** - Web administration interface
- **System Tray** - Windows system tray application

## Quick Start

### Start Everything
```powershell
.\scripts\start-all.ps1
```

### Stop Everything
```powershell
.\scripts\stop-all.ps1
```

## Configuration

### Automatic Startup Settings

The automatic startup behavior is controlled via `appsettings.json` in the Worker project:

```json
{
  "Startup": {
    "AutoStart": {
      "Enabled": true,      // Master switch for automatic startup
      "Qdrant": true,       // Auto-start Qdrant container
      "ReactAdmin": true,   // Auto-start React admin interface
      "SystemTray": true    // Auto-start system tray application
    }
  }
}
```

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Master switch - set to `false` to disable all automatic startup |
| `Qdrant` | `true` | Automatically start Qdrant Docker container |
| `ReactAdmin` | `true` | Automatically start React admin interface on port 8080 |
| `SystemTray` | `true` | Automatically start Windows system tray application |

## Service Details

### Qdrant Vector Database

**What it does:**
- Checks if Qdrant is already running before starting
- Stops and removes existing container if present
- Starts new Qdrant container with proper port mappings
- Waits for service to be ready before continuing

**Requirements:**
- Docker Desktop must be installed and running
- Ports 6333 and 6334 must be available

**Manual Start:**
```powershell
.\scripts\run-qdrant-local.ps1
```

### Tailwind Dashboard Interface

**What it does:**
- Checks for node_modules directory
- Runs `npm install` if dependencies are missing
- Starts React development server on port 8080
- Manages the Node.js process lifecycle

**Requirements:**
- Node.js and npm must be installed
- Port 8080 must be available

**Manual Start:**
```powershell
cd dashboard
npm install
npm start
```

### System Tray Application

**What it does:**
- Builds the tray application if not already built
- Starts the Windows Forms tray application
- Provides quick access to Castellan status and controls

**Requirements:**
- Windows operating system
- .NET 8.0 SDK

**Manual Start:**
```powershell
.\scripts\start-tray.ps1
```

## Advanced Configuration

### Environment-Based Configuration

You can override settings using environment variables:

```powershell
# Disable automatic startup entirely
$env:Startup__AutoStart__Enabled = "false"

# Disable specific services
$env:Startup__AutoStart__Qdrant = "false"
$env:Startup__AutoStart__ReactAdmin = "false"
$env:Startup__AutoStart__SystemTray = "false"
```

### Custom Configuration Files

For different environments, create environment-specific configuration files:

- `appsettings.Development.json` - Development settings
- `appsettings.Production.json` - Production settings

Example `appsettings.Development.json`:
```json
{
  "Startup": {
    "AutoStart": {
      "Enabled": true,
      "Qdrant": true,
      "ReactAdmin": true,
      "SystemTray": false  // Don't start tray in development
    }
  }
}
```

## Process Management

### Health Monitoring

The StartupOrchestratorService monitors all managed processes:
- Checks process health every 30 seconds
- Logs when processes stop unexpectedly
- Does not automatically restart failed processes (by design)

### Graceful Shutdown

When the Worker service stops:
1. All managed processes receive shutdown signal
2. Processes have 5 seconds to stop gracefully
3. Force termination if processes don't stop
4. Docker containers are properly stopped

## Troubleshooting

### Common Issues

#### Docker Not Running
**Error:** "Failed to start Qdrant. Make sure Docker is installed and running."

**Solution:**
1. Install Docker Desktop from https://www.docker.com/products/docker-desktop
2. Start Docker Desktop
3. Wait for Docker to fully initialize
4. Retry starting Castellan

#### Port Already in Use
**Error:** Tailwind Dashboard or other services fail to start due to port conflicts

**Solution:**
```powershell
# Check what's using port 8080
netstat -ano | findstr :3000

# Kill the process using the port (replace PID with actual process ID)
taskkill /PID <PID> /F
```

#### Node Modules Missing
**Error:** Tailwind Dashboard fails to start

**Solution:**
```powershell
cd dashboard
npm install
```

### Manual Service Management

If automatic startup fails, you can start services manually:

```powershell
# Start services individually
.\scripts\run-qdrant-local.ps1        # Start Qdrant
cd src\Castellan.Worker && dotnet run  # Start Worker
cd dashboard && npm start        # Start Tailwind Dashboard
.\scripts\start-tray.ps1               # Start System Tray
```

## Best Practices

### Production Deployment

For production environments:

1. **Disable System Tray**: Set `"SystemTray": false` in production
2. **Use Docker Compose**: Consider using Docker Compose for container orchestration
3. **Service Manager**: Use Windows Service
4. **Health Checks**: Implement proper health check endpoints
5. **Logging**: Configure appropriate log levels and retention

### Development Workflow

For development:

1. **Selective Startup**: Disable services you're not working with
2. **Debug Mode**: Run Worker in debug mode from Visual Studio/VS Code
3. **Hot Reload**: Tailwind Dashboard supports hot module replacement
4. **Log Verbosity**: Increase log levels for troubleshooting

## Implementation Details

### C# Architecture

All startup orchestration logic is implemented in `src\Castellan.Worker\Services\StartupOrchestratorService.cs`:

```csharp
public class StartupOrchestratorService : BackgroundService
{
    // Manages all service lifecycle from C# code
    // No dependency on PowerShell scripts
    // Direct process management using System.Diagnostics.Process
}
```

**Key Features:**
- **Process Management**: Uses `System.Diagnostics.Process` to start/stop services
- **Docker Integration**: Executes Docker commands directly from C#
- **Health Monitoring**: Tracks process health every 30 seconds
- **Graceful Shutdown**: Properly stops all managed processes on exit
- **Configuration-Driven**: Reads settings from `appsettings.json`

### Why C# Instead of PowerShell?

1. **Single Source of Truth**: All logic in one place (C# codebase)
2. **Better Error Handling**: Structured exception handling
3. **Type Safety**: Compile-time checking of configuration
4. **Testability**: Unit tests for startup logic
5. **Extensibility**: Future enhancements without changing startup scripts
6. **Integrated Logging**: Uses same Serilog configuration

## Scripts Reference

The PowerShell scripts are thin wrappers that simply start/stop the Worker service. The actual orchestration happens in C#.

### start-all.ps1 (Minimal Wrapper)
- Simply starts the Worker service
- Worker's C# code handles all service orchestration

### stop-all.ps1 (Cleanup Helper)
- Stops the Worker process
- Worker's shutdown handler stops all managed services

### Individual Service Scripts
- `run-qdrant-local.ps1` - Start only Qdrant
- `start-tray.ps1` - Start only System Tray
- `status.ps1` - Check status of all services
- `stop-rebuild-start.ps1` - Full rebuild and restart

## Security Considerations

### Authentication
Always configure secure authentication before starting services:

```powershell
$env:AUTHENTICATION__JWT__SECRETKEY = "your-secure-64-char-secret"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "secure-password"
```

### Network Security
- Services bind to localhost by default
- Configure firewall rules if exposing services
- Use HTTPS in production environments
- Implement proper CORS policies

## Support

For issues or questions:
- Check logs in `src\Castellan.Worker\run.log`
- Review service-specific logs
- Consult [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- Open an issue on GitHub