// Debug utility for cache and data diagnosis
import { dashboardCache } from './cacheManager';

export const clearAllCache = () => {
  dashboardCache.clear();
  console.log('🧹 All dashboard cache cleared');
  
  // Also clear React dev tools cache
  if (typeof window !== 'undefined') {
    sessionStorage.clear();
    console.log('🧹 Session storage cleared');
  }
};

export const diagnoseData = (label: string, data: any) => {
  console.group(`🔍 Data Diagnosis: ${label}`);
  console.log('Type:', typeof data);
  console.log('Is Array:', Array.isArray(data));
  console.log('Length/Size:', Array.isArray(data) ? data.length : Object.keys(data || {}).length);
  console.log('Sample Data:', data ? (Array.isArray(data) ? data.slice(0, 3) : data) : 'null/undefined');
  console.groupEnd();
};

// Call this function in the browser console to debug
(window as any).debugCache = {
  clear: clearAllCache,
  diagnose: diagnoseData,
  stats: () => dashboardCache.getStats()
};
