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
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Chip,
  Alert,
  Divider,
  Menu,
  MenuItem,
  Tooltip,
  InputAdornment,
  FormControl,
  InputLabel,
  Select,
  SelectChangeEvent,
  CircularProgress
} from '@mui/material';
import {
  BookmarkBorder as BookmarkIcon,
  Bookmark as BookmarkedIcon,
  Edit as EditIcon,
  Delete as DeleteIcon,
  Share as ShareIcon,
  FileCopy as DuplicateIcon,
  Download as ExportIcon,
  Upload as ImportIcon,
  Search as SearchIcon,
  Star as StarIcon,
  StarBorder as StarBorderIcon,
  MoreVert as MoreIcon,
  Schedule as ScheduleIcon,
  Refresh as RefreshIcon
} from '@mui/icons-material';
import { useDataProvider, useNotify, useAuthProvider } from 'react-admin';

// Types matching the API response format
interface SavedSearchResponse {
  id: number;
  name: string;
  description?: string;
  filters: any; // AdvancedSearchRequest object
  isPublic: boolean;
  createdAt: string;
  updatedAt: string;
  lastUsedAt?: string;
  useCount: number;
  tags: string[];
}

interface SavedSearch {
  id: number;
  name: string;
  description?: string;
  query?: string;
  filters: any;
  resource: string;
  createdAt: string;
  lastUsed?: string;
  usageCount: number;
  isPublic: boolean;
  tags: string[];
  createdBy: string;
}

interface SavedSearchManagerAPIProps {
  resource: string;
  onLoadSearch?: (search: SavedSearch) => void;
  currentUser?: string;
  showSharedSearches?: boolean;
}

export const SavedSearchManagerAPI: React.FC<SavedSearchManagerAPIProps> = ({
  resource,
  onLoadSearch,
  currentUser = 'current_user',
  showSharedSearches = false // Disabled until sharing is implemented
}) => {
  const [searches, setSearches] = useState<SavedSearch[]>([]);
  const [filteredSearches, setFilteredSearches] = useState<SavedSearch[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [filterBy, setFilterBy] = useState<'all' | 'mine' | 'shared' | 'favorites'>('all');
  const [selectedSearch, setSelectedSearch] = useState<SavedSearch | null>(null);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [shareDialogOpen, setShareDialogOpen] = useState(false);
  const [shareEmails, setShareEmails] = useState('');
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const [menuSearchId, setMenuSearchId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const dataProvider = useDataProvider();
  const authProvider = useAuthProvider();
  const notify = useNotify();

  // Load saved searches from API
  const loadSearches = useCallback(async () => {
    setLoading(true);
    try {
      const response = await dataProvider.getList('saved-searches', {
        pagination: { page: 1, perPage: 100 },
        sort: { field: 'updatedAt', order: 'DESC' },
        filter: {}
      });

      const apiSearches = response.data as SavedSearchResponse[];
      
      // Transform API response to component format
      const transformedSearches: SavedSearch[] = apiSearches.map(apiSearch => ({
        id: apiSearch.id,
        name: apiSearch.name,
        description: apiSearch.description,
        query: '', // Not stored separately in API
        filters: apiSearch.filters,
        resource: resource,
        createdAt: apiSearch.createdAt,
        lastUsed: apiSearch.lastUsedAt,
        usageCount: apiSearch.useCount,
        isPublic: apiSearch.isPublic,
        tags: apiSearch.tags || [],
        createdBy: currentUser // API doesn't return this yet
      }));

      setSearches(transformedSearches);
    } catch (error) {
      console.error('Failed to load saved searches:', error);
      notify('Failed to load saved searches', { type: 'error' });
    } finally {
      setLoading(false);
    }
  }, [dataProvider, resource, currentUser, notify]);

  // Load searches on mount and when resource changes
  useEffect(() => {
    loadSearches();
  }, [loadSearches]);

  // Filter searches based on query and filter type
  useEffect(() => {
    let filtered = [...searches];

    // Apply text search
    if (searchQuery.trim()) {
      filtered = filtered.filter(search =>
        search.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        search.description?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        search.tags.some(tag => tag.toLowerCase().includes(searchQuery.toLowerCase()))
      );
    }

    // Apply filter type (currently only 'all' and 'mine' are meaningful)
    switch (filterBy) {
      case 'mine':
        filtered = filtered.filter(search => search.createdBy === currentUser);
        break;
      case 'shared':
        filtered = filtered.filter(search => search.isPublic);
        break;
      case 'favorites':
        // TODO: Implement favorites when user preferences are available
        break;
    }

    // Sort by usage count and last used
    filtered.sort((a, b) => {
      if (a.lastUsed && b.lastUsed) {
        return new Date(b.lastUsed).getTime() - new Date(a.lastUsed).getTime();
      }
      
      return b.usageCount - a.usageCount;
    });

    setFilteredSearches(filtered);
  }, [searches, searchQuery, filterBy, currentUser]);

  const loadSearch = useCallback(async (search: SavedSearch) => {
    try {
      // Record usage via API
      await dataProvider.update('saved-searches', {
        id: search.id,
        data: {},
        previousData: search,
        meta: { action: 'use' }
      });

      // Update local state
      const updatedSearches = searches.map(s =>
        s.id === search.id
          ? {
              ...s,
              lastUsed: new Date().toISOString(),
              usageCount: s.usageCount + 1
            }
          : s
      );
      setSearches(updatedSearches);

      // Call the parent handler
      onLoadSearch?.(search);
      notify(`Loaded search: ${search.name}`, { type: 'success' });
    } catch (error) {
      console.error('Failed to record search usage:', error);
      // Still load the search even if usage recording fails
      onLoadSearch?.(search);
      notify(`Loaded search: ${search.name}`, { type: 'success' });
    }
  }, [dataProvider, searches, onLoadSearch, notify]);

  const duplicateSearch = useCallback(async (search: SavedSearch) => {
    setSaving(true);
    try {
      const duplicatedSearch = {
        name: `${search.name} (Copy)`,
        description: search.description,
        filters: search.filters,
        tags: search.tags
      };

      const response = await dataProvider.create('saved-searches', {
        data: duplicatedSearch
      });

      // Reload searches to get the new one
      await loadSearches();
      notify('Search duplicated successfully', { type: 'success' });
    } catch (error) {
      console.error('Failed to duplicate search:', error);
      notify('Failed to duplicate search', { type: 'error' });
    } finally {
      setSaving(false);
    }
  }, [dataProvider, loadSearches, notify]);

  const deleteSearch = useCallback(async (searchId: number) => {
    setSaving(true);
    try {
      await dataProvider.delete('saved-searches', {
        id: searchId,
        previousData: selectedSearch || undefined
      });

      // Remove from local state
      setSearches(searches.filter(s => s.id !== searchId));
      setDeleteDialogOpen(false);
      setSelectedSearch(null);
      notify('Search deleted successfully', { type: 'info' });
    } catch (error) {
      console.error('Failed to delete search:', error);
      notify('Failed to delete search', { type: 'error' });
    } finally {
      setSaving(false);
    }
  }, [dataProvider, searches, selectedSearch, notify]);

  const updateSearch = useCallback(async (updatedSearch: SavedSearch) => {
    setSaving(true);
    try {
      const updateData = {
        name: updatedSearch.name,
        description: updatedSearch.description,
        filters: updatedSearch.filters,
        tags: updatedSearch.tags
      };

      await dataProvider.update('saved-searches', {
        id: updatedSearch.id,
        data: updateData,
        previousData: selectedSearch || undefined
      });

      // Update local state
      setSearches(searches.map(s =>
        s.id === updatedSearch.id ? updatedSearch : s
      ));
      
      setEditDialogOpen(false);
      setSelectedSearch(null);
      notify('Search updated successfully', { type: 'success' });
    } catch (error) {
      console.error('Failed to update search:', error);
      notify('Failed to update search', { type: 'error' });
    } finally {
      setSaving(false);
    }
  }, [dataProvider, searches, selectedSearch, notify]);

  const toggleFavorite = useCallback((searchId: number) => {
    // TODO: Implement when user preferences/favorites are available
    notify('Favorites feature coming soon', { type: 'info' });
  }, [notify]);

  const shareSearch = useCallback(() => {
    // TODO: Implement sharing when backend supports it
    notify('Sharing feature coming soon', { type: 'info' });
    setShareDialogOpen(false);
    setShareEmails('');
  }, [notify]);

  const exportSearches = useCallback(() => {
    const exportData = {
      version: '1.0',
      resource,
      searches: searches,
      exportedAt: new Date().toISOString(),
      exportedBy: currentUser
    };

    const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `saved-searches-${resource}-${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    
    notify('Searches exported successfully', { type: 'success' });
  }, [searches, resource, currentUser, notify]);

  const importSearches = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
    // TODO: Implement import functionality
    notify('Import feature coming soon', { type: 'info' });
    event.target.value = '';
  }, [notify]);

  const handleMenuOpen = (event: React.MouseEvent<HTMLElement>, searchId: number) => {
    setAnchorEl(event.currentTarget);
    setMenuSearchId(searchId);
  };

  const handleMenuClose = () => {
    setAnchorEl(null);
    setMenuSearchId(null);
  };

  const formatLastUsed = (dateString?: string) => {
    if (!dateString) return 'Never used';
    const date = new Date(dateString);
    const now = new Date();
    const diffInHours = Math.abs(now.getTime() - date.getTime()) / (1000 * 60 * 60);
    
    if (diffInHours < 1) return 'Just now';
    if (diffInHours < 24) return `${Math.floor(diffInHours)} hours ago`;
    if (diffInHours < 168) return `${Math.floor(diffInHours / 24)} days ago`;
    
    return date.toLocaleDateString();
  };

  const formatFiltersPreview = (filters: any) => {
    if (!filters) return 'No filters';
    
    // Count meaningful filter values
    let filterCount = 0;
    const filterSummary: string[] = [];
    
    // Common filter fields to check
    const commonFields = ['eventType', 'riskLevel', 'startDate', 'endDate', 'fullTextQuery'];
    
    commonFields.forEach(field => {
      if (filters[field] && filters[field] !== '' && filters[field] !== null) {
        filterCount++;
        if (field === 'fullTextQuery' && filters[field].trim()) {
          filterSummary.push(`Text: "${filters[field].trim()}"`);
        } else if (field === 'riskLevel' && Array.isArray(filters[field]) && filters[field].length > 0) {
          filterSummary.push(`Risk: ${filters[field].join(', ')}`);
        } else if (field === 'eventType' && Array.isArray(filters[field]) && filters[field].length > 0) {
          filterSummary.push(`Events: ${filters[field].length}`);
        }
      }
    });
    
    if (filterSummary.length > 0) {
      return filterSummary.slice(0, 2).join(' • ');
    }
    
    return filterCount > 0 ? `${filterCount} filters` : 'No filters';
  };

  return (
    <Box>
      {/* Header and Controls */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h6">
          Saved Searches ({filteredSearches.length})
        </Typography>
        
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button
            size="small"
            startIcon={<RefreshIcon />}
            onClick={loadSearches}
            disabled={loading}
          >
            Refresh
          </Button>
          
          <input
            type="file"
            accept=".json"
            style={{ display: 'none' }}
            id="import-searches"
            onChange={importSearches}
          />
          <label htmlFor="import-searches">
            <Button component="span" size="small" startIcon={<ImportIcon />}>
              Import
            </Button>
          </label>
          
          <Button
            size="small"
            startIcon={<ExportIcon />}
            onClick={exportSearches}
            disabled={searches.length === 0}
          >
            Export
          </Button>
        </Box>
      </Box>

      {/* Search and Filter */}
      <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
        <TextField
          size="small"
          placeholder="Search saved searches..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" />
              </InputAdornment>
            )
          }}
          sx={{ flexGrow: 1 }}
        />
        
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel>Filter</InputLabel>
          <Select
            value={filterBy}
            label="Filter"
            onChange={(e: SelectChangeEvent<typeof filterBy>) => setFilterBy(e.target.value as typeof filterBy)}
          >
            <MenuItem value="all">All Searches</MenuItem>
            <MenuItem value="mine">My Searches</MenuItem>
            {showSharedSearches && <MenuItem value="shared">Public</MenuItem>}
          </Select>
        </FormControl>
      </Box>

      {/* Searches List */}
      <Card>
        {loading ? (
          <CardContent sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress />
          </CardContent>
        ) : filteredSearches.length === 0 ? (
          <CardContent>
            <Typography color="textSecondary" align="center">
              {searchQuery.trim() 
                ? 'No searches match your query' 
                : 'No saved searches yet'
              }
            </Typography>
          </CardContent>
        ) : (
          <List>
            {filteredSearches.map((search, index) => (
              <React.Fragment key={search.id}>
                <ListItem>
                  <ListItemText
                    primary={
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <Typography variant="subtitle1" component="span">
                          {search.name}
                        </Typography>
                        {search.isPublic && <ShareIcon fontSize="small" color="action" />}
                        {search.tags.map(tag => (
                          <Chip key={tag} label={tag} size="small" variant="outlined" />
                        ))}
                      </Box>
                    }
                    secondary={
                      <Box>
                        {search.description && (
                          <Typography variant="body2" color="textSecondary" gutterBottom>
                            {search.description}
                          </Typography>
                        )}
                        <Typography variant="caption" color="textSecondary">
                          {formatFiltersPreview(search.filters)} • 
                          Used: {search.usageCount} times • 
                          {formatLastUsed(search.lastUsed)}
                        </Typography>
                      </Box>
                    }
                  />
                  
                  <ListItemSecondaryAction>
                    <Box sx={{ display: 'flex', alignItems: 'center' }}>
                      <Button
                        size="small"
                        variant="outlined"
                        onClick={() => loadSearch(search)}
                        sx={{ mr: 1 }}
                        disabled={saving}
                      >
                        Load
                      </Button>
                      
                      <IconButton size="small" onClick={(e) => handleMenuOpen(e, search.id)}>
                        <MoreIcon />
                      </IconButton>
                    </Box>
                  </ListItemSecondaryAction>
                </ListItem>
                {index < filteredSearches.length - 1 && <Divider />}
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
        <MenuItem onClick={() => {
          const search = searches.find(s => s.id === menuSearchId);
          if (search) {
            setSelectedSearch(search);
            setEditDialogOpen(true);
          }
          handleMenuClose();
        }}>
          <EditIcon fontSize="small" sx={{ mr: 1 }} />
          Edit
        </MenuItem>
        
        <MenuItem onClick={() => {
          const search = searches.find(s => s.id === menuSearchId);
          if (search) duplicateSearch(search);
          handleMenuClose();
        }}>
          <DuplicateIcon fontSize="small" sx={{ mr: 1 }} />
          Duplicate
        </MenuItem>
        
        <Divider />
        
        <MenuItem onClick={() => {
          const search = searches.find(s => s.id === menuSearchId);
          if (search) {
            setSelectedSearch(search);
            setDeleteDialogOpen(true);
          }
          handleMenuClose();
        }}>
          <DeleteIcon fontSize="small" sx={{ mr: 1 }} />
          Delete
        </MenuItem>
      </Menu>

      {/* Edit Dialog */}
      {selectedSearch && (
        <Dialog open={editDialogOpen} onClose={() => setEditDialogOpen(false)} maxWidth="md" fullWidth>
          <DialogTitle>Edit Saved Search</DialogTitle>
          <DialogContent>
            <TextField
              autoFocus
              margin="dense"
              label="Name"
              fullWidth
              variant="outlined"
              value={selectedSearch.name}
              onChange={(e) => setSelectedSearch({ ...selectedSearch, name: e.target.value })}
              sx={{ mb: 2 }}
            />
            <TextField
              margin="dense"
              label="Description"
              fullWidth
              multiline
              rows={2}
              variant="outlined"
              value={selectedSearch.description || ''}
              onChange={(e) => setSelectedSearch({ ...selectedSearch, description: e.target.value })}
              sx={{ mb: 2 }}
            />
            <TextField
              margin="dense"
              label="Tags (comma separated)"
              fullWidth
              variant="outlined"
              value={selectedSearch.tags.join(', ')}
              onChange={(e) => setSelectedSearch({ 
                ...selectedSearch, 
                tags: e.target.value.split(',').map(tag => tag.trim()).filter(Boolean)
              })}
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setEditDialogOpen(false)}>Cancel</Button>
            <Button onClick={() => updateSearch(selectedSearch)} variant="contained" disabled={saving}>
              {saving ? <CircularProgress size={20} /> : 'Save Changes'}
            </Button>
          </DialogActions>
        </Dialog>
      )}

      {/* Delete Confirmation Dialog */}
      {selectedSearch && (
        <Dialog open={deleteDialogOpen} onClose={() => setDeleteDialogOpen(false)}>
          <DialogTitle>Delete Saved Search</DialogTitle>
          <DialogContent>
            <Alert severity="warning" sx={{ mb: 2 }}>
              This action cannot be undone.
            </Alert>
            <Typography>
              Are you sure you want to delete "{selectedSearch.name}"?
            </Typography>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDeleteDialogOpen(false)}>Cancel</Button>
            <Button 
              onClick={() => deleteSearch(selectedSearch.id)} 
              color="error" 
              variant="contained"
              disabled={saving}
            >
              {saving ? <CircularProgress size={20} /> : 'Delete'}
            </Button>
          </DialogActions>
        </Dialog>
      )}
    </Box>
  );
};