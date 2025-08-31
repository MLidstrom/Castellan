import { useEffect, useRef, useCallback } from 'react';
import { useDataProvider, useNotify } from 'react-admin';

interface RealtimeMessage {
  type: 'update' | 'create' | 'delete' | 'notification';
  resource: string;
  id?: string | number;
  data?: any;
  message?: string;
  severity?: 'info' | 'warning' | 'error' | 'success';
}

interface UseRealtimeOptions {
  enabled?: boolean;
  onMessage?: (message: RealtimeMessage) => void;
  onConnect?: () => void;
  onDisconnect?: () => void;
  onError?: (error: Event) => void;
}

export const useRealtime = (options: UseRealtimeOptions = {}) => {
  const {
    enabled = true,
    onMessage,
    onConnect,
    onDisconnect,
    onError
  } = options;

  const dataProvider = useDataProvider();
  const notify = useNotify();
  const wsRef = useRef<WebSocket | null>(null);
  const reconnectTimeoutRef = useRef<NodeJS.Timeout | null>(null);
  const reconnectAttempts = useRef(0);
  const maxReconnectAttempts = 5;

  const connect = useCallback(() => {
    if (!enabled || wsRef.current?.readyState === WebSocket.OPEN) {
      return;
    }

    // Use environment variable or fallback to development URL
    const wsUrl = process.env.REACT_APP_WS_URL || 'ws://localhost:5000/ws';
    console.log('Connecting to WebSocket:', wsUrl);
    
    try {
      wsRef.current = new WebSocket(wsUrl);

      wsRef.current.onopen = () => {
        console.log('WebSocket connected');
        reconnectAttempts.current = 0;
        onConnect?.();
        
        // Send authentication if token exists
        const token = localStorage.getItem('auth_token');
        if (token && wsRef.current) {
          wsRef.current.send(JSON.stringify({
            type: 'auth',
            token
          }));
        }
      };

      wsRef.current.onmessage = (event) => {
        try {
          const message: RealtimeMessage = JSON.parse(event.data);
          
          // Handle different message types
          switch (message.type) {
            case 'notification':
              if (message.message) {
                notify(message.message, { 
                  type: message.severity || 'info',
                  autoHideDuration: 6000
                });
              }
              break;
              
            case 'update':
            case 'create':
            case 'delete':
              // Trigger data provider refresh for affected resource
              if (message.resource) {
                // This would trigger a refetch in components using this resource
                console.log(`Resource ${message.resource} ${message.type}:`, message.data);
              }
              break;
          }
          
          // Call custom message handler
          onMessage?.(message);
          
        } catch (error) {
          console.error('Failed to parse WebSocket message:', error);
        }
      };

      wsRef.current.onclose = () => {
        console.log('WebSocket disconnected');
        onDisconnect?.();
        
        // Attempt to reconnect
        if (enabled && reconnectAttempts.current < maxReconnectAttempts) {
          const timeout = Math.min(1000 * Math.pow(2, reconnectAttempts.current), 30000);
          reconnectTimeoutRef.current = setTimeout(() => {
            reconnectAttempts.current++;
            connect();
          }, timeout);
        }
      };

      wsRef.current.onerror = (error) => {
        console.error('WebSocket error:', error);
        onError?.(error);
      };

    } catch (error) {
      console.error('Failed to create WebSocket connection:', error);
    }
  }, [enabled, onConnect, onDisconnect, onError, onMessage, notify]);

  const disconnect = useCallback(() => {
    if (reconnectTimeoutRef.current) {
      clearTimeout(reconnectTimeoutRef.current);
      reconnectTimeoutRef.current = null;
    }
    
    if (wsRef.current) {
      wsRef.current.close();
      wsRef.current = null;
    }
  }, []);

  const sendMessage = useCallback((message: any) => {
    if (wsRef.current?.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify(message));
    }
  }, []);

  useEffect(() => {
    if (enabled) {
      connect();
    }

    return () => {
      disconnect();
    };
  }, [enabled, connect, disconnect]);

  return {
    isConnected: wsRef.current?.readyState === WebSocket.OPEN,
    connect,
    disconnect,
    sendMessage
  };
};

// Hook for resource-specific real-time updates
export const useRealtimeResource = (resource: string, callback?: () => void) => {
  return useRealtime({
    onMessage: (message) => {
      if (message.resource === resource) {
        callback?.();
      }
    }
  });
};