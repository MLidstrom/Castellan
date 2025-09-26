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
}

export interface YaraMatchUpdate {
  id: string;
  timestamp: string;
  ruleName: string;
  fileName: string;
  filePath: string;
  matchedStrings: string[];
  severity: string;
  description?: string;
  mitreTechniques?: string[];
}

interface UseSecurityEventsSignalROptions {
  enabled?: boolean;
  onSecurityEvent?: (event: SecurityEventUpdate) => void;
  onCorrelationAlert?: (alert: CorrelationAlertUpdate) => void;
  onYaraMatch?: (match: YaraMatchUpdate) => void;
  onConnect?: () => void;
  onDisconnect?: () => void;
  onError?: (error: Error) => void;
}

export const useSecurityEventsSignalR = (options: UseSecurityEventsSignalROptions = {}) => {
  const {
    enabled = true,
    onSecurityEvent,
    onCorrelationAlert,
    onYaraMatch,
    onConnect,
    onDisconnect,
    onError
  } = options;

  const connectionRef = useRef<HubConnection | null>(null);
  const [connectionState, setConnectionState] = useState<'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting'>('Disconnected');
  const [isConnected, setIsConnected] = useState(false);
  const notify = useNotify();

  // Store latest security events
  const [latestSecurityEvents, setLatestSecurityEvents] = useState<SecurityEventUpdate[]>([]);
  const [latestCorrelationAlerts, setLatestCorrelationAlerts] = useState<CorrelationAlertUpdate[]>([]);
  const [latestYaraMatches, setLatestYaraMatches] = useState<YaraMatchUpdate[]>([]);

  const connect = useCallback(async () => {
    if (!enabled) {
      console.log('🚫 Security Events SignalR disabled');
      return;
    }

    const authToken = localStorage.getItem('auth_token');
    if (!authToken) {
      console.log('🔑 No auth token, skipping SignalR connection');
      return;
    }

    if (connectionRef.current?.state === 'Connected') {
      console.log('✅ Security Events SignalR already connected');
      return;
    }

    try {
      setConnectionState('Connecting');

      const connection = new HubConnectionBuilder()
        .withUrl('http://localhost:5000/hubs/scan-progress', {
          accessTokenFactory: () => authToken,
          withCredentials: true
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Information)
        .build();

      // Set up event handlers
      connection.on('SecurityEventUpdate', (event: SecurityEventUpdate) => {
        console.log('🔒 New security event received:', event);

        // Update latest events (keep last 100)
        setLatestSecurityEvents(prev => [event, ...prev.slice(0, 99)]);

        // Notify based on risk level
        const riskIcon = event.riskLevel === 'critical' ? '🚨' :
                        event.riskLevel === 'high' ? '⚠️' :
                        event.riskLevel === 'medium' ? '⚡' : '📝';

        notify(`${riskIcon} ${event.summary}`, {
          type: event.riskLevel === 'critical' || event.riskLevel === 'high' ? 'error' :
                event.riskLevel === 'medium' ? 'warning' : 'info',
          autoHideDuration: 10000
        });

        onSecurityEvent?.(event);
      });

      connection.on('CorrelationAlert', (alert: CorrelationAlertUpdate) => {
        console.log('🔗 Correlation alert received:', alert);

        // Update latest alerts (keep last 50)
        setLatestCorrelationAlerts(prev => [alert, ...prev.slice(0, 49)]);

        // Always notify for correlation alerts as they're important
        notify(`🔗 Correlation Detected: ${alert.summary}`, {
          type: alert.riskLevel === 'critical' ? 'error' : 'warning',
          autoHideDuration: 15000
        });

        onCorrelationAlert?.(alert);
      });

      connection.on('YaraMatchDetected', (match: YaraMatchUpdate) => {
        console.log('🎯 YARA match detected:', match);

        // Update latest matches (keep last 50)
        setLatestYaraMatches(prev => [match, ...prev.slice(0, 49)]);

        // Always notify for YARA matches
        notify(`🎯 YARA Match: ${match.ruleName} in ${match.fileName}`, {
          type: match.severity === 'critical' || match.severity === 'high' ? 'error' : 'warning',
          autoHideDuration: 15000
        });

        onYaraMatch?.(match);
      });

      connection.on('JoinedSecurityEvents', (response) => {
        console.log('✅ Joined security events updates:', response);
        setIsConnected(true);
      });

      connection.on('JoinedCorrelationAlerts', (response) => {
        console.log('✅ Joined correlation alerts:', response);
      });

      connection.onclose(() => {
        console.log('🔌 Security Events SignalR disconnected');
        setConnectionState('Disconnected');
        setIsConnected(false);
        onDisconnect?.();
      });

      connection.onreconnected(() => {
        console.log('🔄 Security Events SignalR reconnected');
        setConnectionState('Connected');
        setIsConnected(true);
        joinGroups();
      });

      connection.onreconnecting(() => {
        console.log('🔄 Security Events SignalR reconnecting...');
        setConnectionState('Reconnecting');
        setIsConnected(false);
      });

      // Start the connection
      await connection.start();
      connectionRef.current = connection;
      setConnectionState('Connected');

      console.log('✅ Security Events SignalR connected');

      // Join groups
      await joinGroups();

      onConnect?.();

    } catch (error) {
      console.error('❌ Security Events SignalR connection failed:', error);
      setConnectionState('Disconnected');
      setIsConnected(false);
      onError?.(error as Error);

      // Retry after 10 seconds
      setTimeout(connect, 10000);
    }
  }, [enabled, notify, onSecurityEvent, onCorrelationAlert, onYaraMatch, onConnect, onDisconnect, onError]);

  const joinGroups = useCallback(async () => {
    if (!connectionRef.current || connectionRef.current.state !== 'Connected') {
      return;
    }

    try {
      // Join security events group
      await connectionRef.current.invoke('JoinSecurityEvents');
      console.log('✅ Joined security events group');

      // Join correlation alerts group
      await connectionRef.current.invoke('JoinCorrelationAlerts');
      console.log('✅ Joined correlation alerts group');

    } catch (error) {
      console.error('❌ Failed to join SignalR groups:', error);
    }
  }, []);

  const disconnect = useCallback(async () => {
    if (connectionRef.current) {
      try {
        await connectionRef.current.invoke('LeaveSecurityEvents');
        await connectionRef.current.invoke('LeaveCorrelationAlerts');
        await connectionRef.current.stop();
        connectionRef.current = null;
        setConnectionState('Disconnected');
        setIsConnected(false);
      } catch (error) {
        console.error('Error disconnecting:', error);
      }
    }
  }, []);

  // Auto-connect when enabled
  useEffect(() => {
    if (enabled) {
      connect();
    }

    return () => {
      disconnect();
    };
  }, [enabled, connect, disconnect]);

  return {
    connectionState,
    isConnected,
    latestSecurityEvents,
    latestCorrelationAlerts,
    latestYaraMatches,
    connect,
    disconnect
  };
};