// Cached Data Provider Wrapper
// Adds comprehensive caching to React Admin data provider for instant navigation

import { DataProvider } from 'react-admin';
import { dashboardCache, CACHE_TTL } from '../utils/cacheManager';
import { cachePreloader } from '../utils/cachePreloader';

interface CacheConfig {
  ttl: number;
  enableLogging?: boolean;
}

// Cache configurations per resource type
const RESOURCE_CACHE_CONFIG: Record<string, CacheConfig> = {
  'security-events': { ttl: CACHE_TTL.NORMAL_REFRESH, enableLogging: true },
  'system-status': { ttl: CACHE_TTL.NORMAL_REFRESH, enableLogging: true },
  'compliance-reports': { ttl: CACHE_TTL.SLOW_REFRESH, enableLogging: true },
  'threat-scanner': { ttl: CACHE_TTL.NORMAL_REFRESH, enableLogging: true },
  'mitre-techniques': { ttl: CACHE_TTL.VERY_SLOW, enableLogging: true },
  'notification-settings': { ttl: CACHE_TTL.SLOW_REFRESH, enableLogging: true },
  'configuration': { ttl: CACHE_TTL.VERY_SLOW, enableLogging: true },
};

// Generate cache key for data provider operations - optimized for performance
function generateCacheKey(resource: string, method: string, params?: any): string {
  const baseKey = `dp_${method}_${resource}`;
  
  if (method === 'getList') {
    const { pagination, sort, filter } = params || {};
    const page = pagination?.page || 1;
    const perPage = pagination?.perPage || 25;
    const sortField = sort?.field || 'id';
    const sortOrder = sort?.order || 'ASC';
    
    // Only include filters in key if they exist to reduce cache key variations
    const filterHash = filter && Object.keys(filter).length > 0 
      ? `_f${JSON.stringify(filter).slice(0, 20)}` 
      : '';
    
    // Simplified cache key - only essential parameters
    const cacheKey = `${baseKey}_p${page}_pp${perPage}_s${sortField}_o${sortOrder}${filterHash}`;
    return cacheKey;
  }
  
  if (method === 'getOne') {
    return `${baseKey}_${params?.id}`;
  }
  
  if (method === 'getMany') {
    const ids = params?.ids?.sort().join(',') || '';
    return `${baseKey}_${ids.substring(0, 20)}`;
  }
  
  return baseKey;
}

// Check if cached data is still fresh
function isCacheFresh(cacheKey: string, ttl: number): boolean {
  const cached = dashboardCache.get(cacheKey);
  if (!cached) return false;
  
  const age = Date.now() - (cached as any).timestamp;
  return age < ttl;
}

// Helper function to safely invalidate cache keys
function invalidateCacheForResource(resource: string): string[] {
  try {
    const keysToInvalidate: string[] = [];
    
    if ((dashboardCache as any).memoryCache) {
      (dashboardCache as any).memoryCache.forEach((_value: any, key: string) => {
        if (typeof key === 'string' && key.includes(`_${resource}`)) {
          keysToInvalidate.push(key);
        }
      });
    }
    
    keysToInvalidate.forEach((key: string) => dashboardCache.remove(key));
    return keysToInvalidate;
  } catch (error) {
    console.warn('Cache invalidation failed:', error);
    return [];
  }
}

// Create cached data provider decorator
export function createCachedDataProvider(baseDataProvider: DataProvider): DataProvider {
  const log = (message: string, resource?: string) => {
    if (resource && RESOURCE_CACHE_CONFIG[resource]?.enableLogging) {
      console.log(`[CachedDP] ${message}`);
    }
  };

  return {
    ...baseDataProvider,

    // Cached getList implementation - optimized for performance
    getList: async (resource, params) => {
      const config = RESOURCE_CACHE_CONFIG[resource] || { ttl: CACHE_TTL.NORMAL_REFRESH };
      const cacheKey = generateCacheKey(resource, 'getList', params);
      
      // Single cache key lookup - no complex patterns
      const cached = dashboardCache.get(cacheKey);
      
      if (cached && isCacheFresh(cacheKey, config.ttl)) {
        log(`⚡ INSTANT LOAD: Cache HIT for ${resource} getList`, resource);
        return cached as any;
      }
      
      // Check preloaded data as fallback
      const preloadedData = cachePreloader.getPreloadedData(cacheKey);
      if (preloadedData && !cached) {
        log(`⚡ Using PRELOADED data for ${resource} getList`, resource);
        return preloadedData;
      }
      
      log(`🔄 Cache MISS for ${resource} getList - fetching from API`, resource);
      
      try {
        // Fetch from API
        const result = await baseDataProvider.getList(resource, params);
        
        // Cache the result with timestamp
        const cacheValue = {
          ...result,
          timestamp: Date.now()
        };
        
        // Store with single optimized key to reduce cache bloat
        dashboardCache.set(cacheKey, cacheValue, config.ttl);
        
        // Only log in development to reduce performance overhead
        if (process.env.NODE_ENV === 'development') {
          console.log(`💾 CACHED ${resource}: ${result.data.length} items (TTL: ${config.ttl}ms)`);
        }
        
        log(`💾 Cached ${resource} getList result (${result.data.length} items)`, resource);
        return result;
      } catch (error) {
        // Return stale cache if API fails and we have stale data
        if (cached) {
          log(`⚠️ API failed for ${resource} getList, returning stale cache`, resource);
          return cached as any;
        }
        throw error;
      }
    },

    // Cached getOne implementation
    getOne: async (resource, params) => {
      const config = RESOURCE_CACHE_CONFIG[resource] || { ttl: CACHE_TTL.NORMAL_REFRESH };
      const cacheKey = generateCacheKey(resource, 'getOne', params);
      
      // Check cache first
      const cached = dashboardCache.get(cacheKey);
      if (cached && isCacheFresh(cacheKey, config.ttl)) {
        log(`⚡ INSTANT NAVIGATION: Cache HIT for ${resource} getOne(${params.id}) - no loading!`, resource);
        return cached as any;
      }
      
      log(`🔄 Cache MISS for ${resource} getOne(${params.id}) - fetching from API`, resource);
      
      try {
        // Fetch from API
        const result = await baseDataProvider.getOne(resource, params);
        
        // Cache the result
        dashboardCache.set(cacheKey, {
          ...result,
          timestamp: Date.now()
        }, config.ttl);
        
        log(`💾 Cached ${resource} getOne(${params.id})`, resource);
        return result;
      } catch (error) {
        // Return stale cache if API fails and we have stale data
        if (cached) {
          log(`⚠️ API failed for ${resource} getOne(${params.id}), returning stale cache`, resource);
          return cached as any;
        }
        throw error;
      }
    },

    // Cached getMany implementation
    getMany: async (resource, params) => {
      const config = RESOURCE_CACHE_CONFIG[resource] || { ttl: CACHE_TTL.NORMAL_REFRESH };
      const cacheKey = generateCacheKey(resource, 'getMany', params);
      
      // Check cache first
      const cached = dashboardCache.get(cacheKey);
      if (cached && isCacheFresh(cacheKey, config.ttl)) {
        log(`✅ Cache HIT for ${resource} getMany(${params.ids.length} items)`, resource);
        return cached as any;
      }
      
      log(`🔄 Cache MISS for ${resource} getMany - fetching from API`, resource);
      
      try {
        // Fetch from API
        const result = await baseDataProvider.getMany(resource, params);
        
        // Cache the result
        dashboardCache.set(cacheKey, {
          ...result,
          timestamp: Date.now()
        }, config.ttl);
        
        log(`💾 Cached ${resource} getMany result (${result.data.length} items)`, resource);
        return result;
      } catch (error) {
        // Return stale cache if API fails and we have stale data
        if (cached) {
          log(`⚠️ API failed for ${resource} getMany, returning stale cache`, resource);
          return cached as any;
        }
        throw error;
      }
    },

    // Pass-through for write operations with cache invalidation
    create: async (resource, params) => {
      const result = await baseDataProvider.create(resource, params);
      
      // Invalidate related caches
      const keysToInvalidate = invalidateCacheForResource(resource);
      log(`🗑️ Invalidated ${keysToInvalidate.length} cache entries for ${resource} after create`, resource);
      
      return result;
    },

    update: async (resource, params) => {
      const result = await baseDataProvider.update(resource, params);
      
      // Invalidate related caches
      const keysToInvalidate = invalidateCacheForResource(resource);
      log(`🗑️ Invalidated ${keysToInvalidate.length} cache entries for ${resource} after update`, resource);
      
      return result;
    },

    updateMany: async (resource, params) => {
      const result = await baseDataProvider.updateMany(resource, params);
      
      // Invalidate related caches
      const keysToInvalidate = invalidateCacheForResource(resource);
      log(`🗑️ Invalidated ${keysToInvalidate.length} cache entries for ${resource} after updateMany`, resource);
      
      return result;
    },

    delete: async (resource, params) => {
      const result = await baseDataProvider.delete(resource, params);
      
      // Invalidate related caches
      const keysToInvalidate = invalidateCacheForResource(resource);
      log(`🗑️ Invalidated ${keysToInvalidate.length} cache entries for ${resource} after delete`, resource);
      
      return result;
    },

    deleteMany: async (resource, params) => {
      const result = await baseDataProvider.deleteMany(resource, params);
      
      // Invalidate related caches
      const keysToInvalidate = invalidateCacheForResource(resource);
      log(`🗑️ Invalidated ${keysToInvalidate.length} cache entries for ${resource} after deleteMany`, resource);
      
      return result;
    },

    // Pass-through for getManyReference - could add caching if needed
    getManyReference: async (resource, params) => {
      return await baseDataProvider.getManyReference(resource, params);
    },

    // Cached custom API calls implementation
    custom: async (params: { url: string; method?: string; [key: string]: any }) => {
      const { url, method = 'GET' } = params;
      
      // Only cache GET requests
      if (method.toLowerCase() !== 'get') {
        log(`🔄 Custom API ${method} ${url} - not cached (non-GET request)`);
        return await baseDataProvider.custom(params);
      }
      
      // Generate cache key for custom calls
      const cacheKey = `custom_${method.toLowerCase()}_${url.replace(/[^a-zA-Z0-9]/g, '_')}`;
      
      // Use longest cache TTL for statistics/dashboard data since it changes infrequently
      const config = { ttl: CACHE_TTL.VERY_SLOW, enableLogging: true };
      
      log(`🔍 Checking cache for custom API: ${method} ${url}`);
      
      // Check cache first
      const cached = dashboardCache.get(cacheKey);
      if (cached && isCacheFresh(cacheKey, config.ttl)) {
        log(`⚡ INSTANT LOAD: Cache HIT for custom API ${method} ${url} - no loading!`, 'custom');
        return cached as any;
      }
      
      log(`🔄 Cache MISS for custom API ${method} ${url} - fetching from API`, 'custom');
      
      try {
        // Fetch from API
        const result = await baseDataProvider.custom(params);
        
        // Cache the result
        const cacheValue = {
          ...result,
          timestamp: Date.now()
        };
        
        dashboardCache.set(cacheKey, cacheValue, config.ttl);
        
        console.log(`💾 CACHED custom API result: ${method} ${url} for ${config.ttl}ms`);
        log(`💾 Cached custom API result: ${method} ${url}`, 'custom');
        return result;
      } catch (error) {
        // Return stale cache if API fails and we have stale data
        if (cached) {
          log(`⚠️ API failed for custom ${method} ${url}, returning stale cache`, 'custom');
          return cached as any;
        }
        throw error;
      }
    }
  };
}

// Enhanced preloader that works with simplified cache keys
export function preloadDataProviderCache(): void {
  if (process.env.NODE_ENV === 'development') {
    console.log('🚀 Preloading React Admin data provider caches...');
  }
  
  const resourcesToPreload = Object.keys(RESOURCE_CACHE_CONFIG);
  let preloadedCount = 0;
  
  resourcesToPreload.forEach(resource => {
    // Check most common cache pattern only
    const defaultParams = { pagination: { page: 1, perPage: 25 }, sort: { field: 'id', order: 'ASC' } };
    const listCacheKey = generateCacheKey(resource, 'getList', defaultParams);
    const cached = dashboardCache.get(listCacheKey);
    
    if (cached) {
      preloadedCount++;
      if (process.env.NODE_ENV === 'development') {
        console.log(`✅ Found existing cache for ${resource}`);
      }
    }
  });
  
  if (process.env.NODE_ENV === 'development') {
    console.log(`📊 Data provider cache preload: ${preloadedCount}/${resourcesToPreload.length} resources cached`);
    
    // Debug: List current cache keys (development only)
    console.log('🔍 Current dp_ cache keys:');
    if ((dashboardCache as any).memoryCache) {
      let dpKeyCount = 0;
      (dashboardCache as any).memoryCache.forEach((_value: any, key: string) => {
        if (key.startsWith('dp_')) {
          dpKeyCount++;
          console.log(`  - ${key}`);
        }
      });
      console.log(`Total dp_ cache keys: ${dpKeyCount}`);
    }
  }
}
