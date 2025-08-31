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
  Alert
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
  Refresh as RefreshIcon
} from '@mui/icons-material';
import { useDataProvider, useNotify } from 'react-admin';

interface SearchFilter {
  field: string;
  operator: 'equals' | 'contains' | 'startsWith' | 'endsWith' | 'gt' | 'lt' | 'between' | 'in';
  value: any;
  label?: string;
}

interface SavedSearch {
  id: string;
  name: string;
  query: string;
  filters: SearchFilter[];
  resource: string;
  createdAt: string;
  lastUsed?: string;
}

interface AdvancedSearchProps {
  resource: string;
  fields?: SearchFieldConfig[];
  onSearch?: (query: string, filters: SearchFilter[]) => void;
  onClear?: () => void;
  placeholder?: string;
  showSavedSearches?: boolean;
  maxFilters?: number;
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

export const AdvancedSearch: React.FC<AdvancedSearchProps> = ({
  resource,
  fields = [],
  onSearch,
  onClear,
  placeholder = "Search...",
  showSavedSearches = true,
  maxFilters = 5
}) => {
  const [query, setQuery] = useState('');
  const [filters, setFilters] = useState<SearchFilter[]>([]);
  const [expandedFilters, setExpandedFilters] = useState(false);
  const [savedSearches, setSavedSearches] = useState<SavedSearch[]>([]);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [savedSearchesDialogOpen, setSavedSearchesDialogOpen] = useState(false);
  const [newSearchName, setNewSearchName] = useState('');
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);

  const dataProvider = useDataProvider();
  const notify = useNotify();

  const defaultFields: SearchFieldConfig[] = [
    {
      name: 'riskLevel',
      label: 'Risk Level',
      type: 'select',
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
      type: 'text',
      operators: ['equals', 'contains']
    },
    {
      name: 'timestamp',
      label: 'Timestamp',
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

  // Load saved searches from localStorage
  useEffect(() => {
    const saved = localStorage.getItem(`advancedSearch_${resource}`);
    if (saved) {
      try {
        setSavedSearches(JSON.parse(saved));
      } catch (error) {
        console.error('Failed to load saved searches:', error);
      }
    }
  }, [resource]);

  // Save searches to localStorage
  const saveSavedSearches = useCallback((searches: SavedSearch[]) => {
    localStorage.setItem(`advancedSearch_${resource}`, JSON.stringify(searches));
    setSavedSearches(searches);
  }, [resource]);

  const handleSearch = useCallback(async () => {
    if (!query.trim() && filters.length === 0) {
      return;
    }

    setLoading(true);
    try {
      // Build search parameters
      const searchParams: any = {};
      
      if (query.trim()) {
        searchParams.q = query.trim();
      }

      // Add filters to search parameters
      filters.forEach((filter, index) => {
        const paramName = `filter_${filter.field}`;
        searchParams[paramName] = {
          operator: filter.operator,
          value: filter.value
        };
      });

      // Perform search using data provider
      const result = await dataProvider.getList(resource, {
        pagination: { page: 1, perPage: 100 },
        sort: { field: 'timestamp', order: 'DESC' },
        filter: searchParams
      });

      setSearchResults(result.data);
      onSearch?.(query, filters);
      notify(`Found ${result.data.length} results`, { type: 'info' });

    } catch (error) {
      notify('Search failed', { type: 'error' });
      console.error('Search error:', error);
    } finally {
      setLoading(false);
    }
  }, [query, filters, resource, dataProvider, onSearch, notify]);

  const handleClear = useCallback(() => {
    setQuery('');
    setFilters([]);
    setSearchResults([]);
    onClear?.();
    notify('Search cleared', { type: 'info' });
  }, [onClear, notify]);

  const addFilter = useCallback(() => {
    if (filters.length >= maxFilters) {
      notify(`Maximum ${maxFilters} filters allowed`, { type: 'warning' });
      return;
    }

    const newFilter: SearchFilter = {
      field: searchFields[0].name,
      operator: searchFields[0].operators?.[0] || 'equals',
      value: '',
      label: searchFields[0].label
    };

    setFilters(prev => [...prev, newFilter]);
    setExpandedFilters(true);
  }, [filters.length, maxFilters, searchFields, notify]);

  const updateFilter = useCallback((index: number, updates: Partial<SearchFilter>) => {
    setFilters(prev => prev.map((filter, i) => 
      i === index ? { ...filter, ...updates } : filter
    ));
  }, []);

  const removeFilter = useCallback((index: number) => {
    setFilters(prev => prev.filter((_, i) => i !== index));
  }, []);

  const saveCurrentSearch = useCallback(() => {
    if (!newSearchName.trim()) {
      notify('Please enter a name for the search', { type: 'warning' });
      return;
    }

    const newSavedSearch: SavedSearch = {
      id: Date.now().toString(),
      name: newSearchName.trim(),
      query,
      filters: [...filters],
      resource,
      createdAt: new Date().toISOString()
    };

    saveSavedSearches([...savedSearches, newSavedSearch]);
    setNewSearchName('');
    setSaveDialogOpen(false);
    notify('Search saved successfully', { type: 'success' });
  }, [newSearchName, query, filters, resource, savedSearches, saveSavedSearches, notify]);

  const loadSavedSearch = useCallback((savedSearch: SavedSearch) => {
    setQuery(savedSearch.query);
    setFilters([...savedSearch.filters]);
    
    // Update last used timestamp
    const updatedSearches = savedSearches.map(s => 
      s.id === savedSearch.id 
        ? { ...s, lastUsed: new Date().toISOString() }
        : s
    );
    saveSavedSearches(updatedSearches);
    
    setSavedSearchesDialogOpen(false);
    notify(`Loaded search: ${savedSearch.name}`, { type: 'success' });
  }, [savedSearches, saveSavedSearches, notify]);

  const deleteSavedSearch = useCallback((searchId: string) => {
    const updatedSearches = savedSearches.filter(s => s.id !== searchId);
    saveSavedSearches(updatedSearches);
    notify('Search deleted', { type: 'info' });
  }, [savedSearches, saveSavedSearches, notify]);

  const getFieldConfig = useCallback((fieldName: string) => {
    return searchFields.find(f => f.name === fieldName);
  }, [searchFields]);

  const renderFilterValue = (filter: SearchFilter, index: number) => {
    const fieldConfig = getFieldConfig(filter.field);
    
    switch (fieldConfig?.type) {
      case 'select':
        return (
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <Select
              value={filter.value}
              onChange={(e) => updateFilter(index, { value: e.target.value })}
            >
              {fieldConfig.options?.map(option => (
                <MenuItem key={option.value} value={option.value}>
                  {option.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        );
      
      case 'multiselect':
        return (
          <Autocomplete
            multiple
            size="small"
            options={fieldConfig.options || []}
            getOptionLabel={(option) => option.label}
            value={fieldConfig.options?.filter(opt => filter.value?.includes(opt.value)) || []}
            onChange={(_, newValue) => {
              updateFilter(index, { value: newValue.map(v => v.value) });
            }}
            renderInput={(params) => <TextField {...params} />}
            sx={{ minWidth: 200 }}
          />
        );
      
      case 'number':
        return (
          <TextField
            type="number"
            size="small"
            value={filter.value}
            onChange={(e) => updateFilter(index, { value: Number(e.target.value) })}
            sx={{ width: 120 }}
          />
        );
      
      case 'date':
        return (
          <TextField
            type="date"
            size="small"
            value={filter.value}
            onChange={(e) => updateFilter(index, { value: e.target.value })}
            InputLabelProps={{ shrink: true }}
            sx={{ width: 150 }}
          />
        );
      
      default:
        return (
          <TextField
            size="small"
            value={filter.value}
            onChange={(e) => updateFilter(index, { value: e.target.value })}
            sx={{ width: 200 }}
          />
        );
    }
  };

  return (
    <Card>
      <CardContent>
        {/* Main Search Bar */}
        <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
          <TextField
            fullWidth
            placeholder={placeholder}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
            InputProps={{
              startAdornment: <SearchIcon sx={{ color: 'text.secondary', mr: 1 }} />
            }}
          />
          <Button
            variant="contained"
            onClick={handleSearch}
            disabled={loading}
            startIcon={loading ? <RefreshIcon className="spin" /> : <SearchIcon />}
          >
            Search
          </Button>
          <Button
            variant="outlined"
            onClick={handleClear}
            startIcon={<ClearIcon />}
          >
            Clear
          </Button>
        </Box>

        {/* Filter Controls */}
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
            <Button
              size="small"
              onClick={() => setExpandedFilters(!expandedFilters)}
              startIcon={expandedFilters ? <CollapseIcon /> : <ExpandIcon />}
            >
              Filters ({filters.length})
            </Button>
            <Button
              size="small"
              onClick={addFilter}
              startIcon={<FilterIcon />}
              disabled={filters.length >= maxFilters}
            >
              Add Filter
            </Button>
          </Box>
          
          {showSavedSearches && (
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button
                size="small"
                onClick={() => setSaveDialogOpen(true)}
                startIcon={<SaveIcon />}
                disabled={!query.trim() && filters.length === 0}
              >
                Save Search
              </Button>
              <Button
                size="small"
                onClick={() => setSavedSearchesDialogOpen(true)}
                startIcon={<BookmarkIcon />}
              >
                Saved Searches ({savedSearches.length})
              </Button>
            </Box>
          )}
        </Box>

        {/* Active Filters Display */}
        {filters.length > 0 && (
          <Box sx={{ mb: 2 }}>
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
              {filters.map((filter, index) => (
                <Chip
                  key={index}
                  label={`${filter.label || filter.field}: ${filter.operator} ${filter.value}`}
                  onDelete={() => removeFilter(index)}
                  size="small"
                  variant="outlined"
                />
              ))}
            </Box>
          </Box>
        )}

        {/* Expandable Filters */}
        <Collapse in={expandedFilters}>
          <Box sx={{ mt: 2 }}>
            <Divider sx={{ mb: 2 }} />
            <Typography variant="subtitle2" gutterBottom>
              Advanced Filters
            </Typography>
            
            {filters.map((filter, index) => (
              <Box key={index} sx={{ display: 'flex', gap: 1, alignItems: 'center', mb: 2 }}>
                <FormControl size="small" sx={{ minWidth: 120 }}>
                  <InputLabel>Field</InputLabel>
                  <Select
                    value={filter.field}
                    label="Field"
                    onChange={(e) => {
                      const field = searchFields.find(f => f.name === e.target.value);
                      updateFilter(index, { 
                        field: e.target.value,
                        label: field?.label,
                        operator: field?.operators?.[0] || 'equals'
                      });
                    }}
                  >
                    {searchFields.map(field => (
                      <MenuItem key={field.name} value={field.name}>
                        {field.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <FormControl size="small" sx={{ minWidth: 120 }}>
                  <InputLabel>Operator</InputLabel>
                  <Select
                    value={filter.operator}
                    label="Operator"
                    onChange={(e) => updateFilter(index, { operator: e.target.value as SearchFilter['operator'] })}
                  >
                    {(getFieldConfig(filter.field)?.operators || defaultOperators).map(op => (
                      <MenuItem key={op} value={op}>
                        {op.replace(/([A-Z])/g, ' $1').toLowerCase()}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                {renderFilterValue(filter, index)}

                <IconButton
                  size="small"
                  onClick={() => removeFilter(index)}
                  color="error"
                >
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Box>
            ))}
          </Box>
        </Collapse>

        {/* Search Results Summary */}
        {searchResults.length > 0 && (
          <Alert severity="info" sx={{ mt: 2 }}>
            Found {searchResults.length} results for your search
          </Alert>
        )}
      </CardContent>

      {/* Save Search Dialog */}
      <Dialog open={saveDialogOpen} onClose={() => setSaveDialogOpen(false)}>
        <DialogTitle>Save Search</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            margin="dense"
            label="Search Name"
            fullWidth
            variant="outlined"
            value={newSearchName}
            onChange={(e) => setNewSearchName(e.target.value)}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSaveDialogOpen(false)}>Cancel</Button>
          <Button onClick={saveCurrentSearch} variant="contained">Save</Button>
        </DialogActions>
      </Dialog>

      {/* Saved Searches Dialog */}
      <Dialog 
        open={savedSearchesDialogOpen} 
        onClose={() => setSavedSearchesDialogOpen(false)}
        maxWidth="md"
        fullWidth
      >
        <DialogTitle>Saved Searches</DialogTitle>
        <DialogContent>
          {savedSearches.length === 0 ? (
            <Typography color="textSecondary">
              No saved searches yet. Create a search and save it to access it later.
            </Typography>
          ) : (
            <List>
              {savedSearches.map(search => (
                <ListItem key={search.id}>
                  <BookmarkedIcon sx={{ mr: 2, color: 'primary.main' }} />
                  <ListItemText
                    primary={search.name}
                    secondary={
                      <Box>
                        <Typography variant="body2" color="textSecondary">
                          Query: {search.query || 'No text query'}
                        </Typography>
                        <Typography variant="body2" color="textSecondary">
                          Filters: {search.filters.length}
                        </Typography>
                        <Typography variant="body2" color="textSecondary">
                          Created: {new Date(search.createdAt).toLocaleDateString()}
                          {search.lastUsed && ` • Last used: ${new Date(search.lastUsed).toLocaleDateString()}`}
                        </Typography>
                      </Box>
                    }
                  />
                  <ListItemSecondaryAction>
                    <Button
                      size="small"
                      onClick={() => loadSavedSearch(search)}
                      sx={{ mr: 1 }}
                    >
                      Load
                    </Button>
                    <IconButton
                      size="small"
                      onClick={() => deleteSavedSearch(search.id)}
                      color="error"
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </ListItemSecondaryAction>
                </ListItem>
              ))}
            </List>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSavedSearchesDialogOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Card>
  );
};