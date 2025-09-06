# 🔧 Troubleshooting Guide

This guide covers common issues you might encounter while using Castellan and their solutions.

## React Admin Interface Issues

### MITRE ATT&CK Techniques Page Issues

**"dataProvider error. Check the console for details."**

✅ **FIXED (January 2025)**: This issue has been resolved. If you still encounter this error:

1. **Verify MITRE Data Import**: Check if MITRE techniques were imported successfully
   ```powershell
   # Test MITRE endpoint directly
   $headers = @{ "Authorization" = "Bearer YOUR_TOKEN" }
   Invoke-WebRequest -Uri "http://localhost:5000/api/mitre/techniques" -Headers $headers
   ```

2. **Import MITRE Data Manually**: If the endpoint returns empty results
   ```powershell
   # Trigger manual import
   Invoke-WebRequest -Uri "http://localhost:5000/api/mitre/import" -Method POST -Headers $headers
   ```

3. **Clear Browser Cache**: Force reload the React Admin interface
   - Press Ctrl+F5 for hard refresh
   - Or clear browser cache and cookies for localhost:8080

4. **Check Browser Console**: Open DevTools (F12) → Console tab for specific errors
   - Look for authentication token issues
   - Check for network connectivity problems
   - Verify API response format errors

**Root Cause**: The issue was caused by MITRE API endpoints returning `{ techniques: [...] }` format while the dataProvider expected standard array responses. This has been fixed in the castellanDataProvider.ts transformation logic.

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

### "No tokens found" Error on Login Page

✅ **IMPROVED (January 2025)**: Enhanced error handling to provide clearer user experience.

1. **Expected Behavior**: This message is normal on initial page load before authentication
   - The login page will show this briefly while loading
   - It should disappear once the login form is displayed

2. **Persistent "No tokens found"**: If the error persists or prevents login:
   ```powershell
   # Check if Worker API is running
   Invoke-WebRequest -Uri "http://localhost:5000/api/system/edition" -Method GET
   
   # If Worker API is down, start services
   .\scripts\start.ps1
   ```

3. **Backend Server Not Available**: If you see "Backend server not available":
   - This indicates the Worker API at localhost:5000 is not responding
   - Check that the Worker API service is running and stable
   - The semaphore fix prevents Worker API crashes that previously caused this error

**Root Cause**: Previous versions had unstable Worker API due to semaphore bugs, causing authentication endpoints to be unavailable. This has been resolved with the Pipeline.cs stability fix.

## Worker API Issues

### Worker Service Crashes Immediately After Startup

**"SemaphoreFullException: Adding the specified count to the semaphore would cause it to exceed its maximum count."**

✅ **FIXED (January 2025)**: This critical stability issue has been resolved. If you still experience crashes:

1. **Update to Latest Version**: Ensure you have the latest Pipeline.cs with the semaphore fix
   ```powershell
   # Pull latest changes
   git pull origin main
   
   # Rebuild the project
   dotnet build -c Release
   ```

2. **Check Semaphore Configuration**: Verify pipeline settings in appsettings.json
   ```json
   "Pipeline": {
     "EnableSemaphoreThrottling": true,
     "MaxConcurrentTasks": 8
   }
   ```

3. **Monitor Background Jobs**: If running in background, check job status
   ```powershell
   # Check background job status
   Get-Job | Where-Object { $_.Name -like "*Worker*" }
   
   # View job output for errors
   Receive-Job -Id JOB_ID
   ```

4. **Start in Foreground for Debugging**: Run Worker in foreground to see detailed errors
   ```powershell
   cd src\Castellan.Worker
   dotnet run --configuration Release
   ```

**Root Cause**: The issue was caused by mismatched semaphore acquisition and release logic where `TryAcquireSemaphoreAsync()` returned true without actually acquiring the semaphore, but `ReleaseSemaphore()` tried to release it anyway. This has been fixed with proper semaphore state tracking.

### "Failed to fetch" Errors During Login

✅ **RESOLVED**: This is typically caused by Worker API not running or crashing. With the semaphore fix, the Worker API now runs stable and login endpoints remain available.

1. **Verify Worker API Status**: Check if Worker is running and responding
   ```powershell
   # Test system endpoint
   Invoke-WebRequest -Uri "http://localhost:5000/api/system/edition" -Method GET
   
   # Test login endpoint
   $body = @{ username = "admin"; password = "your-password" } | ConvertTo-Json
   Invoke-WebRequest -Uri "http://localhost:5000/api/auth/login" -Method POST -ContentType "application/json" -Body $body
   ```

2. **Restart Services if Needed**: Use background job commands for stable operation
   ```powershell
   # Stop any existing services
   .\scripts\stop.ps1
   
   # Start with background jobs (recommended)
   .\scripts\start.ps1
   ```

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
