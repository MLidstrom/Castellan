// Cache invalidation helper to avoid TypeScript issues
export function invalidateCacheForResource(dashboardCache: any, resource: string): string[] {
  try {
    const keys: string[] = [];
    if (dashboardCache && dashboardCache.memoryCache) {
      dashboardCache.memoryCache.forEach((_value: any, key: string) => {
        if (key.includes(`_${resource}`)) {
          keys.push(key);
        }
      });
    }
    
    keys.forEach(key => dashboardCache.remove(key));
    return keys;
  } catch (error) {
    console.warn('Cache invalidation failed:', error);
    return [];
  }
}
