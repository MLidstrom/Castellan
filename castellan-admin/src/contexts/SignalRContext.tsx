import React, { createContext, useContext, useCallback, useEffect, ReactNode } from 'react';
import { useSignalR, SystemMetricsUpdate, ScanProgressUpdate, ScanCompleteNotification, ScanErrorNotification, ThreatIntelligenceStatusUpdate } from '../hooks/useSignalR';
import { useNotify } from 'react-admin';

interface SignalRContextType {
  connectionState: 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';
  isConnected: boolean;
  realtimeMetrics: SystemMetricsUpdate | null;
  connect: () => void;
  disconnect: () => void;
  joinScanUpdates: (scanId: string) => Promise<void>;
  leaveScanUpdates: (scanId: string) => Promise<void>;
  triggerSystemUpdate: () => Promise<void>;
}

const SignalRContext = createContext<SignalRContextType | undefined>(undefined);

interface SignalRProviderProps {
  children: ReactNode;
}

export const SignalRProvider: React.FC<SignalRProviderProps> = ({ children }) => {
  const notify = useNotify();
  const [realtimeMetrics, setRealtimeMetrics] = React.useState<SystemMetricsUpdate | null>(null);

  const signalROptions = {
    enabled: true,
    onSystemMetrics: useCallback((update: SystemMetricsUpdate) => {
      console.log('📊 Global SignalR - Received system metrics:', update);
      setRealtimeMetrics(update);
    }, []),
    onScanProgress: useCallback((update: ScanProgressUpdate) => {
      console.log('🔍 Global SignalR - Received scan progress:', update);
      if (update.progress.percentComplete % 25 === 0) {
        notify(`Scan ${update.progress.percentComplete}% complete`, { type: 'info' });
      }
    }, [notify]),
    onScanComplete: useCallback((notification: ScanCompleteNotification) => {
      console.log('✅ Global SignalR - Scan completed:', notification);
      notify(`Scan ${notification.scanId} completed successfully`, { type: 'success' });
    }, [notify]),
    onScanError: useCallback((notification: ScanErrorNotification) => {
      console.log('❌ Global SignalR - Scan error:', notification);
      notify(`Scan error: ${notification.error}`, { type: 'error' });
    }, [notify]),
    onThreatIntelligenceStatus: useCallback((status: ThreatIntelligenceStatusUpdate) => {
      console.log('🛡️ Global SignalR - Threat intelligence update:', status);
      Object.entries(status.services).forEach(([serviceName, service]) => {
        if (!service.isHealthy && service.lastError) {
          notify(`${serviceName} service error: ${service.lastError}`, {
            type: 'error',
            autoHideDuration: 10000
          });
        }
        if (service.remainingQuota < service.rateLimit * 0.1) {
          notify(`${serviceName} approaching rate limit (${service.remainingQuota} remaining)`, {
            type: 'warning',
            autoHideDuration: 8000
          });
        }
      });
    }, [notify]),
    onConnect: useCallback(() => {
      console.log('✅ Global SignalR - Connected');
      notify('Real-time connection established', { type: 'success' });
    }, [notify]),
    onDisconnect: useCallback(() => {
      console.log('🔌 Global SignalR - Disconnected');
      notify('Real-time connection lost', { type: 'warning' });
    }, [notify]),
    onError: useCallback((error: Error) => {
      console.error('❌ Global SignalR - Error:', error);
      notify('Real-time connection error', { type: 'error' });
    }, [notify])
  };

  const {
    connectionState,
    isConnected,
    connect,
    disconnect,
    joinScanUpdates,
    leaveScanUpdates,
    triggerSystemUpdate
  } = useSignalR(signalROptions);

  const contextValue: SignalRContextType = {
    connectionState,
    isConnected,
    realtimeMetrics,
    connect,
    disconnect,
    joinScanUpdates,
    leaveScanUpdates,
    triggerSystemUpdate
  };

  return (
    <SignalRContext.Provider value={contextValue}>
      {children}
    </SignalRContext.Provider>
  );
};

export const useSignalRContext = () => {
  const context = useContext(SignalRContext);
  if (context === undefined) {
    throw new Error('useSignalRContext must be used within a SignalRProvider');
  }
  return context;
};