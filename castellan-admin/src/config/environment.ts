// Environment configuration for different deployment stages

interface EnvironmentConfig {
  apiUrl: string;
  wsUrl: string;
  environment: 'development' | 'staging' | 'production';
  enableDevTools: boolean;
  enableMockData: boolean;
  logLevel: 'debug' | 'info' | 'warn' | 'error';
  edition: {
    defaultEdition: 'Free' | 'Pro';
    showProFeatures: boolean;
    enableEditionUpgradePrompts: boolean;
  };
  features: {
    realTimeUpdates: boolean;
    advancedSearch: boolean;
    fileUploads: boolean;
    notifications: boolean;
    dashboardAnalytics: boolean;
  };
  auth: {
    tokenRefreshInterval: number; // minutes
    maxRetryAttempts: number;
    sessionTimeout: number; // minutes
  };
  api: {
    timeout: number; // milliseconds
    retryAttempts: number;
    retryDelay: number; // milliseconds
  };
}

// Default configuration
const defaultConfig: EnvironmentConfig = {
  apiUrl: 'http://localhost:5000/api',
  wsUrl: 'ws://localhost:5000/ws',
  environment: 'development',
  enableDevTools: true,
  enableMockData: false,
  logLevel: 'debug',
  edition: {
    defaultEdition: 'Free', // Default to Free Edition for CastellanFree
    showProFeatures: true, // Show Pro features as disabled with upgrade prompts
    enableEditionUpgradePrompts: true, // Enable upgrade prompts
  },
  features: {
    realTimeUpdates: true,
    advancedSearch: true,
    fileUploads: true,
    notifications: true,
    dashboardAnalytics: true,
  },
  auth: {
    tokenRefreshInterval: 15, // Refresh token every 15 minutes
    maxRetryAttempts: 3,
    sessionTimeout: 480, // 8 hours
  },
  api: {
    timeout: 30000, // 30 seconds
    retryAttempts: 3,
    retryDelay: 1000, // 1 second
  },
};

// Environment-specific configurations
const configurations: Record<string, Partial<EnvironmentConfig>> = {
  development: {
    apiUrl: 'http://localhost:5000/api',
    wsUrl: 'ws://localhost:5000/ws',
    environment: 'development',
    enableDevTools: true,
    enableMockData: false, // Now using real API in Phase 5
    logLevel: 'debug',
  },
  
  staging: {
    apiUrl: 'https://staging-api.castellanpro.security/api',
    wsUrl: 'wss://staging-api.castellanpro.security/ws',
    environment: 'staging',
    enableDevTools: true,
    enableMockData: false,
    logLevel: 'info',
    api: {
      timeout: 45000, // Longer timeout for staging
      retryAttempts: 5,
      retryDelay: 2000,
    },
  },
  
  production: {
    apiUrl: 'https://api.castellanpro.security/api',
    wsUrl: 'wss://api.castellanpro.security/ws',
    environment: 'production',
    enableDevTools: false,
    enableMockData: false,
    logLevel: 'warn',
    auth: {
      tokenRefreshInterval: 10, // More frequent refresh in production
      maxRetryAttempts: 5,
      sessionTimeout: 240, // 4 hours for production
    },
    api: {
      timeout: 60000, // 1 minute for production
      retryAttempts: 5,
      retryDelay: 3000,
    },
  },
};

// Get environment from various sources
const getEnvironment = (): string => {
  // Check environment variable
  if (process.env.REACT_APP_ENVIRONMENT) {
    return process.env.REACT_APP_ENVIRONMENT;
  }
  
  // Check NODE_ENV
  if (process.env.NODE_ENV === 'production') {
    return 'production';
  }
  
  if (process.env.NODE_ENV === 'test') {
    return 'staging';
  }
  
  // Default to development
  return 'development';
};

// Create final configuration by merging default with environment-specific
const createConfig = (): EnvironmentConfig => {
  const env = getEnvironment();
  const envConfig = configurations[env] || {};
  
  // Deep merge configurations
  const config: EnvironmentConfig = {
    ...defaultConfig,
    ...envConfig,
    edition: {
      ...defaultConfig.edition,
      ...envConfig.edition,
    },
    features: {
      ...defaultConfig.features,
      ...envConfig.features,
    },
    auth: {
      ...defaultConfig.auth,
      ...envConfig.auth,
    },
    api: {
      ...defaultConfig.api,
      ...envConfig.api,
    },
  };
  
  // Override with environment variables if they exist
  if (process.env.REACT_APP_CASTELLANPRO_API_URL) {
    config.apiUrl = process.env.REACT_APP_CASTELLANPRO_API_URL;
  }
  
  if (process.env.REACT_APP_WS_URL) {
    config.wsUrl = process.env.REACT_APP_WS_URL;
  }
  
  return config;
};

// Export the configuration
export const config = createConfig();

// Helper functions for feature flags
export const isFeatureEnabled = (feature: keyof EnvironmentConfig['features']): boolean => {
  return config.features[feature];
};

export const isDevelopment = (): boolean => {
  return config.environment === 'development';
};

export const isProduction = (): boolean => {
  return config.environment === 'production';
};

export const isStaging = (): boolean => {
  return config.environment === 'staging';
};

// Logger utility based on log level
export const logger = {
  debug: (...args: any[]) => {
    if (['debug'].includes(config.logLevel)) {
      console.debug('[CASTELLANPRO]', ...args);
    }
  },
  
  info: (...args: any[]) => {
    if (['debug', 'info'].includes(config.logLevel)) {
      console.info('[CASTELLANPRO]', ...args);
    }
  },
  
  warn: (...args: any[]) => {
    if (['debug', 'info', 'warn'].includes(config.logLevel)) {
      console.warn('[CASTELLANPRO]', ...args);
    }
  },
  
  error: (...args: any[]) => {
    console.error('[CASTELLANPRO]', ...args);
  },
};

// API health check utility
export const checkApiHealth = async (): Promise<{
  healthy: boolean;
  apiUrl: string;
  wsUrl: string;
  latency?: number;
  error?: string;
}> => {
  const startTime = Date.now();
  
  try {
    const response = await fetch(`${config.apiUrl}/health`, {
      method: 'GET',
    });
    
    const latency = Date.now() - startTime;
    
    if (response.ok) {
      return {
        healthy: true,
        apiUrl: config.apiUrl,
        wsUrl: config.wsUrl,
        latency,
      };
    } else {
      return {
        healthy: false,
        apiUrl: config.apiUrl,
        wsUrl: config.wsUrl,
        latency,
        error: `HTTP ${response.status}: ${response.statusText}`,
      };
    }
  } catch (error) {
    const latency = Date.now() - startTime;
    
    return {
      healthy: false,
      apiUrl: config.apiUrl,
      wsUrl: config.wsUrl,
      latency,
      error: error instanceof Error ? error.message : 'Unknown error',
    };
  }
};

// Configuration validation
export const validateConfiguration = (): {
  valid: boolean;
  errors: string[];
  warnings: string[];
} => {
  const errors: string[] = [];
  const warnings: string[] = [];
  
  // Validate required URLs
  if (!config.apiUrl) {
    errors.push('API URL is required');
  } else if (!config.apiUrl.startsWith('http')) {
    errors.push('API URL must start with http:// or https://');
  }
  
  if (!config.wsUrl) {
    errors.push('WebSocket URL is required');
  } else if (!config.wsUrl.startsWith('ws')) {
    errors.push('WebSocket URL must start with ws:// or wss://');
  }
  
  // Validate timeouts
  if (config.api.timeout <= 0) {
    errors.push('API timeout must be positive');
  }
  
  if (config.auth.sessionTimeout <= 0) {
    errors.push('Session timeout must be positive');
  }
  
  // Production warnings
  if (config.environment === 'production') {
    if (config.enableDevTools) {
      warnings.push('Development tools should be disabled in production');
    }
    
    if (config.logLevel === 'debug') {
      warnings.push('Debug logging should be disabled in production');
    }
    
    if (config.apiUrl.startsWith('http://')) {
      warnings.push('Production API should use HTTPS');
    }
    
    if (config.wsUrl.startsWith('ws://')) {
      warnings.push('Production WebSocket should use WSS');
    }
  }
  
  return {
    valid: errors.length === 0,
    errors,
    warnings,
  };
};

// Log configuration on startup
logger.info('Configuration loaded:', {
  environment: config.environment,
  apiUrl: config.apiUrl,
  wsUrl: config.wsUrl,
  features: config.features,
});

// Validate configuration and log any issues
const validation = validateConfiguration();
if (validation.errors.length > 0) {
  logger.error('Configuration errors:', validation.errors);
}
if (validation.warnings.length > 0) {
  logger.warn('Configuration warnings:', validation.warnings);
}

export default config;