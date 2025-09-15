import { AuthProvider } from 'react-admin';

// API Configuration
const API_URL = process.env.REACT_APP_CASTELLAN_API_URL || 'http://localhost:5000/api';

// JWT Token Management
interface TokenData {
  token: string;
  refreshToken: string;
  expiresAt: number;
  user: {
    id: string;
    username: string;
    email: string;
    roles: string[];
    permissions: string[];
    profile?: {
      firstName?: string;
      lastName?: string;
      avatar?: string;
    };
  };
}

class TokenManager {
  private static readonly TOKEN_KEY = 'auth_token';
  private static readonly REFRESH_TOKEN_KEY = 'refresh_token';
  private static readonly USER_KEY = 'user_data';
  private static readonly PERMISSIONS_KEY = 'user_permissions';
  private static readonly EXPIRES_KEY = 'token_expires';

  static saveTokenData(tokenData: TokenData) {
    localStorage.setItem(this.TOKEN_KEY, tokenData.token);
    localStorage.setItem(this.REFRESH_TOKEN_KEY, tokenData.refreshToken);
    localStorage.setItem(this.USER_KEY, JSON.stringify(tokenData.user));
    localStorage.setItem(this.PERMISSIONS_KEY, JSON.stringify(tokenData.user.permissions));
    localStorage.setItem(this.EXPIRES_KEY, tokenData.expiresAt.toString());
  }

  static getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  static getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  static getUser() {
    const userData = localStorage.getItem(this.USER_KEY);
    return userData ? JSON.parse(userData) : null;
  }

  static getPermissions(): string[] {
    const permissions = localStorage.getItem(this.PERMISSIONS_KEY);
    return permissions ? JSON.parse(permissions) : [];
  }

  static isTokenExpired(): boolean {
    const expiresAt = localStorage.getItem(this.EXPIRES_KEY);
    if (!expiresAt) return true;
    
    const now = Date.now();
    const expires = parseInt(expiresAt, 10);
    
    // Check if token expires in the next 30 seconds (30000ms)
    return now >= (expires - 30000);
  }

  static clearTokenData() {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem(this.PERMISSIONS_KEY);
    localStorage.removeItem(this.EXPIRES_KEY);
  }

  static async refreshTokenIfNeeded(): Promise<boolean> {
    if (!this.isTokenExpired()) {
      return true;
    }

    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      return false;
    }

    try {
      const response = await fetch(`${API_URL}/auth/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ refreshToken }),
      });

      if (!response.ok) {
        throw new Error('Token refresh failed');
      }

      const tokenData = await response.json();
      this.saveTokenData(tokenData);
      return true;
    } catch (error) {
      console.error('Token refresh failed:', error);
      this.clearTokenData();
      return false;
    }
  }
}

// Authentication Provider
export const authProvider: AuthProvider = {
  // Login method
  login: async ({ username, password }) => {
    try {
      const response = await fetch(`${API_URL}/auth/login`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ username, password }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        const message = errorData.message || 'Login failed';
        
        if (response.status === 401) {
          throw new Error('Invalid credentials');
        } else if (response.status === 403) {
          throw new Error('Account disabled or insufficient permissions');
        } else if (response.status === 429) {
          throw new Error('Too many login attempts. Please try again later');
        }
        
        throw new Error(message);
      }

      const tokenData = await response.json();
      
      // Validate token data structure
      if (!tokenData.token || !tokenData.user) {
        throw new Error('Invalid response from authentication server');
      }

      // Save token data
      TokenManager.saveTokenData(tokenData);
      
      return Promise.resolve();
    } catch (error) {
      console.error('Login error:', error);
      return Promise.reject(error);
    }
  },

  // Logout method
  logout: async () => {
    try {
      const token = TokenManager.getToken();
      
      if (token) {
        // Notify server about logout (optional - fire and forget)
        fetch(`${API_URL}/auth/logout`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
          },
        }).catch(() => {
          // Ignore logout endpoint errors
        });
      }

      // Clear local storage
      TokenManager.clearTokenData();
      
      return Promise.resolve();
    } catch (error) {
      console.error('Logout error:', error);
      // Always clear tokens on logout, even if server request fails
      TokenManager.clearTokenData();
      return Promise.resolve();
    }
  },

  // Check authentication status
  checkAuth: async () => {
    const token = TokenManager.getToken();

    if (!token) {
      // Silently reject without error message for login page
      return Promise.reject();
    }

    // Try to refresh token if needed
    const tokenValid = await TokenManager.refreshTokenIfNeeded();

    if (!tokenValid) {
      return Promise.reject(new Error('Session expired'));
    }

    return Promise.resolve();
  },

  // Check error for authentication issues
  checkError: (error) => {
    const status = error.status;

    if (status === 401 || status === 403) {
      TokenManager.clearTokenData();
      // Silently redirect to login without error message
      return Promise.reject();
    }

    // Check for network errors that might indicate server issues
    if (status === 0 || !status) {
      // Don't logout on network errors, just show error
      return Promise.resolve();
    }

    return Promise.resolve();
  },

  // Get user permissions
  getPermissions: async () => {
    const permissions = TokenManager.getPermissions();
    const user = TokenManager.getUser();
    
    if (!permissions || !user) {
      return Promise.reject(new Error('No permissions found'));
    }

    // Refresh token if needed before returning permissions
    const tokenValid = await TokenManager.refreshTokenIfNeeded();
    
    if (!tokenValid) {
      return Promise.reject(new Error('Token expired'));
    }

    // Return permissions with roles for backward compatibility
    return Promise.resolve({
      permissions,
      roles: user.roles,
      user
    });
  },

  // Get user identity
  getIdentity: async () => {
    const user = TokenManager.getUser();
    
    if (!user) {
      return Promise.reject(new Error('No user data found'));
    }

    // Refresh token if needed
    const tokenValid = await TokenManager.refreshTokenIfNeeded();
    
    if (!tokenValid) {
      return Promise.reject(new Error('Token expired'));
    }

    return Promise.resolve({
      id: user.id,
      fullName: user.profile?.firstName && user.profile?.lastName 
        ? `${user.profile.firstName} ${user.profile.lastName}`
        : '',
      avatar: user.profile?.avatar,
      email: user.email,
      username: user.username,
      roles: user.roles,
      permissions: user.permissions
    });
  },
};

// Enhanced authentication provider with additional methods
export const enhancedAuthProvider = {
  ...authProvider,

  // Change password
  changePassword: async (currentPassword: string, newPassword: string) => {
    const token = TokenManager.getToken();
    
    if (!token) {
      throw new Error('Not authenticated');
    }

    try {
      const response = await fetch(`${API_URL}/auth/change-password`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          currentPassword,
          newPassword,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.message || 'Password change failed');
      }

      return await response.json();
    } catch (error) {
      console.error('Change password error:', error);
      throw error;
    }
  },

  // Update user profile
  updateProfile: async (profileData: any) => {
    const token = TokenManager.getToken();
    
    if (!token) {
      throw new Error('Not authenticated');
    }

    try {
      const response = await fetch(`${API_URL}/auth/profile`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(profileData),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.message || 'Profile update failed');
      }

      const updatedUser = await response.json();
      
      // Update local storage with new user data
      const currentUser = TokenManager.getUser();
      if (currentUser) {
        const updatedUserData = { ...currentUser, ...updatedUser };
        localStorage.setItem('user_data', JSON.stringify(updatedUserData));
      }

      return updatedUser;
    } catch (error) {
      console.error('Update profile error:', error);
      throw error;
    }
  },

  // Request password reset
  requestPasswordReset: async (email: string) => {
    try {
      const response = await fetch(`${API_URL}/auth/request-password-reset`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ email }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.message || 'Password reset request failed');
      }

      return await response.json();
    } catch (error) {
      console.error('Password reset request error:', error);
      throw error;
    }
  },

  // Reset password with token
  resetPassword: async (token: string, newPassword: string) => {
    try {
      const response = await fetch(`${API_URL}/auth/reset-password`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          token,
          newPassword,
        }),
      });

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        throw new Error(errorData.message || 'Password reset failed');
      }

      return await response.json();
    } catch (error) {
      console.error('Password reset error:', error);
      throw error;
    }
  },

  // Check if user has specific permission
  hasPermission: (permission: string): boolean => {
    const permissions = TokenManager.getPermissions();
    return permissions.includes(permission);
  },

  // Check if user has specific role
  hasRole: (role: string): boolean => {
    const user = TokenManager.getUser();
    return user?.roles?.includes(role) || false;
  },

  // Get current user data
  getCurrentUser: () => {
    return TokenManager.getUser();
  },

  // Manual token refresh
  refreshToken: async () => {
    return await TokenManager.refreshTokenIfNeeded();
  },

  // Check if user is authenticated
  isAuthenticated: (): boolean => {
    const token = TokenManager.getToken();
    return !!token && !TokenManager.isTokenExpired();
  },
};

export default enhancedAuthProvider;