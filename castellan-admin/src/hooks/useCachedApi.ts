import { useState, useEffect, useCallback, useRef } from 'react';
import { dashboardCache, CACHE_TTL } from '../utils/cacheManager';
import { cachePreloader } from '../utils/cachePreloader';

export interface UseCachedApiOptions {
  cacheKey: string;
  cacheTtl?: number;
  refreshInterval?: number;
  enabled?: boolean;
  dependencies?: any[];
}

export interface UseCachedApiReturn<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
  isStale: boolean;
  lastUpdated: Date | null;
  clearCache: () => void;
}

export function useCachedApi<T>(
  apiCall: () => Promise<T>,
  options: UseCachedApiOptions
): UseCachedApiReturn<T> {
  const {
    cacheKey,
    cacheTtl = CACHE_TTL.NORMAL_REFRESH,
    refreshInterval,
    enabled = true,
    dependencies = []
  } = options;

  // Try preloaded data first, then cache lookup for initialization
  const preloadedData = cachePreloader.getPreloadedData<T>(cacheKey);
  const initialCachedData = preloadedData ? 
    { data: preloadedData, timestamp: Date.now() } : 
    dashboardCache.get<{
      data: T;
      timestamp: number;
    }>(cacheKey);
  
  const [data, setData] = useState<T | null>(initialCachedData ? initialCachedData.data : null);
  const [loading, setLoading] = useState(() => {
    // Only show loading if we have no cached data
    return !initialCachedData;
  });
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(
    initialCachedData ? new Date(initialCachedData.timestamp) : null
  );
  const [isStale, setIsStale] = useState(() => {
    if (!initialCachedData) return false;
    const age = Date.now() - initialCachedData.timestamp;
    return age > cacheTtl / 2;
  });
  
  const refreshIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const mountedRef = useRef(true);

  // Check if cached data is stale (older than half the TTL)
  const checkStale = useCallback((timestamp: number) => {
    const age = Date.now() - timestamp;
    return age > cacheTtl / 2;
  }, [cacheTtl]);

  // Fetch data function
  const fetchData = useCallback(async (forceRefresh = false) => {
    if (!enabled) return;

    // Check cache first if not forcing refresh
    if (!forceRefresh) {
      const cachedData = dashboardCache.get<{
        data: T;
        timestamp: number;
      }>(cacheKey);

      if (cachedData) {
        if (mountedRef.current) {
          setData(cachedData.data);
          setLastUpdated(new Date(cachedData.timestamp));
          setIsStale(checkStale(cachedData.timestamp));
          setError(null);
        }
        return;
      }
    }

    // Only set loading state if we don't have any data yet
    if (mountedRef.current) {
      // Check if we already have cached data to avoid showing loading unnecessarily
      const existingCache = dashboardCache.get<{ data: T; timestamp: number }>(cacheKey);
      const hasExistingData = data !== null || existingCache !== null;
      
      if (!hasExistingData) {
        setLoading(true);
      }
      setError(null);
    }

    try {
      const result = await apiCall();
      const timestamp = Date.now();

      // Cache the result
      dashboardCache.set(
        cacheKey,
        { data: result, timestamp },
        cacheTtl
      );

      if (mountedRef.current) {
        setData(result);
        setLastUpdated(new Date(timestamp));
        setIsStale(false);
        setError(null);
      }
    } catch (err) {
      if (mountedRef.current) {
        setError(err instanceof Error ? err.message : 'Failed to fetch data');
        console.error(`API call failed for ${cacheKey}:`, err);
      }
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, [apiCall, cacheKey, cacheTtl, enabled, checkStale]);

  // Force refetch function
  const refetch = useCallback(async () => {
    await fetchData(true);
  }, [fetchData]);

  // Clear cache function
  const clearCache = useCallback(() => {
    dashboardCache.remove(cacheKey);
    if (mountedRef.current) {
      setData(null);
      setLastUpdated(null);
      setIsStale(false);
    }
  }, [cacheKey]);

  // Initial fetch and dependency handling
  useEffect(() => {
    if (enabled) {
      // Check for cached data first
      const cachedData = dashboardCache.get<{
        data: T;
        timestamp: number;
      }>(cacheKey);
      
      if (cachedData) {
        // Use cached data immediately if available
        if (mountedRef.current) {
          setData(cachedData.data);
          setLastUpdated(new Date(cachedData.timestamp));
          setIsStale(checkStale(cachedData.timestamp));
          setError(null);
          setLoading(false);
          console.log(`⚡ Instant data load for ${cacheKey} - no loading spinner!`);
        }
        
        // Only fetch if cache is expired (not just stale)
        const isExpired = cachedData.timestamp < Date.now() - cacheTtl;
        if (isExpired) {
          console.log(`🔄 Cache expired for ${cacheKey}, refreshing...`);
          fetchData();
        } else {
          console.log(`✅ Using cached data for ${cacheKey}`);
        }
      } else {
        // No cached data, fetch fresh
        console.log(`🆕 No cache found for ${cacheKey}, fetching fresh data...`);
        fetchData();
      }
    }
  }, [fetchData, enabled, checkStale, cacheKey, cacheTtl, ...dependencies]);

  // Set up refresh interval
  useEffect(() => {
    if (refreshInterval && enabled) {
      refreshIntervalRef.current = setInterval(() => {
        fetchData(false); // Don't force refresh, use cache if available
      }, refreshInterval);

      return () => {
        if (refreshIntervalRef.current) {
          clearInterval(refreshIntervalRef.current);
          refreshIntervalRef.current = null;
        }
      };
    }
  }, [refreshInterval, enabled, fetchData]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      mountedRef.current = false;
      if (refreshIntervalRef.current) {
        clearInterval(refreshIntervalRef.current);
      }
    };
  }, []);

  // Mark data as stale periodically
  useEffect(() => {
    if (lastUpdated) {
      const staleCheckInterval = setInterval(() => {
        if (mountedRef.current && lastUpdated) {
          setIsStale(checkStale(lastUpdated.getTime()));
        }
      }, 30000); // Check every 30 seconds

      return () => clearInterval(staleCheckInterval);
    }
  }, [lastUpdated, checkStale]);

  return {
    data,
    loading,
    error,
    refetch,
    isStale,
    lastUpdated,
    clearCache
  };
}
