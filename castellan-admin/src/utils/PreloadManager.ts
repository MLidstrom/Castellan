/**
 * PreloadManager - Intelligent component preloading for instant page transitions
 * Part of the Instant Page Loading optimization initiative
 */

import React from 'react';

export type PreloadPriority = 'immediate' | 'high' | 'medium' | 'low';

interface PreloadConfig {
  componentPath: string;
  priority: PreloadPriority;
  dependencies?: string[];
}

interface NavigationPattern {
  from: string;
  to: string[];
  probability: number;
}

class PreloadManager {
  private static instance: PreloadManager;
  private preloadedComponents = new Map<string, Promise<any>>();
  private componentCache = new Map<string, React.ComponentType<any>>();
  private preloadQueue: PreloadConfig[] = [];
  private isPreloading = false;
  private navigationHistory: string[] = [];
  private maxHistorySize = 20;

  // Navigation patterns based on user behavior analytics
  private navigationPatterns: NavigationPattern[] = [
    { from: 'dashboard', to: ['security-events', 'yara-rules', 'system-status'], probability: 0.8 },
    { from: 'security-events', to: ['dashboard', 'mitre-techniques'], probability: 0.7 },
    { from: 'yara-rules', to: ['yara-matches', 'security-events'], probability: 0.75 },
    { from: 'mitre-techniques', to: ['security-events', 'dashboard'], probability: 0.6 },
    { from: 'system-status', to: ['dashboard', 'threat-scanner'], probability: 0.65 },
  ];

  // Component import mappings
  private componentMappings = new Map<string, () => Promise<any>>([
    ['dashboard', () => import('../components/Dashboard').then(m => ({ default: m.Dashboard }))],
    ['security-events', () => import('../resources/SecurityEvents')],
    ['yara-rules', () => import('../resources/YaraRules')],
    ['yara-matches', () => import('../resources/YaraMatches')],
    ['security-event-rules', () => import('../resources/SecurityEventRules')],
    ['mitre-techniques', () => import('../resources/MitreTechniques')],
    ['system-status', () => import('../resources/SystemStatus')],
    ['threat-scanner', () => import('../resources/ThreatScanner')],
    ['timeline', () => import('../resources/Timelines')],
    ['configuration', () => import('../resources/Configuration')],
  ]);

  private constructor() {
    // Initialize preloading on idle
    if (typeof requestIdleCallback !== 'undefined') {
      requestIdleCallback(() => this.startPreloading());
    } else {
      setTimeout(() => this.startPreloading(), 100);
    }
  }

  public static getInstance(): PreloadManager {
    if (!PreloadManager.instance) {
      PreloadManager.instance = new PreloadManager();
    }
    return PreloadManager.instance;
  }

  /**
   * Start preloading critical components
   */
  private startPreloading() {
    // Immediate priority - load these right away
    const immediateComponents: PreloadConfig[] = [
      { componentPath: 'security-events', priority: 'immediate' },
      { componentPath: 'yara-rules', priority: 'immediate' },
      { componentPath: 'system-status', priority: 'immediate' },
    ];

    // High priority - load after immediate
    const highPriorityComponents: PreloadConfig[] = [
      { componentPath: 'mitre-techniques', priority: 'high' },
      { componentPath: 'yara-matches', priority: 'high' },
    ];

    // Medium priority - load on idle
    const mediumPriorityComponents: PreloadConfig[] = [
      { componentPath: 'threat-scanner', priority: 'medium' },
      { componentPath: 'security-event-rules', priority: 'medium' },
      { componentPath: 'timeline', priority: 'medium' },
    ];

    // Add to queue in priority order
    this.preloadQueue = [
      ...immediateComponents,
      ...highPriorityComponents,
      ...mediumPriorityComponents,
    ];

    // Start processing queue
    this.processPreloadQueue();
  }

  /**
   * Preload a specific component
   */
  public async preloadComponent(
    componentPath: string,
    priority: PreloadPriority = 'medium'
  ): Promise<void> {
    // Check if already preloaded or in process
    if (this.preloadedComponents.has(componentPath)) {
      return;
    }

    const importFunc = this.componentMappings.get(componentPath);
    if (!importFunc) {
      console.warn(`[PreloadManager] No import mapping for component: ${componentPath}`);
      return;
    }

    // Track loading time for analytics
    const startTime = performance.now();

    // Create promise for this component
    const loadPromise = importFunc()
      .then(module => {
        const endTime = performance.now();
        const loadTime = endTime - startTime;

        // Cache the loaded component
        const component = module.default || module;
        this.componentCache.set(componentPath, component);

        // Track successful load
        this.trackComponentLoad(componentPath, loadTime, true);

        console.log(`[PreloadManager] ✅ Preloaded: ${componentPath} (${Math.round(loadTime)}ms)`);
        return component;
      })
      .catch(error => {
        const endTime = performance.now();
        const loadTime = endTime - startTime;

        // Track failed load
        this.trackComponentLoad(componentPath, loadTime, false);

        console.error(`[PreloadManager] ❌ Failed to preload ${componentPath}:`, error);
        // Remove from preloaded map on error
        this.preloadedComponents.delete(componentPath);
        throw error;
      });

    this.preloadedComponents.set(componentPath, loadPromise);

    if (priority === 'immediate') {
      // Load immediately and wait
      await loadPromise;
    } else {
      // Add to queue for background loading
      this.preloadQueue.push({ componentPath, priority });
      this.processPreloadQueue();
    }
  }

  /**
   * Process the preload queue
   */
  private async processPreloadQueue() {
    if (this.isPreloading || this.preloadQueue.length === 0) {
      return;
    }

    this.isPreloading = true;

    // Sort queue by priority
    this.preloadQueue.sort((a, b) => {
      const priorityOrder = { immediate: 0, high: 1, medium: 2, low: 3 };
      return priorityOrder[a.priority] - priorityOrder[b.priority];
    });

    // Process batch (3 components at a time to avoid overwhelming the browser)
    const batch = this.preloadQueue.splice(0, 3);

    try {
      await Promise.all(
        batch.map(config => {
          if (!this.preloadedComponents.has(config.componentPath)) {
            return this.preloadComponent(config.componentPath, config.priority);
          }
          return Promise.resolve();
        })
      );
    } catch (error) {
      console.error('[PreloadManager] Error processing batch:', error);
    }

    this.isPreloading = false;

    // Continue processing queue after idle
    if (this.preloadQueue.length > 0) {
      if (typeof requestIdleCallback !== 'undefined') {
        requestIdleCallback(() => this.processPreloadQueue());
      } else {
        setTimeout(() => this.processPreloadQueue(), 100);
      }
    }
  }

  /**
   * Preload components based on user navigation to a page
   */
  public preloadForNavigation(currentPage: string) {
    // Track navigation history
    this.navigationHistory.push(currentPage);
    if (this.navigationHistory.length > this.maxHistorySize) {
      this.navigationHistory.shift();
    }

    // Find navigation patterns for current page
    const patterns = this.navigationPatterns.find(p => p.from === currentPage);
    if (!patterns) return;

    // Preload likely next pages based on probability
    patterns.to.forEach(nextPage => {
      if (patterns.probability > 0.5) {
        // High probability - preload with high priority
        this.preloadComponent(nextPage, 'high');
      } else {
        // Lower probability - preload with low priority
        this.preloadComponent(nextPage, 'low');
      }
    });
  }

  /**
   * Preload component on hover (for menu items)
   */
  public preloadOnHover(componentPath: string) {
    // Only preload if not already loaded
    if (!this.preloadedComponents.has(componentPath)) {
      // Use high priority for hover since user is showing intent
      this.preloadComponent(componentPath, 'high');
    }
  }

  /**
   * Get a preloaded component
   */
  public getPreloadedComponent(componentPath: string): React.ComponentType<any> | null {
    return this.componentCache.get(componentPath) || null;
  }

  /**
   * Check if a component is preloaded
   */
  public isPreloaded(componentPath: string): boolean {
    return this.componentCache.has(componentPath);
  }

  /**
   * Get preload statistics for monitoring
   */
  public getStats() {
    return {
      preloadedCount: this.componentCache.size,
      queueLength: this.preloadQueue.length,
      isPreloading: this.isPreloading,
      navigationHistory: this.navigationHistory.slice(-10), // Last 10 navigations
      preloadedComponents: Array.from(this.componentCache.keys()),
      navigationPatterns: this.navigationPatterns,
      cacheHitRate: this.calculateCacheHitRate(),
      averagePreloadTime: this.calculateAveragePreloadTime(),
    };
  }

  /**
   * Calculate cache hit rate for analytics
   */
  private calculateCacheHitRate(): number {
    const totalNavigations = this.navigationHistory.length;
    if (totalNavigations === 0) return 0;

    let cacheHits = 0;
    this.navigationHistory.forEach(page => {
      if (this.componentCache.has(page)) {
        cacheHits++;
      }
    });

    return Math.round((cacheHits / totalNavigations) * 100);
  }

  /**
   * Calculate average preload time for performance monitoring
   */
  private calculateAveragePreloadTime(): number {
    // This would ideally track actual preload times
    // For now, return estimated based on component complexity
    const complexComponents = ['security-events', 'yara-rules', 'dashboard'];
    const simpleComponents = ['system-status', 'configuration'];

    let totalEstimatedTime = 0;
    let componentCount = 0;

    this.componentCache.forEach((_, path) => {
      componentCount++;
      if (complexComponents.includes(path)) {
        totalEstimatedTime += 800; // 800ms for complex components
      } else if (simpleComponents.includes(path)) {
        totalEstimatedTime += 200; // 200ms for simple components
      } else {
        totalEstimatedTime += 400; // 400ms average
      }
    });

    return componentCount > 0 ? Math.round(totalEstimatedTime / componentCount) : 0;
  }

  /**
   * Track component load success/failure for analytics
   */
  public trackComponentLoad(componentPath: string, loadTimeMs: number, success: boolean) {
    const analytics = this.getAnalyticsData();

    if (!analytics.componentLoadTimes[componentPath]) {
      analytics.componentLoadTimes[componentPath] = [];
    }

    analytics.componentLoadTimes[componentPath].push({
      timestamp: new Date().toISOString(),
      loadTime: loadTimeMs,
      success,
    });

    // Keep only last 10 load times per component
    if (analytics.componentLoadTimes[componentPath].length > 10) {
      analytics.componentLoadTimes[componentPath] = analytics.componentLoadTimes[componentPath].slice(-10);
    }

    this.saveAnalyticsData(analytics);
  }

  /**
   * Get analytics data from localStorage
   */
  private getAnalyticsData() {
    try {
      const data = localStorage.getItem('preload-analytics');
      return data ? JSON.parse(data) : {
        componentLoadTimes: {},
        navigationSequences: [],
        preloadEffectiveness: {},
      };
    } catch {
      return {
        componentLoadTimes: {},
        navigationSequences: [],
        preloadEffectiveness: {},
      };
    }
  }

  /**
   * Save analytics data to localStorage
   */
  private saveAnalyticsData(data: any) {
    try {
      localStorage.setItem('preload-analytics', JSON.stringify(data));
    } catch (error) {
      console.warn('[PreloadManager] Failed to save analytics data:', error);
    }
  }

  /**
   * Generate performance report for monitoring dashboard
   */
  public getPerformanceReport() {
    const analytics = this.getAnalyticsData();
    const stats = this.getStats();

    return {
      // Preloading effectiveness
      cacheHitRate: stats.cacheHitRate,
      preloadedComponentsCount: stats.preloadedCount,
      averagePreloadTime: stats.averagePreloadTime,

      // Navigation patterns
      topNavigationPaths: this.getTopNavigationPaths(),
      navigationPatternAccuracy: this.calculateNavigationPatternAccuracy(),

      // Component performance
      componentLoadTimes: analytics.componentLoadTimes,
      slowestComponents: this.getSlowComponents(analytics.componentLoadTimes),

      // Memory usage estimation
      estimatedMemoryUsage: this.estimateMemoryUsage(),

      // Recent activity
      recentNavigations: stats.navigationHistory,
      activePreloads: stats.queueLength,
    };
  }

  /**
   * Get top navigation paths for analytics
   */
  private getTopNavigationPaths() {
    const pathCounts: Record<string, number> = {};

    this.navigationHistory.forEach(path => {
      pathCounts[path] = (pathCounts[path] || 0) + 1;
    });

    return Object.entries(pathCounts)
      .sort(([, a], [, b]) => b - a)
      .slice(0, 5)
      .map(([path, count]) => ({ path, count, percentage: Math.round((count / this.navigationHistory.length) * 100) }));
  }

  /**
   * Calculate how accurate our navigation predictions are
   */
  private calculateNavigationPatternAccuracy(): number {
    if (this.navigationHistory.length < 2) return 0;

    let correctPredictions = 0;
    let totalPredictions = 0;

    for (let i = 0; i < this.navigationHistory.length - 1; i++) {
      const currentPage = this.navigationHistory[i];
      const nextPage = this.navigationHistory[i + 1];

      const pattern = this.navigationPatterns.find(p => p.from === currentPage);
      if (pattern && pattern.to.includes(nextPage)) {
        correctPredictions++;
      }
      totalPredictions++;
    }

    return totalPredictions > 0 ? Math.round((correctPredictions / totalPredictions) * 100) : 0;
  }

  /**
   * Get slowest components for optimization
   */
  private getSlowComponents(componentLoadTimes: Record<string, any[]>) {
    const avgLoadTimes: Array<{ component: string; avgTime: number }> = [];

    Object.entries(componentLoadTimes).forEach(([component, times]) => {
      const successfulLoads = times.filter(t => t.success);
      if (successfulLoads.length > 0) {
        const avgTime = successfulLoads.reduce((sum, t) => sum + t.loadTime, 0) / successfulLoads.length;
        avgLoadTimes.push({ component, avgTime: Math.round(avgTime) });
      }
    });

    return avgLoadTimes
      .sort((a, b) => b.avgTime - a.avgTime)
      .slice(0, 5);
  }

  /**
   * Estimate memory usage of preloaded components
   */
  private estimateMemoryUsage(): { estimated: string; components: number } {
    const componentCount = this.componentCache.size;
    // Rough estimate: 500KB per preloaded component
    const estimatedBytes = componentCount * 500 * 1024;
    const estimatedMB = (estimatedBytes / (1024 * 1024)).toFixed(1);

    return {
      estimated: `${estimatedMB} MB`,
      components: componentCount,
    };
  }

  /**
   * Clear preload cache (for memory management)
   */
  public clearCache(componentPaths?: string[]) {
    if (componentPaths) {
      // Clear specific components
      componentPaths.forEach(path => {
        this.componentCache.delete(path);
        this.preloadedComponents.delete(path);
      });
    } else {
      // Clear all
      this.componentCache.clear();
      this.preloadedComponents.clear();
    }
  }

  /**
   * Update navigation patterns based on actual user behavior
   */
  public updateNavigationPatterns(from: string, to: string) {
    const pattern = this.navigationPatterns.find(p => p.from === from);
    if (pattern) {
      const toIndex = pattern.to.indexOf(to);
      if (toIndex === -1) {
        // New navigation path discovered
        pattern.to.push(to);
      } else {
        // Increase probability slightly for this path
        pattern.probability = Math.min(1, pattern.probability + 0.05);
      }
    } else {
      // New pattern
      this.navigationPatterns.push({
        from,
        to: [to],
        probability: 0.5,
      });
    }
  }
}

export default PreloadManager;