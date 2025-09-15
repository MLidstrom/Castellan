# Real-time Dashboard Integration Guide

**Version**: 1.0  
**Created**: September 14, 2025  
**Status**: ✅ **OPERATIONAL** - Production Ready

## 🎯 Overview

The Castellan dashboard now includes comprehensive real-time updates powered by SignalR WebSocket connections. This provides live system metrics, scan progress, and instant notifications without requiring manual refresh.

## 🚀 Features

### ✅ Implemented Features

- **Real-time System Metrics**: CPU, memory, component health updated every 30 seconds
- **Live Connection Status**: Visual indicator showing connection state
- **Adaptive Refresh Button**: Changes behavior based on connection status
- **Automatic Fallback**: Falls back to manual API calls when SignalR is unavailable
- **Live System Component Health**: Real-time status of all system components
- **Scan Progress Tracking**: Live updates during active threat scans
- **Threat Intelligence Alerts**: Real-time notifications for service issues and rate limits
- **Error Handling**: Automatic reconnection with exponential backoff
- **Performance Metrics**: Live event processing and vector operation statistics

### 🎛️ Connection States

| State | Description | UI Indicator |
|-------|-------------|--------------|
| **Connected** | ✅ Real-time updates active | Green wifi icon |
| **Connecting** | 🔄 Establishing connection | Loading spinner |
| **Reconnecting** | 🔄 Attempting to restore connection | Loading spinner |
| **Disconnected** | ❌ Manual refresh required | Red wifi off icon |

## 🏗️ Architecture

### Backend Components

#### SignalR Hub
- **File**: `src/Castellan.Worker/Hubs/ScanProgressHub.cs`
- **Endpoint**: `http://localhost:5000/hubs/scan-progress`
- **Authentication**: JWT Bearer token required

#### System Metrics Service
- **File**: `src/Castellan.Worker/Services/EnhancedProgressTrackingService.cs`
- **Broadcast Frequency**: Every 30 seconds
- **Manual Trigger**: `POST /api/system-metrics/broadcast`

### Frontend Components

#### ✅ **UPDATED: Global SignalR Context (September 2025)**
- **File**: `castellan-admin/src/contexts/SignalRContext.tsx`
- **Purpose**: Provides persistent SignalR connection across entire React Admin application
- **Problem Solved**: Connection no longer disconnects when navigating between pages
- **Architecture**: App-level context provider wraps entire Admin component

#### useSignalR Hook
- **File**: `castellan-admin/src/hooks/useSignalR.ts`
- **Purpose**: Manages WebSocket connection and event handling
- **Features**: Auto-reconnect, JWT auth, typed event handlers

#### Enhanced Dashboard
- **File**: `castellan-admin/src/components/Dashboard.tsx`
- **Features**: Live metrics display, connection status, adaptive UI

#### RealtimeSystemMetrics Component
- **File**: `castellan-admin/src/components/RealtimeSystemMetrics.tsx`
- **Purpose**: Displays live system performance and health data

## 📡 Real-time Events

### System Metrics Update
- **Event**: `SystemMetricsUpdate`
- **Frequency**: Every 30 seconds
- **Contains**: CPU, memory, component health, performance stats

```typescript
interface SystemMetricsUpdate {
  timestamp: string;
  health: {
    isHealthy: boolean;
    totalComponents: number;
    healthyComponents: number;
    systemUptime: string;
    components: Record<string, ComponentHealth>;
  };
  performance: {
    cpuUsagePercent: number;
    memoryUsageMB: number;
    threadCount: number;
    handleCount: number;
    eventProcessing: EventProcessingStats;
  };
  threatIntelligence: ThreatIntelligenceStatus;
  cache: CacheStatistics;
  activeScans: ActiveScanInfo;
}
```

### Scan Progress Update
- **Event**: `ScanProgressUpdate`
- **Frequency**: Real-time during scans
- **Contains**: Files scanned, threats found, completion percentage

### System Alerts
- **Event**: `ThreatIntelligenceStatus`
- **Triggers**: Service errors, rate limit warnings
- **Notifications**: Toast messages with severity levels

## 🔧 Setup & Configuration

### Prerequisites
1. **Backend**: Castellan Worker running on `http://localhost:5000`
2. **SignalR Package**: `@microsoft/signalr` installed in frontend
3. **Authentication**: Valid JWT token in localStorage

### Starting Services
```powershell
# Start all Castellan services
.\scripts\start.ps1

# Or manually start Worker service
cd src\Castellan.Worker
dotnet run
```

### Verifying Connection
1. **Dashboard**: Look for green wifi icon in top-right
2. **Browser Console**: Check for "SignalR connected" messages
3. **Network Tab**: WebSocket connection to `/hubs/scan-progress`

## 🎛️ Dashboard Features

### Connection Status Indicator
Located in dashboard header:
- **Green WiFi Icon**: Connected, receiving real-time updates
- **Loading Spinner**: Connecting/Reconnecting
- **Red WiFi Off**: Disconnected, manual refresh required
- **Tooltip**: Shows last update time and connection details

### Adaptive Refresh Button
- **When Connected**: "Update Now" - triggers immediate SignalR broadcast
- **When Disconnected**: "Manual Refresh" - falls back to API calls
- **Visual Cues**: Color changes based on connection state

### Live System Metrics
- **CPU Usage**: Real-time percentage with progress bar
- **Memory Usage**: Current memory consumption in MB
- **System Uptime**: Live system uptime display
- **Component Health**: Status of all system components
- **Event Processing**: Live statistics on event throughput

### Real-time Notifications
- **Connection Events**: Connect/disconnect notifications
- **Scan Milestones**: Progress updates at 25%, 50%, 75%, 100%
- **Service Alerts**: Threat intelligence service warnings
- **Error Notifications**: System errors and connection issues

## 🧪 Testing & Validation

### ✅ **Comprehensive Navigation Testing (September 2025)**

**Test Date**: September 15, 2025
**Test Coverage**: All 11 admin interface menu items
**Result**: 100% Pass Rate - No failures detected

#### Tested Menu Items ✅
- ✅ **Dashboard** - Caching system functional, real-time updates working
- ✅ **Security Events** - API connectivity verified, filtering operational
- ✅ **MITRE Techniques** - Search functionality confirmed working
- ✅ **YARA Rules** - Large dataset (1950 entries) loading correctly
- ✅ **YARA Matches** - Live alerts functionality operational
- ✅ **Timeline** - Date filters and granularity controls working
- ✅ **Compliance Reports** - Framework filtering available and functional
- ✅ **System Status** - Real-time updates via SignalR confirmed
- ✅ **Threat Scanner** - Full interface loaded with all controls
- ✅ **Notification Settings** - Empty state handling correct
- ✅ **Configuration** - Complex threat intelligence config loading

#### Test Results Summary
- **Navigation**: All menu items accessible without errors
- **API Connectivity**: All endpoints responding correctly (200 status codes)
- **SignalR**: Real-time connections established successfully across all pages
- **Data Loading**: Proper loading states and error handling confirmed
- **Dashboard Caching**: Cache hit/miss system working perfectly
- **Real-time Features**: Live indicators and updates operational

### Manual Testing Steps

1. **Connection Test**:
   ```bash
   # Start backend
   .\scripts\start.ps1
   # Open dashboard, verify green connection indicator
   ```

2. **Live Updates**:
   - Watch system metrics update every 30 seconds
   - Verify "Last updated" timestamp changes
   - Check browser console for "System metrics update" logs

3. **Fallback Test**:
   ```bash
   # Stop backend
   .\scripts\stop.ps1
   # Verify red disconnection indicator
   # Test manual refresh button functionality
   ```

4. **Reconnection Test**:
   ```bash
   # Restart backend while dashboard is open
   .\scripts\start.ps1
   # Verify automatic reconnection and green indicator
   ```

5. **✅ Full Menu Navigation Test**:
   - Navigate through all 11 menu items systematically
   - Verify each page loads without errors
   - Confirm API calls complete successfully
   - Check real-time features on applicable pages

### Browser Console Monitoring
Expected log messages:
```
🔧 Testing backend connectivity...
📡 Triggering SignalR system update...
📊 Received real-time system metrics: {...}
SignalR connected: {connectionId: "...", timestamp: "..."}
```

## 🐛 Troubleshooting

### Common Issues

#### No Connection (Red WiFi Icon)
1. **Backend Not Running**: Start with `.\scripts\start.ps1`
2. **Port Issues**: Verify Worker service on `localhost:5000`
3. **CORS Issues**: Check CORS configuration allows SignalR connections
4. **JWT Token**: Verify valid authentication token in localStorage

#### Partial Connection (Reconnecting Loop)
1. **Network Issues**: Check network connectivity
2. **Authentication**: JWT token may be expired or invalid
3. **SignalR Hub**: Verify hub registration in `Program.cs`

#### No Live Updates (Connected but No Data)
1. **Service Registration**: Check `EnhancedProgressTrackingService` is registered
2. **Timer Issues**: Verify 30-second broadcast timer is active
3. **Group Membership**: Ensure client joined `SystemMetrics` group

### Debug Commands

```bash
# Check backend health
curl http://localhost:5000/api/system-status

# Trigger manual metrics broadcast
curl -X POST http://localhost:5000/api/system-metrics/broadcast \
     -H "Authorization: Bearer YOUR_JWT_TOKEN"

# View SignalR logs in browser console
# Filter by "SignalR" to see connection events
```

## 📊 Performance Impact

### Resource Usage
- **WebSocket Connection**: ~5KB persistent connection
- **Update Frequency**: Every 30 seconds (configurable)
- **Payload Size**: ~2-5KB per system metrics update
- **Browser Memory**: ~1-2MB additional for real-time state

### Network Traffic
- **Initial Connection**: ~10KB handshake
- **Ongoing Updates**: ~5KB every 30 seconds
- **Reconnection**: Automatic with exponential backoff

## 🔮 Future Enhancements

### Planned Features
- [ ] Real-time security event alerts
- [ ] Live threat map updates
- [ ] Customizable update frequencies
- [ ] Real-time YARA rule compilation status
- [ ] Live log streaming interface

### Configuration Options (Future)
- Dashboard refresh intervals
- Notification preferences
- Alert thresholds
- Connection retry settings

## 📝 Developer Notes

### Adding New Real-time Events

1. **Backend**: Add event to `ScanProgressBroadcaster`
2. **Frontend**: Add interface to `useSignalR.ts`
3. **Handler**: Add callback to Dashboard component
4. **UI**: Update components to display new data

### Custom Components
```typescript
// Using real-time system metrics in custom components
import { useRealtimeSystemMetrics } from '../hooks/useSignalR';

export const CustomMetrics = () => {
  const { connectionState, isConnected } = useRealtimeSystemMetrics(
    (metrics) => {
      console.log('Received metrics:', metrics);
      // Handle real-time updates
    }
  );
  
  return (
    <div>Status: {connectionState}</div>
  );
};
```

### Testing with Mock Data
```typescript
// Mock SignalR connection for testing
const mockConnection = {
  invoke: jest.fn(),
  on: jest.fn(),
  start: jest.fn(),
  stop: jest.fn()
};
```

---

**Questions or Issues?** 
- Check browser console for SignalR connection logs
- Verify backend services are running with `.\scripts\start.ps1`
- Monitor network tab for WebSocket connections
- Ensure JWT authentication token is valid
