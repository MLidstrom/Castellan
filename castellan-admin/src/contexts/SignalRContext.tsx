import React, { createContext, useContext, useCallback, useEffect, ReactNode } from 'react';
import { useSignalR, SystemMetricsUpdate, ScanProgressUpdate, ScanCompleteNotification, ScanErrorNotification, ThreatIntelligenceStatusUpdate, SecurityEventUpdate, CorrelationAlertUpdate, YaraMatchUpdate, ConsolidatedDashboardData } from '../hooks/useSignalR';
import { useNotify } from 'react-admin';

interface SignalRContextType {
  connectionState: 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';
  isConnected: boolean;
  realtimeMetrics: SystemMetricsUpdate | null;
  latestSecurityEvents: SecurityEventUpdate[];
  latestCorrelationAlerts: CorrelationAlertUpdate[];
  latestYaraMatches: YaraMatchUpdate[];
  consolidatedDashboardData: ConsolidatedDashboardData | null;
  connect: () => void;
  disconnect: () => void;
  joinScanUpdates: (scanId: string) => Promise<void>;
  leaveScanUpdates: (scanId: string) => Promise<void>;
  triggerSystemUpdate: () => Promise<void>;
  joinDashboardUpdates: () => Promise<void>;
  leaveDashboardUpdates: () => Promise<void>;
  requestDashboardData: (timeRange: string) => Promise<void>;
}

const SignalRContext = createContext<SignalRContextType | undefined>(undefined);

interface SignalRProviderProps {
  children: ReactNode;
}

export const SignalRProvider: React.FC<SignalRProviderProps> = ({ children }) => {
  const notify = useNotify();
  const [realtimeMetrics, setRealtimeMetrics] = React.useState<SystemMetricsUpdate | null>(null);
  const [latestSecurityEvents, setLatestSecurityEvents] = React.useState<SecurityEventUpdate[]>([]);
  const [latestCorrelationAlerts, setLatestCorrelationAlerts] = React.useState<CorrelationAlertUpdate[]>([]);
  const [latestYaraMatches, setLatestYaraMatches] = React.useState<YaraMatchUpdate[]>([]);
  const [consolidatedDashboardData, setConsolidatedDashboardData] = React.useState<ConsolidatedDashboardData | null>(null);

  const signalROptions = {
    enabled: true,
    onSystemMetrics: useCallback((update: SystemMetricsUpdate) => {
      console.log('📊 Global SignalR - Received system metrics:', update);
      setRealtimeMetrics(update);
    }, []),
    onSecurityEvent: useCallback((event: SecurityEventUpdate) => {
      console.log('🔒 Global SignalR - Received security event:', event);
      setLatestSecurityEvents(prev => [event, ...prev.slice(0, 99)]);

      const riskIcon = event.riskLevel === 'critical' ? '🚨' :
                      event.riskLevel === 'high' ? '⚠️' :
                      event.riskLevel === 'medium' ? '⚡' : '📝';

      notify(`${riskIcon} ${event.summary}`, {
        type: event.riskLevel === 'critical' || event.riskLevel === 'high' ? 'error' :
              event.riskLevel === 'medium' ? 'warning' : 'info',
        autoHideDuration: 10000
      });
    }, [notify]),
    onCorrelationAlert: useCallback((alert: CorrelationAlertUpdate) => {
      console.log('🔗 Global SignalR - Received correlation alert:', alert);
      setLatestCorrelationAlerts(prev => [alert, ...prev.slice(0, 49)]);

      notify(`🔗 Correlation Detected: ${alert.summary}`, {
        type: alert.riskLevel === 'critical' ? 'error' : 'warning',
        autoHideDuration: 15000
      });
    }, [notify]),
    onYaraMatch: useCallback((match: YaraMatchUpdate) => {
      console.log('🎯 Global SignalR - Received YARA match:', match);
      setLatestYaraMatches(prev => [match, ...prev.slice(0, 49)]);

      notify(`🎯 YARA Match: ${match.ruleName} in ${match.fileName}`, {
        type: match.severity === 'critical' || match.severity === 'high' ? 'error' : 'warning',
        autoHideDuration: 15000
      });
    }, [notify]),
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
    }, [notify]),
    onDashboardData: useCallback((data: ConsolidatedDashboardData) => {
      console.log('📊 Global SignalR - Received dashboard data:', data);
      setConsolidatedDashboardData(data);
    }, [])
  };

  const {
    connectionState,
    isConnected,
    connect,
    disconnect,
    joinScanUpdates,
    leaveScanUpdates,
    triggerSystemUpdate,
    joinDashboardUpdates,
    leaveDashboardUpdates,
    requestDashboardData
  } = useSignalR(signalROptions);

  const contextValue: SignalRContextType = {
    connectionState,
    isConnected,
    realtimeMetrics,
    latestSecurityEvents,
    latestCorrelationAlerts,
    latestYaraMatches,
    consolidatedDashboardData,
    connect,
    disconnect,
    joinScanUpdates,
    leaveScanUpdates,
    triggerSystemUpdate,
    joinDashboardUpdates,
    leaveDashboardUpdates,
    requestDashboardData
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