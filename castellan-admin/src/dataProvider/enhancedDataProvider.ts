import { DataProvider } from 'react-admin';
import { performanceMonitor } from '../utils/preload';

/**
 * Cache entry with expiration and validation
 */
class CacheEntry {
  constructor(
    public data: any,
    public timestamp: number,
    public ttl: number,
    public resource: string
  ) {}

  isExpired(): boolean {
    return Date.now() - this.timestamp > this.ttl;
  }

  isStale(): boolean {
    // Consider data stale after 50% of TTL for background refresh
    return Date.now() - this.timestamp > (this.ttl * 0.5);
  }
}

/**
 * Enhanced Data Provider with intelligent caching and background refresh
 * Provides instant page loads through cache-first strategy with background updates
 */
export class EnhancedDataProvider implements DataProvider {
  private cache = new Map<string, CacheEntry>();
  private backgroundRefreshQueue = new Set<string>();
  private pendingRequests = new Map<string, Promise<any>>();

  // Default TTL configuration for different resource types
  private readonly ttlConfig: Record<string, number> = {
    'security-events': 15000,        // 15 seconds - frequently updated
    'yara-matches': 20000,           // 20 seconds
    'dashboard': 30000,              // 30 seconds
    'system-status': 10000,          // 10 seconds - real-time monitoring
    'threat-scanner': 5000,          // 5 seconds - real-time scanning
    'yara-rules': 60000,             // 1 minute - less frequently changed
    'mitre/techniques': 120000,      // 2 minutes - rarely changes
    'configuration': 300000,         // 5 minutes - rarely changes
    'default': 30000                 // Default 30 seconds
  };

  constructor(private baseProvider: DataProvider) {}

  /**
   * Get list with cache-first strategy and background refresh
   */
  async getList(resource: string, params: any): Promise<any> {
    const startTime = performance.now();
    const cacheKey = this.generateCacheKey('getList', resource, params);
    const cached = this.cache.get(cacheKey);

    // Return cached data immediately if valid
    if (cached && !cached.isExpired()) {
      performanceMonitor.trackCacheHit(resource);

      // Trigger background refresh if data is getting stale
      if (cached.isStale()) {
        this.backgroundRefresh('getList', resource, params);
      }

      const loadTime = performance.now() - startTime;
      performanceMonitor.trackPageLoad(resource, loadTime);
      return cached.data;
    }

    // Check if there's already a pending request for this data
    const pendingKey = `getList:${cacheKey}`;
    const pendingRequest = this.pendingRequests.get(pendingKey);
    if (pendingRequest) {
      console.log(`[EnhancedDataProvider] Reusing pending request for ${resource}`);
      return pendingRequest;
    }

    // Fetch fresh data
    const requestPromise = this.baseProvider.getList(resource, params)
      .then(data => {
        const ttl = this.getTTL(resource);
        this.cache.set(cacheKey, new CacheEntry(data, Date.now(), ttl, resource));

        // Predict and preload related data
        this.predictAndPreload(resource, params);

        const loadTime = performance.now() - startTime;
        performanceMonitor.trackPageLoad(resource, loadTime);

        // Clean up pending request
        this.pendingRequests.delete(pendingKey);

        return data;
      })
      .catch(error => {
        // Clean up pending request on error
        this.pendingRequests.delete(pendingKey);
        throw error;
      });

    // Store pending request to avoid duplicates
    this.pendingRequests.set(pendingKey, requestPromise);

    return requestPromise;
  }

  /**
   * Get single record with caching
   */
  async getOne(resource: string, params: any): Promise<any> {
    const cacheKey = this.generateCacheKey('getOne', resource, params);
    const cached = this.cache.get(cacheKey);

    if (cached && !cached.isExpired()) {
      performanceMonitor.trackCacheHit(`${resource}:${params.id}`);

      if (cached.isStale()) {
        this.backgroundRefresh('getOne', resource, params);
      }

      return cached.data;
    }

    const data = await this.baseProvider.getOne(resource, params);
    const ttl = this.getTTL(resource);
    this.cache.set(cacheKey, new CacheEntry(data, Date.now(), ttl, resource));

    return data;
  }

  /**
   * Get many records with caching
   */
  async getMany(resource: string, params: any): Promise<any> {
    const cacheKey = this.generateCacheKey('getMany', resource, params);
    const cached = this.cache.get(cacheKey);

    if (cached && !cached.isExpired()) {
      performanceMonitor.trackCacheHit(resource);
      return cached.data;
    }

    const data = await this.baseProvider.getMany(resource, params);
    const ttl = this.getTTL(resource);
    this.cache.set(cacheKey, new CacheEntry(data, Date.now(), ttl, resource));

    return data;
  }

  /**
   * Get many references with caching
   */
  async getManyReference(resource: string, params: any): Promise<any> {
    const cacheKey = this.generateCacheKey('getManyReference', resource, params);
    const cached = this.cache.get(cacheKey);

    if (cached && !cached.isExpired()) {
      performanceMonitor.trackCacheHit(resource);
      return cached.data;
    }

    const data = await this.baseProvider.getManyReference(resource, params);
    const ttl = this.getTTL(resource);
    this.cache.set(cacheKey, new CacheEntry(data, Date.now(), ttl, resource));

    return data;
  }

  /**
   * Create record and invalidate related caches
   */
  async create(resource: string, params: any): Promise<any> {
    const result = await this.baseProvider.create(resource, params);
    this.invalidateCache(resource);
    this.invalidateRelatedCaches(resource);
    return result;
  }

  /**
   * Update record and invalidate related caches
   */
  async update(resource: string, params: any): Promise<any> {
    const result = await this.baseProvider.update(resource, params);
    this.invalidateCache(resource);
    this.invalidateRelatedCaches(resource);

    // Also invalidate the specific record cache
    const oneKey = this.generateCacheKey('getOne', resource, { id: params.id });
    this.cache.delete(oneKey);

    return result;
  }

  /**
   * Update many records and invalidate related caches
   */
  async updateMany(resource: string, params: any): Promise<any> {
    const result = await this.baseProvider.updateMany(resource, params);
    this.invalidateCache(resource);
    this.invalidateRelatedCaches(resource);
    return result;
  }

  /**
   * Delete record and invalidate related caches
   */
  async delete(resource: string, params: any): Promise<any> {
    const result = await this.baseProvider.delete(resource, params);
    this.invalidateCache(resource);
    this.invalidateRelatedCaches(resource);

    // Also invalidate the specific record cache
    const oneKey = this.generateCacheKey('getOne', resource, { id: params.id });
    this.cache.delete(oneKey);

    return result;
  }

  /**
   * Delete many records and invalidate related caches
   */
  async deleteMany(resource: string, params: any): Promise<any> {
    const result = await this.baseProvider.deleteMany(resource, params);
    this.invalidateCache(resource);
    this.invalidateRelatedCaches(resource);
    return result;
  }

  /**
   * Custom method support with optional caching
   */
  async custom?(method: string, resource: string, params: any): Promise<any> {
    // Check if base provider supports custom method
    if (!this.baseProvider.custom) {
      throw new Error('Custom method not supported by base provider');
    }

    // For GET requests, apply caching
    if (params?.method === 'GET') {
      const cacheKey = this.generateCacheKey('custom', `${resource}/${method}`, params);
      const cached = this.cache.get(cacheKey);

      if (cached && !cached.isExpired()) {
        performanceMonitor.trackCacheHit(`${resource}/${method}`);
        return cached.data;
      }

      const data = await this.baseProvider.custom(method, resource, params);
      const ttl = this.getTTL(resource);
      this.cache.set(cacheKey, new CacheEntry(data, Date.now(), ttl, resource));

      return data;
    }

    // For non-GET requests, bypass cache
    return this.baseProvider.custom(method, resource, params);
  }

  /**
   * Background refresh of stale data
   */
  private backgroundRefresh(method: string, resource: string, params: any): void {
    const cacheKey = this.generateCacheKey(method, resource, params);

    // Prevent duplicate background refreshes
    if (this.backgroundRefreshQueue.has(cacheKey)) {
      return;
    }

    this.backgroundRefreshQueue.add(cacheKey);
    console.log(`[EnhancedDataProvider] Background refresh triggered for ${resource}`);

    // Perform refresh after a small delay to batch requests
    setTimeout(async () => {
      try {
        let freshData: any;

        switch (method) {
          case 'getList':
            freshData = await this.baseProvider.getList(resource, params);
            break;
          case 'getOne':
            freshData = await this.baseProvider.getOne(resource, params);
            break;
          case 'getMany':
            freshData = await this.baseProvider.getMany(resource, params);
            break;
          case 'getManyReference':
            freshData = await this.baseProvider.getManyReference(resource, params);
            break;
          default:
            console.warn(`[EnhancedDataProvider] Unknown method for background refresh: ${method}`);
            return;
        }

        const ttl = this.getTTL(resource);
        this.cache.set(cacheKey, new CacheEntry(freshData, Date.now(), ttl, resource));
        console.log(`[EnhancedDataProvider] Background refresh completed for ${resource}`);

      } catch (error) {
        console.error(`[EnhancedDataProvider] Background refresh failed for ${resource}:`, error);
      } finally {
        this.backgroundRefreshQueue.delete(cacheKey);
      }
    }, 500);
  }

  /**
   * Predict and preload related data based on navigation patterns
   */
  private predictAndPreload(resource: string, params: any): void {
    const predictions = this.getPredictedResources(resource);

    predictions.forEach((predictedResource, index) => {
      // Stagger preloading to avoid overwhelming the server
      setTimeout(() => {
        const preloadParams = {
          pagination: { page: 1, perPage: 25 },
          sort: this.getDefaultSort(predictedResource),
          filter: {}
        };

        const cacheKey = this.generateCacheKey('getList', predictedResource, preloadParams);

        // Only preload if not already cached
        if (!this.cache.has(cacheKey)) {
          console.log(`[EnhancedDataProvider] Predictively loading ${predictedResource}`);

          this.baseProvider.getList(predictedResource, preloadParams)
            .then(data => {
              const ttl = this.getTTL(predictedResource);
              this.cache.set(cacheKey, new CacheEntry(data, Date.now(), ttl, predictedResource));
              console.log(`[EnhancedDataProvider] Predictive load completed for ${predictedResource}`);
            })
            .catch(error => {
              console.warn(`[EnhancedDataProvider] Predictive load failed for ${predictedResource}:`, error);
            });
        }
      }, 1000 + (index * 500)); // Start after 1s, then 500ms apart
    });
  }

  /**
   * Get predicted resources based on current resource
   */
  private getPredictedResources(resource: string): string[] {
    const predictions: Record<string, string[]> = {
      'dashboard': ['security-events', 'system-status'],
      'security-events': ['mitre/techniques', 'yara-matches'],
      'yara-rules': ['yara-matches'],
      'system-status': ['dashboard'],
      'mitre/techniques': ['security-events'],
    };

    return predictions[resource] || [];
  }

  /**
   * Get default sort for a resource
   */
  private getDefaultSort(resource: string): { field: string; order: 'ASC' | 'DESC' } {
    const sorts: Record<string, { field: string; order: 'ASC' | 'DESC' }> = {
      'security-events': { field: 'timestamp', order: 'DESC' },
      'yara-rules': { field: 'updatedAt', order: 'DESC' },
      'yara-matches': { field: 'detectedAt', order: 'DESC' },
      'mitre/techniques': { field: 'techniqueId', order: 'ASC' }
    };

    return sorts[resource] || { field: 'id', order: 'DESC' };
  }

  /**
   * Get TTL for a resource
   */
  private getTTL(resource: string): number {
    return this.ttlConfig[resource] || this.ttlConfig.default;
  }

  /**
   * Generate cache key
   */
  private generateCacheKey(method: string, resource: string, params: any): string {
    // Create a stable key that includes method, resource, and relevant params
    const sortedParams = this.sortObject(params);
    return `${method}:${resource}:${JSON.stringify(sortedParams)}`;
  }

  /**
   * Sort object keys for stable cache key generation
   */
  private sortObject(obj: any): any {
    if (obj === null || typeof obj !== 'object') {
      return obj;
    }

    if (Array.isArray(obj)) {
      return obj.map(item => this.sortObject(item));
    }

    const sortedObj: any = {};
    Object.keys(obj).sort().forEach(key => {
      sortedObj[key] = this.sortObject(obj[key]);
    });

    return sortedObj;
  }

  /**
   * Invalidate cache for a resource
   */
  private invalidateCache(resource: string): void {
    const keysToDelete: string[] = [];

    this.cache.forEach((entry, key) => {
      if (entry.resource === resource || key.includes(`:${resource}:`)) {
        keysToDelete.push(key);
      }
    });

    keysToDelete.forEach(key => {
      this.cache.delete(key);
      console.log(`[EnhancedDataProvider] Cache invalidated: ${key}`);
    });
  }

  /**
   * Invalidate related caches when a resource changes
   */
  private invalidateRelatedCaches(resource: string): void {
    const relatedResources: Record<string, string[]> = {
      'security-events': ['dashboard', 'mitre/techniques'],
      'yara-rules': ['yara-matches'],
      'yara-matches': ['dashboard', 'security-events'],
      'system-status': ['dashboard'],
      'configuration': ['system-status']
    };

    const related = relatedResources[resource] || [];
    related.forEach(relatedResource => {
      this.invalidateCache(relatedResource);
    });
  }

  /**
   * Clear all cache entries
   */
  public clearCache(): void {
    this.cache.clear();
    this.backgroundRefreshQueue.clear();
    this.pendingRequests.clear();
    console.log('[EnhancedDataProvider] Cache cleared');
  }

  /**
   * Get cache statistics
   */
  public getCacheStats(): { size: number; entries: number; hitRate: number } {
    const entries = this.cache.size;
    const size = JSON.stringify(Array.from(this.cache.values())).length;

    // Calculate hit rate (would need to track hits/misses for accurate rate)
    const hitRate = 0; // Placeholder - implement tracking if needed

    return { size, entries, hitRate };
  }
}

/**
 * Factory function to create enhanced data provider with Proxy for custom methods
 */
export const createEnhancedDataProvider = (baseProvider: DataProvider): any => {
  const enhancedProvider = new EnhancedDataProvider(baseProvider);

  // Create a Proxy to handle custom methods not defined in EnhancedDataProvider
  return new Proxy(enhancedProvider, {
    get(target: any, prop: string) {
      // If the property exists on the enhanced provider, use it
      if (prop in target) {
        return target[prop];
      }

      // Otherwise, check if it exists on the base provider
      if (prop in baseProvider && typeof (baseProvider as any)[prop] === 'function') {
        console.log(`[EnhancedDataProvider] Proxying custom method: ${prop}`);
        return (baseProvider as any)[prop].bind(baseProvider);
      }

      return undefined;
    }
  });
};