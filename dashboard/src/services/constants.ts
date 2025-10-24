/**
 * ✅ FIX 6.1: Configuration Constants with Proper Typing and Production Checks
 *
 * This file centralizes all configuration values with:
 * - Type-safe environment variable access
 * - Development/Production environment detection
 * - Sensible defaults with validation
 * - Clear documentation for each constant
 */

/**
 * Environment Detection
 */
export const isDev = import.meta.env.DEV;
export const isProd = import.meta.env.PROD;
export const mode = import.meta.env.MODE; // 'development' | 'production'

/**
 * API Configuration
 *
 * Development: Uses Vite proxy (/api -> http://localhost:5000/api)
 * Production: Uses environment variable or falls back to same-origin
 */
export const API_URL = import.meta.env.VITE_API_URL || (isDev ? '/api' : '/api');

/**
 * SignalR Configuration
 *
 * SignalR WebSocket connections cannot use Vite proxy, so we need full URLs.
 * Development: http://localhost:5000/hubs
 * Production: Environment variable or same-origin with /hubs path
 */
export const SIGNALR_BASE_URL = import.meta.env.VITE_SIGNALR_URL || (isDev ? 'http://localhost:5000/hubs' : `${window.location.origin}/hubs`);

/**
 * SignalR Hub Path
 *
 * The specific hub endpoint to connect to.
 * Default: 'scan-progress' (used for threat scanner updates)
 */
export const SIGNALR_HUB_PATH = import.meta.env.VITE_SIGNALR_HUB || 'scan-progress';

/**
 * Authentication Storage Key
 *
 * LocalStorage key for storing JWT authentication token.
 * Warning: Changing this will log out all users.
 */
export const AUTH_STORAGE_KEY = 'auth_token';

/**
 * Application Metadata
 */
export const APP_NAME = 'CastellanAI Dashboard';
export const APP_VERSION = '1.0.0';

/**
 * Feature Flags
 *
 * Enable/disable features based on environment or configuration.
 */
export const FEATURES = {
  // Enable debug logging in development
  debugLogging: isDev,

  // Enable React DevTools profiler in development
  reactProfiler: isDev,

  // Enable CSRF token validation (backend must support)
  csrfProtection: true,

  // Enable tab synchronization for auth state
  tabSync: true,
} as const;

/**
 * Performance Configuration
 */
export const PERFORMANCE = {
  // Query stale time (5 minutes)
  queryStaleTime: 5 * 60 * 1000,

  // Query cache time (30 minutes)
  queryCacheTime: 30 * 60 * 1000,

  // Auto-refetch interval (30 seconds)
  queryRefetchInterval: 30 * 1000,

  // SignalR reconnect interval (10 seconds)
  signalrReconnectInterval: 10 * 1000,

  // SignalR connection timeout (30 seconds)
  signalrConnectionTimeout: 30 * 1000,
} as const;

/**
 * Validation
 *
 * Validates critical configuration at startup.
 * Logs warnings for missing optional configuration.
 */
if (isProd) {
  if (!import.meta.env.VITE_API_URL) {
    console.warn('[Config] VITE_API_URL not set, using same-origin (/api)');
  }

  if (!import.meta.env.VITE_SIGNALR_URL) {
    console.warn('[Config] VITE_SIGNALR_URL not set, using same-origin (/hubs)');
  }
}

if (isDev) {
  console.log('[Config] Environment:', {
    mode,
    API_URL,
    SIGNALR_BASE_URL,
    SIGNALR_HUB_PATH,
    features: FEATURES,
  });
}


