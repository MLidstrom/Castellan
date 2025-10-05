import { QueryClient, QueryClientConfig, keepPreviousData } from '@tanstack/react-query';
import { persistQueryClient } from '@tanstack/react-query-persist-client';
import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister';

/**
 * React Query Cache Configuration
 *
 * Centralized cache configuration for all resources in Castellan.
 * This is the SINGLE source of truth for cache TTLs and refetch behavior.
 *
 * Configuration Options:
 * - staleTime: How long data is considered fresh (won't refetch)
 * - gcTime: How long unused data stays in memory
 * - refetchOnWindowFocus: Refetch when user returns to tab
 * - refetchInterval: Background polling interval (false = disabled)
 * - retry: Number of retry attempts on failure
 *
 * @see SimplifiedDataProvider - Handles API calls (no caching)
 * @see REACT_QUERY_CACHING_REFACTOR_PLAN.md - Architecture details
 */

/**
 * Resource-specific cache configuration
 * Define TTL and refetch behavior for each resource type
 */
export const resourceCacheConfig = {
    /**
     * Security Events - Frequently updated, critical data
     * Background polling: Every 30s
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'security-events': {
        staleTime: 15000,           // 15 seconds - data is fresh
        gcTime: 1800000,            // 30 minutes - keep snapshot in memory
        refetchOnWindowFocus: true, // Refetch when tab becomes active
        refetchInterval: 30000,     // Poll every 30 seconds
        retry: 2,
    },

    /**
     * YARA Matches - Real-time malware detections
     * Background polling: Every 60s
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'yara-matches': {
        staleTime: 20000,           // 20 seconds
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: true,
        refetchInterval: 60000,     // Poll every 60 seconds
        retry: 2,
    },

    /**
     * System Status - Real-time health monitoring
     * Background polling: Every 15s (most frequent)
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'system-status': {
        staleTime: 10000,           // 10 seconds
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: true,
        refetchInterval: 15000,     // Poll every 15 seconds
        retry: 3,
    },

    /**
     * Threat Scanner - Active scanning status
     * Background polling: Every 10s
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'threat-scanner': {
        staleTime: 5000,            // 5 seconds
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: true,
        refetchInterval: 10000,     // Poll every 10 seconds
        retry: 3,
    },

    /**
     * YARA Rules - Rarely changes
     * No background polling
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'yara-rules': {
        staleTime: 60000,           // 1 minute
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: false,
        refetchInterval: false,     // No background polling
        retry: 2,
    },

    /**
     * MITRE ATT&CK Techniques - Static data
     * No background polling
     */
    'mitre/techniques': {
        staleTime: 120000,          // 2 minutes
        gcTime: 1800000,         // 30 minutes
        refetchOnWindowFocus: false,
        refetchInterval: false,     // No background polling
        retry: 1,
    },

    /**
     * MITRE Techniques (alternative name)
     */
    'mitre-techniques': {
        staleTime: 120000,          // 2 minutes
        gcTime: 1800000,         // 30 minutes
        refetchOnWindowFocus: false,
        refetchInterval: false,
        retry: 1,
    },

    /**
     * Configuration - Rarely changes
     * No background polling
     */
    'configuration': {
        staleTime: 300000,          // 5 minutes
        gcTime: 1800000,         // 30 minutes
        refetchOnWindowFocus: false,
        refetchInterval: false,     // No background polling
        retry: 2,
    },

    /**
     * Timeline - Aggregated historical data
     * Background polling: Every 60s
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'timeline': {
        staleTime: 30000,           // 30 seconds
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: true,
        refetchInterval: 60000,     // Poll every 60 seconds
        retry: 2,
    },

    /**
     * Timeline Stats - Aggregated metrics
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'timeline/stats': {
        staleTime: 30000,           // 30 seconds
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: true,
        refetchInterval: 60000,     // Poll every 60 seconds
        retry: 2,
    },

    /**
     * Security Event Rules - Detection rules
     * No background polling
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'security-event-rules': {
        staleTime: 60000,           // 1 minute
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: false,
        refetchInterval: false,     // No background polling
        retry: 2,
    },

    /**
     * Dashboard - Consolidated data
     * Background polling: Every 30s
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'dashboard': {
        staleTime: 15000,           // 15 seconds
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: true,
        refetchInterval: 30000,     // Poll every 30 seconds
        retry: 2,
    },

    /**
     * Trend Analysis - Historical trends
     * No background polling (heavy query)
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    'trend-analysis': {
        staleTime: 120000,          // 2 minutes
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: false,
        refetchInterval: false,     // No background polling
        retry: 2,
    },

    /**
     * Default for unlisted resources
     * Snapshot: 30 minutes in memory + localStorage persistence
     */
    default: {
        staleTime: 30000,           // 30 seconds
        gcTime: 1800000,            // 30 minutes - keep snapshot
        refetchOnWindowFocus: true,
        refetchInterval: false,     // No background polling by default
        retry: 2,
    },
} as const;

/**
 * Get cache configuration for a specific resource
 *
 * @param resource - Resource name (e.g., 'security-events')
 * @returns Cache configuration object
 *
 * @example
 * const config = getResourceCacheConfig('security-events');
 * useQuery({ queryKey: [...], queryFn: ..., ...config });
 */
export const getResourceCacheConfig = (resource: string) => {
    return resourceCacheConfig[resource as keyof typeof resourceCacheConfig]
        || resourceCacheConfig.default;
};

/**
 * Create configured QueryClient with smart defaults
 *
 * This creates a single QueryClient instance for the entire app.
 * All queries inherit these default settings unless overridden.
 *
 * @returns Configured QueryClient instance
 */
export const createConfiguredQueryClient = (): QueryClient => {
    const config: QueryClientConfig = {
        defaultOptions: {
            queries: {
                // Global defaults - can be overridden per query
                staleTime: 30000,                    // 30 seconds default
                gcTime: 1800000,                     // 30 minutes default (extended for snapshots)
                refetchOnMount: true,                // Refetch on component mount if stale
                refetchOnWindowFocus: true,          // Refetch when returning to tab
                refetchOnReconnect: true,            // Refetch when internet reconnects
                retry: 2,                            // Retry failed requests twice
                retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000), // Exponential backoff

                // Snapshot mode - always show previous data while fetching
                placeholderData: keepPreviousData,   // Instant navigation, no loading spinners!

                // Network mode
                networkMode: 'online',               // Only fetch when online

                // Error handling (v5: useErrorBoundary renamed to throwOnError)
                throwOnError: false,                 // Don't throw errors to error boundaries
            },
            mutations: {
                retry: 1,                            // Retry mutations once
                retryDelay: 1000,                    // 1 second delay
            },
        },
    };

    return new QueryClient(config);
};

/**
 * Critical resources that require background polling
 *
 * These resources are polled in the background when their pages are active.
 * Used by useBackgroundPolling hook to determine what to poll.
 */
export const CRITICAL_RESOURCES = [
    'security-events',
    'system-status',
    'threat-scanner',
    'yara-matches',
    'timeline',
    'dashboard',
] as const;

/**
 * Check if a resource should have background polling enabled
 *
 * @param resource - Resource name
 * @returns true if resource should be polled
 */
export const shouldPollResource = (resource: string): boolean => {
    return CRITICAL_RESOURCES.includes(resource as any);
};

/**
 * Get recommended prefetch resources based on current page
 *
 * When user is on a page, we can predict what they'll navigate to next
 * and prefetch those resources for instant loading.
 *
 * @param currentResource - Current page resource
 * @returns Array of resources to prefetch
 */
export const getPrefetchRecommendations = (currentResource: string): string[] => {
    const recommendations: Record<string, string[]> = {
        'dashboard': ['security-events', 'yara-rules', 'system-status'],
        'security-events': ['mitre/techniques', 'yara-matches', 'dashboard'],
        'yara-rules': ['yara-matches', 'security-events'],
        'yara-matches': ['yara-rules', 'security-events'],
        'system-status': ['dashboard', 'threat-scanner'],
        'mitre/techniques': ['security-events', 'dashboard'],
        'threat-scanner': ['system-status', 'security-events'],
    };

    return recommendations[currentResource] || [];
};

/**
 * Query key helpers for consistent key generation
 *
 * React Query uses query keys to identify and cache queries.
 * These helpers ensure consistent key format across the app.
 */
export const queryKeys = {
    /**
     * List query key
     * @example queryKeys.list('security-events', { page: 1, perPage: 25 })
     */
    list: (resource: string, params: any) => [resource, 'getList', params],

    /**
     * Single record query key
     * @example queryKeys.one('security-events', { id: '123' })
     */
    one: (resource: string, params: { id: string | number }) => [resource, 'getOne', params],

    /**
     * Many records query key
     * @example queryKeys.many('security-events', { ids: ['1', '2', '3'] })
     */
    many: (resource: string, params: { ids: (string | number)[] }) => [resource, 'getMany', params],

    /**
     * Custom query key
     * @example queryKeys.custom('dashboard', 'consolidated', { timeRange: '24h' })
     */
    custom: (resource: string, operation: string, params?: any) =>
        [resource, 'custom', operation, params].filter(Boolean),

    /**
     * All queries for a resource
     * @example queryKeys.all('security-events')
     */
    all: (resource: string) => [resource],
};

/**
 * Create localStorage persister for cache persistence
 *
 * Persists React Query cache to browser localStorage.
 * Survives page refreshes and browser restarts.
 *
 * @returns Configured storage persister
 */
export const createCachePersister = () => {
    return createSyncStoragePersister({
        storage: window.localStorage,
        key: 'CASTELLAN_CACHE_v1', // Versioned key for cache invalidation on breaking changes
        throttleTime: 1000, // Only save to localStorage once per second (performance)
    });
};

/**
 * Setup cache persistence for QueryClient
 *
 * Call this AFTER creating the QueryClient to enable localStorage persistence.
 * Cache snapshots persist for 24 hours in localStorage.
 *
 * @param queryClient - The QueryClient instance to persist
 *
 * @example
 * const queryClient = createConfiguredQueryClient();
 * setupCachePersistence(queryClient);
 */
export const setupCachePersistence = (queryClient: QueryClient) => {
    const persister = createCachePersister();

    persistQueryClient({
        queryClient,
        persister,
        maxAge: 1000 * 60 * 60 * 24, // 24 hours - cache expires after 1 day in localStorage
        dehydrateOptions: {
            // Don't persist queries with errors
            shouldDehydrateQuery: (query) => {
                return query.state.status === 'success';
            },
        },
    });

    console.log('[CachePersistence] localStorage persistence enabled (24h retention, 5min fresh)');
};

/**
 * Type definitions for better TypeScript support
 */
export type ResourceName = keyof typeof resourceCacheConfig;
export type CacheConfig = typeof resourceCacheConfig[ResourceName];
export type CriticalResource = typeof CRITICAL_RESOURCES[number];
