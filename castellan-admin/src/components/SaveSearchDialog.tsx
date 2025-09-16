import React, { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Button,
  Box,
  Typography,
  Chip,
  FormControlLabel,
  Checkbox,
  Alert,
  CircularProgress
} from '@mui/material';
import { Save as SaveIcon, Cancel as CancelIcon } from '@mui/icons-material';
import { useDataProvider, useNotify } from 'react-admin';

interface SaveSearchDialogProps {
  open: boolean;
  onClose: () => void;
  searchFilters: any; // AdvancedSearchRequest object
  initialName?: string;
  onSaved?: (savedSearch: any) => void;
}

export const SaveSearchDialog: React.FC<SaveSearchDialogProps> = ({
  open,
  onClose,
  searchFilters,
  initialName = '',
  onSaved
}) => {
  const [name, setName] = useState(initialName);
  const [description, setDescription] = useState('');
  const [tags, setTags] = useState<string[]>([]);
  const [tagsInput, setTagsInput] = useState('');
  const [isPublic, setIsPublic] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const dataProvider = useDataProvider();
  const notify = useNotify();

  const handleSave = async () => {
    if (!name.trim()) {
      setError('Name is required');
      return;
    }

    setSaving(true);
    setError(null);

    try {
      const response = await dataProvider.create('saved-searches', {
        data: {
          name: name.trim(),
          description: description.trim() || undefined,
          filters: searchFilters,
          tags: tags,
          isPublic: isPublic
        }
      });

      notify(`Saved search "${name}" created successfully`, { type: 'success' });
      onSaved?.(response.data);
      onClose();
      
      // Reset form
      setName('');
      setDescription('');
      setTags([]);
      setTagsInput('');
      setIsPublic(false);
      setError(null);
    } catch (error) {
      console.error('Failed to save search:', error);
      setError('Failed to save search. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  const handleTagsInputKeyPress = (event: React.KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      const newTag = tagsInput.trim();
      if (newTag && !tags.includes(newTag)) {
        setTags([...tags, newTag]);
        setTagsInput('');
      }
    }
  };

  const removeTag = (tagToRemove: string) => {
    setTags(tags.filter(tag => tag !== tagToRemove));
  };

  const handleClose = () => {
    if (!saving) {
      onClose();
      // Reset form on close
      setName('');
      setDescription('');
      setTags([]);
      setTagsInput('');
      setIsPublic(false);
      setError(null);
    }
  };

  const formatSearchPreview = (filters: any) => {
    if (!filters) return 'No filters';
    
    const summary: string[] = [];
    
    if (filters.fullTextQuery?.trim()) {
      summary.push(`Text: "${filters.fullTextQuery.trim()}"`);
    }
    
    if (filters.riskLevels?.length > 0) {
      summary.push(`Risk: ${filters.riskLevels.join(', ')}`);
    }
    
    if (filters.eventTypes?.length > 0) {
      summary.push(`Events: ${filters.eventTypes.length}`);
    }
    
    if (filters.startDate && filters.endDate) {
      const start = new Date(filters.startDate).toLocaleDateString();
      const end = new Date(filters.endDate).toLocaleDateString();
      summary.push(start === end ? `Date: ${start}` : `Date: ${start} - ${end}`);
    } else if (filters.startDate) {
      summary.push(`Since: ${new Date(filters.startDate).toLocaleDateString()}`);
    } else if (filters.endDate) {
      summary.push(`Until: ${new Date(filters.endDate).toLocaleDateString()}`);
    }
    
    if (summary.length === 0) {
      let filterCount = 0;
      Object.keys(filters).forEach(key => {
        if (filters[key] && filters[key] !== '' && filters[key] !== null) {
          if (!['page', 'pageSize', 'sortField', 'sortOrder', 'includeArchivedEvents', 'enableFuzzySearch'].includes(key)) {
            filterCount++;
          }
        }
      });
      return filterCount > 0 ? `${filterCount} filters` : 'No filters';
    }
    
    return summary.join(' • ');
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle>Save Search</DialogTitle>
      <DialogContent>
        {/* Search Preview */}
        <Box sx={{ mb: 3, p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
          <Typography variant="subtitle2" gutterBottom>
            Search to be saved:
          </Typography>
          <Typography variant="body2" color="textSecondary">
            {formatSearchPreview(searchFilters)}
          </Typography>
        </Box>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {/* Name Field */}
        <TextField
          autoFocus
          margin="dense"
          label="Search Name"
          fullWidth
          variant="outlined"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g., High Risk Login Events"
          sx={{ mb: 2 }}
          required
          error={!name.trim() && error !== null}
          helperText={!name.trim() && error !== null ? 'Name is required' : ''}
        />

        {/* Description Field */}
        <TextField
          margin="dense"
          label="Description (optional)"
          fullWidth
          multiline
          rows={2}
          variant="outlined"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="Describe what this search is for..."
          sx={{ mb: 2 }}
        />

        {/* Tags Field */}
        <TextField
          margin="dense"
          label="Tags"
          fullWidth
          variant="outlined"
          value={tagsInput}
          onChange={(e) => setTagsInput(e.target.value)}
          onKeyPress={handleTagsInputKeyPress}
          placeholder="Type tags and press Enter or comma to add..."
          helperText="Tags help organize and find your saved searches"
          sx={{ mb: 1 }}
        />

        {/* Tags Display */}
        {tags.length > 0 && (
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mb: 2 }}>
            {tags.map((tag) => (
              <Chip
                key={tag}
                label={tag}
                size="small"
                onDelete={() => removeTag(tag)}
                variant="outlined"
              />
            ))}
          </Box>
        )}

        {/* Public Checkbox */}
        <FormControlLabel
          control={
            <Checkbox
              checked={isPublic}
              onChange={(e) => setIsPublic(e.target.checked)}
            />
          }
          label="Make this search public (visible to other users)"
          sx={{ mt: 1 }}
        />
      </DialogContent>

      <DialogActions>
        <Button onClick={handleClose} disabled={saving}>
          <CancelIcon sx={{ mr: 1 }} />
          Cancel
        </Button>
        <Button 
          onClick={handleSave} 
          variant="contained" 
          disabled={saving || !name.trim()}
        >
          {saving ? (
            <CircularProgress size={20} sx={{ mr: 1 }} />
          ) : (
            <SaveIcon sx={{ mr: 1 }} />
          )}
          Save Search
        </Button>
      </DialogActions>
    </Dialog>
  );
};