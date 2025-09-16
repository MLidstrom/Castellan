import React, { useState, useEffect, useCallback } from 'react';
import {
  Box,
  Card,
  CardContent,
  TextField,
  Button,
  Chip,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Autocomplete,
  Typography,
  Divider,
  Collapse,
  IconButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  Alert,
  Tabs,
  Tab,
  Paper
} from '@mui/material';
import {
  Search as SearchIcon,
  FilterList as FilterIcon,
  Save as SaveIcon,
  BookmarkBorder as BookmarkIcon,
  Bookmark as BookmarkedIcon,
  Clear as ClearIcon,
  ExpandMore as ExpandIcon,
  ExpandLess as CollapseIcon,
  Delete as DeleteIcon,
  Refresh as RefreshIcon,
  History as HistoryIcon
} from '@mui/icons-material';
import { useDataProvider, useNotify } from 'react-admin';

import { SavedSearchManagerAPI } from './SavedSearchManagerAPI';
import { SearchHistory } from './SearchHistory';
import { SaveSearchDialog } from './SaveSearchDialog';

interface SearchFilter {
  field: string;
  operator: 'equals' | 'contains' | 'startsWith' | 'endsWith' | 'gt' | 'lt' | 'between' | 'in';
  value: any;
  label?: string;
}

interface AdvancedSearchWithSavedSearchesProps {
  resource: string;
  fields?: SearchFieldConfig[];
  onSearch?: (searchFilters: any, resultCount: number, executionTimeMs: number) => void;
  onClear?: () => void;
  placeholder?: string;
  maxFilters?: number;
  initialFilters?: any; // AdvancedSearchRequest object
}

interface SearchFieldConfig {
  name: string;
  label: string;
  type: 'text' | 'number' | 'date' | 'select' | 'multiselect';
  options?: Array<{ value: any; label: string }>;
  operators?: SearchFilter['operator'][];
}

const defaultOperators: SearchFilter['operator'][] = [
  'equals', 'contains', 'startsWith', 'endsWith', 'gt', 'lt'
];

export const AdvancedSearchWithSavedSearches: React.FC<AdvancedSearchWithSavedSearchesProps> = ({
  resource,
  fields = [],
  onSearch,
  onClear,
  placeholder = "Search...",
  maxFilters = 5,
  initialFilters
}) => {
  const [searchFilters, setSearchFilters] = useState<any>(initialFilters || {
    fullTextQuery: '',
    riskLevels: [],
    eventTypes: [],
    startDate: '',
    endDate: '',
    page: 1,
    pageSize: 25,
    sortField: 'Timestamp',
    sortOrder: 'DESC'
  });
  
  const [expandedFilters, setExpandedFilters] = useState(false);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [tabValue, setTabValue] = useState(0); // 0 = Search, 1 = Saved Searches, 2 = History

  const dataProvider = useDataProvider();
  const notify = useNotify();

  const defaultFields: SearchFieldConfig[] = [
    {
      name: 'riskLevel',
      label: 'Risk Level',
      type: 'multiselect',
      options: [
        { value: 'low', label: 'Low' },
        { value: 'medium', label: 'Medium' },
        { value: 'high', label: 'High' },
        { value: 'critical', label: 'Critical' }
      ],
      operators: ['equals', 'in']
    },
    {
      name: 'eventType',
      label: 'Event Type',
      type: 'multiselect',
      options: [
        { value: 'Login', label: 'Login' },
        { value: 'Logout', label: 'Logout' },
        { value: 'FileAccess', label: 'File Access' },
        { value: 'NetworkAccess', label: 'Network Access' },
        { value: 'ProcessCreation', label: 'Process Creation' }
      ],
      operators: ['equals', 'contains']
    },
    {
      name: 'timestamp',
      label: 'Date Range',
      type: 'date',
      operators: ['equals', 'gt', 'lt', 'between']
    },
    {
      name: 'confidenceScore',
      label: 'Confidence Score',
      type: 'number',
      operators: ['equals', 'gt', 'lt', 'between']
    }
  ];

  const searchFields = fields.length > 0 ? fields : defaultFields;

  // Update filters when initialFilters changes
  useEffect(() => {
    if (initialFilters) {
      setSearchFilters(initialFilters);
    }
  }, [initialFilters]);

  const handleSearch = useCallback(async () => {
    if (!searchFilters.fullTextQuery?.trim() && 
        (!searchFilters.riskLevels || searchFilters.riskLevels.length === 0) &&
        (!searchFilters.eventTypes || searchFilters.eventTypes.length === 0) &&
        !searchFilters.startDate && !searchFilters.endDate) {
      notify('Please enter search criteria', { type: 'info' });
      return;
    }

    setLoading(true);
    const startTime = Date.now();
    
    try {
      // Build search parameters
      const searchParams: any = { ...searchFilters };
      
      // Perform search using data provider
      const result = await dataProvider.getList(resource, {
        pagination: { page: searchParams.page || 1, perPage: searchParams.pageSize || 25 },
        sort: { field: searchParams.sortField || 'timestamp', order: searchParams.sortOrder || 'DESC' },
        filter: {
          fullTextQuery: searchParams.fullTextQuery,
          riskLevels: searchParams.riskLevels,
          eventTypes: searchParams.eventTypes,
          startDate: searchParams.startDate,
          endDate: searchParams.endDate,
          // Add other filters as needed
        }
      });

      const executionTime = Date.now() - startTime;
      
      setSearchResults(result.data);
      onSearch?.(searchFilters, result.data.length, executionTime);
      
      // Record in search history
      recordSearchInHistory(searchFilters, result.data.length, executionTime);
      
      notify(`Found ${result.data.length} results`, { type: 'info' });

    } catch (error) {
      notify('Search failed', { type: 'error' });
      console.error('Search error:', error);
    } finally {
      setLoading(false);
    }
  }, [searchFilters, resource, dataProvider, onSearch, notify]);

  const recordSearchInHistory = useCallback(async (filters: any, resultCount: number, executionTime: number) => {
    try {
      await dataProvider.create('search-history', {
        data: {
          searchFilters: filters,
          resultCount,
          executionTimeMs: executionTime
        }
      });
    } catch (error) {
      console.error('Failed to record search history:', error);
      // Don't show error to user as this is not critical
    }
  }, [dataProvider]);

  const handleClear = useCallback(() => {
    setSearchFilters({
      fullTextQuery: '',
      riskLevels: [],
      eventTypes: [],
      startDate: '',
      endDate: '',
      page: 1,
      pageSize: 25,
      sortField: 'Timestamp',
      sortOrder: 'DESC'
    });
    setSearchResults([]);
    onClear?.();
    notify('Search filters cleared', { type: 'info' });
  }, [onClear, notify]);

  const handleLoadSavedSearch = useCallback((savedSearch: any) => {
    setSearchFilters({ ...savedSearch.filters });
    setTabValue(0); // Switch back to search tab
    notify(`Loaded search: ${savedSearch.name}`, { type: 'success' });
  }, [notify]);

  const handleLoadFromHistory = useCallback((filters: any) => {
    setSearchFilters({ ...filters });
    setTabValue(0); // Switch back to search tab
    notify('Search filters loaded from history', { type: 'info' });
  }, [notify]);

  const handleSaveSearch = useCallback((filters?: any) => {
    const filtersToSave = filters || searchFilters;
    // Check if there are meaningful filters to save
    if (!filtersToSave.fullTextQuery?.trim() && 
        (!filtersToSave.riskLevels || filtersToSave.riskLevels.length === 0) &&
        (!filtersToSave.eventTypes || filtersToSave.eventTypes.length === 0) &&
        !filtersToSave.startDate && !filtersToSave.endDate) {
      notify('No search criteria to save', { type: 'info' });
      return;
    }
    setSaveDialogOpen(true);
  }, [searchFilters, notify]);

  const handleFilterChange = (field: string, value: any) => {
    setSearchFilters((prev: any) => ({
      ...prev,
      [field]: value
    }));
  };

  const TabPanel = ({ children, value, index, ...other }: any) => (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`search-tabpanel-${index}`}
      aria-labelledby={`search-tab-${index}`}
      {...other}
    >
      {value === index && (
        <Box sx={{ p: 2 }}>
          {children}
        </Box>
      )}
    </div>
  );

  return (
    <Card sx={{ mb: 2 }}>
      <CardContent>
        {/* Tabs */}
        <Box sx={{ borderBottom: 1, borderColor: 'divider', mb: 2 }}>
          <Tabs value={tabValue} onChange={(_, newValue) => setTabValue(newValue)}>
            <Tab label="Search" icon={<SearchIcon />} />
            <Tab label="Saved Searches" icon={<BookmarkIcon />} />
            <Tab label="History" icon={<HistoryIcon />} />
          </Tabs>
        </Box>

        {/* Search Tab */}
        <TabPanel value={tabValue} index={0}>
          {/* Main Search Input */}
          <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
            <TextField
              fullWidth
              placeholder={placeholder}
              value={searchFilters.fullTextQuery || ''}
              onChange={(e) => handleFilterChange('fullTextQuery', e.target.value)}
              InputProps={{
                startAdornment: <SearchIcon sx={{ mr: 1, color: 'action.active' }} />
              }}
              onKeyPress={(e) => {
                if (e.key === 'Enter') {
                  handleSearch();
                }
              }}
            />
            
            <Button
              variant="contained"
              onClick={handleSearch}
              disabled={loading}
              startIcon={<SearchIcon />}
              sx={{ minWidth: 120 }}
            >
              {loading ? 'Searching...' : 'Search'}
            </Button>
            
            <Button
              variant="outlined"
              onClick={handleClear}
              startIcon={<ClearIcon />}
            >
              Clear
            </Button>
          </Box>

          {/* Quick Filters */}
          <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
            {/* Risk Level Filter */}
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel>Risk Level</InputLabel>
              <Select
                multiple
                value={searchFilters.riskLevels || []}
                onChange={(e) => handleFilterChange('riskLevels', e.target.value)}
                label="Risk Level"
                renderValue={(selected) => (
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                    {(selected as string[]).map((value) => (
                      <Chip key={value} label={value} size="small" />
                    ))}
                  </Box>
                )}
              >
                <MenuItem value="low">Low</MenuItem>
                <MenuItem value="medium">Medium</MenuItem>
                <MenuItem value="high">High</MenuItem>
                <MenuItem value="critical">Critical</MenuItem>
              </Select>
            </FormControl>

            {/* Event Type Filter */}
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel>Event Type</InputLabel>
              <Select
                multiple
                value={searchFilters.eventTypes || []}
                onChange={(e) => handleFilterChange('eventTypes', e.target.value)}
                label="Event Type"
                renderValue={(selected) => (
                  <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                    {(selected as string[]).map((value) => (
                      <Chip key={value} label={value} size="small" />
                    ))}
                  </Box>
                )}
              >
                <MenuItem value="Login">Login</MenuItem>
                <MenuItem value="Logout">Logout</MenuItem>
                <MenuItem value="FileAccess">File Access</MenuItem>
                <MenuItem value="NetworkAccess">Network Access</MenuItem>
                <MenuItem value="ProcessCreation">Process Creation</MenuItem>
              </Select>
            </FormControl>

            {/* Date Range */}
            <TextField
              size="small"
              type="date"
              label="Start Date"
              value={searchFilters.startDate || ''}
              onChange={(e) => handleFilterChange('startDate', e.target.value)}
              InputLabelProps={{ shrink: true }}
              sx={{ minWidth: 140 }}
            />
            
            <TextField
              size="small"
              type="date"
              label="End Date"
              value={searchFilters.endDate || ''}
              onChange={(e) => handleFilterChange('endDate', e.target.value)}
              InputLabelProps={{ shrink: true }}
              sx={{ minWidth: 140 }}
            />

            <Button
              variant="outlined"
              size="small"
              onClick={() => handleSaveSearch()}
              startIcon={<SaveIcon />}
              disabled={!searchFilters.fullTextQuery?.trim() && 
                       (!searchFilters.riskLevels || searchFilters.riskLevels.length === 0) &&
                       (!searchFilters.eventTypes || searchFilters.eventTypes.length === 0) &&
                       !searchFilters.startDate && !searchFilters.endDate}
            >
              Save Search
            </Button>
          </Box>

          {/* Search Results Summary */}
          {searchResults.length > 0 && (
            <Alert severity="info" sx={{ mb: 2 }}>
              Found {searchResults.length} results. Use the "Save Search" button to save these criteria for future use.
            </Alert>
          )}
        </TabPanel>

        {/* Saved Searches Tab */}
        <TabPanel value={tabValue} index={1}>
          <SavedSearchManagerAPI
            resource={resource}
            onLoadSearch={handleLoadSavedSearch}
          />
        </TabPanel>

        {/* History Tab */}
        <TabPanel value={tabValue} index={2}>
          <SearchHistory
            resource={resource}
            onLoadSearch={handleLoadFromHistory}
            onSaveSearch={handleSaveSearch}
          />
        </TabPanel>

        {/* Save Search Dialog */}
        <SaveSearchDialog
          open={saveDialogOpen}
          onClose={() => setSaveDialogOpen(false)}
          searchFilters={searchFilters}
          onSaved={() => {
            // Refresh saved searches if on that tab
            if (tabValue === 1) {
              // The SavedSearchManagerAPI component will auto-refresh
            }
          }}
        />
      </CardContent>
    </Card>
  );
};