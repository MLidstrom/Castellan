// Cache Performance Monitor and Debugger
import { dashboardCache } from './cacheManager';

interface CacheStats {
  memoryHits: number;
  memoryMisses: number;
  localStorageHits: number;
  localStorageMisses: number;
  totalRequests: number;
  averageResponseTime: number;
}

class CacheDebugger {
  private stats: CacheStats = {
    memoryHits: 0,
    memoryMisses: 0,
    localStorageHits: 0,
    localStorageMisses: 0,
    totalRequests: 0,
    averageResponseTime: 0
  };

  private responseTimes: number[] = [];
  private enabled: boolean = false;

  enable() {
    this.enabled = true;
    console.log('🔍 Cache debugger enabled');
  }

  disable() {
    this.enabled = false;
  }

  // Monitor a cache operation
  measureCacheOperation<T>(key: string, operation: () => T | null): T | null {
    if (!this.enabled) return operation();

    const startTime = performance.now();
    const result = operation();
    const endTime = performance.now();
    
    const responseTime = endTime - startTime;
    this.responseTimes.push(responseTime);
    
    // Keep only last 100 measurements
    if (this.responseTimes.length > 100) {
      this.responseTimes.shift();
    }
    
    this.stats.totalRequests++;
    this.stats.averageResponseTime = 
      this.responseTimes.reduce((sum, time) => sum + time, 0) / this.responseTimes.length;

    if (result !== null) {
      // Check if it came from memory or localStorage
      // This is a simplified check - in reality we'd need more instrumentation
      if (responseTime < 1) { // Assume < 1ms = memory cache
        this.stats.memoryHits++;
      } else {
        this.stats.localStorageHits++;
      }
    } else {
      if (responseTime < 1) {
        this.stats.memoryMisses++;
      } else {
        this.stats.localStorageMisses++;
      }
    }

    // Log slow operations
    if (responseTime > 10) {
      console.warn(`🐌 Slow cache operation for key "${key}": ${responseTime.toFixed(2)}ms`);
    }

    return result;
  }

  // Get current stats
  getStats(): CacheStats & { hitRate: number; memoryHitRate: number } {
    const totalHits = this.stats.memoryHits + this.stats.localStorageHits;
    const hitRate = this.stats.totalRequests > 0 
      ? (totalHits / this.stats.totalRequests) * 100 
      : 0;
    
    const memoryHitRate = this.stats.totalRequests > 0
      ? (this.stats.memoryHits / this.stats.totalRequests) * 100
      : 0;

    return {
      ...this.stats,
      hitRate,
      memoryHitRate
    };
  }

  // Log comprehensive cache analysis
  logCacheAnalysis() {
    if (!this.enabled) return;

    const stats = this.getStats();
    const cacheManagerStats = dashboardCache.getStats();
    
    console.group('🔍 Cache Performance Analysis');
    console.log('📊 Request Stats:');
    console.log(`  Total Requests: ${stats.totalRequests}`);
    console.log(`  Hit Rate: ${stats.hitRate.toFixed(1)}%`);
    console.log(`  Memory Hit Rate: ${stats.memoryHitRate.toFixed(1)}%`);
    console.log(`  Average Response Time: ${stats.averageResponseTime.toFixed(2)}ms`);
    
    console.log('\n💾 Cache Storage Stats:');
    console.log(`  Memory Items: ${cacheManagerStats.memoryItems}`);
    console.log(`  LocalStorage Items: ${cacheManagerStats.localStorageItems}`);
    console.log(`  Total Size: ${(cacheManagerStats.totalSize / 1024).toFixed(1)}KB`);
    
    console.log('\n🎯 Hit/Miss Breakdown:');
    console.log(`  Memory Hits: ${stats.memoryHits}`);
    console.log(`  Memory Misses: ${stats.memoryMisses}`);
    console.log(`  LocalStorage Hits: ${stats.localStorageHits}`);
    console.log(`  LocalStorage Misses: ${stats.localStorageMisses}`);
    
    // Performance recommendations
    console.log('\n💡 Performance Recommendations:');
    if (stats.memoryHitRate < 70) {
      console.warn('  - Low memory hit rate - consider increasing cache TTL');
    }
    if (stats.averageResponseTime > 5) {
      console.warn('  - High average response time - localStorage may be slow');
    }
    if (cacheManagerStats.totalSize > 1024 * 1024) { // 1MB
      console.warn('  - Large cache size - consider cleanup or shorter TTLs');
    }
    
    console.groupEnd();
  }

  // Reset stats
  reset() {
    this.stats = {
      memoryHits: 0,
      memoryMisses: 0,
      localStorageHits: 0,
      localStorageMisses: 0,
      totalRequests: 0,
      averageResponseTime: 0
    };
    this.responseTimes = [];
  }
}

// Singleton instance
export const cacheDebugger = new CacheDebugger();

// Enable in development
if (process.env.NODE_ENV === 'development') {
  cacheDebugger.enable();
  
  // Log cache analysis every 30 seconds
  setInterval(() => {
    cacheDebugger.logCacheAnalysis();
  }, 30000);
}

// Global access for debugging
(window as any).cacheDebugger = cacheDebugger;
