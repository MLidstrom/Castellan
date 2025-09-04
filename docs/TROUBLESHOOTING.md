# 🔧 Troubleshooting Guide

This guide covers common issues you might encounter while using Castellan and their solutions.

## React Admin Interface Issues

### Threat Scanner Buttons Not Working
If the "Quick Scan" or "Full Scan" buttons in the React Admin interface don't work:

1. **Check Backend Connection**: Ensure backend is running on `http://localhost:5000`
   ```powershell
   # Test backend API directly with your configured credentials
   curl -X POST "http://localhost:5000/api/auth/login" -H "Content-Type: application/json" -d "{\"username\":\"your-username\",\"password\":\"your-password\"}"
   ```

2. **Verify Frontend Compilation**: Check for TypeScript errors in the React app console
   - Look for compilation errors in the browser console
   - Ensure React app is running without TypeScript errors

3. **Authentication Issues**: Make sure you're logged in with correct credentials
   - Use the credentials you configured via environment variables
   - See [AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md) for detailed setup instructions

### Common Port Issues
- **Backend API**: Always runs on `http://localhost:5000`
- **React Admin Interface**: Always runs on `http://localhost:8080`
- **CORS Errors**: Make sure the React app uses full URLs (`http://localhost:5000/api/...`) not relative paths
- **Custom API URL**: Set `REACT_APP_API_BASE_URL` environment variable to override default backend URL

## System Tray Application Issues

### Tray Icon Not Visible
If you can't see the Castellan system tray icon:

1. **Check for Duplicate Processes**: Multiple tray applications may be running
   ```powershell
   # Check for running tray processes
   Get-Process -Name "Castellan.Tray" -ErrorAction SilentlyContinue
   
   # Stop duplicate processes if found
   Get-Process -Name "Castellan.Tray" -ErrorAction SilentlyContinue | Stop-Process -Force
   ```

2. **Restart Services Cleanly**: Use the provided scripts for clean startup/shutdown
   ```powershell
   # Stop all services
   .\scripts\stop.ps1
   
   # Start all services (includes tray app)
   .\scripts\start.ps1
   ```

3. **Process Detection Issues**: If tray app reports "Castellan not running" when it is:
   - This can occur when running Worker via `dotnet run` instead of compiled executable
   - The tray app uses enhanced process detection to handle both scenarios
   - Restart the tray application if detection fails

### Tray App Startup Issues
If the system tray application doesn't start automatically:

1. **Check Auto-Start Configuration**: Verify settings in `appsettings.json`
   ```json
   "Startup": {
     "AutoStart": {
       "Enabled": true,
       "SystemTray": true
     }
   }
   ```

2. **Path Resolution Problems**: Tray executable must be built before starting
   ```powershell
   # Ensure tray app is built
   dotnet build src\Castellan.Tray\Castellan.Tray.csproj -c Release
   
   # Or build entire solution
   dotnet build Castellan.sln
   ```

3. **Manual Tray Start**: If auto-start fails, start manually
   ```powershell
   .\scripts\start-tray.ps1
   ```

## Performance Issues
- **Large Threat Scans**: Full system scans can take several minutes and find thousands of threats
- **Memory Usage**: Monitor system memory during large scans
- **False Positives**: Legitimate software in temp directories may be flagged as threats

## AI Provider Issues

### Ollama Issues
- **Model Not Found**: Ensure you've pulled the required models:
  ```powershell
  ollama pull nomic-embed-text
  ollama pull llama3.1:8b-instruct-q8_0
  ```
- **Connection Failed**: Verify Ollama is running on `http://localhost:11434`
- **Out of Memory**: Large models require significant RAM (8GB+ recommended for llama3.1:8b)

### OpenAI Issues
- **API Key Invalid**: Verify your API key at [https://platform.openai.com/api-keys](https://platform.openai.com/api-keys)
- **Rate Limiting**: Check your usage limits and billing status
- **Network Issues**: Ensure internet connectivity for OpenAI API calls

## Vector Database Issues

### Qdrant Issues
- **Connection Failed**: Ensure Qdrant is running on the configured port (default: 6333)
  ```powershell
  # Check if Qdrant container is running
  docker ps | findstr qdrant
  
  # Start Qdrant if not running
  .\scripts\run-qdrant-local.ps1
  ```
- **Collection Not Found**: The application will automatically create collections on first run
- **Disk Space**: Ensure sufficient disk space for vector storage (vectors are cleaned up after 24 hours)

## Authentication Issues

### JWT Configuration
- **Secret Key Too Short**: JWT secret must be at least 64 characters long
- **Environment Variables Not Set**: Ensure all required authentication environment variables are configured:
  ```powershell
  $env:AUTHENTICATION__JWT__SECRETKEY = "your-secure-jwt-secret-key-minimum-64-characters"
  $env:AUTHENTICATION__ADMINUSER__USERNAME = "admin"
  $env:AUTHENTICATION__ADMINUSER__PASSWORD = "your-secure-password"
  ```

### Login Issues
- **Invalid Credentials**: Verify username and password match your configuration
- **Token Expired**: JWT tokens expire after 24 hours by default, re-login required
- **Browser Cache**: Clear browser cache and cookies if experiencing persistent login issues

## General Troubleshooting Steps

### Service Status Check
```powershell
# Check all Castellan processes
Get-Process | Where-Object {$_.ProcessName -like "*Castellan*"}

# Check specific services
Get-Process -Name "Castellan.Worker" -ErrorAction SilentlyContinue
Get-Process -Name "Castellan.Tray" -ErrorAction SilentlyContinue
```

### Log Analysis
Check log files for detailed error information:
- **Worker Service**: `src/Castellan.Worker/run*.log`
- **System Events**: Windows Event Viewer > Application logs
- **Browser Console**: F12 Developer Tools for React interface issues

### Complete Reset
If all else fails, perform a complete reset:
```powershell
# Stop all services
.\scripts\stop.ps1

# Clean build
dotnet clean
dotnet build

# Restart everything
.\scripts\start.ps1
```

## Getting Help

If you're still experiencing issues after trying these solutions:

1. **Check [GitHub Issues](https://github.com/MLidstrom/castellan/issues)** for similar problems
2. **Create a new issue** with:
   - Detailed description of the problem
   - Steps to reproduce
   - Error messages and logs
   - System information (OS version, .NET version, etc.)
3. **Join [GitHub Discussions](https://github.com/MLidstrom/castellan/discussions)** for community support

## Common Error Messages

### "Collection does not exist"
- **Cause**: Qdrant vector database collection not initialized
- **Solution**: Restart the application to auto-create collections

### "Model not found"
- **Cause**: Ollama model not pulled or incorrect model name
- **Solution**: Pull the correct models with `ollama pull` commands

### "Authentication failed"
- **Cause**: Invalid credentials or expired token
- **Solution**: Verify credentials and re-login to get fresh token
