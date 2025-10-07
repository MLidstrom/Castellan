# 🔧 Troubleshooting Guide

This guide covers common issues you might encounter while using Castellan and their solutions.

## React Admin Interface Issues

### Dashboard Security Events Count Discrepancy

**Symptom**: Dashboard shows incorrect total security events count (e.g., 10 instead of 2000+)

✅ **FIXED (September 2025)**: This issue has been resolved. If you still encounter count discrepancies:

1. **Verify Dashboard Data Source**: The dashboard should display the full total count from the API
   ```javascript
   // Fixed implementation uses API total field
   const securityEventsData = securityEventsApi.data as { events: SecurityEvent[], total: number };
   const totalEvents = securityEventsData?.total || 0; // Correct
   // Instead of: securityEvents?.length || 0; // Incorrect - only page length
   ```

2. **Check API Response Structure**: Verify the API returns data in the expected format
   ```powershell
   # Test security events endpoint directly
   $headers = @{ "Authorization" = "Bearer YOUR_TOKEN" }
   Invoke-WebRequest -Uri "http://localhost:5000/api/security-events?sort=timestamp&order=desc" -Headers $headers
   ```
   Expected response: `{ data: [...], total: 2168 }`

3. **Force Dashboard Refresh**: Clear any cached data
   - Use the "Force Refresh" button in the dashboard
   - Or restart the React admin interface

4. **Compare with Security Events Page**: The detailed Security Events page should show the same total
   - Dashboard KPI card should match the count shown in the Security Events list page
   - Both use the same API endpoint but parse the response differently

**Root Cause**: The dashboard was incorrectly using the length of the paginated data array (`data.length` = 10) instead of the total count field (`total` = 2168) from the API response. This has been fixed by properly parsing the API response structure.

**Technical Fix**: Modified Dashboard.tsx to extract both `events` array and `total` count from the API response, then use `total` for the dashboard KPI display.

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
If the \"Quick Scan\" or \"Full Scan\" buttons in the React Admin interface don't work:

1. **Check Backend Connection**: Ensure backend is running on `http://localhost:5000`
   ```powershell
   # Test backend API directly with your configured credentials
   curl -X POST \"http://localhost:5000/api/auth/login\" -H \"Content-Type: application/json\" -d \"{\\\"username\\\":\\\"your-username\\\",\\\"password\\\":\\\"your-password\\\"}\"
   ```

2. **Verify Frontend Compilation**: Check for TypeScript errors in the React app console
   - Look for compilation errors in the browser console
   - Ensure React app is running without TypeScript errors

3. **Authentication Issues**: Make sure you're logged in with correct credentials
   - Use the credentials you configured via environment variables
   - See [AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md) for detailed setup instructions

### React Server Fails to Start in Background

If `npm start` or `npm run start` fails when running as a background job with `Start-Job`:

1.  **Use `Start-Process`**: The recommended way to run the React server in the background is with `Start-Process`.
    ```powershell
    Start-Process -FilePath "npm.cmd" -ArgumentList "start" -WindowStyle Hidden
    ```

### Frontend "Failed to fetch" Errors

**Symptom**: React Admin Interface shows "Failed to fetch" errors and won't load properly at http://localhost:8080

**Quick Fix**: Use the approved startup script to start both backend and frontend properly:
```powershell
# From the project root directory
.\scripts\start.ps1 -NoBuild
```

**Manual Troubleshooting Steps**:

1. **Verify Backend API is Running**: Check that port 5000 is responding
   ```powershell
   Test-NetConnection -ComputerName localhost -Port 5000
   Invoke-WebRequest -Uri "http://localhost:5000/api/system-status/test" -Method GET
   ```

2. **Check Frontend Server Status**: Verify port 8080 is listening
   ```powershell
   Test-NetConnection -ComputerName localhost -Port 8080
   ```

3. **Install Frontend Dependencies**: Ensure React dependencies are current
   ```powershell
   cd castellan-admin
   npm ci
   ```

4. **Start Frontend Manually** (if the script doesn't work):
   ```powershell
   cd castellan-admin
   npm start
   ```
   - This will start the dev server on http://localhost:8080
   - The app will proxy API requests to http://localhost:5000 automatically

**Root Cause**: The React frontend server wasn't running. The backend API on port 5000 was working correctly, but without the frontend server on port 8080, users see "Failed to fetch" errors because there's no web interface to serve the React application.

**Prevention**: Always use `.\scripts\start.ps1` to start both backend and frontend services together. This ensures proper service orchestration and avoids port conflicts.

### Common Port Issues
- **Backend API**: Always runs on `http://localhost:5000`
- **React Admin Interface**: Always runs on `http://localhost:8080`
- **CORS Errors**: Make sure the React app uses full URLs (`http://localhost:5000/api/...`) not relative paths
- **Custom API URL**: Set `REACT_APP_API_BASE_URL` environment variable to override default backend URL

## Startup Script Issues

### start.ps1 Hangs at "Checking Qdrant vector database..."

**Symptom**: The start.ps1 script hangs indefinitely when checking Docker/Qdrant status

**Cause**: Docker Desktop CLI commands become unresponsive, causing the script to hang even though services are running properly.

**Solutions**:

1. **Quick Fix - Kill the hanging script and verify services are running**:
   ```powershell
   # Press Ctrl+C to stop the hanging script
   # Then check if services are actually running:
   .\scripts\status.ps1
   ```

2. **Restart Docker Desktop**:
   - Exit Docker Desktop completely
   - Restart Docker Desktop
   - Wait for it to fully initialize
   - Try running start.ps1 again

3. **Start services manually without Docker checks**:
   ```powershell
   # Start Worker API directly
   Start-Process powershell -ArgumentList "-NoProfile", "-Command", "cd C:\Users\matsl\Castellan\src\Castellan.Worker; dotnet run" -WindowStyle Hidden
   
   # Start React Admin
   Start-Process powershell -ArgumentList "-NoProfile", "-Command", "cd C:\Users\matsl\Castellan\castellan-admin; npm start" -WindowStyle Hidden
   ```

4. **Verify services are running despite the hang**:
   - Qdrant: http://localhost:6333 (should return status 200)
   - Worker API: http://localhost:5000/api/security-events (returns 401 if working)
   - React Admin: http://localhost:8080 (should show login page)

**Note**: Even if the script hangs, all services may still be running correctly. Check the status before assuming failure.

### Worker API Shows "Not Accessible" But Is Actually Running

**Symptom**: status.ps1 reports "Worker API is not accessible (port 5000)" even though the API is running

**Cause**: The status script checks the /api/health endpoint which doesn't exist. The API returns 404 for this endpoint, which the script interprets as "not accessible".

**Verification**: The Worker API is actually running if:
```powershell
# This returns 401 Unauthorized (which means the API is working)
Invoke-WebRequest -Uri "http://localhost:5000/api/security-events" -UseBasicParsing
```

**Solutions**:

1. **Ignore the false negative**: If you get a 401 response, the API is working correctly
2. **Check process is running**:
   ```powershell
   Get-Process Castellan.Worker -ErrorAction SilentlyContinue
   ```
3. **Login to confirm API access**:
   - Navigate to http://localhost:8080
   - Login with your configured credentials
   - If the dashboard loads data, the API is working

**Note**: The 401 Unauthorized response indicates the API is functioning properly but requires authentication. This is expected behavior.

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

### Slow Timeline Page Loading

✅ **FIXED (October 2025)**: Timeline page performance has been fully optimized with connection pooling, database-level GROUP BY, MITRE optimization, and React Query caching.

**Problem**: The `/timeline` endpoint was slow (25-32 seconds) and didn't cache results properly.

**Solution Applied**:

**Backend Optimizations**:
- Database-level GROUP BY aggregation using SQLite `strftime()` for date bucketing (TimelineService.cs:573-673)
- Returns only 8-100 aggregated rows instead of loading 180K+ events into memory
- MITRE technique optimization: reduced from 180K to 500 records (360x improvement)
- Database indexes: IX_SecurityEvents_Timestamp and composite indexes for query optimization
- Connection pooling: Converted `TimelineService` to use `IDbContextFactory<CastellanDbContext>`
- Added `AsNoTracking()` for read-only queries to reduce memory overhead

**Frontend Optimizations** (October 5, 2025):
- React Query snapshot caching with `placeholderData: keepPreviousData`
- 30-minute memory retention, 24-hour localStorage persistence
- Background polling with automatic cache refresh

**Performance Results**:
- First visit: <2 seconds (database-optimized GROUP BY)
- Repeat visits: <50ms (React Query cache)
- Overall improvement: 94% faster than original 32-second load time

**Implementation Details**:
- Timeline data fetching: `GetTimelineDataAsync` (src/Castellan.Worker/Services/TimelineService.cs:36)
- Statistics aggregation: `GetTimelineStatsAsync` (src/Castellan.Worker/Services/TimelineService.cs:114)
- SQL aggregation: `GetTimelineDataPointsViaSqlAsync` (src/Castellan.Worker/Services/TimelineService.cs:576)
- Frontend caching: `TimelinePanel.tsx` converted to React Query hooks

### Slow Saved Searches Loading

✅ **FIXED (October 2025)**: Saved Searches endpoint performance has been optimized.

**Problem**: The `/api/saved-searches` endpoint was very slow.

**Solution Applied**:
- Converted `SavedSearchService` to use `IDbContextFactory<CastellanDbContext>` for connection pooling
- All 8 service methods now use pooled database contexts

**Performance Improvements**:
- `GetUserSavedSearchesAsync` - Fetch user's saved searches
- `GetSavedSearchAsync` - Get specific saved search
- `CreateSavedSearchAsync` - Create new saved search
- `UpdateSavedSearchAsync` - Update existing search
- `DeleteSavedSearchAsync` - Delete saved search
- `RecordSearchUsageAsync` - Track search usage
- `GetMostUsedSearchesAsync` - Get frequently used searches
- `SearchSavedSearchesAsync` - Search through saved searches

**Expected Results**: Saved searches should load instantly with connection pooling.

### Dashboard Widget Loading Performance

✅ **FIXED (October 2025)**: Dashboard widgets now load instantly without unnecessary loading states.

**Problem**: Dashboard widgets (Security Events, System Health, Threat Scans, Security Events by Risk Level) showed loading skeletons for too long even when data was already available.

**Solution Applied**:
- Initialize dashboard state from SignalR context data to avoid unnecessary loading states
- Fixed useMemo dependencies to use consolidated data instead of legacy state
- Removed duplicate SignalR context hook calls

**Performance Improvements**:
- Dashboard now renders instantly if data is already in SignalR context
- Loading state only shows when data is genuinely unavailable
- Memoized computations properly trigger on data changes

**Expected Results**: Dashboard should render data immediately on navigation, with minimal skeleton loading time.

### General Performance Guidelines
- **Large Threat Scans**: Full system scans can take several minutes and find thousands of threats
- **Memory Usage**: Monitor system memory during large scans
- **False Positives**: Legitimate software in temp directories may be flagged as threats
- **Database Connection Pooling**: All services should use `IDbContextFactory<CastellanDbContext>` for optimal performance
- **Monitor Performance**: Use `/api/database-pool/metrics` to check connection pool utilization

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

## MITRE ATT&CK Import Issues

### Import Dialog Not Working

✅ **FIXED (September 2025)**: MITRE import functionality has been restored and enhanced.

**Problem**: "Import Techniques" button on MITRE-techniques page was not functioning properly.

**Solution Applied**:
1. **Enhanced Error Handling**: Added comprehensive debugging and error reporting
2. **Authentication Verification**: Confirmed JWT tokens are properly passed
3. **Backend Validation**: Verified API endpoints are functional

**Testing Steps**:
```powershell
# 1. Login to the application
# URL: http://localhost:8080
# Username: admin
# Password: CastellanAdmin2024!

# 2. Navigate to MITRE ATT&CK Techniques
# Click "Import Techniques" button

# 3. Monitor browser console (F12) for detailed logs:
# Look for: [MITRE Import] Starting import request...
# Check: Token status (Present/Missing)
```

**Expected Behavior**:
- If techniques already imported (823+): "No new techniques to import" or similar success message
- If new techniques available: Progress dialog with import statistics
- Any errors: Detailed error messages with debugging information

**Current Status**: ✅ **FULLY OPERATIONAL**

---

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
