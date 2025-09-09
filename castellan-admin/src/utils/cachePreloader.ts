// Cache Preloader Utility
// Provides cache hydration on app startup for instant data availability

import { dashboardCache, CACHE_KEYS, CACHE_TTL } from './cacheManager';
import { preloadDataProviderCache } from '../dataProvider/cachedDataProvider';

export interface CachePreloadConfig {
  preloadKeys: string[];
  maxAge?: number; // Maximum age in milliseconds to consider cache fresh
  enableBackgroundRefresh?: boolean;
}

/**
 * Preloads and validates cached data on app startup
 * Returns immediately available cached data
 */
export class CachePreloader {
  private static instance: CachePreloader | null = null;
  private preloadedData: Map<string, any> = new Map();
  private isPreloaded = false;

  static getInstance(): CachePreloader {
    if (!CachePreloader.instance) {
      CachePreloader.instance = new CachePreloader();
    }
    return CachePreloader.instance;
  }

  /**
   * Preload cache data synchronously for immediate availability
   */
  preloadCache(config: CachePreloadConfig = { preloadKeys: Object.values(CACHE_KEYS) }): void {
    if (this.isPreloaded) return;

    console.log('🚀 Preloading cache for immediate data availability...');
    
    const maxAge = config.maxAge || CACHE_TTL.SLOW_REFRESH;
    let cacheHits = 0;
    let staleCacheHits = 0;

    for (const cacheKey of config.preloadKeys) {
      try {
        const cachedItem = dashboardCache.get(cacheKey);
        
        if (cachedItem) {
          const age = Date.now() - (cachedItem as any).timestamp;
          
          if (age <= maxAge) {
            // Fresh cache hit
            this.preloadedData.set(cacheKey, cachedItem);
            cacheHits++;
            console.log(`✅ Fresh cache hit for ${cacheKey}`);
          } else {
            // Stale but usable cache hit
            this.preloadedData.set(cacheKey, cachedItem);
            staleCacheHits++;
            console.log(`⚠️ Stale cache hit for ${cacheKey} (${Math.round(age / 1000)}s old)`);
          }
        } else {
          console.log(`❌ Cache miss for ${cacheKey}`);
        }
      } catch (error) {
        console.warn(`Failed to preload cache for ${cacheKey}:`, error);
      }
    }

    this.isPreloaded = true;
    
    console.log(`📊 Cache preload complete: ${cacheHits} fresh, ${staleCacheHits} stale, ${config.preloadKeys.length - cacheHits - staleCacheHits} misses`);
  }

  /**
   * Get preloaded cached data immediately without async operations
   */
  getPreloadedData<T>(cacheKey: string): T | null {
    if (!this.isPreloaded) {
      console.warn('Cache not preloaded yet, consider calling preloadCache() first');
      return null;
    }

    const cachedItem = this.preloadedData.get(cacheKey);
    return cachedItem ? (cachedItem as any).data : null;
  }

  /**
   * Check if data is available in preloaded cache
   */
  hasPreloadedData(cacheKey: string): boolean {
    return this.isPreloaded && this.preloadedData.has(cacheKey);
  }

  /**
   * Get cache statistics for debugging
   */
  getPreloadStats() {
    return {
      isPreloaded: this.isPreloaded,
      preloadedKeys: Array.from(this.preloadedData.keys()),
      preloadedCount: this.preloadedData.size
    };
  }

  /**
   * Clear preloaded cache
   */
  clearPreloadedCache(): void {
    this.preloadedData.clear();
    this.isPreloaded = false;
    console.log('🗑️ Preloaded cache cleared');
  }
}

// Singleton instance
export const cachePreloader = CachePreloader.getInstance();

// Default preload configuration
export const DEFAULT_PRELOAD_CONFIG: CachePreloadConfig = {
  preloadKeys: [
    CACHE_KEYS.SECURITY_EVENTS,
    CACHE_KEYS.COMPLIANCE_REPORTS,
    CACHE_KEYS.SYSTEM_STATUS,
    CACHE_KEYS.THREAT_SCANNER
  ],
  maxAge: 10 * 60 * 1000, // 10 minutes
  enableBackgroundRefresh: true
};

/**
 * Convenient function to preload cache with default configuration
 */
export function initializeCachePreloader(): void {
  console.log('🏃‍♂️ Initializing cache preloader...');
  cachePreloader.preloadCache(DEFAULT_PRELOAD_CONFIG);
  
  // Also preload React Admin data provider caches
  preloadDataProviderCache();
  
  // Make cache preloader available globally for debugging
  (window as any).cachePreloader = {
    stats: () => cachePreloader.getPreloadStats(),
    clear: () => cachePreloader.clearPreloadedCache(),
    reload: () => {
      cachePreloader.clearPreloadedCache();
      cachePreloader.preloadCache(DEFAULT_PRELOAD_CONFIG);
    },
    // Manual cache warming for testing (minimal to prevent bloat)
    warmCache: (resource: string) => {
      const { dashboardCache } = require('./cacheManager');
      const mockData = {
        data: [{id: 1, name: 'Mock data for ' + resource}],
        total: 1,
        timestamp: Date.now()
      };
      
      // Cache with only one key pattern to prevent bloat
      const key = `dp_getList_${resource}_default`;
      dashboardCache.set(key, mockData, 60 * 1000); // 1 minute TTL
      console.log(`🔥 Warmed cache: ${key}`);
      
      return `Warmed cache for ${resource}`;
    },
    // Debug function to list all cache keys
    listKeys: () => {
      const { dashboardCache } = require('./cacheManager');
      console.log('🔍 All Cache Keys:');
      if ((dashboardCache as any).memoryCache) {
        const keys: string[] = [];
        (dashboardCache as any).memoryCache.forEach((_value: any, key: string) => {
          keys.push(key);
        });
        keys.sort().forEach(key => console.log(`  - ${key}`));
        console.log(`Total: ${keys.length} keys`);
        return keys;
      }
      return [];
    },
    // Force populate cache for immediate testing
    forceCacheHit: () => {
      const { dashboardCache } = require('./cacheManager');
      const testData = {
        data: [{ id: 1, eventType: 'Test Event', timestamp: new Date().toISOString() }],
        total: 1,
        timestamp: Date.now()
      };
      
      // Populate cache with ALL possible key patterns for security-events
      const patterns = [
        'dp_getList_security-events_default',
        'dp_getList_security-events_p1_pp25_sid_oASC',
        'dp_getList_security-events_p1_pp10_sid_oASC',
        'dp_getList_security-events_p1_pp25_stimestamp_oDESC',
        'dp_getList_security-events_p1_pp25_stimestamp_oASC',
        'security_events' // Dashboard cache key
      ];
      
      patterns.forEach(key => {
        dashboardCache.set(key, testData, 2 * 60 * 1000); // 2 min TTL
        console.log(`✅ Forced cache: ${key}`);
      });
      
      return `Forced cache population with ${patterns.length} keys`;
    }
  };
  
  console.log('🔍 Cache preloader debugging available - type cachePreloader.stats() in console');
}
