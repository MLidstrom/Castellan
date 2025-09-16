import React, { useState, useEffect } from 'react';
import {
  Drawer,
  Box,
  Typography,
  Button,
  IconButton,
  Divider,
  Grid,
  Chip,
  Paper,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  FormControlLabel,
  Switch,
  Alert
} from '@mui/material';
import {
  Close as CloseIcon,
  Search as SearchIcon,
  Clear as ClearIcon,
  Save as SaveIcon,
  History as HistoryIcon,
  ExpandMore as ExpandMoreIcon,
  FilterList as FilterListIcon
} from '@mui/icons-material';

// Import our custom filter components
import { FullTextSearchInput } from './FullTextSearchInput';
import { DateRangePicker } from './DateRangePicker';
import { MultiSelectFilter } from './MultiSelectFilter';
import { RangeSliderFilter } from './RangeSliderFilter';
import { MitreTechniqueFilter } from './MitreTechniqueFilter';

export interface AdvancedSearchFilters {
  fullTextQuery?: string;
  useExactMatch?: boolean;
  startDate?: Date;
  endDate?: Date;
  riskLevels?: string[];
  eventTypes?: string[];
  machines?: string[];
  users?: string[];
  sources?: string[];
  mitreTechniques?: string[];
  minConfidence?: number;
  maxConfidence?: number;
  minCorrelationScore?: number;
  maxCorrelationScore?: number;
  minBurstScore?: number;
  maxBurstScore?: number;
  minAnomalyScore?: number;
  maxAnomalyScore?: number;
  statuses?: string[];
  ipAddresses?: string[];
  enableFuzzySearch?: boolean;
}

export interface AdvancedSearchDrawerProps {
  open: boolean;
  onClose: () => void;
  onSearch: (filters: AdvancedSearchFilters) => void;
  onClearAll: () => void;
  initialFilters?: AdvancedSearchFilters;
  isLoading?: boolean;
  searchResults?: {
    total: number;
    queryTime: number;
  };
}

export const AdvancedSearchDrawer = React.forwardRef<HTMLDivElement, AdvancedSearchDrawerProps>(({
  open,
  onClose,
  onSearch,
  onClearAll,
  initialFilters = {},
  isLoading = false,
  searchResults
}, ref) => {
  const [filters, setFilters] = useState<AdvancedSearchFilters>(initialFilters);
  const [expandedSections, setExpandedSections] = useState<Set<string>>(
    new Set(['text', 'date', 'categories'])
  );

  // Update filters when initialFilters change
  useEffect(() => {
    setFilters(initialFilters);
  }, [initialFilters]);

  const handleFilterChange = (key: keyof AdvancedSearchFilters, value: any) => {
    setFilters(prev => ({
      ...prev,
      [key]: value
    }));
  };

  const handleSearch = () => {
    onSearch(filters);
  };

  const handleClearAll = () => {
    setFilters({});
    onClearAll();
  };

  const toggleSection = (section: string) => {
    setExpandedSections(prev => {
      const newSet = new Set(prev);
      if (newSet.has(section)) {
        newSet.delete(section);
      } else {
        newSet.add(section);
      }
      return newSet;
    });
  };

  const getActiveFilterCount = (): number => {
    let count = 0;
    if (filters.fullTextQuery) count++;
    if (filters.startDate || filters.endDate) count++;
    if (filters.riskLevels?.length) count++;
    if (filters.eventTypes?.length) count++;
    if (filters.mitreTechniques?.length) count++;
    if (filters.minConfidence !== undefined || filters.maxConfidence !== undefined) count++;
    if (filters.minCorrelationScore !== undefined || filters.maxCorrelationScore !== undefined) count++;
    return count;
  };

  const riskLevelOptions = [
    { id: 'critical', name: 'Critical' },
    { id: 'high', name: 'High' },
    { id: 'medium', name: 'Medium' },
    { id: 'low', name: 'Low' }
  ];

  const eventTypeOptions = [
    { id: 'AuthenticationFailure', name: 'Authentication Failure' },
    { id: 'AuthenticationSuccess', name: 'Authentication Success' },
    { id: 'ProcessCreation', name: 'Process Creation' },
    { id: 'PrivilegeEscalation', name: 'Privilege Escalation' },
    { id: 'NetworkConnection', name: 'Network Connection' },
    { id: 'FileSystemActivity', name: 'File System Activity' },
    { id: 'BurstActivity', name: 'Burst Activity' },
    { id: 'SuspiciousActivity', name: 'Suspicious Activity' }
  ];

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      sx={{ zIndex: 1300 }}
      ref={ref}
      keepMounted={false}
      disablePortal
      ModalProps={{
        keepMounted: false,
        // Render at body level to avoid aria-hidden conflicts with #root
        container: document.body,
        // Important: this prevents the aria-hidden warning in some browsers
        hideBackdrop: true
      }}
      PaperProps={{
        sx: {
          width: { xs: '100%', sm: 480 },
          maxWidth: '100%'
        },
        // Ensure the drawer can receive focus
        tabIndex: -1,
        role: 'dialog',
        'aria-modal': true,
        ref: ref as any
      }}
    >
    <Box 
      sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}
      // Important: ensure content is not hidden from screen readers
      role="dialog"
      aria-modal="true"
      tabIndex={-1}
    >
        {/* Header */}
        <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <FilterListIcon color="primary" />
              <Typography variant="h6">
                Advanced Search
              </Typography>
              {getActiveFilterCount() > 0 && (
                <Chip
                  label={getActiveFilterCount()}
                  size="small"
                  color="primary"
                  variant="filled"
                />
              )}
            </Box>
            <IconButton onClick={onClose} edge="end">
              <CloseIcon />
            </IconButton>
          </Box>
          
          {/* Search Results Summary */}
          {searchResults && (
            <Alert severity="info" sx={{ mt: 1 }}>
              Found {searchResults.total.toLocaleString()} results in {searchResults.queryTime}ms
            </Alert>
          )}
        </Box>

        {/* Filters Content */}
        <Box sx={{ flex: 1, overflow: 'auto', p: 2 }}>
          {/* Full-Text Search Section */}
          <Accordion
            expanded={expandedSections.has('text')}
            onChange={() => toggleSection('text')}
            sx={{ mb: 1 }}
          >
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle1" sx={{ fontWeight: 'medium' }}>
                Text Search
              </Typography>
              {filters.fullTextQuery && (
                <Chip label="Active" size="small" color="primary" sx={{ ml: 1 }} />
              )}
            </AccordionSummary>
            <AccordionDetails>
              <FullTextSearchInput
                value={filters.fullTextQuery || ''}
                onChange={(value) => handleFilterChange('fullTextQuery', value)}
                exactMatch={filters.useExactMatch || false}
                onExactMatchChange={(checked) => handleFilterChange('useExactMatch', checked)}
                placeholder="Search messages, descriptions, event data..."
              />
              
              <FormControlLabel
                control={
                  <Switch
                    checked={filters.enableFuzzySearch !== false}
                    onChange={(e) => handleFilterChange('enableFuzzySearch', e.target.checked)}
                    size="small"
                  />
                }
                label="Enable fuzzy search"
                sx={{ mt: 1 }}
              />
            </AccordionDetails>
          </Accordion>

          {/* Date Range Section */}
          <Accordion
            expanded={expandedSections.has('date')}
            onChange={() => toggleSection('date')}
            sx={{ mb: 1 }}
          >
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle1" sx={{ fontWeight: 'medium' }}>
                Date Range
              </Typography>
              {(filters.startDate || filters.endDate) && (
                <Chip label="Active" size="small" color="primary" sx={{ ml: 1 }} />
              )}
            </AccordionSummary>
            <AccordionDetails>
              <DateRangePicker
                startDate={filters.startDate}
                endDate={filters.endDate}
                onStartDateChange={(date) => handleFilterChange('startDate', date)}
                onEndDateChange={(date) => handleFilterChange('endDate', date)}
              />
            </AccordionDetails>
          </Accordion>

          {/* Categories Section */}
          <Accordion
            expanded={expandedSections.has('categories')}
            onChange={() => toggleSection('categories')}
            sx={{ mb: 1 }}
          >
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle1" sx={{ fontWeight: 'medium' }}>
                Categories
              </Typography>
              {(filters.riskLevels?.length || filters.eventTypes?.length) && (
                <Chip label="Active" size="small" color="primary" sx={{ ml: 1 }} />
              )}
            </AccordionSummary>
            <AccordionDetails>
              <Grid container spacing={2}>
                <Grid item xs={12}>
                  <MultiSelectFilter
                    label="Risk Levels"
                    options={riskLevelOptions}
                    selectedValues={filters.riskLevels || []}
                    onChange={(values) => handleFilterChange('riskLevels', values)}
                    colorMapping={{
                      critical: '#f44336',
                      high: '#ff9800',
                      medium: '#2196f3',
                      low: '#4caf50'
                    }}
                  />
                </Grid>
                <Grid item xs={12}>
                  <MultiSelectFilter
                    label="Event Types"
                    options={eventTypeOptions}
                    selectedValues={filters.eventTypes || []}
                    onChange={(values) => handleFilterChange('eventTypes', values)}
                  />
                </Grid>
              </Grid>
            </AccordionDetails>
          </Accordion>

          {/* MITRE Techniques Section */}
          <Accordion
            expanded={expandedSections.has('mitre')}
            onChange={() => toggleSection('mitre')}
            sx={{ mb: 1 }}
          >
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle1" sx={{ fontWeight: 'medium' }}>
                MITRE ATT&CK Techniques
              </Typography>
              {filters.mitreTechniques?.length && (
                <Chip label={filters.mitreTechniques.length} size="small" color="primary" sx={{ ml: 1 }} />
              )}
            </AccordionSummary>
            <AccordionDetails>
              <MitreTechniqueFilter
                selectedTechniques={filters.mitreTechniques || []}
                onChange={(techniques) => handleFilterChange('mitreTechniques', techniques)}
              />
            </AccordionDetails>
          </Accordion>

          {/* Score Ranges Section */}
          <Accordion
            expanded={expandedSections.has('scores')}
            onChange={() => toggleSection('scores')}
            sx={{ mb: 1 }}
          >
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Typography variant="subtitle1" sx={{ fontWeight: 'medium' }}>
                Score Ranges
              </Typography>
            </AccordionSummary>
            <AccordionDetails>
              <Grid container spacing={2}>
                <Grid item xs={12}>
                  <RangeSliderFilter
                    label="Confidence Score"
                    min={0}
                    max={100}
                    value={[filters.minConfidence || 0, filters.maxConfidence || 100]}
                    onChange={(values) => {
                      handleFilterChange('minConfidence', values[0]);
                      handleFilterChange('maxConfidence', values[1]);
                    }}
                    step={1}
                    unit="%"
                  />
                </Grid>
                <Grid item xs={12}>
                  <RangeSliderFilter
                    label="Correlation Score"
                    min={0}
                    max={1}
                    value={[filters.minCorrelationScore || 0, filters.maxCorrelationScore || 1]}
                    onChange={(values) => {
                      handleFilterChange('minCorrelationScore', values[0]);
                      handleFilterChange('maxCorrelationScore', values[1]);
                    }}
                    step={0.01}
                  />
                </Grid>
              </Grid>
            </AccordionDetails>
          </Accordion>
        </Box>

        {/* Footer Actions */}
        <Paper sx={{ p: 2, borderTop: 1, borderColor: 'divider' }} square>
          <Grid container spacing={1}>
            <Grid item xs={6}>
              <Button
                fullWidth
                variant="outlined"
                startIcon={<ClearIcon />}
                onClick={handleClearAll}
                disabled={getActiveFilterCount() === 0}
              >
                Clear All
              </Button>
            </Grid>
            <Grid item xs={6}>
              <Button
                fullWidth
                variant="contained"
                startIcon={<SearchIcon />}
                onClick={handleSearch}
                disabled={isLoading}
              >
                {isLoading ? 'Searching...' : 'Search'}
              </Button>
            </Grid>
          </Grid>
        </Paper>
      </Box>
    </Drawer>
  );
});
