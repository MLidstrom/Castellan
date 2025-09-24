# SignalR Real-time Integration Guide

**Version**: 2.0
**Created**: September 7, 2025
**Updated**: September 24, 2025
**Status**: ✅ **ENHANCED** - Dashboard Data Consolidation Complete

## 🎯 Overview

Castellan includes comprehensive SignalR integration for real-time monitoring and live system updates. This implementation provides enterprise-grade WebSocket communication between the backend Worker service and the React admin interface.

## 🏗️ Architecture

### Backend Components

#### 1. SignalR Hub (`ScanProgressHub.cs`)
**Location**: `src/Castellan.Worker/Hubs/ScanProgressHub.cs`

```csharp
[Authorize]
public class ScanProgressHub : Hub
{
    // Group-based subscriptions for targeted updates
    public async Task JoinScanGroup(string scanId)
    public async Task LeaveScanGroup(string scanId) 
    public async Task JoinSystemMetrics()
    public async Task LeaveSystemMetrics()
}
```

**Features**:
- JWT-authenticated connections
- Group-based subscriptions (scan-specific, system-wide)
- Connection lifecycle management
- Automatic cleanup on disconnect

#### 2. Enhanced Progress Tracking Service
**Location**: `src/Castellan.Worker/Services/EnhancedProgressTrackingService.cs`

```csharp
public class EnhancedProgressTrackingService : IHostedService
{
    // Comprehensive system metrics aggregation
    public SystemHealthMetrics GetSystemHealthMetrics()
    public ThreatIntelligenceStatus GetThreatIntelligenceStatus()
    public CacheMetrics GetCacheMetrics()
    public List<ActiveScanInfo> GetActiveScans()
}
```

**Metrics Provided**:
- **System Health**: CPU usage, memory consumption, disk space
- **Threat Intelligence**: Service status for VirusTotal, MalwareBazaar, OTX
- **Performance Metrics**: Cache hit rates, API response times
- **Active Scans**: Real-time scan progress and status

#### 3. System Metrics API Controller
**Location**: `src/Castellan.Worker/Controllers/SystemMetricsController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class SystemMetricsController : ControllerBase
{
    [HttpGet("health")]
    public SystemHealthMetrics GetSystemHealth()
    
    [HttpPost("broadcast")]
    public async Task<IActionResult> TriggerBroadcast()
}
```

### Frontend Components

#### 1. SignalR React Hook (`useSignalR.ts`)
**Location**: `castellan-admin/src/hooks/useSignalR.ts`

```typescript
export const useSignalR = (options: SignalROptions) => {
    // Returns connection state and real-time data
    return {
        connection,
        isConnected,
        systemMetrics,
        scanProgress,
        errors
    };
};
```

**Features**:
- Automatic connection management
- JWT token authentication
- Error handling and reconnection
- Type-safe event handling

#### 2. **NEW: Global SignalR Context** (`SignalRContext.tsx`)
**Location**: `castellan-admin/src/contexts/SignalRContext.tsx`

```typescript
export const SignalRProvider: React.FC = ({ children }) => {
    // Provides persistent SignalR connection across entire app
    return (
        <SignalRContext.Provider value={contextValue}>
            {children}
        </SignalRContext.Provider>
    );
};

export const useSignalRContext = () => {
    // Hook for accessing global SignalR state
    const context = useContext(SignalRContext);
    return context;
};
```

**✅ Key Benefits**:
- **Persistent Connection**: Maintains SignalR connection across all page navigation
- **Global State**: Single source of truth for real-time data
- **Performance**: One connection shared across all components
- **Navigation Fix**: Resolves disconnection issues when changing menu pages

#### 2. Real-time System Metrics Component
**Location**: `castellan-admin/src/components/RealtimeSystemMetrics.tsx`

```typescript
export const RealtimeSystemMetrics: React.FC = () => {
    const { systemMetrics, isConnected } = useSignalR({
        url: '/hubs/scan-progress',
        accessTokenFactory: () => localStorage.getItem('accessToken')
    });
    
    // Renders live dashboard with real-time updates
};
```

**UI Features**:
- Live system health indicators
- Real-time progress bars
- Threat intelligence status badges
- Performance metrics charts
- Connection status indicators

## 🔧 Configuration

### Backend Configuration (`Program.cs`)

```csharp
// SignalR service registration
builder.Services.AddSignalR();

// Enhanced progress tracking service
builder.Services.AddSingleton<EnhancedProgressTrackingService>();
builder.Services.AddHostedService<EnhancedProgressTrackingService>();

// Hub mapping
app.MapHub<ScanProgressHub>("/hubs/scan-progress");
```

### CORS Configuration

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:8080")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});
```

### Frontend Integration

#### 1. Install SignalR Client

```bash
npm install @microsoft/signalr
```

#### 2. **UPDATED: App-Level Integration**

```typescript
// App.tsx - Wrap entire application
import { SignalRProvider } from '../contexts/SignalRContext';

function App() {
    return (
        <SignalRProvider>
            <Admin
                dataProvider={dataProvider}
                authProvider={authProvider}
                dashboard={Dashboard}
            >
                {/* All resources have access to persistent SignalR */}
            </Admin>
        </SignalRProvider>
    );
}

// Component usage - now uses global context
import { useSignalRContext } from '../contexts/SignalRContext';

function Dashboard() {
    const { isConnected, realtimeMetrics } = useSignalRContext();

    return (
        <div>
            <div>Status: {isConnected ? 'Live' : 'Offline'}</div>
            <RealtimeSystemMetrics />
        </div>
    );
}
```

## 📡 Real-time Events

### System Metrics Updates
**Event**: `SystemMetricsUpdate`  
**Frequency**: Every 30 seconds (configurable)  
**Payload**:
```typescript
interface SystemHealthMetrics {
    timestamp: Date;
    cpuUsagePercent: number;
    memoryUsagePercent: number;
    diskUsagePercent: number;
    uptimeMinutes: number;
}
```

### Scan Progress Updates
**Event**: `ScanProgressUpdate`  
**Frequency**: Real-time during scans  
**Payload**:
```typescript
interface ScanProgressUpdate {
    scanId: string;
    progress: number;
    status: string;
    filesScanned: number;
    threatsFound: number;
}
```

### Threat Intelligence Status
**Event**: `ThreatIntelligenceStatusUpdate`  
**Frequency**: On status changes  
**Payload**:
```typescript
interface ThreatIntelligenceStatus {
    virusTotalStatus: 'operational' | 'degraded' | 'down';
    malwareBazaarStatus: 'operational' | 'degraded' | 'down';
    alienvaultOtxStatus: 'operational' | 'degraded' | 'down';
    lastUpdated: Date;
}
```

## 🔐 Security

### JWT Authentication
All SignalR connections require JWT authentication:

```csharp
[Authorize] // Requires valid JWT token
public class ScanProgressHub : Hub
```

### Connection Validation
```typescript
const connection = new HubConnectionBuilder()
    .withUrl('/hubs/scan-progress', {
        accessTokenFactory: () => localStorage.getItem('accessToken')
    })
    .build();
```

### Group-based Access Control
Users can only join groups they have permission for:
- System administrators: All groups
- Regular users: Limited to their own scan groups

## 🚀 Usage Examples

### 1. Basic System Monitoring

```typescript
function SystemMonitor() {
    const { systemMetrics, isConnected } = useSignalR({
        url: '/hubs/scan-progress',
        accessTokenFactory: () => localStorage.getItem('accessToken')
    });

    return (
        <div>
            <div>Status: {isConnected ? 'Connected' : 'Disconnected'}</div>
            {systemMetrics && (
                <div>
                    <p>CPU: {systemMetrics.cpuUsagePercent}%</p>
                    <p>Memory: {systemMetrics.memoryUsagePercent}%</p>
                    <p>Disk: {systemMetrics.diskUsagePercent}%</p>
                </div>
            )}
        </div>
    );
}
```

### 2. Scan Progress Tracking

```typescript
function ScanTracker({ scanId }: { scanId: string }) {
    const { scanProgress } = useSignalR({
        url: '/hubs/scan-progress',
        accessTokenFactory: () => localStorage.getItem('accessToken'),
        onConnected: (connection) => {
            connection.invoke('JoinScanGroup', scanId);
        }
    });

    const currentScan = scanProgress.find(s => s.scanId === scanId);

    return currentScan ? (
        <div>
            <progress value={currentScan.progress} max={100} />
            <p>Files Scanned: {currentScan.filesScanned}</p>
            <p>Threats Found: {currentScan.threatsFound}</p>
        </div>
    ) : null;
}
```

### 3. Manual Broadcast Trigger

```typescript
// Trigger immediate system metrics broadcast
const response = await fetch('/api/systemmetrics/broadcast', {
    method: 'POST',
    headers: {
        'Authorization': `Bearer ${token}`
    }
});
```

## 🔧 Troubleshooting

### Common Issues

#### 1. Connection Failed
**Symptoms**: Unable to establish SignalR connection
**Solutions**:
- Verify JWT token is valid and not expired
- Check CORS configuration includes `AllowCredentials()`
- Ensure SignalR hub is properly mapped in `Program.cs`

#### 2. Authentication Errors
**Symptoms**: 401 Unauthorized when connecting
**Solutions**:
- Verify `accessTokenFactory` returns valid JWT token
- Check token has not expired
- Ensure `[Authorize]` attribute is properly configured

#### 3. Missing Updates
**Symptoms**: Not receiving real-time updates
**Solutions**:
- Verify background service is running
- Check group subscriptions are active
- Ensure periodic timer is configured correctly

#### 4. **FIXED: Connection Drops on Page Navigation**
**Previous Issue**: SignalR connection disconnected when navigating between React Admin pages
**✅ **Solution Implemented**: Global SignalR Context
- **Root Cause**: Connection was component-scoped, got destroyed on page changes
- **Fix**: Moved connection to app-level context provider
- **Result**: Persistent connection across all navigation
- **Files Updated**:
  - `src/contexts/SignalRContext.tsx` (new)
  - `src/App.tsx` (wrapped with provider)
  - `src/components/Dashboard.tsx` (uses context)
  - `src/components/RealtimeSystemMetrics.tsx` (uses context)

### Debugging

#### Enable SignalR Client Logging
```typescript
const connection = new HubConnectionBuilder()
    .withUrl('/hubs/scan-progress')
    .configureLogging(LogLevel.Debug)
    .build();
```

#### Backend Logging
```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
});
```

## 📊 Performance Considerations

### Connection Limits
- Default: 100 concurrent connections per hub
- Configurable in `appsettings.json`
- Monitor connection count in production

### Broadcast Frequency
- Default: 30-second intervals for system metrics
- Adjustable based on performance requirements
- Consider client-side throttling for high-frequency updates

### Memory Usage
- SignalR maintains connection state in memory
- Group subscriptions add minimal overhead
- Monitor memory usage with many concurrent connections

## 🚀 Production Deployment

### Health Checks
```csharp
builder.Services.AddHealthChecks()
    .AddSignalRHub("ScanProgressHub", "/hubs/scan-progress");
```

### Monitoring
- Monitor connection counts
- Track message delivery rates
- Log authentication failures
- Monitor memory usage

### Scaling Considerations
- Use Redis backplane for multiple instances
- Configure sticky sessions for load balancing
- Monitor WebSocket connection limits

## 📚 References

- [ASP.NET Core SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [SignalR JavaScript Client](https://docs.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
- [React SignalR Integration Patterns](https://docs.microsoft.com/en-us/aspnet/core/signalr/client-features)

---

**Status**: ✅ Production Ready  
**Last Updated**: September 7, 2025  
**Next Review**: October 2025
