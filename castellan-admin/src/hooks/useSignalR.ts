import { useEffect, useRef, useCallback, useState } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useNotify } from 'react-admin';

export interface SecurityEventUpdate {
  id: string;
  timestamp: string;
  eventType: string;
  riskLevel: string;
  confidence: number;
  summary: string;
  eventId?: number;
  machineName?: string;
  userName?: string;
  hasCorrelation: boolean;
  correlationContext?: string;
  mitreTechniques?: string[];
  recommendedActions?: string[];
}

export interface CorrelationAlertUpdate {
  id: string;
  timestamp: string;
  eventType: string;
  riskLevel: string;
  correlationIds: string[];
  correlationContext: string;
  summary: string;
  confidence: number;
  correlationScore: number;
  mitreTechniques?: string[];
  recommendedActions?: string[];
}

export interface YaraMatchUpdate {
  id: string;
  timestamp: string;
  fileName: string;
  ruleName: string;
  severity: string;
  ruleCategory: string;
  filePath: string;
  fileSize: number;
  fileHash: string;
  tags: string[];
  malwareFamily?: string;
  confidence: number;
  description: string;
}

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

// Dashboard Data Consolidation Types
export interface ConsolidatedDashboardData {
  securityEvents: {
    totalEvents: number;
    riskLevelCounts: Record<string, number>;
    recentEvents: Array<{
      id: string;
      eventType: string;
      timestamp: string;
      riskLevel: string;
      source: string;
      machine: string;
    }>;
    lastEventTime: string;
  };
  systemStatus: {
    totalComponents: number;
    healthyComponents: number;
    components: Array<{
      component: string;
      status: string;
      responseTime: number;
      lastCheck: string;
    }>;
    componentStatuses: Record<string, string>;
  };
  threatScanner: {
    totalScans: number;
    activeScans: number;
    completedScans: number;
    threatsFound: number;
    lastScanTime: string;
    recentScans: Array<{
      id: string;
      scanType: string;
      timestamp: string;
      status: string;
      filesScanned: number;
      threatsFound: number;
    }>;
  };
  lastUpdated: string;
  timeRange: string;
}

interface UseSignalROptions {
  enabled?: boolean;
  onScanProgress?: (update: ScanProgressUpdate) => void;
  onSystemMetrics?: (update: SystemMetricsUpdate) => void;
  onScanComplete?: (notification: ScanCompleteNotification) => void;
  onScanError?: (notification: ScanErrorNotification) => void;
  onThreatIntelligenceStatus?: (status: ThreatIntelligenceStatusUpdate) => void;
  onDashboardData?: (data: ConsolidatedDashboardData) => void;
  onSecurityEvent?: (event: SecurityEventUpdate) => void;
  onCorrelationAlert?: (alert: CorrelationAlertUpdate) => void;
  onYaraMatch?: (match: YaraMatchUpdate) => void;
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
    onDashboardData,
    onSecurityEvent,
    onCorrelationAlert,
    onYaraMatch,
    onConnect,
    onDisconnect,
    onError
  } = options;

  const connectionRef = useRef<HubConnection | null>(null);
  const connectingRef = useRef(false); // Track connection attempts
  const retryTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const connectionAttemptsRef = useRef(0); // Track connection attempts for backoff
  const lastAttemptRef = useRef<number>(0); // Track last attempt timestamp
  const [connectionState, setConnectionState] = useState<'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'>('Disconnected');
  const notify = useNotify();
  
  // Use refs to store latest callback functions to prevent stale closures
  const callbacksRef = useRef({
    onScanProgress,
    onSystemMetrics,
    onScanComplete,
    onScanError,
    onThreatIntelligenceStatus,
    onDashboardData,
    onSecurityEvent,
    onCorrelationAlert,
    onYaraMatch,
    onConnect,
    onDisconnect,
    onError
  });
  
  // Update callbacks ref whenever props change
  callbacksRef.current = {
    onScanProgress,
    onSystemMetrics,
    onScanComplete,
    onScanError,
    onThreatIntelligenceStatus,
    onDashboardData,
    onSecurityEvent,
    onCorrelationAlert,
    onYaraMatch,
    onConnect,
    onDisconnect,
    onError
  };

  const connect = useCallback(async () => {
    if (!enabled) {
      console.log('🚫 SignalR disabled, skipping connection attempt');
      return;
    }

    // Prevent multiple connections with ref-based tracking
    if (connectingRef.current) {
      console.log('⚠️ SignalR connection attempt already in progress, skipping...');
      return;
    }

    if (connectionRef.current?.state === 'Connected' || connectionRef.current?.state === 'Connecting') {
      console.log('⚠️ SignalR connection already exists or is connecting, skipping...', connectionRef.current?.state);
      return;
    }

    // Check if we have an auth token - if not, don't attempt connection
    const authToken = localStorage.getItem('auth_token');
    if (!authToken) {
      console.log('🔑 No auth token available, waiting for authentication before connecting to SignalR');
      return;
    }

    // Enforce minimum time between connection attempts (5 seconds)
    const now = Date.now();
    const timeSinceLastAttempt = now - lastAttemptRef.current;
    const minInterval = 5000; // 5 seconds minimum
    if (timeSinceLastAttempt < minInterval) {
      console.log(`⏳ Too soon for retry (${timeSinceLastAttempt}ms < ${minInterval}ms), skipping...`);
      return;
    }
    lastAttemptRef.current = now;

    // Clear any pending retry timeouts
    if (retryTimeoutRef.current) {
      clearTimeout(retryTimeoutRef.current);
      retryTimeoutRef.current = null;
      console.log('🕐 Cleared existing retry timeout');
    }

    try {
      connectingRef.current = true;
      setConnectionState('Connecting');
      console.log('🔄 Setting connecting flag to true');

      // Build SignalR connection with proper authentication
      console.log('🔍 Connecting to SignalR hub: http://localhost:5000/hubs/scan-progress');
      console.log('🔑 Auth token: Present (' + authToken.substring(0, 20) + '...)');

      const connection = new HubConnectionBuilder()
        .withUrl('http://localhost:5000/hubs/scan-progress', {
          accessTokenFactory: () => {
            const token = localStorage.getItem('auth_token');
            console.log('🔑 Providing token for SignalR: Token provided');
            return token || '';
          },
          skipNegotiation: false,
          // Try all transports in order: WebSockets, Server-Sent Events, Long Polling
          transport: 1 | 2 | 4, // WebSockets, ServerSentEvents, and LongPolling
          withCredentials: true, // Match backend CORS AllowCredentials setting
          logger: LogLevel.Debug // Enable transport debugging
        })
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .configureLogging(LogLevel.Debug) // More verbose logging
        .build();
        
      console.log('🔍 SignalR connection object created, attempting to start...');

      // Set up event handlers
      connection.on('joinedSystemMetrics', (data) => {
        console.log('✅ Joined SystemMetrics group:', data);
      });

      // Dashboard updates response handlers
      connection.on('JoinedDashboardUpdates', (data) => {
        console.log('✅ Joined DashboardUpdates group:', data);
      });

      connection.on('LeftDashboardUpdates', (data) => {
        console.log('✅ Left DashboardUpdates group:', data);
      });

      connection.on('DashboardDataRequested', (data) => {
        console.log('✅ Dashboard data requested:', data);
      });

      // Security events response handlers
      connection.on('JoinedSecurityEvents', (data) => {
        console.log('✅ Joined SecurityEvents group:', data);
      });

      connection.on('LeftSecurityEvents', (data) => {
        console.log('✅ Left SecurityEvents group:', data);
      });

      // Correlation alerts response handlers
      connection.on('JoinedCorrelationAlerts', (data) => {
        console.log('✅ Joined CorrelationAlerts group:', data);
      });

      connection.on('LeftCorrelationAlerts', (data) => {
        console.log('✅ Left CorrelationAlerts group:', data);
      });

      connection.on('Connected', (data) => {
        console.log('✅ SignalR "Connected" event received:', data);
        setConnectionState('Connected');
        callbacksRef.current.onConnect?.();
      });
      
      // Add more granular connection state logging
      connection.onclose((error) => {
        console.log('🔌 SignalR connection closed:', {
          hasError: !!error,
          errorMessage: error ? error.message : 'No error',
          errorName: error ? error.name : 'N/A',
          connectionState: connection.state,
          connectionId: connection.connectionId
        });
        connectingRef.current = false;
        setConnectionState('Disconnected');
        callbacksRef.current.onDisconnect?.();
        if (error) {
          console.error('❌ SignalR close error details:', {
            message: error.message,
            name: error.name,
            stack: error.stack
          });
          callbacksRef.current.onError?.(error);
        }
      });

      connection.on('ScanProgressUpdate', (update: ScanProgressUpdate) => {
        console.log('Scan progress update:', update);
        callbacksRef.current.onScanProgress?.(update);
        
        // Show notification for scan progress milestones
        if (update.progress.percentComplete % 25 === 0) {
          notify(`Scan ${update.progress.percentComplete}% complete`, { type: 'info' });
        }
      });

      connection.on('SystemMetricsUpdate', (update: SystemMetricsUpdate) => {
        console.log('System metrics update:', update);
        callbacksRef.current.onSystemMetrics?.(update);
      });

      connection.on('ScanCompleted', (notification: ScanCompleteNotification) => {
        console.log('Scan completed:', notification);
        callbacksRef.current.onScanComplete?.(notification);
        notify(`Scan ${notification.scanId} completed successfully`, { type: 'success' });
      });

      connection.on('ScanError', (notification: ScanErrorNotification) => {
        console.log('Scan error:', notification);
        callbacksRef.current.onScanError?.(notification);
        notify(`Scan error: ${notification.error}`, { type: 'error' });
      });

      connection.on('ThreatIntelligenceStatus', (status: ThreatIntelligenceStatusUpdate) => {
        console.log('Threat intelligence status:', status);
        callbacksRef.current.onThreatIntelligenceStatus?.(status);
      });

      connection.on('DashboardDataUpdate', (data: ConsolidatedDashboardData) => {
        console.log('Dashboard data update received:', data);
        callbacksRef.current.onDashboardData?.(data);
      });

      // Security Events SignalR handlers
      connection.on('SecurityEventUpdate', (event: SecurityEventUpdate) => {
        console.log('🔒 Security event received:', event);
        callbacksRef.current.onSecurityEvent?.(event);
      });

      connection.on('CorrelationAlert', (alert: CorrelationAlertUpdate) => {
        console.log('🔗 Correlation alert received:', alert);
        callbacksRef.current.onCorrelationAlert?.(alert);
      });

      connection.on('YaraMatchDetected', (match: YaraMatchUpdate) => {
        console.log('🎯 YARA match received:', match);
        callbacksRef.current.onYaraMatch?.(match);
      });

      // Handle connection state changes
      connection.onreconnecting(() => {
        console.log('SignalR reconnecting...');
        setConnectionState('Reconnecting');
      });

      connection.onreconnected(() => {
        console.log('SignalR reconnected');
        connectingRef.current = false;
        setConnectionState('Connected');
        notify('Connection restored', { type: 'success' });
      });


      // Start the connection with extended timeout and detailed logging
      console.log('🚀 Starting SignalR connection...');
      const connectionPromise = connection.start().then(() => {
        console.log('✅ SignalR connection.start() completed successfully');
        console.log('🔗 Connection state:', connection.state);
        console.log('🆔 Connection ID:', connection.connectionId);
        
        // Check if connection is established but "Connected" event wasn't fired
        if (connection.state === 'Connected') {
          console.log('ℹ️ Connection is in Connected state, manually triggering connected logic');
          setConnectionState('Connected');
          callbacksRef.current.onConnect?.();
        }
      });
      
      const timeoutPromise = new Promise((_, reject) => {
        setTimeout(() => {
          console.log('⏰ SignalR connection timeout after 30 seconds');
          console.log('🔗 Connection state at timeout:', connection.state);
          reject(new Error('Connection timeout after 30 seconds'));
        }, 30000); // Increased to 30 second timeout
      });
      
      await Promise.race([connectionPromise, timeoutPromise]);
      connectionRef.current = connection;
      connectingRef.current = false;
      
      // Reset connection attempts on successful connection
      connectionAttemptsRef.current = 0;

      console.log('✅ SignalR connection established successfully');
      
      // Join general progress updates and system metrics groups
      try {
        await connection.invoke('JoinSystemMetrics');
        console.log('✅ Joined SystemMetrics group');
      } catch (invokeError) {
        console.warn('⚠️ Failed to join SystemMetrics group:', invokeError);
        // Continue anyway, connection is still valid
      }

    } catch (error) {
      // Increment connection attempts for exponential backoff
      connectionAttemptsRef.current += 1;
      
      console.error('❌ Failed to connect to SignalR hub (attempt #' + connectionAttemptsRef.current + '):', {
        error: error,
        errorMessage: error instanceof Error ? error.message : String(error),
        errorStack: error instanceof Error ? error.stack : undefined,
        connectionState: connectionRef.current?.state,
        connectionId: connectionRef.current?.connectionId
      });
      
      connectingRef.current = false;
      setConnectionState('Disconnected');
      
      // Clean up failed connection
      if (connectionRef.current) {
        try {
          await connectionRef.current.stop();
        } catch (stopError) {
          console.error('Error stopping failed connection:', stopError);
        } finally {
          connectionRef.current = null;
        }
      }

    }
  }, [enabled, notify]);

  // Handle retry logic for failed connections
  useEffect(() => {
    if (!enabled) return;

    // Exponential backoff with max attempts limit
    const maxAttempts = 5;
    if (connectionAttemptsRef.current >= maxAttempts) {
      console.log(`🚫 Max connection attempts reached (${maxAttempts}), stopping retries`);
      return;
    }

    const retryDelay = Math.min(60000, 5000 * Math.pow(2, connectionAttemptsRef.current - 1)); // 5s, 10s, 20s, 40s, 60s max
    console.log(`⏳ Will retry SignalR connection in ${retryDelay}ms (attempt ${connectionAttemptsRef.current}/${maxAttempts})`);

    retryTimeoutRef.current = setTimeout(() => {
      retryTimeoutRef.current = null;
      if (enabled && !connectionRef.current && connectionAttemptsRef.current < maxAttempts) {
        console.log(`🔄 Retrying SignalR connection (attempt ${connectionAttemptsRef.current + 1}/${maxAttempts})...`);
        connect();
      }
    }, retryDelay);

    return () => {
      if (retryTimeoutRef.current) {
        clearTimeout(retryTimeoutRef.current);
        retryTimeoutRef.current = null;
      }
    };
  }, [enabled, connect]);

  const disconnect = useCallback(async () => {
    // Clear retry timeout
    if (retryTimeoutRef.current) {
      clearTimeout(retryTimeoutRef.current);
      retryTimeoutRef.current = null;
    }
    
    // Reset connecting flag and attempt counter
    connectingRef.current = false;
    connectionAttemptsRef.current = 0;
    
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

  const joinDashboardUpdates = useCallback(async () => {
    if (connectionRef.current?.state === 'Connected') {
      try {
        await connectionRef.current.invoke('JoinDashboardUpdates');
        console.log('Joined dashboard updates group');
      } catch (error) {
        console.error('Failed to join dashboard updates:', error);
      }
    }
  }, []);

  const leaveDashboardUpdates = useCallback(async () => {
    if (connectionRef.current?.state === 'Connected') {
      try {
        await connectionRef.current.invoke('LeaveDashboardUpdates');
        console.log('Left dashboard updates group');
      } catch (error) {
        console.error('Failed to leave dashboard updates:', error);
      }
    }
  }, []);

  const requestDashboardData = useCallback(async (timeRange: string = '24h') => {
    if (connectionRef.current?.state === 'Connected') {
      try {
        await connectionRef.current.invoke('RequestDashboardData', timeRange);
        console.log(`Requested dashboard data for ${timeRange}`);
      } catch (error) {
        console.error('Failed to request dashboard data:', error);
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
      // Check for auth token periodically to connect after login
      const checkAndConnect = () => {
        const hasToken = !!localStorage.getItem('auth_token');
        if (hasToken && connectionRef.current?.state !== 'Connected' && !connectingRef.current) {
          console.log('🔑 Auth token found, attempting SignalR connection...');
          connect();
        }
      };

      // Initial check
      checkAndConnect();

      // Set up more frequent check for auth token (every 1 second for faster response)
      const tokenCheckInterval = setInterval(checkAndConnect, 1000);

      // Also listen for storage events (when token is added)
      const handleStorageChange = (e: StorageEvent) => {
        if (e.key === 'auth_token' && e.newValue) {
          console.log('🔑 Auth token detected via storage event, connecting SignalR...');
          setTimeout(checkAndConnect, 500); // Small delay to ensure token is properly set
        }
      };

      window.addEventListener('storage', handleStorageChange);

      return () => {
        clearInterval(tokenCheckInterval);
        window.removeEventListener('storage', handleStorageChange);
        disconnect();
      };
    }

    return () => {
      disconnect();
    };
  }, [enabled, connect, disconnect]);

  return {
    connectionState,
    isConnected: connectionState === 'Connected',
    connect,
    disconnect,
    joinScanUpdates,
    leaveScanUpdates,
    joinDashboardUpdates,
    leaveDashboardUpdates,
    requestDashboardData,
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

// Hook for consolidated dashboard data updates
export const useRealtimeDashboardData = (onUpdate?: (data: ConsolidatedDashboardData) => void, timeRange: string = '24h') => {
  const { connectionState, joinDashboardUpdates, leaveDashboardUpdates, requestDashboardData, isConnected } = useSignalR({
    onDashboardData: onUpdate
  });

  useEffect(() => {
    if (connectionState === 'Connected') {
      joinDashboardUpdates();
      // Request immediate data on connection
      requestDashboardData(timeRange);
      return () => {
        leaveDashboardUpdates();
      };
    }
  }, [connectionState, joinDashboardUpdates, leaveDashboardUpdates, requestDashboardData, timeRange]);

  return { connectionState, isConnected, requestDashboardData };
};
