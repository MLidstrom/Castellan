import { useEffect, useRef } from 'react';
import { useAuthState } from 'react-admin';
import { useSignalRContext } from '../contexts/SignalRContext';

/**
 * Dashboard Warmup Hook
 *
 * Implements early prefetch and SignalR connection initialization to reduce
 * perceived latency when users first visit the dashboard after login.
 *
 * Features:
 * - Waits 1.5 seconds after auth to avoid interfering with login flow
 * - Connects to SignalR early (before dashboard navigation)
 * - Joins dashboard updates group
 * - Prefetches consolidated dashboard data
 *
 * Usage:
 * Add to App component: useDashboardWarmup();
 */
export const useDashboardWarmup = () => {
  const { authenticated, isLoading: authLoading } = useAuthState();
  const { isConnected, connect, joinDashboardUpdates, requestDashboardData } = useSignalRContext();
  const warmupExecutedRef = useRef(false);

  useEffect(() => {
    // Only run warmup once per session
    if (warmupExecutedRef.current) {
      return;
    }

    // Skip if auth is still loading or user not authenticated
    if (authLoading || !authenticated) {
      return;
    }

    // Idle detection: wait 1.5 seconds after auth before prefetching
    const idleTimer = setTimeout(async () => {
      try {
        console.log('🚀 Dashboard Warmup - Starting prefetch sequence');

        // Step 1: Ensure SignalR connection is established
        if (!isConnected) {
          console.log('🔌 Dashboard Warmup - Connecting to SignalR');
          connect();
          // Wait briefly for connection to establish
          await new Promise(resolve => setTimeout(resolve, 500));
        }

        // Step 2: Join dashboard updates group
        console.log('📊 Dashboard Warmup - Joining dashboard updates');
        await joinDashboardUpdates();

        // Step 3: Prefetch consolidated dashboard data
        console.log('📦 Dashboard Warmup - Prefetching dashboard data');
        await requestDashboardData('24h');

        console.log('✅ Dashboard Warmup - Prefetch complete');
        warmupExecutedRef.current = true;
      } catch (error) {
        console.warn('⚠️ Dashboard Warmup - Prefetch failed (non-critical):', error);
        // Non-critical: dashboard will still load normally if prefetch fails
      }
    }, 1500); // 1.5 second idle delay

    return () => clearTimeout(idleTimer);
  }, [authenticated, authLoading, isConnected, connect, joinDashboardUpdates, requestDashboardData]);
};
