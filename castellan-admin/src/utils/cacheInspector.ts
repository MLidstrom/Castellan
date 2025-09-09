// Cache Inspector - Diagnostic tool for cache issues
import { dashboardCache, CACHE_KEYS, CACHE_TTL } from './cacheManager';

export class CacheInspector {
  // Inspect current cache state
  static inspectCache(): void {
    console.group('🔍 Cache Inspection Report');
    
    // Get cache stats
    const stats = dashboardCache.getStats();
    console.log('📊 Cache Statistics:', stats);
    
    // Check memory cache contents
    console.log('\n💾 Memory Cache Contents:');
    this.listCacheKeys();
    
    // Check localStorage (if enabled)
    console.log('\n📦 LocalStorage Cache:');
    this.checkLocalStorage();
    
    // Test cache operations
    console.log('\n🧪 Cache Operation Tests:');
    this.testCacheOperations();
    
    console.groupEnd();
  }
  
  // List all cache keys
  static listCacheKeys(): void {
    try {
      // Access internal memory cache map
      const memoryCache = (dashboardCache as any).memoryCache;
      if (memoryCache && memoryCache.size > 0) {
        const keys = Array.from(memoryCache.keys());
        console.log(`Found ${keys.length} keys in memory cache:`);
        keys.forEach((key) => {
          const keyStr = String(key);
          const item = memoryCache.get(key);
          if (item) {
            const age = Date.now() - item.timestamp;
            const isExpired = age > item.ttl;
            console.log(`  - ${keyStr}: ${isExpired ? '❌ EXPIRED' : '✅ VALID'} (age: ${(age/1000).toFixed(1)}s, ttl: ${(item.ttl/1000).toFixed(1)}s)`);
          }
        });
      } else {
        console.log('  Memory cache is empty');
      }
    } catch (error) {
      console.error('  Could not access memory cache:', error);
    }
  }
  
  // Check localStorage cache status
  static checkLocalStorage(): void {
    const useLocalStorage = (dashboardCache as any).useLocalStorage;
    console.log(`  LocalStorage enabled: ${useLocalStorage}`);
    
    if (useLocalStorage) {
      const prefix = (dashboardCache as any).keyPrefix || 'castellan_cache_';
      const keys = Object.keys(localStorage).filter(key => key.startsWith(prefix));
      console.log(`  Found ${keys.length} localStorage keys with prefix "${prefix}"`);
      
      keys.forEach(key => {
        try {
          const item = JSON.parse(localStorage.getItem(key) || '{}');
          const age = Date.now() - (item.timestamp || 0);
          const isExpired = age > (item.ttl || 0);
          console.log(`  - ${key}: ${isExpired ? '❌ EXPIRED' : '✅ VALID'} (age: ${(age/1000).toFixed(1)}s)`);
        } catch (e) {
          console.log(`  - ${key}: ⚠️ CORRUPTED`);
        }
      });
    }
  }
  
  // Test cache operations
  static testCacheOperations(): void {
    const testKey = 'test_cache_key';
    const testData = { test: true, timestamp: Date.now() };
    
    // Test SET operation
    console.log('  Testing SET operation...');
    dashboardCache.set(testKey, testData, 5000);
    
    // Test GET operation
    console.log('  Testing GET operation...');
    const retrieved = dashboardCache.get(testKey);
    if (retrieved) {
      console.log('  ✅ Cache SET/GET working correctly');
      console.log('  Retrieved data:', retrieved);
    } else {
      console.log('  ❌ Cache GET failed - data not retrieved');
    }
    
    // Test HAS operation
    const hasKey = dashboardCache.has(testKey);
    console.log(`  Testing HAS operation: ${hasKey ? '✅ PASS' : '❌ FAIL'}`);
    
    // Clean up test key
    dashboardCache.remove(testKey);
    console.log('  Test key cleaned up');
  }
  
  // Monitor cache hit/miss for specific keys
  static monitorCacheKey(key: string): void {
    console.log(`\n📡 Monitoring cache key: "${key}"`);
    
    const cached = dashboardCache.get(key);
    if (cached) {
      console.log('  ✅ Key found in cache');
      console.log('  Data:', cached);
      
      // Check if it's a cached API response
      if ((cached as any).timestamp) {
        const age = Date.now() - (cached as any).timestamp;
        console.log(`  Age: ${(age/1000).toFixed(1)}s`);
      }
    } else {
      console.log('  ❌ Key not found in cache');
      
      // Check if key exists but is expired
      const memoryCache = (dashboardCache as any).memoryCache;
      if (memoryCache && memoryCache.has(key)) {
        const item = memoryCache.get(key);
        console.log('  ⚠️ Key exists but may be expired:', item);
      }
    }
  }
  
  // Debug React Admin cache keys
  static debugReactAdminCache(): void {
    console.group('🔍 React Admin Cache Debug');
    
    const resources = [
      'security-events',
      'system-status', 
      'compliance-reports',
      'threat-scanner',
      'mitre-techniques',
      'notification-settings',
      'configuration'
    ];
    
    resources.forEach(resource => {
      console.log(`\n📋 Resource: ${resource}`);
      
      // Check common React Admin cache patterns
      const patterns = [
        `dp_getList_${resource}`,
        `dp_getList_${resource}_default`,
        `dp_getList_${resource}_p1_pp25_sid_oASC`,
        `dp_getList_${resource}_p1_pp10_sid_oASC`,
        `dp_getList_${resource}_p1_pp25_stimestamp_oDESC`
      ];
      
      patterns.forEach(pattern => {
        const cached = dashboardCache.get(pattern);
        if (cached) {
          console.log(`  ✅ Found: ${pattern}`);
          if ((cached as any).data) {
            console.log(`     Items: ${(cached as any).data.length}`);
          }
        }
      });
    });
    
    console.groupEnd();
  }
  
  // Clear all cache and verify
  static clearAndVerify(): void {
    console.log('\n🗑️ Clearing all cache...');
    dashboardCache.clear();
    
    const stats = dashboardCache.getStats();
    console.log('After clear - Memory items:', stats.memoryItems);
    console.log('After clear - LocalStorage items:', stats.localStorageItems);
    
    if (stats.memoryItems === 0 && stats.localStorageItems === 0) {
      console.log('✅ Cache successfully cleared');
    } else {
      console.log('⚠️ Cache may not be fully cleared');
    }
  }
}

// Export for global access in browser console
if (typeof window !== 'undefined') {
  (window as any).CacheInspector = CacheInspector;
  (window as any).dashboardCache = dashboardCache;
  
  console.log('🔧 Cache Inspector loaded. Available commands:');
  console.log('  CacheInspector.inspectCache() - Full cache inspection');
  console.log('  CacheInspector.listCacheKeys() - List all cache keys');
  console.log('  CacheInspector.debugReactAdminCache() - Debug React Admin cache');
  console.log('  CacheInspector.monitorCacheKey(key) - Monitor specific key');
  console.log('  CacheInspector.clearAndVerify() - Clear all cache');
  console.log('  dashboardCache.getStats() - Get cache statistics');
}

export default CacheInspector;
