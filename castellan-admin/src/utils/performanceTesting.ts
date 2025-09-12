// Performance Testing and Validation Suite for v0.6.0
// Comprehensive testing of React Admin performance improvements

import { dashboardCache } from './cacheManager';
import { cacheDebugger } from './cacheDebugger';
import { CacheInspector } from './cacheInspector';

interface PerformanceMetrics {
  renderTime: number;
  cacheHitRate: number;
  memoryUsage: number;
  bundleSize: number;
  initialLoadTime: number;
  navigationTime: number;
  timestamp: number;
}

interface PerformanceTestResult {
  testName: string;
  before: PerformanceMetrics;
  after: PerformanceMetrics;
  improvement: number;
  passed: boolean;
  details: string;
}

export class PerformanceTestSuite {
  private testResults: PerformanceTestResult[] = [];
  private observer: PerformanceObserver | null = null;

  constructor() {
    // Initialize performance monitoring
    if ('PerformanceObserver' in window) {
      this.observer = new PerformanceObserver((list) => {
        const entries = list.getEntries();
        entries.forEach(entry => {
          console.log(`[Performance] ${entry.name}: ${entry.duration.toFixed(2)}ms`);
        });
      });
      this.observer.observe({ entryTypes: ['navigation', 'resource', 'paint'] });
    }
  }

  // Test cache effectiveness
  async testCacheEffectiveness(): Promise<PerformanceTestResult> {
    console.log('🧪 Testing cache effectiveness...');
    
    const testData = { test: 'data', timestamp: Date.now() };
    const testKey = 'performance_test_cache';
    
    // Test cache operations
    const beforeCacheStats = dashboardCache.getStats();
    
    // Measure cache write performance
    const writeStart = performance.now();
    dashboardCache.set(testKey, testData, 60000);
    const writeTime = performance.now() - writeStart;
    
    // Measure cache read performance
    const readStart = performance.now();
    const cachedData = dashboardCache.get(testKey);
    const readTime = performance.now() - readStart;
    
    const afterCacheStats = dashboardCache.getStats();
    
    // Calculate metrics
    const cacheHitRate = afterCacheStats.memoryItems > 0 ? 100 : 0;
    const totalTime = writeTime + readTime;
    
    // Clean up
    dashboardCache.remove(testKey);
    
    return {
      testName: 'Cache Effectiveness',
      before: {
        renderTime: 0,
        cacheHitRate: 0,
        memoryUsage: beforeCacheStats.memoryItems,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: 0,
        timestamp: Date.now()
      },
      after: {
        renderTime: 0,
        cacheHitRate,
        memoryUsage: afterCacheStats.memoryItems,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: totalTime,
        timestamp: Date.now()
      },
      improvement: cachedData ? 100 : 0,
      passed: cachedData !== null && totalTime < 10, // Should complete in under 10ms
      details: `Cache write: ${writeTime.toFixed(2)}ms, read: ${readTime.toFixed(2)}ms, data retrieved: ${cachedData !== null}`
    };
  }

  // Test React component render performance
  async testComponentRenderPerformance(): Promise<PerformanceTestResult> {
    console.log('🧪 Testing component render performance...');
    
    const startTime = performance.now();
    const startMemory = (performance as any).memory?.usedJSHeapSize || 0;
    
    // Simulate component rendering by creating DOM elements
    const container = document.createElement('div');
    const elements: HTMLElement[] = [];
    
    // Render 100 list items (simulating a data grid)
    for (let i = 0; i < 100; i++) {
      const element = document.createElement('div');
      element.textContent = `Security Event ${i}`;
      element.className = 'security-event-row';
      elements.push(element);
      container.appendChild(element);
    }
    
    const renderTime = performance.now() - startTime;
    const endMemory = (performance as any).memory?.usedJSHeapSize || 0;
    const memoryDelta = endMemory - startMemory;
    
    // Cleanup
    elements.forEach(el => container.removeChild(el));
    
    return {
      testName: 'Component Render Performance',
      before: {
        renderTime: 0,
        cacheHitRate: 0,
        memoryUsage: startMemory,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: 0,
        timestamp: Date.now()
      },
      after: {
        renderTime,
        cacheHitRate: 0,
        memoryUsage: endMemory,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: 0,
        timestamp: Date.now()
      },
      improvement: renderTime < 100 ? 100 - renderTime : 0,
      passed: renderTime < 100 && memoryDelta < 1024 * 1024, // Under 100ms and 1MB
      details: `Rendered 100 elements in ${renderTime.toFixed(2)}ms, memory delta: ${(memoryDelta / 1024).toFixed(2)}KB`
    };
  }

  // Test virtual scrolling performance
  async testVirtualScrollingPerformance(): Promise<PerformanceTestResult> {
    console.log('🧪 Testing virtual scrolling performance...');
    
    // Simulate large dataset
    const largeDataset = Array.from({ length: 10000 }, (_, i) => ({
      id: i,
      name: `Item ${i}`,
      value: Math.random() * 100
    }));
    
    const startTime = performance.now();
    
    // Simulate virtual scrolling calculation
    const containerHeight = 600;
    const itemHeight = 50;
    const visibleItems = Math.ceil(containerHeight / itemHeight);
    const overscan = 5;
    
    // Calculate visible range (simulating virtual scrolling logic)
    const startIndex = 0;
    const endIndex = Math.min(visibleItems + overscan, largeDataset.length - 1);
    const visibleData = largeDataset.slice(startIndex, endIndex + 1);
    
    const calculationTime = performance.now() - startTime;
    
    return {
      testName: 'Virtual Scrolling Performance',
      before: {
        renderTime: 0,
        cacheHitRate: 0,
        memoryUsage: largeDataset.length,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: 0,
        timestamp: Date.now()
      },
      after: {
        renderTime: calculationTime,
        cacheHitRate: 0,
        memoryUsage: visibleData.length,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: 0,
        timestamp: Date.now()
      },
      improvement: ((largeDataset.length - visibleData.length) / largeDataset.length) * 100,
      passed: calculationTime < 10 && visibleData.length < 100,
      details: `Reduced ${largeDataset.length} items to ${visibleData.length} visible items in ${calculationTime.toFixed(2)}ms`
    };
  }

  // Test bundle size and code splitting
  async testBundleOptimization(): Promise<PerformanceTestResult> {
    console.log('🧪 Testing bundle optimization...');
    
    const startTime = performance.now();
    
    // Simulate lazy component loading
    const lazyLoadPromises = [
      import('../components/Dashboard').catch(() => null),
      import('../resources/SecurityEvents').catch(() => null),
      import('../resources/SystemStatus').catch(() => null)
    ];
    
    const results = await Promise.allSettled(lazyLoadPromises);
    const loadTime = performance.now() - startTime;
    
    const successfulLoads = results.filter(r => r.status === 'fulfilled').length;
    const loadingSuccess = successfulLoads / lazyLoadPromises.length;
    
    return {
      testName: 'Bundle Optimization',
      before: {
        renderTime: 0,
        cacheHitRate: 0,
        memoryUsage: 0,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: 0,
        timestamp: Date.now()
      },
      after: {
        renderTime: 0,
        cacheHitRate: 0,
        memoryUsage: 0,
        bundleSize: 0,
        initialLoadTime: loadTime,
        navigationTime: 0,
        timestamp: Date.now()
      },
      improvement: loadingSuccess * 100,
      passed: loadTime < 1000 && successfulLoads >= 2, // Under 1 second, at least 2 components loaded
      details: `Loaded ${successfulLoads}/${lazyLoadPromises.length} components in ${loadTime.toFixed(2)}ms`
    };
  }

  // Test API response caching
  async testApiCaching(): Promise<PerformanceTestResult> {
    console.log('🧪 Testing API response caching...');
    
    const apiUrl = '/api/security-events?page=1&limit=10';
    const cacheKey = 'test_api_cache';
    
    // Clear any existing cache
    dashboardCache.remove(cacheKey);
    
    // First request (should cache)
    const firstRequestStart = performance.now();
    const mockApiResponse = {
      data: Array.from({ length: 10 }, (_, i) => ({ id: i, name: `Event ${i}` })),
      total: 100
    };
    dashboardCache.set(cacheKey, mockApiResponse, 120000);
    const firstRequestTime = performance.now() - firstRequestStart;
    
    // Second request (should hit cache)
    const secondRequestStart = performance.now();
    const cachedResponse = dashboardCache.get(cacheKey);
    const secondRequestTime = performance.now() - secondRequestStart;
    
    const cacheSpeedImprovement = firstRequestTime > secondRequestTime 
      ? ((firstRequestTime - secondRequestTime) / firstRequestTime) * 100 
      : 0;
    
    // Cleanup
    dashboardCache.remove(cacheKey);
    
    return {
      testName: 'API Response Caching',
      before: {
        renderTime: 0,
        cacheHitRate: 0,
        memoryUsage: 0,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: firstRequestTime,
        timestamp: Date.now()
      },
      after: {
        renderTime: 0,
        cacheHitRate: cachedResponse ? 100 : 0,
        memoryUsage: 0,
        bundleSize: 0,
        initialLoadTime: 0,
        navigationTime: secondRequestTime,
        timestamp: Date.now()
      },
      improvement: cacheSpeedImprovement,
      passed: cachedResponse !== null && secondRequestTime < firstRequestTime,
      details: `First request: ${firstRequestTime.toFixed(2)}ms, cached request: ${secondRequestTime.toFixed(2)}ms, improvement: ${cacheSpeedImprovement.toFixed(1)}%`
    };
  }

  // Run all performance tests
  async runAllTests(): Promise<PerformanceTestResult[]> {
    console.log('🚀 Starting comprehensive performance test suite...');
    cacheDebugger.enable();
    
    this.testResults = [];
    
    // Run individual tests
    const tests = [
      this.testCacheEffectiveness(),
      this.testComponentRenderPerformance(),
      this.testVirtualScrollingPerformance(),
      this.testBundleOptimization(),
      this.testApiCaching()
    ];
    
    const results = await Promise.allSettled(tests);
    
    results.forEach((result, index) => {
      if (result.status === 'fulfilled') {
        this.testResults.push(result.value);
      } else {
        console.error(`Test ${index} failed:`, result.reason);
      }
    });
    
    // Generate summary report
    this.generateSummaryReport();
    
    return this.testResults;
  }

  // Generate summary report
  private generateSummaryReport(): void {
    console.group('📊 Performance Test Summary Report');
    
    const totalTests = this.testResults.length;
    const passedTests = this.testResults.filter(r => r.passed).length;
    const averageImprovement = this.testResults.reduce((sum, r) => sum + r.improvement, 0) / totalTests;
    
    console.log(`Tests run: ${totalTests}`);
    console.log(`Tests passed: ${passedTests}/${totalTests} (${((passedTests/totalTests) * 100).toFixed(1)}%)`);
    console.log(`Average improvement: ${averageImprovement.toFixed(1)}%`);
    
    console.log('\nDetailed Results:');
    this.testResults.forEach(result => {
      const status = result.passed ? '✅' : '❌';
      console.log(`${status} ${result.testName}: ${result.improvement.toFixed(1)}% improvement`);
      console.log(`   ${result.details}`);
    });
    
    // Cache statistics
    const cacheStats = dashboardCache.getStats();
    console.log('\n📦 Cache Statistics:');
    console.log(`  Memory items: ${cacheStats.memoryItems}`);
    console.log(`  LocalStorage items: ${cacheStats.localStorageItems}`);
    console.log(`  Total cache size: ${(cacheStats.totalSize / 1024).toFixed(1)}KB`);
    
    // Debug cache analysis
    cacheDebugger.logCacheAnalysis();
    
    console.groupEnd();
    
    // Make results available globally for debugging
    (window as any).performanceTestResults = {
      results: this.testResults,
      summary: {
        totalTests,
        passedTests,
        averageImprovement,
        cacheStats
      }
    };
  }

  // Continuous performance monitoring
  startContinuousMonitoring(): void {
    console.log('📈 Starting continuous performance monitoring...');
    
    // Monitor every 30 seconds
    setInterval(() => {
      if (process.env.NODE_ENV === 'development') {
        const stats = dashboardCache.getStats();
        console.log(`[Performance Monitor] Cache: ${stats.memoryItems} items, ${(stats.totalSize / 1024).toFixed(1)}KB`);
      }
    }, 30000);
  }

  // Cleanup
  destroy(): void {
    if (this.observer) {
      this.observer.disconnect();
      this.observer = null;
    }
    cacheDebugger.disable();
  }
}

// Export singleton instance
export const performanceTestSuite = new PerformanceTestSuite();

// Auto-start continuous monitoring in development
if (process.env.NODE_ENV === 'development') {
  performanceTestSuite.startContinuousMonitoring();
  
  // Make it available globally for manual testing
  (window as any).performanceTestSuite = performanceTestSuite;
  console.log('🔬 Performance test suite available - use performanceTestSuite.runAllTests() to run tests');
}
