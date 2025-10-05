import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useDataProvider } from 'react-admin';
import { CRITICAL_RESOURCES, getResourceCacheConfig } from '../config/reactQueryConfig';

/**
 * Background Polling Hook
 *
 * Enables automatic background polling for specified resources.
 * Only polls when user is authenticated and online to avoid wasted API calls.
 *
 * Features:
 * - Respects resource-specific refetchInterval from reactQueryConfig
 * - Automatically stops polling on unmount
 * - Only refetches active queries (currently visible on screen)
 * - Authentication and online status checks
 *
 * @param resources - Array of resource names to poll
 *
 * @example
 * // In a component
 * useBackgroundPolling(['security-events', 'system-status']);
 *
 * @see reactQueryConfig.ts for polling intervals
 */
export const useBackgroundPolling = (resources: string[]) => {
    const queryClient = useQueryClient();
    const dataProvider = useDataProvider();

    useEffect(() => {
        const intervals: NodeJS.Timeout[] = [];

        resources.forEach(resource => {
            const config = getResourceCacheConfig(resource);

            // Only poll if configured with a refetchInterval
            if (typeof config.refetchInterval !== 'number') {
                console.log(`[BackgroundPolling] Skipping ${resource} (no refetchInterval configured)`);
                return;
            }

            console.log(`[BackgroundPolling] Starting for ${resource} (interval: ${config.refetchInterval}ms)`);

            const interval = setInterval(() => {
                // Only refetch if user is authenticated
                const isAuthenticated = localStorage.getItem('auth_token');
                if (!isAuthenticated) {
                    console.log(`[BackgroundPolling] Skipping ${resource} (not authenticated)`);
                    return;
                }

                // Only refetch if online
                if (!navigator.onLine) {
                    console.log(`[BackgroundPolling] Skipping ${resource} (offline)`);
                    return;
                }

                console.log(`[BackgroundPolling] Refetching ${resource}`);

                // Refetch all queries for this resource
                // Using 'active' type means only queries currently rendered on screen will refetch
                queryClient.refetchQueries({
                    queryKey: [resource],
                    type: 'active', // Only refetch active queries
                });
            }, config.refetchInterval as number);

            intervals.push(interval);
        });

        // Cleanup: Stop all polling intervals on unmount
        return () => {
            intervals.forEach(interval => clearInterval(interval));
            console.log(`[BackgroundPolling] Stopped ${intervals.length} polling intervals`);
        };
    }, [resources, queryClient, dataProvider]);
};

/**
 * Auto Background Polling Hook
 *
 * Automatically polls ALL critical resources defined in reactQueryConfig.
 * Use this in top-level components (App.tsx or Dashboard) for global polling.
 *
 * Critical resources (as of v0.7.1):
 * - security-events (30s interval)
 * - system-status (15s interval)
 * - threat-scanner (10s interval)
 * - yara-matches (60s interval)
 * - timeline (60s interval)
 * - dashboard (30s interval)
 *
 * @example
 * // In Dashboard component
 * export const Dashboard = () => {
 *   useAutoBackgroundPolling();
 *   return <div>Dashboard content</div>;
 * };
 */
export const useAutoBackgroundPolling = () => {
    useBackgroundPolling([...CRITICAL_RESOURCES]);
};

/**
 * Smart Background Polling Hook
 *
 * Only polls resources if they have active queries.
 * More efficient than useBackgroundPolling for conditional polling.
 *
 * @param resources - Array of resource names to poll
 * @param enabled - Whether polling is enabled (default: true)
 *
 * @example
 * // Only poll when dashboard is visible
 * const [isDashboardVisible, setIsDashboardVisible] = useState(true);
 * useSmartBackgroundPolling(['dashboard'], isDashboardVisible);
 */
export const useSmartBackgroundPolling = (resources: string[], enabled: boolean = true) => {
    const queryClient = useQueryClient();

    useEffect(() => {
        if (!enabled) {
            return;
        }

        const intervals: NodeJS.Timeout[] = [];

        resources.forEach(resource => {
            const config = getResourceCacheConfig(resource);

            if (typeof config.refetchInterval !== 'number') {
                return;
            }

            const interval = setInterval(() => {
                const isAuthenticated = localStorage.getItem('auth_token');
                if (!isAuthenticated || !navigator.onLine) {
                    return;
                }

                // Check if there are any active queries for this resource
                const activeQueries = queryClient.getQueriesData({ queryKey: [resource] });
                if (activeQueries.length === 0) {
                    console.log(`[SmartBackgroundPolling] Skipping ${resource} (no active queries)`);
                    return;
                }

                console.log(`[SmartBackgroundPolling] Refetching ${resource} (${activeQueries.length} active queries)`);

                queryClient.refetchQueries({
                    queryKey: [resource],
                    type: 'active',
                });
            }, config.refetchInterval as number);

            intervals.push(interval);
        });

        return () => {
            intervals.forEach(interval => clearInterval(interval));
        };
    }, [resources, enabled, queryClient]);
};

/**
 * Page-Specific Background Polling Hook
 *
 * Polls only resources relevant to the current page.
 * Automatically determines what to poll based on page name.
 *
 * @param pageName - Current page identifier
 *
 * @example
 * // In SecurityEventList component
 * export const SecurityEventList = () => {
 *   usePageBackgroundPolling('security-events');
 *   return <List>...</List>;
 * };
 */
export const usePageBackgroundPolling = (pageName: string) => {
    // Define page-specific polling resources
    const pagePollingMap: Record<string, string[]> = {
        'dashboard': ['dashboard', 'security-events', 'system-status'],
        'security-events': ['security-events', 'yara-matches'],
        'system-status': ['system-status'],
        'threat-scanner': ['threat-scanner'],
        'yara-matches': ['yara-matches'],
        'timeline': ['timeline'],
    };

    const resourcesToPoll = pagePollingMap[pageName] || [pageName];
    useBackgroundPolling(resourcesToPoll);
};
