import { useCallback, useEffect, useState } from 'react';
import { AuthService, AuthTokens } from '../services/auth';

export function useAuth() {
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setToken(AuthService.getToken());
    setLoading(false);
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<AuthTokens> => {
    const t = await AuthService.login(username, password);
    setToken(t.accessToken);
    return t;
  }, []);

  const logout = useCallback(() => {
    AuthService.logout();
    setToken(null);
  }, []);

  return { token, loading, login, logout, isAuthenticated: !!token };
}


