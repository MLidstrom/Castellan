// Cache Manager Utility for Dashboard Components
// Provides centralized caching with TTL and localStorage persistence

export interface CacheItem<T> {
  data: T;
  timestamp: number;
  ttl: number; // Time to live in milliseconds
}

export interface CacheConfig {
  ttl?: number; // Default TTL in milliseconds
  useLocalStorage?: boolean;
  keyPrefix?: string;
}

export class CacheManager {
  private memoryCache: Map<string, CacheItem<any>> = new Map();
  private accessTimes: Map<string, number> = new Map(); // Track access for LRU
  private defaultTtl: number;
  private useLocalStorage: boolean;
  private keyPrefix: string;
  private cleanupInterval: NodeJS.Timeout | null = null;

  constructor(config: CacheConfig = {}) {
    this.defaultTtl = config.ttl || 2 * 60 * 1000; // Default 2 minutes
    this.useLocalStorage = config.useLocalStorage ?? false;
    this.keyPrefix = config.keyPrefix || 'castellan_cache_';
    
    // Set up periodic cleanup every 30 seconds
    this.cleanupInterval = setInterval(() => {
      this.cleanup();
    }, 30000);
    
    // Initial cleanup after short delay
    setTimeout(() => this.cleanup(), 100);
  }

  // Store data in cache with size management
  set<T>(key: string, data: T, ttl?: number): void {
    const cacheItem: CacheItem<T> = {
      data,
      timestamp: Date.now(),
      ttl: ttl || this.defaultTtl
    };

    // Check cache size and cleanup if needed
    if (this.memoryCache.size > 100) { // Max 100 items
      this.cleanup();
      console.log('🧹 Cache cleanup triggered - cache size was over 100 items');
    }

  // Store in memory cache
    this.memoryCache.set(key, cacheItem);
    this.accessTimes.set(key, Date.now());

    // Store in localStorage if enabled
    if (this.useLocalStorage) {
      try {
        localStorage.setItem(
          `${this.keyPrefix}${key}`,
          JSON.stringify(cacheItem)
        );
      } catch (error) {
        console.warn('Failed to store in localStorage:', error);
      }
    }
  }

  // Retrieve data from cache
  get<T>(key: string): T | null {
    // First check memory cache (fast path)
    let cacheItem = this.memoryCache.get(key);

    // Fast path: if in memory and not expired, return immediately
    if (cacheItem) {
      const isExpired = Date.now() - cacheItem.timestamp > cacheItem.ttl;
      if (!isExpired) {
        // Update access time for LRU tracking
        this.accessTimes.set(key, Date.now());
        return cacheItem.data;
      } else {
        // Remove expired item from memory and access tracking
        this.memoryCache.delete(key);
        this.accessTimes.delete(key);
        cacheItem = undefined;
      }
    }

    // Only check localStorage if not in memory cache
    if (!cacheItem && this.useLocalStorage) {
      try {
        const stored = localStorage.getItem(`${this.keyPrefix}${key}`);
        if (stored) {
          cacheItem = JSON.parse(stored);
          
          if (cacheItem) {
            const isExpired = Date.now() - cacheItem.timestamp > cacheItem.ttl;
            if (!isExpired) {
              // Restore to memory cache for fast future access
              this.memoryCache.set(key, cacheItem);
              return cacheItem.data;
            } else {
              // Remove expired localStorage item
              localStorage.removeItem(`${this.keyPrefix}${key}`);
            }
          }
        }
      } catch (error) {
        console.warn('Failed to retrieve from localStorage:', error);
        // Try to remove corrupted item
        try {
          localStorage.removeItem(`${this.keyPrefix}${key}`);
        } catch (e) {}
      }
    }

    return null;
  }

  // Remove item from cache
  remove(key: string): void {
    this.memoryCache.delete(key);
    this.accessTimes.delete(key);
    
    if (this.useLocalStorage) {
      try {
        localStorage.removeItem(`${this.keyPrefix}${key}`);
      } catch (error) {
        console.warn('Failed to remove from localStorage:', error);
      }
    }
  }

  // Check if key exists and is not expired
  has(key: string): boolean {
    return this.get(key) !== null;
  }

  // Clear all cache
  clear(): void {
    this.memoryCache.clear();
    this.accessTimes.clear();
    
    if (this.useLocalStorage) {
      try {
        const keys = Object.keys(localStorage).filter(key => 
          key.startsWith(this.keyPrefix)
        );
        keys.forEach(key => localStorage.removeItem(key));
      } catch (error) {
        console.warn('Failed to clear localStorage:', error);
      }
    }
  }

  // Clean up expired items and enforce size limits
  cleanup(): void {
    const now = Date.now();
    let removedCount = 0;
    
    // Clean expired items first
    const entries = Array.from(this.memoryCache.entries());
    for (const [key, item] of entries) {
      if (now - item.timestamp > item.ttl) {
        this.memoryCache.delete(key);
        this.accessTimes.delete(key);
        removedCount++;
      }
    }
    
    // If still too large, remove oldest accessed items (LRU)
    if (this.memoryCache.size > 50) { // Keep max 50 items
      const accessEntries = Array.from(this.accessTimes.entries())
        .sort(([, a], [, b]) => a - b); // Sort by access time (oldest first)
      
      const toRemove = this.memoryCache.size - 30; // Keep only 30 most recent
      for (let i = 0; i < toRemove && i < accessEntries.length; i++) {
        const [key] = accessEntries[i];
        this.memoryCache.delete(key);
        this.accessTimes.delete(key);
        removedCount++;
      }
    }
    
    if (removedCount > 0) {
      console.log(`🧹 Cache cleanup: removed ${removedCount} items, ${this.memoryCache.size} remaining`);
    }

    // Clean localStorage
    if (this.useLocalStorage) {
      try {
        const keys = Object.keys(localStorage).filter(key => 
          key.startsWith(this.keyPrefix)
        );
        
        keys.forEach(key => {
          try {
            const stored = localStorage.getItem(key);
            if (stored) {
              const item: CacheItem<any> = JSON.parse(stored);
              if (now - item.timestamp > item.ttl) {
                localStorage.removeItem(key);
              }
            }
          } catch (error) {
            // Remove corrupted items
            localStorage.removeItem(key);
          }
        });
      } catch (error) {
        console.warn('Failed to cleanup localStorage:', error);
      }
    }
  }

  // Get cache statistics
  getStats(): {
    memoryItems: number;
    localStorageItems: number;
    totalSize: number;
    accessTracking: number;
  } {
    let localStorageItems = 0;
    let totalSize = 0;

    if (this.useLocalStorage) {
      try {
        const keys = Object.keys(localStorage).filter(key => 
          key.startsWith(this.keyPrefix)
        );
        localStorageItems = keys.length;
        
        keys.forEach(key => {
          const item = localStorage.getItem(key);
          if (item) {
            totalSize += item.length;
          }
        });
      } catch (error) {
        console.warn('Failed to get localStorage stats:', error);
      }
    }

  return {
    memoryItems: this.memoryCache.size,
    localStorageItems,
    totalSize,
    accessTracking: this.accessTimes.size
  };
}

// Destroy cache manager and clear intervals
destroy(): void {
  if (this.cleanupInterval) {
    clearInterval(this.cleanupInterval);
    this.cleanupInterval = null;
  }
  this.clear();
}
}

// Create singleton instance with persistent storage
export const dashboardCache = new CacheManager({
  ttl: 2 * 60 * 1000, // 2 minutes default TTL
  useLocalStorage: true, // Enable localStorage for persistence across page refreshes
  keyPrefix: 'castellan_dashboard_'
});

// Cache keys for different dashboard components
export const CACHE_KEYS = {
  SECURITY_EVENTS: 'security_events',
  COMPLIANCE_REPORTS: 'compliance_reports',
  SYSTEM_STATUS: 'system_status',
  THREAT_SCANNER: 'threat_scanner',
  PERFORMANCE_METRICS: 'performance_metrics',
  THREAT_INTELLIGENCE: 'threat_intelligence',
  GEOGRAPHIC_THREATS: 'geographic_threats',
  REALTIME_METRICS: 'realtime_metrics',
  CONNECTION_POOL: 'connection_pool',
  // React Admin resource cache keys
  RA_SECURITY_EVENTS: 'dp_getList_security-events',
  RA_SYSTEM_STATUS: 'dp_getList_system-status',
  RA_COMPLIANCE_REPORTS: 'dp_getList_compliance-reports',
  RA_THREAT_SCANNER: 'dp_getList_threat-scanner',
  RA_MITRE_TECHNIQUES: 'dp_getList_mitre-techniques',
  RA_NOTIFICATION_SETTINGS: 'dp_getList_notification-settings',
  RA_CONFIGURATION: 'dp_getList_configuration'
} as const;

// Cache TTL configurations - optimized for better cache hit rates
export const CACHE_TTL = {
  FAST_REFRESH: 30 * 1000,        // 30 seconds for real-time data
  NORMAL_REFRESH: 2 * 60 * 1000,  // 2 minutes for regular data
  SLOW_REFRESH: 5 * 60 * 1000,    // 5 minutes for static data
  VERY_SLOW: 10 * 60 * 1000       // 10 minutes for rarely changing data
} as const;
