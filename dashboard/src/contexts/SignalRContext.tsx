import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
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
  const [hub] = useState(() => new SignalRService());
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (loading || !token) {
      setIsConnected(false);
      return;
    }

    let mounted = true;

    const connect = async () => {
      try {
        await hub.start();
        if (mounted) {
          setIsConnected(true);
        }
      } catch (e) {
        console.warn('[SignalR] Connection failed', e);
        if (mounted) {
          setIsConnected(false);
        }
      }
    };

    connect();

    // Monitor connection state
    hub.onReconnected(() => {
      if (mounted) {
        console.log('[SignalR] Reconnected');
        setIsConnected(true);
      }
    });

    hub.onReconnecting(() => {
      if (mounted) {
        console.log('[SignalR] Reconnecting...');
        setIsConnected(false);
      }
    });

    hub.onClose(() => {
      if (mounted) {
        console.log('[SignalR] Connection closed');
        setIsConnected(false);
      }
    });

    // Poll connection state as backup
    const interval = setInterval(() => {
      if (mounted) {
        const state = hub.getConnectionState();
        setIsConnected(state === 'Connected');
      }
    }, 5000);

    return () => {
      mounted = false;
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
