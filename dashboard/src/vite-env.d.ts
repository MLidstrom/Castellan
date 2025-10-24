/// <reference types="vite/client" />

/**
 * Type definitions for Vite environment variables
 *
 * All custom environment variables must be prefixed with VITE_
 * to be exposed to the client-side code.
 */
interface ImportMetaEnv {
  /**
   * Base URL for API requests
   * @default '/api' (development), '/api' (production)
   * @example 'https://api.yourdomain.com/api'
   */
  readonly VITE_API_URL?: string;

  /**
   * Base URL for SignalR hub connections
   * @default 'http://localhost:5000/hubs' (development), window.location.origin + '/hubs' (production)
   * @example 'https://api.yourdomain.com/hubs'
   */
  readonly VITE_SIGNALR_URL?: string;

  /**
   * SignalR hub path/name
   * @default 'scan-progress'
   */
  readonly VITE_SIGNALR_HUB?: string;

  /**
   * Development mode flag
   * @default true in dev, false in production
   */
  readonly DEV: boolean;

  /**
   * Production mode flag
   * @default false in dev, true in production
   */
  readonly PROD: boolean;

  /**
   * Build mode
   * @default 'development' or 'production'
   */
  readonly MODE: string;

  /**
   * Base URL for the application
   * @default '/' (Vite sets this automatically)
   */
  readonly BASE_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
