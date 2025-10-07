import * as React from 'react';
import { useEffect } from 'react';
import { Menu, useDataProvider } from 'react-admin';
import { useQueryClient } from '@tanstack/react-query';
import {
  preloadMap,
  onIdle,
  bundleStrategy,
  performanceMonitor,
  predictNextPages,
  canPreload
} from '../utils/preload';
import { getResourceCacheConfig, queryKeys } from '../config/reactQueryConfig';
import DashboardIcon from '@mui/icons-material/Dashboard';
import SecurityIcon from '@mui/icons-material/Security';
import BugReportIcon from '@mui/icons-material/BugReport';
import RuleIcon from '@mui/icons-material/Rule';
import FindInPageIcon from '@mui/icons-material/FindInPage';
import CalendarMonthIcon from '@mui/icons-material/CalendarMonth';
import MonitorHeartIcon from '@mui/icons-material/MonitorHeart';
import RadarIcon from '@mui/icons-material/Radar';
import SettingsIcon from '@mui/icons-material/Settings';
import PolicyIcon from '@mui/icons-material/Policy';

// Data prefetchers for each page - using centralized React Query config
const dataPrefetchers: Record<string, (q: ReturnType<typeof useQueryClient>, dp: any) => void> = {
  'dashboard': (q, dp) => {
    const config = getResourceCacheConfig('dashboard');
    q.prefetchQuery({
      queryKey: queryKeys.custom('dashboard', 'consolidated', { timeRange: '24h' }),
      queryFn: () => dp.custom({ url: 'dashboarddata/consolidated?timeRange=24h', method: 'GET' }),
      ...config, // Apply centralized config
    });
  },
  'security-events': (q, dp) => {
    const config = getResourceCacheConfig('security-events');
    const params = {
      pagination: { page: 1, perPage: 25 },
      sort: { field: 'timestamp', order: 'DESC' },
      filter: {}
    };
    q.prefetchQuery({
      queryKey: queryKeys.list('security-events', params),
      queryFn: () => dp.getList('security-events', params),
      ...config, // Same config as the actual page uses
    });
  },
  'yara-rules': (q, dp) => {
    const config = getResourceCacheConfig('yara-rules');
    const params = {
      pagination: { page: 1, perPage: 25 },
      sort: { field: 'updatedAt', order: 'DESC' },
      filter: {}
    };
    q.prefetchQuery({
      queryKey: queryKeys.list('yara-rules', params),
      queryFn: () => dp.getList('yara-rules', params),
      ...config,
    });
  },
  'system-status': (q, dp) => {
    const config = getResourceCacheConfig('system-status');
    q.prefetchQuery({
      queryKey: queryKeys.custom('system-status', 'current'),
      queryFn: () => dp.custom({ url: 'system-status', method: 'GET' }),
      ...config,
    });
  },
  'mitre/techniques': (q, dp) => {
    const config = getResourceCacheConfig('mitre/techniques');
    const params = {
      pagination: { page: 1, perPage: 25 },
      sort: { field: 'techniqueId', order: 'ASC' },
      filter: {}
    };
    q.prefetchQuery({
      queryKey: queryKeys.list('mitre/techniques', params),
      queryFn: () => dp.getList('mitre/techniques', params),
      ...config,
    });
  },
  'yara-matches': (q, dp) => {
    const config = getResourceCacheConfig('yara-matches');
    const params = {
      pagination: { page: 1, perPage: 25 },
      sort: { field: 'detectedAt', order: 'DESC' },
      filter: {}
    };
    q.prefetchQuery({
      queryKey: queryKeys.list('yara-matches', params),
      queryFn: () => dp.getList('yara-matches', params),
      ...config,
    });
  },
  'threat-scanner': (q, dp) => {
    const config = getResourceCacheConfig('threat-scanner');
    q.prefetchQuery({
      queryKey: queryKeys.custom('threat-scanner', 'status'),
      queryFn: () => dp.custom({ url: 'threat-scanner/status', method: 'GET' }),
      ...config,
    });
  },
  'configuration': (q, dp) => {
    const config = getResourceCacheConfig('configuration');
    q.prefetchQuery({
      queryKey: queryKeys.one('configuration', { id: 'threat-intelligence' }),
      queryFn: async () => {
        // Use getOne to hit the correct mapped endpoint
        const res = await dp.getOne('configuration', { id: 'threat-intelligence' });
        return res;
      },
      ...config,
    });
  },
  'security-event-rules': (q, dp) => {
    const config = getResourceCacheConfig('security-event-rules');
    const params = {
      pagination: { page: 1, perPage: 25 },
      sort: { field: 'eventId', order: 'ASC' },
      filter: {}
    };
    q.prefetchQuery({
      queryKey: queryKeys.list('security-event-rules', params),
      queryFn: () => dp.getList('security-event-rules', params),
      ...config,
    });
  },
};

export const MenuWithPreloading: React.FC = () => {
  const queryClient = useQueryClient();
  const dataProvider = useDataProvider();
  const preloadManager = PreloadManager.getInstance();

  // Initialize preloading on menu mount
  useEffect(() => {
    console.log('[MenuWithPreloading] Initializing component preloading...');

    // Track navigation patterns and start preloading
    const currentPath = window.location.pathname.replace('/#', '') || '/';
    preloadManager.trackNavigation(currentPath);

    // Only preload if conditions are favorable
    if (!canPreload()) {
      console.log('[MenuWithPreloading] Preloading disabled due to network/memory conditions');
      return;
    }

    // Warm critical pages after app settles
    onIdle(() => {
      console.log('[MenuWithPreloading] Starting idle preload of critical pages...');

      bundleStrategy.immediate.forEach(pageId => {
        const preloader = preloadMap[pageId];
        if (preloader) {
          preloader()
            .then(() => performanceMonitor.trackPreloadSuccess(pageId))
            .catch(err => console.error(`Failed to preload ${pageId}:`, err));
        }

        // Prefetch data for immediate pages
        const dataPrefetcher = dataPrefetchers[pageId];
        if (dataPrefetcher) {
          try {
            dataPrefetcher(queryClient, dataProvider);
          } catch (err) {
            console.error(`Failed to prefetch data for ${pageId}:`, err);
          }
        }
      });
    }, 1200);

    // Preload predicted pages based on current page
    const predictedPages = predictNextPages(currentPath.substring(1));
    predictedPages.forEach((pageId, index) => {
      // Stagger preloading to avoid overwhelming the browser
      setTimeout(() => {
        const preloader = preloadMap[pageId];
        if (preloader && canPreload()) {
          preloader()
            .then(() => console.log(`[MenuWithPreloading] Predictively preloaded ${pageId}`))
            .catch(err => console.error(`Failed to predictively preload ${pageId}:`, err));
        }
      }, 2000 + (index * 500)); // Start after 2s, then 500ms apart
    });

    return () => {
      preloadManager.cleanup();
    };
  }, [queryClient, dataProvider, preloadManager]);

  const handleHover = (pageId: string) => {
    if (!canPreload()) return;

    performanceMonitor.trackHoverPreload(pageId);

    // Preload component
    const preloader = preloadMap[pageId];
    if (preloader) {
      preloader().catch(err => console.error(`Hover preload failed for ${pageId}:`, err));
    }

    // Prefetch data
    const dataPrefetcher = dataPrefetchers[pageId];
    if (dataPrefetcher) {
      try {
        dataPrefetcher(queryClient, dataProvider);
      } catch (err) {
        console.error(`Hover data prefetch failed for ${pageId}:`, err);
      }
    }

    // Track hover for predictive preloading
    preloadManager.trackHover(pageId);
  };

  return (
    <Menu>
      <Menu.Item
        to="/"
        primaryText="Dashboard"
        leftIcon={<DashboardIcon />}
        onMouseEnter={() => handleHover('dashboard')}
      />
      <Menu.Item
        to="/security-events"
        primaryText="Security Events"
        leftIcon={<SecurityIcon />}
        onMouseEnter={() => handleHover('security-events')}
      />
      <Menu.Item
        to="/security-event-rules"
        primaryText="Security Event Rules"
        leftIcon={<PolicyIcon />}
        onMouseEnter={() => handleHover('security-event-rules')}
      />
      <Menu.Item
        to="/mitre-techniques"
        primaryText="MITRE Techniques"
        leftIcon={<BugReportIcon />}
        onMouseEnter={() => handleHover('mitre/techniques')}
      />
      <Menu.Item
        to="/yara-rules"
        primaryText="YARA Rules"
        leftIcon={<RuleIcon />}
        onMouseEnter={() => handleHover('yara-rules')}
      />
      <Menu.Item
        to="/yara-matches"
        primaryText="YARA Matches"
        leftIcon={<FindInPageIcon />}
        onMouseEnter={() => handleHover('yara-matches')}
      />
      <Menu.Item
        to="/timeline"
        primaryText="Timeline"
        leftIcon={<CalendarMonthIcon />}
        onMouseEnter={() => handleHover('timeline')}
      />
      <Menu.Item
        to="/system-status"
        primaryText="System Status"
        leftIcon={<MonitorHeartIcon />}
        onMouseEnter={() => handleHover('system-status')}
      />
      <Menu.Item
        to="/threat-scanner"
        primaryText="Threat Scanner"
        leftIcon={<RadarIcon />}
        onMouseEnter={() => handleHover('threat-scanner')}
      />
      <Menu.Item
        to="/configuration"
        primaryText="Configuration"
        leftIcon={<SettingsIcon />}
        onMouseEnter={() => handleHover('configuration')}
      />
    </Menu>
  );
};

// Singleton preload manager for tracking navigation patterns and optimizing preloading
class PreloadManager {
  private static instance: PreloadManager;
  private navigationHistory: string[] = [];
  private hoverHistory: Map<string, number> = new Map();
  private preloadedComponents: Set<string> = new Set();

  private constructor() {}

  public static getInstance(): PreloadManager {
    if (!PreloadManager.instance) {
      PreloadManager.instance = new PreloadManager();
    }
    return PreloadManager.instance;
  }

  public trackNavigation(path: string) {
    this.navigationHistory.push(path);
    // Keep only last 20 navigations
    if (this.navigationHistory.length > 20) {
      this.navigationHistory.shift();
    }
  }

  public trackHover(pageId: string) {
    const count = this.hoverHistory.get(pageId) || 0;
    this.hoverHistory.set(pageId, count + 1);

    // If hovered multiple times, add to preload queue
    if (count >= 2 && !this.preloadedComponents.has(pageId)) {
      this.preloadedComponents.add(pageId);
      console.log(`[PreloadManager] Adding ${pageId} to priority preload due to frequent hovers`);
    }
  }

  public getNavigationPatterns(): Record<string, number> {
    const patterns: Record<string, number> = {};

    for (let i = 0; i < this.navigationHistory.length - 1; i++) {
      const from = this.navigationHistory[i];
      const to = this.navigationHistory[i + 1];
      const key = `${from}->${to}`;
      patterns[key] = (patterns[key] || 0) + 1;
    }

    return patterns;
  }

  public cleanup() {
    // Clean up old entries
    this.navigationHistory = this.navigationHistory.slice(-10);
  }
}

export default MenuWithPreloading;