/**
 * Preloading utilities for instant page loading
 * Provides intelligent component preloading and prefetching strategies
 */

// Preload component helper - starts fetch immediately but returns lazy-compatible promise
export const preloadComponent = <T>(importer: () => Promise<T>): Promise<T> => {
  // Start the fetch now; return a lazy-compatible promise later if needed
  return importer();
};

// Idle preloading helper - executes function when browser is idle
export const onIdle = (fn: () => void, timeout = 1500) => {
  const ric = (window as any).requestIdleCallback as undefined | ((cb: () => void, opts?: any) => number);
  if (ric) {
    ric(fn, { timeout });
  } else {
    setTimeout(fn, timeout);
  }
};

// Preload map with webpack prefetch hints for optimal chunk loading
export const preloadMap: Record<string, () => Promise<any>> = {
  'dashboard': () => import(/* webpackPrefetch: true */ '../components/Dashboard'),
  'security-events': () => import(/* webpackPrefetch: true */ '../resources/SecurityEvents'),
  'yara-rules': () => import(/* webpackPrefetch: true */ '../resources/MalwareRules'),
  'mitre/techniques': () => import(/* webpackPrefetch: true */ '../resources/MitreTechniques'),
  'system-status': () => import(/* webpackPrefetch: true */ '../resources/SystemStatus'),
  'yara-matches': () => import(/* webpackPrefetch: true */ '../resources/MalwareMatches'),
  'timelines': () => import(/* webpackPrefetch: true */ '../resources/Timelines'),
  'threat-scanner': () => import(/* webpackPrefetch: true */ '../resources/ThreatScanner'),
  'configuration': () => import(/* webpackPrefetch: true */ '../resources/Configuration'),
};

// Bundle loading strategy based on usage patterns
export const bundleStrategy = {
  // Immediate load: Core + High-frequency pages
  immediate: ['dashboard', 'security-events', 'yara-rules'],

  // Hover preload: Medium frequency
  hover: ['mitre/techniques', 'system-status', 'yara-matches'],

  // Lazy load: Low frequency
  lazy: ['threat-scanner', 'configuration', 'timelines']
};

// Performance monitoring utilities
export const performanceMonitor = {
  trackPageLoad: (pageName: string, loadTime: number) => {
    console.log(`[Performance] Page ${pageName} loaded in ${loadTime}ms`);
    // Send to analytics service if available
  },

  trackCacheHit: (resource: string) => {
    console.log(`[Performance] Cache hit for ${resource}`);
    // Track cache effectiveness
  },

  trackPreloadSuccess: (component: string) => {
    console.log(`[Performance] Preloaded ${component} successfully`);
    // Track preloading effectiveness
  },

  trackHoverPreload: (component: string) => {
    console.log(`[Performance] Hover preload triggered for ${component}`);
  }
};

// Navigation prediction based on common user patterns
export const navigationPatterns: Record<string, string[]> = {
  'dashboard': ['security-events', 'yara-rules', 'system-status'], // 80% probability
  'security-events': ['dashboard', 'mitre/techniques', 'yara-matches'], // 70% probability
  'yara-rules': ['yara-matches', 'security-events'], // 75% probability
  'yara-matches': ['yara-rules', 'security-events'], // 70% probability
  'system-status': ['dashboard', 'configuration'], // 65% probability
  'mitre/techniques': ['security-events', 'dashboard'], // 60% probability
};

// Predict next likely navigation based on current page
export const predictNextPages = (currentPage: string): string[] => {
  return navigationPatterns[currentPage] || [];
};

// Connection-aware preloading - adjust based on network conditions
export const shouldPreload = (): boolean => {
  // Check if browser supports connection API
  const connection = (navigator as any).connection || (navigator as any).mozConnection || (navigator as any).webkitConnection;

  if (!connection) {
    // Can't determine connection, default to preloading
    return true;
  }

  // Don't preload on slow connections
  const effectiveType = connection.effectiveType;
  if (effectiveType === 'slow-2g' || effectiveType === '2g') {
    return false;
  }

  // Don't preload if user has data saver enabled
  if (connection.saveData === true) {
    return false;
  }

  return true;
};

// Memory-aware preloading - check available memory
export const hasAdequateMemory = (): boolean => {
  // Check if browser supports memory API
  const memory = (performance as any).memory;

  if (!memory) {
    // Can't determine memory, default to preloading
    return true;
  }

  // Don't preload if memory usage is too high (>80% of heap limit)
  const memoryUsageRatio = memory.usedJSHeapSize / memory.jsHeapSizeLimit;
  return memoryUsageRatio < 0.8;
};

// Smart preloading decision maker
export const canPreload = (): boolean => {
  return shouldPreload() && hasAdequateMemory();
};