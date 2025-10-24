import { useCallback, useEffect, useState } from 'react';
import { AuthService, AuthTokens } from '../services/auth';
import { AUTH_STORAGE_KEY } from '../services/constants';

export function useAuth() {
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  // ✅ FIX 5.1: Tab synchronization for auth state
  useEffect(() => {
    // Initialize token from storage
    setToken(AuthService.getToken());
    setLoading(false);

    // Listen for storage changes in other tabs
    const handleStorageChange = (e: StorageEvent) => {
      // Only respond to changes to the auth token key
      if (e.key === AUTH_STORAGE_KEY) {
        const newToken = e.newValue;
        setToken(newToken);

        if (import.meta.env.DEV) {
          console.log('[useAuth] Storage event detected:', {
            oldValue: e.oldValue?.substring(0, 10) + '...',
            newValue: newToken?.substring(0, 10) + '...',
            url: e.url
          });
        }
      }
    };

    // Add storage event listener for cross-tab sync
    window.addEventListener('storage', handleStorageChange);

    // Cleanup on unmount
    return () => {
      window.removeEventListener('storage', handleStorageChange);
    };
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<AuthTokens> => {
    const t = await AuthService.login(username, password);
    setToken(t.accessToken);

    // Trigger storage event for other tabs (manual dispatch since storage event doesn't fire in same tab)
    window.dispatchEvent(new StorageEvent('storage', {
      key: AUTH_STORAGE_KEY,
      newValue: t.accessToken,
      oldValue: null,
      storageArea: localStorage,
      url: window.location.href
    }));

    return t;
  }, []);

  const logout = useCallback(() => {
    const oldToken = AuthService.getToken();
    AuthService.logout();
    setToken(null);

    // Trigger storage event for other tabs
    window.dispatchEvent(new StorageEvent('storage', {
      key: AUTH_STORAGE_KEY,
      newValue: null,
      oldValue: oldToken,
      storageArea: localStorage,
      url: window.location.href
    }));
  }, []);

  return { token, loading, login, logout, isAuthenticated: !!token };
}


