import React, { useState, useEffect, useCallback } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  IconButton,
  Button,
  Chip,
  Divider,
  Menu,
  MenuItem,
  Tooltip,
  CircularProgress,
  Alert
} from '@mui/material';
import {
  History as HistoryIcon,
  Replay as ReplayIcon,
  Delete as DeleteIcon,
  DeleteSweep as ClearAllIcon,
  Save as SaveIcon,
  Schedule as ScheduleIcon,
  Search as SearchIcon,
  MoreVert as MoreIcon,
  Refresh as RefreshIcon
} from '@mui/icons-material';
import { useDataProvider, useNotify } from 'react-admin';

interface SearchHistoryItem {
  id: number;
  searchFilters: any; // AdvancedSearchRequest object
  searchHash: string;
  resultCount: number;
  executionTimeMs: number;
  createdAt: string;
}

interface SearchHistoryProps {
  resource: string;
  onLoadSearch?: (searchFilters: any) => void;
  onSaveSearch?: (searchFilters: any) => void;
  maxItems?: number;
}

export const SearchHistory: React.FC<SearchHistoryProps> = ({
  resource,
  onLoadSearch,
  onSaveSearch,
  maxItems = 20
}) => {
  const [history, setHistory] = useState<SearchHistoryItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const [selectedHistoryItem, setSelectedHistoryItem] = useState<SearchHistoryItem | null>(null);

  const dataProvider = useDataProvider();
  const notify = useNotify();

  // Load search history from API
  const loadHistory = useCallback(async () => {
    setLoading(true);
    try {
      const response = await dataProvider.getList('search-history', {
        pagination: { page: 1, perPage: maxItems },
        sort: { field: 'createdAt', order: 'DESC' },
        filter: {}
      });

      setHistory(response.data as SearchHistoryItem[]);
    } catch (error) {
      console.error('Failed to load search history:', error);
      notify('Failed to load search history', { type: 'error' });
    } finally {
      setLoading(false);
    }
  }, [dataProvider, maxItems, notify]);

  // Load history on mount
  useEffect(() => {
    loadHistory();
  }, [loadHistory]);

  // Record a search in history (called by parent component)
  const recordSearch = useCallback(async (searchFilters: any, resultCount: number, executionTimeMs: number) => {
    try {
      await dataProvider.create('search-history', {
        data: {
          searchFilters,
          resultCount,
          executionTimeMs
        }
      });

      // Refresh history to show the new entry
      await loadHistory();
    } catch (error) {
      console.error('Failed to record search in history:', error);
      // Don't notify user about this error as it's not critical
    }
  }, [dataProvider, loadHistory]);

  // recordSearch function is available via the useSearchHistory hook

  const replaySearch = useCallback((historyItem: SearchHistoryItem) => {
    onLoadSearch?.(historyItem.searchFilters);
    notify(`Replayed search from ${formatDate(historyItem.createdAt)}`, { type: 'info' });
    handleMenuClose();
  }, [onLoadSearch, notify]);

  const saveAsNewSearch = useCallback((historyItem: SearchHistoryItem) => {
    onSaveSearch?.(historyItem.searchFilters);
    notify('Search filters loaded for saving', { type: 'info' });
    handleMenuClose();
  }, [onSaveSearch, notify]);

  const clearHistory = useCallback(async () => {
    try {
      // Note: This would require a bulk delete endpoint in the backend
      // For now, we'll just clear the local state and show a message
      notify('Clear all history feature coming soon', { type: 'info' });
    } catch (error) {
      console.error('Failed to clear search history:', error);
      notify('Failed to clear search history', { type: 'error' });
    }
  }, [notify]);

  const handleMenuOpen = (event: React.MouseEvent<HTMLElement>, historyItem: SearchHistoryItem) => {
    setAnchorEl(event.currentTarget);
    setSelectedHistoryItem(historyItem);
  };

  const handleMenuClose = () => {
    setAnchorEl(null);
    setSelectedHistoryItem(null);
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffInHours = Math.abs(now.getTime() - date.getTime()) / (1000 * 60 * 60);
    
    if (diffInHours < 1) return 'Just now';
    if (diffInHours < 24) return `${Math.floor(diffInHours)} hours ago`;
    if (diffInHours < 168) return `${Math.floor(diffInHours / 24)} days ago`;
    
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  const formatSearchSummary = (searchFilters: any) => {
    if (!searchFilters) return 'Empty search';
    
    const summary: string[] = [];
    
    // Full text query
    if (searchFilters.fullTextQuery && searchFilters.fullTextQuery.trim()) {
      summary.push(`Text: "${searchFilters.fullTextQuery.trim()}"`);
    }
    
    // Risk levels
    if (searchFilters.riskLevels && searchFilters.riskLevels.length > 0) {
      summary.push(`Risk: ${searchFilters.riskLevels.join(', ')}`);
    }
    
    // Event types
    if (searchFilters.eventTypes && searchFilters.eventTypes.length > 0) {
      summary.push(`Events: ${searchFilters.eventTypes.length}`);
    }
    
    // Date range
    if (searchFilters.startDate && searchFilters.endDate) {
      const start = new Date(searchFilters.startDate).toLocaleDateString();
      const end = new Date(searchFilters.endDate).toLocaleDateString();
      if (start === end) {
        summary.push(`Date: ${start}`);
      } else {
        summary.push(`Date: ${start} - ${end}`);
      }
    } else if (searchFilters.startDate) {
      summary.push(`Since: ${new Date(searchFilters.startDate).toLocaleDateString()}`);
    } else if (searchFilters.endDate) {
      summary.push(`Until: ${new Date(searchFilters.endDate).toLocaleDateString()}`);
    }
    
    if (summary.length === 0) {
      // Count other meaningful filters
      let filterCount = 0;
      Object.keys(searchFilters).forEach(key => {
        if (searchFilters[key] && searchFilters[key] !== '' && searchFilters[key] !== null) {
          if (!['page', 'pageSize', 'sortField', 'sortOrder', 'includeArchivedEvents', 'enableFuzzySearch'].includes(key)) {
            filterCount++;
          }
        }
      });
      
      return filterCount > 0 ? `${filterCount} filters` : 'No filters';
    }
    
    return summary.slice(0, 2).join(' • ');
  };

  const formatExecutionTime = (timeMs: number) => {
    if (timeMs < 1000) {
      return `${timeMs}ms`;
    } else if (timeMs < 60000) {
      return `${(timeMs / 1000).toFixed(1)}s`;
    } else {
      return `${(timeMs / 60000).toFixed(1)}m`;
    }
  };

  return (
    <Box>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h6" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <HistoryIcon />
          Search History ({history.length})
        </Typography>
        
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button
            size="small"
            startIcon={<RefreshIcon />}
            onClick={loadHistory}
            disabled={loading}
          >
            Refresh
          </Button>
          
          <Button
            size="small"
            startIcon={<ClearAllIcon />}
            onClick={clearHistory}
            disabled={history.length === 0}
            color="warning"
          >
            Clear
          </Button>
        </Box>
      </Box>

      {/* History List */}
      <Card>
        {loading ? (
          <CardContent sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress />
          </CardContent>
        ) : history.length === 0 ? (
          <CardContent>
            <Typography color="textSecondary" align="center" sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 1 }}>
              <SearchIcon />
              No search history yet. Start searching to build your history.
            </Typography>
          </CardContent>
        ) : (
          <List>
            {history.map((historyItem, index) => (
              <React.Fragment key={historyItem.id}>
                <ListItem>
                  <ListItemText
                    primary={
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
                        <Typography variant="subtitle2" component="span">
                          {formatSearchSummary(historyItem.searchFilters)}
                        </Typography>
                        <Chip 
                          label={`${historyItem.resultCount} results`} 
                          size="small" 
                          variant="outlined"
                          color={historyItem.resultCount > 0 ? 'primary' : 'default'}
                        />
                      </Box>
                    }
                    secondary={
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mt: 0.5 }}>
                        <Typography variant="caption" color="textSecondary" sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                          <ScheduleIcon fontSize="small" />
                          {formatDate(historyItem.createdAt)}
                        </Typography>
                        <Typography variant="caption" color="textSecondary">
                          Exec: {formatExecutionTime(historyItem.executionTimeMs)}
                        </Typography>
                      </Box>
                    }
                  />
                  
                  <ListItemSecondaryAction>
                    <Box sx={{ display: 'flex', alignItems: 'center' }}>
                      <Tooltip title="Replay this search">
                        <IconButton size="small" onClick={() => replaySearch(historyItem)}>
                          <ReplayIcon />
                        </IconButton>
                      </Tooltip>
                      
                      <IconButton size="small" onClick={(e) => handleMenuOpen(e, historyItem)}>
                        <MoreIcon />
                      </IconButton>
                    </Box>
                  </ListItemSecondaryAction>
                </ListItem>
                {index < history.length - 1 && <Divider />}
              </React.Fragment>
            ))}
          </List>
        )}
      </Card>

      {/* Context Menu */}
      <Menu
        anchorEl={anchorEl}
        open={Boolean(anchorEl)}
        onClose={handleMenuClose}
      >
        <MenuItem onClick={() => selectedHistoryItem && replaySearch(selectedHistoryItem)}>
          <ReplayIcon fontSize="small" sx={{ mr: 1 }} />
          Replay Search
        </MenuItem>
        
        <MenuItem onClick={() => selectedHistoryItem && saveAsNewSearch(selectedHistoryItem)}>
          <SaveIcon fontSize="small" sx={{ mr: 1 }} />
          Save as New Search
        </MenuItem>
      </Menu>

      {/* Info Alert */}
      {history.length === 0 && !loading && (
        <Alert severity="info" sx={{ mt: 2 }}>
          <Typography variant="body2">
            Search history tracks your recent searches automatically. Use the replay button to quickly re-run previous searches, 
            or save frequently used searches for easy access.
          </Typography>
        </Alert>
      )}
    </Box>
  );
};

// Hook to use search history functionality
export const useSearchHistory = (resource: string) => {
  const [historyRef, setHistoryRef] = useState<{ recordSearch: (searchFilters: any, resultCount: number, executionTimeMs: number) => void } | null>(null);

  const recordSearch = useCallback((searchFilters: any, resultCount: number, executionTimeMs: number) => {
    historyRef?.recordSearch(searchFilters, resultCount, executionTimeMs);
  }, [historyRef]);

  return { recordSearch, setHistoryRef };
};