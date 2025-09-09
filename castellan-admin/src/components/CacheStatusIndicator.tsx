import React, { useState, useEffect } from 'react';
import {
  Box,
  Chip,
  IconButton,
  Popover,
  Typography,
  List,
  ListItem,
  ListItemText,
  Divider,
  Button
} from '@mui/material';
import {
  Storage as CacheIcon,
  Clear as ClearIcon,
  Info as InfoIcon
} from '@mui/icons-material';
import { dashboardCache, CACHE_KEYS } from '../utils/cacheManager';

export const CacheStatusIndicator: React.FC = () => {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [stats, setStats] = useState({
    memoryItems: 0,
    localStorageItems: 0,
    totalSize: 0
  });

  const updateStats = () => {
    setStats(dashboardCache.getStats());
  };

  useEffect(() => {
    updateStats();
    const interval = setInterval(updateStats, 10000); // Update every 10 seconds
    return () => clearInterval(interval);
  }, []);

  const handleClick = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleClearCache = () => {
    dashboardCache.clear();
    updateStats();
    handleClose();
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const getCacheStatus = () => {
    const totalItems = stats.memoryItems + stats.localStorageItems;
    if (totalItems === 0) return { color: 'default' as const, text: 'Empty' };
    if (totalItems < 5) return { color: 'info' as const, text: 'Light' };
    if (totalItems < 10) return { color: 'success' as const, text: 'Active' };
    return { color: 'warning' as const, text: 'Full' };
  };

  const cacheStatus = getCacheStatus();
  const open = Boolean(anchorEl);
  const totalItems = stats.memoryItems + stats.localStorageItems;

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Chip
          icon={<CacheIcon />}
          label={`Cache: ${totalItems} items`}
          color={cacheStatus.color}
          size="small"
          variant="outlined"
          onClick={handleClick}
          sx={{ cursor: 'pointer' }}
        />
        <IconButton size="small" onClick={handleClick}>
          <InfoIcon fontSize="small" />
        </IconButton>
      </Box>

      <Popover
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{
          vertical: 'bottom',
          horizontal: 'right',
        }}
        transformOrigin={{
          vertical: 'top',
          horizontal: 'right',
        }}
      >
        <Box sx={{ p: 2, minWidth: 300 }}>
          <Typography variant="h6" gutterBottom>
            Cache Status
          </Typography>
          
          <List dense>
            <ListItem>
              <ListItemText
                primary="Memory Cache"
                secondary={`${stats.memoryItems} items`}
              />
            </ListItem>
            <ListItem>
              <ListItemText
                primary="Persistent Cache"
                secondary={`${stats.localStorageItems} items`}
              />
            </ListItem>
            <ListItem>
              <ListItemText
                primary="Storage Size"
                secondary={formatBytes(stats.totalSize)}
              />
            </ListItem>
          </List>

          <Divider sx={{ my: 2 }} />

          <Typography variant="subtitle2" gutterBottom>
            Cached Components:
          </Typography>
          <List dense>
            {Object.entries(CACHE_KEYS).map(([key, value]) => {
              const hasCache = dashboardCache.has(value);
              return (
                <ListItem key={key}>
                  <ListItemText
                    primary={key.replace(/_/g, ' ')}
                    secondary={hasCache ? 'Cached' : 'No cache'}
                  />
                  <Chip
                    size="small"
                    color={hasCache ? 'success' : 'default'}
                    label={hasCache ? '✓' : '○'}
                  />
                </ListItem>
              );
            })}
          </List>

          <Box sx={{ mt: 2, display: 'flex', gap: 1 }}>
            <Button
              variant="outlined"
              size="small"
              startIcon={<ClearIcon />}
              onClick={handleClearCache}
              color="warning"
            >
              Clear Cache
            </Button>
            <Button
              variant="outlined"
              size="small"
              onClick={() => {
                updateStats();
                // Trigger a refresh of all cached data
                window.location.reload();
              }}
            >
              Refresh All
            </Button>
          </Box>
        </Box>
      </Popover>
    </>
  );
};
