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
  SelectChangeEvent
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
  Schedule as ScheduleIcon
} from '@mui/icons-material';
import { useNotify } from 'react-admin';

interface SavedSearch {
  id: string;
  name: string;
  description?: string;
  query: string;
  filters: SearchFilter[];
  resource: string;
  createdAt: string;
  lastUsed?: string;
  usageCount: number;
  isPublic: boolean;
  isFavorite: boolean;
  tags: string[];
  createdBy: string;
  sharedWith?: string[];
}

interface SearchFilter {
  field: string;
  operator: 'equals' | 'contains' | 'startsWith' | 'endsWith' | 'gt' | 'lt' | 'between' | 'in';
  value: any;
  label?: string;
}

interface SavedSearchManagerProps {
  resource: string;
  onLoadSearch?: (search: SavedSearch) => void;
  currentUser?: string;
  showSharedSearches?: boolean;
}

export const SavedSearchManager: React.FC<SavedSearchManagerProps> = ({
  resource,
  onLoadSearch,
  currentUser = 'current_user',
  showSharedSearches = true
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
  const [menuSearchId, setMenuSearchId] = useState<string | null>(null);

  const notify = useNotify();

  // Load saved searches from localStorage
  useEffect(() => {
    const loadSearches = () => {
      const allSearchesKey = `savedSearches_${resource}`;
      const saved = localStorage.getItem(allSearchesKey);
      if (saved) {
        try {
          const parsedSearches: SavedSearch[] = JSON.parse(saved);
          setSearches(parsedSearches);
        } catch (error) {
          console.error('Failed to load saved searches:', error);
        }
      }
    };

    loadSearches();
  }, [resource]);

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

    // Apply filter type
    switch (filterBy) {
      case 'mine':
        filtered = filtered.filter(search => search.createdBy === currentUser);
        break;
      case 'shared':
        filtered = filtered.filter(search => 
          search.isPublic || search.sharedWith?.includes(currentUser)
        );
        break;
      case 'favorites':
        filtered = filtered.filter(search => search.isFavorite);
        break;
    }

    // Sort by usage count and last used
    filtered.sort((a, b) => {
      if (a.isFavorite && !b.isFavorite) return -1;
      if (!a.isFavorite && b.isFavorite) return 1;
      
      if (a.lastUsed && b.lastUsed) {
        return new Date(b.lastUsed).getTime() - new Date(a.lastUsed).getTime();
      }
      
      return b.usageCount - a.usageCount;
    });

    setFilteredSearches(filtered);
  }, [searches, searchQuery, filterBy, currentUser]);

  // Save searches to localStorage
  const saveSearches = useCallback((updatedSearches: SavedSearch[]) => {
    const allSearchesKey = `savedSearches_${resource}`;
    localStorage.setItem(allSearchesKey, JSON.stringify(updatedSearches));
    setSearches(updatedSearches);
  }, [resource]);

  const toggleFavorite = useCallback((searchId: string) => {
    const updatedSearches = searches.map(search =>
      search.id === searchId 
        ? { ...search, isFavorite: !search.isFavorite }
        : search
    );
    saveSearches(updatedSearches);
    notify(`Search ${updatedSearches.find(s => s.id === searchId)?.isFavorite ? 'added to' : 'removed from'} favorites`, { type: 'info' });
  }, [searches, saveSearches, notify]);

  const loadSearch = useCallback((search: SavedSearch) => {
    // Update usage statistics
    const updatedSearches = searches.map(s =>
      s.id === search.id
        ? {
            ...s,
            lastUsed: new Date().toISOString(),
            usageCount: s.usageCount + 1
          }
        : s
    );
    saveSearches(updatedSearches);

    // Call the parent handler
    onLoadSearch?.(search);
    notify(`Loaded search: ${search.name}`, { type: 'success' });
  }, [searches, saveSearches, onLoadSearch, notify]);

  const duplicateSearch = useCallback((search: SavedSearch) => {
    const duplicatedSearch: SavedSearch = {
      ...search,
      id: Date.now().toString(),
      name: `${search.name} (Copy)`,
      createdAt: new Date().toISOString(),
      lastUsed: undefined,
      usageCount: 0,
      createdBy: currentUser,
      isPublic: false,
      sharedWith: undefined
    };

    saveSearches([...searches, duplicatedSearch]);
    notify('Search duplicated successfully', { type: 'success' });
  }, [searches, saveSearches, currentUser, notify]);

  const deleteSearch = useCallback((searchId: string) => {
    const updatedSearches = searches.filter(s => s.id !== searchId);
    saveSearches(updatedSearches);
    setDeleteDialogOpen(false);
    setSelectedSearch(null);
    notify('Search deleted successfully', { type: 'info' });
  }, [searches, saveSearches, notify]);

  const updateSearch = useCallback((updatedSearch: SavedSearch) => {
    const updatedSearches = searches.map(s =>
      s.id === updatedSearch.id ? updatedSearch : s
    );
    saveSearches(updatedSearches);
    setEditDialogOpen(false);
    setSelectedSearch(null);
    notify('Search updated successfully', { type: 'success' });
  }, [searches, saveSearches, notify]);

  const shareSearch = useCallback(() => {
    if (!selectedSearch) return;

    const emails = shareEmails.split(',').map(email => email.trim()).filter(Boolean);
    const updatedSearch: SavedSearch = {
      ...selectedSearch,
      sharedWith: [...(selectedSearch.sharedWith || []), ...emails]
    };

    updateSearch(updatedSearch);
    setShareDialogOpen(false);
    setShareEmails('');
    notify(`Search shared with ${emails.length} user(s)`, { type: 'success' });
  }, [selectedSearch, shareEmails, updateSearch, notify]);

  const exportSearches = useCallback(() => {
    const exportData = {
      version: '1.0',
      resource,
      searches: searches.filter(s => s.createdBy === currentUser),
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
    const file = event.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const importData = JSON.parse(e.target?.result as string);
        const importedSearches = importData.searches.map((search: SavedSearch) => ({
          ...search,
          id: Date.now() + Math.random().toString(), // Generate new ID
          createdBy: currentUser, // Set current user as owner
          createdAt: new Date().toISOString(),
          lastUsed: undefined,
          usageCount: 0
        }));

        saveSearches([...searches, ...importedSearches]);
        notify(`Imported ${importedSearches.length} search(es)`, { type: 'success' });
      } catch (error) {
        notify('Failed to import searches', { type: 'error' });
        console.error('Import error:', error);
      }
    };
    reader.readAsText(file);

    // Reset file input
    event.target.value = '';
  }, [searches, saveSearches, currentUser, notify]);

  const handleMenuOpen = (event: React.MouseEvent<HTMLElement>, searchId: string) => {
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

  return (
    <Box>
      {/* Header and Controls */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h6">
          Saved Searches ({filteredSearches.length})
        </Typography>
        
        <Box sx={{ display: 'flex', gap: 1 }}>
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
            disabled={searches.filter(s => s.createdBy === currentUser).length === 0}
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
            <MenuItem value="favorites">Favorites</MenuItem>
            {showSharedSearches && <MenuItem value="shared">Shared</MenuItem>}
          </Select>
        </FormControl>
      </Box>

      {/* Searches List */}
      <Card>
        {filteredSearches.length === 0 ? (
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
                        {search.isFavorite && <StarIcon fontSize="small" color="primary" />}
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
                          Query: "{search.query || 'No text query'}" • 
                          Filters: {search.filters.length} • 
                          Used: {search.usageCount} times • 
                          {formatLastUsed(search.lastUsed)}
                        </Typography>
                      </Box>
                    }
                  />
                  
                  <ListItemSecondaryAction>
                    <Box sx={{ display: 'flex', alignItems: 'center' }}>
                      <Tooltip title={search.isFavorite ? 'Remove from favorites' : 'Add to favorites'}>
                        <IconButton size="small" onClick={() => toggleFavorite(search.id)}>
                          {search.isFavorite ? <StarIcon color="primary" /> : <StarBorderIcon />}
                        </IconButton>
                      </Tooltip>
                      
                      <Button
                        size="small"
                        variant="outlined"
                        onClick={() => loadSearch(search)}
                        sx={{ mr: 1 }}
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
        
        <MenuItem onClick={() => {
          const search = searches.find(s => s.id === menuSearchId);
          if (search && search.createdBy === currentUser) {
            setSelectedSearch(search);
            setShareDialogOpen(true);
          }
          handleMenuClose();
        }}>
          <ShareIcon fontSize="small" sx={{ mr: 1 }} />
          Share
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
            <Button onClick={() => updateSearch(selectedSearch)} variant="contained">
              Save Changes
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
            <Button onClick={() => deleteSearch(selectedSearch.id)} color="error" variant="contained">
              Delete
            </Button>
          </DialogActions>
        </Dialog>
      )}

      {/* Share Dialog */}
      {selectedSearch && (
        <Dialog open={shareDialogOpen} onClose={() => setShareDialogOpen(false)}>
          <DialogTitle>Share Search: {selectedSearch.name}</DialogTitle>
          <DialogContent>
            <TextField
              autoFocus
              margin="dense"
              label="Email addresses (comma separated)"
              fullWidth
              variant="outlined"
              value={shareEmails}
              onChange={(e) => setShareEmails(e.target.value)}
              placeholder="user1@example.com, user2@example.com"
              helperText="Enter email addresses of users to share this search with"
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setShareDialogOpen(false)}>Cancel</Button>
            <Button onClick={shareSearch} variant="contained" disabled={!shareEmails.trim()}>
              Share
            </Button>
          </DialogActions>
        </Dialog>
      )}
    </Box>
  );
};