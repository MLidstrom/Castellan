import { createContext, useContext, useEffect, useState, useMemo, useRef, ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { SignalRService } from '../services/signalr';
import { useAuth } from '../hooks/useAuth';

interface SignalRContextType {
  hub: SignalRService | null;
  isConnected: boolean;
}

const SignalRContext = createContext<SignalRContextType>({
  hub: null,
  isConnected: false,
});

export function SignalRProvider({ children }: { children: ReactNode }) {
  const { token, loading } = useAuth();
  const navigate = useNavigate();

  const hub = useMemo(() => new SignalRService(), [token]);

  const [isConnected, setIsConnected] = useState(false);
  const navigateRef = useRef(navigate);
  useEffect(() => {
    navigateRef.current = navigate;
  }, [navigate]);

  useEffect(() => {
    if (loading) {
      return;
    }

    if (!token) {
      setIsConnected(false);
      navigateRef.current('/login', { replace: true });
      return;
    }

    let mounted = true;
    let retryCount = 0;
    let retryTimeout: NodeJS.Timeout | null = null;
    let retryIntervals = [0, 2000, 5000]; // Default intervals

    // Fetch SignalR configuration from backend
    const fetchConfig = async () => {
      try {
        const response = await fetch('/api/config/signalr');
        if (response.ok) {
          const config = await response.json();
          if (config.retryIntervalsMs && Array.isArray(config.retryIntervalsMs)) {
            retryIntervals = config.retryIntervalsMs;
            hub.setRetryIntervals(retryIntervals); // Update hub with intervals
          }
        }
      } catch (error) {
        console.warn('[SignalR] Could not fetch config, using defaults', error);
      }
    };

    const connect = async () => {
      try {
        await hub.start();
        if (mounted) {
          console.log('[SignalR] Connected');
          setIsConnected(true);
          retryCount = 0; // Reset retry count on successful connection
        }
      } catch (e) {
        console.warn('[SignalR] Connection failed', e);
        if (mounted) {
          setIsConnected(false);
          const error = e as any;
          if (error?.statusCode === 401 || error?.statusCode === 403) {
            navigateRef.current('/login', { replace: true });
            return; // Don't retry on auth errors
          }

          // Calculate retry delay using configured intervals
          const delay = retryCount < retryIntervals.length
            ? retryIntervals[retryCount]
            : retryIntervals[retryIntervals.length - 1];
          retryCount++;

          // Retry connection after delay
          retryTimeout = setTimeout(() => {
            if (mounted) {
              connect();
            }
          }, delay);
        }
      }
    };

    // Fetch config then connect
    fetchConfig().then(() => connect());

    const onReconnected = () => {
      if (mounted) {
        console.log('[SignalR] Reconnected');
        setIsConnected(true);
      }
    };

    const onReconnecting = () => {
      if (mounted) {
        setIsConnected(false);
      }
    };

    const onClose = () => {
      if (mounted) {
        setIsConnected(false);
      }
    };

    hub.onReconnected(onReconnected);
    hub.onReconnecting(onReconnecting);
    hub.onClose(onClose);

    // Verify connection state every 10 seconds
    const interval = setInterval(() => {
      if (mounted) {
        const state = hub.getConnectionState();
        const connected = state === 'Connected';
        setIsConnected(connected);
      }
    }, 10000); // Increased from 5000ms to 10000ms

    return () => {
      mounted = false;
      if (retryTimeout) clearTimeout(retryTimeout);
      clearInterval(interval);
      hub.stop().catch(() => undefined);
    };
  }, [token, loading, hub]);

  return (
    <SignalRContext.Provider value={{ hub, isConnected }}>
      {children}
    </SignalRContext.Provider>
  );
}

export function useSignalR() {
  return useContext(SignalRContext);
}
