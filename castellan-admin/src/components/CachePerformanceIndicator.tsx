import React, { useState, useEffect } from 'react';
import { Box, Chip, Tooltip, IconButton } from '@mui/material';
import { Speed as SpeedIcon, Cached as CachedIcon, Refresh as RefreshIcon } from '@mui/icons-material';
import { dashboardCache } from '../utils/cacheManager';
import { cachePreloader } from '../utils/cachePreloader';

interface CacheStats {
  memoryItems: number;
  localStorageItems: number;
  totalSize: number;
  preloadedCount: number;
  preloadedKeys: string[];
}

export const CachePerformanceIndicator: React.FC = () => {
  const [stats, setStats] = useState<CacheStats>({
    memoryItems: 0,
    localStorageItems: 0,
    totalSize: 0,
    preloadedCount: 0,
    preloadedKeys: []
  });

  const updateStats = () => {
    const cacheStats = dashboardCache.getStats();
    const preloadStats = cachePreloader.getPreloadStats();
    
    setStats({
      ...cacheStats,
      preloadedCount: preloadStats.preloadedCount,
      preloadedKeys: preloadStats.preloadedKeys
    });
  };

  useEffect(() => {
    updateStats();
    
    // Update stats every 30 seconds
    const interval = setInterval(updateStats, 30000);
    
    return () => clearInterval(interval);
  }, []);

  const formatSize = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };

  const getCacheEfficiency = () => {
    const totalItems = stats.memoryItems + stats.localStorageItems;
    if (totalItems === 0) return 0;
    return Math.round((stats.preloadedCount / totalItems) * 100);
  };

  const getStatusColor = () => {
    const efficiency = getCacheEfficiency();
    if (efficiency >= 75) return 'success';
    if (efficiency >= 50) return 'warning';
    return 'error';
  };

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      <Tooltip 
        title={
          <Box>
            <div><strong>Cache Performance</strong></div>
            <div>Memory items: {stats.memoryItems}</div>
            <div>Storage items: {stats.localStorageItems}</div>
            <div>Preloaded: {stats.preloadedCount}</div>
            <div>Total size: {formatSize(stats.totalSize)}</div>
            <div>Efficiency: {getCacheEfficiency()}%</div>
            <div style={{ marginTop: 8 }}>
              <strong>Preloaded keys:</strong><br />
              {stats.preloadedKeys.join(', ') || 'None'}
            </div>
          </Box>
        }
      >
        <Chip
          icon={<SpeedIcon />}
          label={`Cache: ${getCacheEfficiency()}%`}
          color={getStatusColor() as any}
          size="small"
          variant="outlined"
        />
      </Tooltip>
      
      <Tooltip title="Refresh cache stats">
        <IconButton size="small" onClick={updateStats}>
          <RefreshIcon fontSize="small" />
        </IconButton>
      </Tooltip>
    </Box>
  );
};
