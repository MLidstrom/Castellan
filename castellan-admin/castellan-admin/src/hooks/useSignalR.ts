import { useEffect, useRef, useCallback, useState } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useNotify } from 'react-admin';

export interface ScanProgressUpdate {
  type: 'progress';
  progress: {
    scanId: string;
    status: string;
    filesScanned: number;
    totalEstimatedFiles: number;
    directoriesScanned: number;
    threatsFound: number;
    currentFile: string;
    currentDirectory: string;
    percentComplete: number;
    startTime: string;
    elapsedTime: string;
    estimatedTimeRemaining?: string;
    bytesScanned: number;
    scanPhase: string;
  };
  timestamp: string;
}

export interface SystemMetricsUpdate {
  timestamp: string;
  health: {
    isHealthy: boolean;
    totalComponents: number;
    healthyComponents: number;
    systemUptime: string;
    components: Record<string, {
      isHealthy: boolean;
      status: string;
      lastCheck: string;
      responseTimeMs: number;
      details: string;
    }>;
  };
  threatIntelligence: {
    isEnabled: boolean;
    services: Record<string, {
      isEnabled: boolean;
      isHealthy: boolean;
      apiCallsToday: number;
      rateLimit: number;
      remainingQuota: number;
      lastSuccessfulQuery: string;
      lastError: string;
    }>;
    totalQueries: number;
    cacheHits: number;
    cacheHitRate: number;
    lastQuery: string;
  };
  performance: {
    cpuUsagePercent: number;
    memoryUsageMB: number;
    threadCount: number;
    handleCount: number;
    eventProcessing: {
      eventsPerSecond: number;
      totalEventsProcessed: number;
      queuedEvents: number;
      failedEvents: number;
    };
    vectorOperations: {
      vectorsPerSecond: number;
      averageEmbeddingTime: string;
      averageUpsertTime: string;
      averageSearchTime: string;
      batchOperations: number;
    };
  };
  cache: {
    embedding: {
      totalEntries: number;
      hits: number;
      misses: number;
      hitRate: number;
      memoryUsageMB: number;
    };
    threatIntelligence: {
      totalHashes: number;
      cachedResults: number;
      cacheUtilization: number;
      oldestEntry: string;
      expiredEntries: number;
    };
    general: {
      totalMemoryUsageMB: number;
      activeCaches: number;
      memoryPressure: number;
      evictedEntries: number;
    };
  };
  activeScans: {
    hasActiveScan: boolean;
    currentScan?: {
      scanId: string;
      status: string;
      filesScanned: number;
      totalEstimatedFiles: number;
      percentComplete: number;
      currentFile: string;
      scanPhase: string;
    };
    queuedScans: number;
    recentScans: Array<{
      id: string;
      type: string;
      status: string;
      startTime: string;
      duration: string;
      filesScanned: number;
      threatsFound: number;
    }>;
  };
}

export interface ScanCompleteNotification {
  type: 'scanComplete';
  scanId: string;
  result: any;
  timestamp: string;
}

export interface ScanErrorNotification {
  type: 'scanError';
  scanId: string;
  error: string;
  timestamp: string;
}

export interface ThreatIntelligenceStatusUpdate {
  services: Record<string, {
    isEnabled: boolean;
    isHealthy: boolean;
    apiCallsToday: number;
    rateLimit: number;
    remainingQuota: number;
    lastSuccessfulQuery: string;
    lastError: string;
  }>;
  isEnabled: boolean;
  totalQueries: number;
  cacheHits: number;
  cacheHitRate: number;
  lastQuery: string;
}

interface UseSignalROptions {
  enabled?: boolean;
  onScanProgress?: (update: ScanProgressUpdate) => void;
  onSystemMetrics?: (update: SystemMetricsUpdate) => void;
  onScanComplete?: (notification: ScanCompleteNotification) => void;
  onScanError?: (notification: ScanErrorNotification) => void;
  onThreatIntelligenceStatus?: (status: ThreatIntelligenceStatusUpdate) => void;
  onConnect?: () => void;
  onDisconnect?: () => void;
  onError?: (error: Error) => void;
}

export const useSignalR = (options: UseSignalROptions = {}) => {
  const {
    enabled = true,
    onScanProgress,
    onSystemMetrics,
    onScanComplete,
    onScanError,
    onThreatIntelligenceStatus,
    onConnect,
    onDisconnect,
    onError
  } = options;

  const connectionRef = useRef<HubConnection | null>(null);
  const [connectionState, setConnectionState] = useState<'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'>('Disconnected');
  const notify = useNotify();

  const connect = useCallback(async () => {
    if (!enabled || connectionRef.current?.state === 'Connected') {
      return;
    }

    try {
      setConnectionState('Connecting');

      // Build SignalR connection
      const connection = new HubConnectionBuilder()
        .withUrl('http://localhost:5000/hubs/scan-progress', {
          withCredentials: true,
          accessTokenFactory: () => {
            // Get JWT token for authentication
            return localStorage.getItem('auth_token') || '';
          }
        })
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .configureLogging(LogLevel.Information)
        .build();

      // Set up event handlers
      connection.on('Connected', (data) => {
        console.log('SignalR connected:', data);
        setConnectionState('Connected');
        onConnect?.();
      });

      connection.on('ScanProgressUpdate', (update: ScanProgressUpdate) => {
        console.log('Scan progress update:', update);
        onScanProgress?.(update);
        
        // Show notification for scan progress milestones
        if (update.progress.percentComplete % 25 === 0) {
          notify(`Scan ${update.progress.percentComplete}% complete`, { type: 'info' });
        }
      });

      connection.on('SystemMetricsUpdate', (update: SystemMetricsUpdate) => {
        console.log('System metrics update:', update);
        onSystemMetrics?.(update);
      });

      connection.on('ScanCompleted', (notification: ScanCompleteNotification) => {
        console.log('Scan completed:', notification);
        onScanComplete?.(notification);
        notify(`Scan ${notification.scanId} completed successfully`, { type: 'success' });
      });

      connection.on('ScanError', (notification: ScanErrorNotification) => {
        console.log('Scan error:', notification);
        onScanError?.(notification);
        notify(`Scan error: ${notification.error}`, { type: 'error' });
      });

      connection.on('ThreatIntelligenceStatus', (status: ThreatIntelligenceStatusUpdate) => {
        console.log('Threat intelligence status:', status);
        onThreatIntelligenceStatus?.(status);
      });

      // Handle connection state changes
      connection.onreconnecting(() => {
        console.log('SignalR reconnecting...');
        setConnectionState('Reconnecting');
      });

      connection.onreconnected(() => {
        console.log('SignalR reconnected');
        setConnectionState('Connected');
        notify('Connection restored', { type: 'success' });
      });

      connection.onclose((error) => {
        console.log('SignalR connection closed:', error);
        setConnectionState('Disconnected');
        onDisconnect?.();
        if (error) {
          onError?.(error);
          notify('Connection lost', { type: 'warning' });
        }
      });

      // Start the connection
      await connection.start();
      connectionRef.current = connection;

      // Join general progress updates and system metrics groups
      await connection.invoke('JoinSystemMetrics');
      
    } catch (error) {
      console.error('Failed to connect to SignalR hub:', error);
      setConnectionState('Disconnected');
      onError?.(error as Error);
      notify('Failed to connect to real-time updates', { type: 'error' });
    }
  }, [enabled]); // Removed callback dependencies to prevent infinite loops

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      try {
        await connectionRef.current.stop();
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      }
      connectionRef.current = null;
      setConnectionState('Disconnected');
    }
  }, []);

  const joinScanUpdates = useCallback(async (scanId: string) => {
    if (connectionRef.current?.state === 'Connected') {
      try {
        await connectionRef.current.invoke('JoinScanUpdates', scanId);
        console.log(`Joined scan updates for ${scanId}`);
      } catch (error) {
        console.error(`Failed to join scan updates for ${scanId}:`, error);
      }
    }
  }, []);

  const leaveScanUpdates = useCallback(async (scanId: string) => {
    if (connectionRef.current?.state === 'Connected') {
      try {
        await connectionRef.current.invoke('LeaveScanUpdates', scanId);
        console.log(`Left scan updates for ${scanId}`);
      } catch (error) {
        console.error(`Failed to leave scan updates for ${scanId}:`, error);
      }
    }
  }, []);

  const triggerSystemUpdate = useCallback(async () => {
    if (connectionRef.current?.state === 'Connected') {
      try {
        // Call the API endpoint to trigger a manual broadcast
        const response = await fetch('http://localhost:5000/api/system-metrics/broadcast', {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${localStorage.getItem('auth_token')}`,
            'Content-Type': 'application/json'
          }
        });
        
        if (response.ok) {
          notify('System update triggered', { type: 'info' });
        }
      } catch (error) {
        console.error('Failed to trigger system update:', error);
      }
    }
  }, [notify]);

  useEffect(() => {
    if (enabled) {
      connect();
    }

    return () => {
      disconnect();
    };
  }, [enabled]); // Removed connect and disconnect from dependencies to prevent infinite loops

  return {
    connectionState,
    isConnected: connectionState === 'Connected',
    connect,
    disconnect,
    joinScanUpdates,
    leaveScanUpdates,
    triggerSystemUpdate
  };
};

// Hook for scan-specific real-time updates
export const useRealtimeScan = (scanId?: string, onProgress?: (update: ScanProgressUpdate) => void) => {
  const { connectionState, joinScanUpdates, leaveScanUpdates } = useSignalR({
    onScanProgress: onProgress
  });

  useEffect(() => {
    if (scanId && connectionState === 'Connected') {
      joinScanUpdates(scanId);
      return () => {
        leaveScanUpdates(scanId);
      };
    }
  }, [scanId, connectionState, joinScanUpdates, leaveScanUpdates]);

  return { connectionState };
};

// Hook for system metrics updates
export const useRealtimeSystemMetrics = (onUpdate?: (update: SystemMetricsUpdate) => void) => {
  return useSignalR({
    onSystemMetrics: onUpdate
  });
};

// Hook for threat intelligence status updates
export const useRealtimeThreatIntelligence = (onUpdate?: (status: ThreatIntelligenceStatusUpdate) => void) => {
  return useSignalR({
    onThreatIntelligenceStatus: onUpdate
  });
};
