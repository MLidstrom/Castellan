# Getting Started with CastellanPro Free

This is the free edition of CastellanPro, focused on core security monitoring capabilities for local deployment only. No cloud features are available in the Free Edition.

## Quick Start (Automatic)

### Prerequisites

- .NET 8.0 SDK
- Docker Desktop (for Qdrant vector database)
- Windows 10/11 (for Windows Event Log monitoring)
- Node.js (for React admin interface)

### 1. Configure Authentication

```powershell
# Copy template configuration file
cd src\CastellanPro.Worker
Copy-Item appsettings.template.json appsettings.json

# Edit appsettings.json with your secure credentials
# Or use environment variables:
$env:AUTHENTICATION__JWT__SECRETKEY = "your-secure-jwt-secret-key-minimum-64-characters"
$env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
$env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password"
```

**⚠️ Security Note:** See [CONFIGURATION_SETUP.md](CONFIGURATION_SETUP.md) for detailed configuration instructions.

### 2. Start Everything

```powershell
# Start all services (C# handles orchestration)
.\scripts\start.ps1

# This will automatically:
# ✓ Start Qdrant vector database in Docker
# ✓ Start the Worker service (main API)
# ✓ Install and start React Admin interface
# ✓ Start System Tray application
```

### 3. Access Web Interface

Open your browser to http://localhost:8080 to access the admin interface.

**Login Credentials:** Use the username and password you configured in step 1.

### 4. Stop All Services

```powershell
# Stop everything cleanly
.\scripts\stop.ps1
```

## Troubleshooting

### System Tray Icon Issues

If the system tray icon doesn't appear or behaves unexpectedly:

1. **Multiple Tray Processes**: Check for and stop duplicate processes
   ```powershell
   # Check running tray processes
   Get-Process -Name "CastellanPro.Tray" -ErrorAction SilentlyContinue
   
   # Clean restart if duplicates found
   .\scripts\stop.ps1
   .\scripts\start.ps1
   ```

2. **Process Detection Problems**: If tray shows "CastellanPro not running" when it is running:
   - This is common when using `dotnet run` instead of compiled executable
   - The tray app automatically detects both scenarios
   - Simply restart the tray app or use the start/stop scripts

3. **Auto-Start Issues**: If tray doesn't start automatically:
   - Ensure the tray app is built: `dotnet build src\CastellanPro.Tray\CastellanPro.Tray.csproj`
   - Check auto-start is enabled in `appsettings.json`
   - Start manually: `.\scripts\start-tray.ps1`

### Service Startup Issues

If services fail to start automatically:

1. **Check Dependencies**: Ensure Docker is running for Qdrant
2. **Build Requirements**: Make sure all projects are built before starting
3. **Port Conflicts**: Verify ports 5000, 6333, 8080, and 11434 are available
4. **Configuration**: Check environment variables and appsettings.json

## Manual Setup (Advanced)

<details>
<summary>Click to see manual setup steps</summary>

### 1. Disable Auto-Start

Edit `src\CastellanPro.Worker\appsettings.json`:
```json
"Startup": {
  "AutoStart": {
    "Enabled": false
  }
}
```

### 2. Start Services Manually

```powershell
# Start Qdrant
docker run -d --name qdrant -p 6333:6333 qdrant/qdrant

# Build and run Worker
dotnet build src\CastellanPro.Worker\CastellanPro.Worker.csproj -c Release
cd src\CastellanPro.Worker
dotnet run

# Start React Admin (new terminal)
cd castellan-admin
npm install
npm start
```

</details>

## Key Features (Free Edition)

- ✅ Windows Event Log monitoring
- ✅ AI-powered security event analysis 
- ✅ Vector-based threat detection
- ✅ Persistent storage with 24-hour rolling window
- ✅ Desktop notifications
- ✅ Web admin interface
- ✅ Local deployment only
- ❌ Teams/Slack integration (Pro edition only)
- ❌ Cloud connectivity (Pro edition only)
- ❌ Advanced compliance reporting (Pro edition only)

## Configuration

The free edition uses local AI models via Ollama or OpenAI API:

- **Embeddings**: Ollama (nomic-embed-text) or OpenAI
- **LLM**: Ollama (llama3.1:8b-instruct-q8_0) or OpenAI GPT models
- **Vector DB**: Qdrant (local Docker instance)

## Support

This is the free community edition for local deployment only. For cloud features, enterprise support, and advanced compliance, consider the Pro edition.

## License

MIT License - see LICENSE file for details.