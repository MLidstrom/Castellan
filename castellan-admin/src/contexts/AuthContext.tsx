import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

interface User {
  id: string;
  username: string;
  email: string;
  roles: string[];
  permissions: string[];
  profile: {
    firstName: string;
    lastName: string;
    avatar?: string;
  };
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  refreshToken: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Check for existing token on mount
    const savedToken = localStorage.getItem('auth_token');
    if (savedToken) {
      setToken(savedToken);
      // Validate token by making a test request
      validateToken(savedToken);
    } else {
      setIsLoading(false);
    }
  }, []);

  const validateToken = async (tokenToValidate: string) => {
    try {
      const response = await fetch('/api/system-status', {
        headers: {
          'Authorization': `Bearer ${tokenToValidate}`,
          'Content-Type': 'application/json'
        }
      });

      if (response.ok) {
        // Token is valid - decode user info from JWT
        const payload = JSON.parse(atob(tokenToValidate.split('.')[1]));
        const userData: User = {
          id: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || '1',
          username: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || 'admin',
          email: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '',
          roles: [payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']] || [],
          permissions: (payload['permissions'] || '').split(','),
          profile: {
            firstName: 'Castellan',
            lastName: 'Administrator'
          }
        };
        setUser(userData);
        console.log('✅ Token validated, user authenticated:', userData.username);
      } else {
        // Token is invalid
        console.warn('❌ Token validation failed, clearing auth');
        localStorage.removeItem('auth_token');
        setToken(null);
        setUser(null);
      }
    } catch (error) {
      console.error('❌ Token validation error:', error);
      localStorage.removeItem('auth_token');
      setToken(null);
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  };

  const login = async (username: string, password: string) => {
    setIsLoading(true);
    try {
      console.log('🔐 Attempting login for:', username);
      
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ username, password }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.message || `Login failed: ${response.status} ${response.statusText}`);
      }

      const data = await response.json();
      const newToken = data.token;
      
      // Store token
      localStorage.setItem('auth_token', newToken);
      setToken(newToken);
      
      // Set user data
      setUser(data.user);
      
      console.log('✅ Login successful for:', data.user.username);
    } catch (error) {
      console.error('❌ Login failed:', error);
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const logout = () => {
    console.log('🚪 Logging out user:', user?.username);
    localStorage.removeItem('auth_token');
    setToken(null);
    setUser(null);
    
    // Call logout endpoint to invalidate token server-side
    if (token) {
      fetch('/api/auth/logout', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
      }).catch(err => console.warn('Logout API call failed:', err));
    }
  };

  const refreshToken = async () => {
    // Implementation for refresh token if needed
    console.log('🔄 Token refresh not implemented yet');
  };

  const value: AuthContextType = {
    user,
    token,
    isAuthenticated: !!user && !!token,
    isLoading,
    login,
    logout,
    refreshToken,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
};
