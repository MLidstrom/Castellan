import React from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  ChipField,
  NumberField,
  FunctionField,
  TopToolbar,
  FilterButton,
  ExportButton,
  CreateButton,
  Button
} from 'react-admin';
import {
  Box,
  Alert
} from '@mui/material';
import {
  FilterList as FilterListIcon,
  Share as ShareIcon,
  Download as DownloadIcon
} from '@mui/icons-material';
import { AdvancedSearchDrawer } from './AdvancedSearchDrawer';
import { useAdvancedSearch } from '../hooks/useAdvancedSearch';
import type { AdvancedSearchFilters } from './AdvancedSearchDrawer';

/**
 * Example integration of AdvancedSearchDrawer with SecurityEvents list
 * This shows how to integrate the advanced search functionality
 */
export const SecurityEventsWithAdvancedSearch = () => {
  // Use the advanced search hook with URL synchronization
  const {
    state,
    openDrawer,
    closeDrawer,
    performSearch,
    updateFilters,
    clearSearch,
    savedSearches,
    saveCurrentSearch,
    deleteSavedSearch,
    exportResults,
    getShareableURL
  } = useAdvancedSearch({
    syncWithURL: true,
    debounceMs: 300
  });

  // Handle search from the drawer
  const handleSearch = async (filters: AdvancedSearchFilters) => {
    // Convert UI filters to API request format
    const apiFilters = {
      ...filters,
      // Convert Date objects to ISO strings
      startDate: filters.startDate?.toISOString(),
      endDate: filters.endDate?.toISOString(),
    };

    await performSearch(apiFilters);
    closeDrawer();
  };

  // Handle clear all filters
  const handleClearAll = () => {
    clearSearch();
    updateFilters({});
  };

  // Custom toolbar with advanced search button
  const CustomToolbar = () => (
    <TopToolbar>
      <FilterButton />
      <CreateButton />
      <ExportButton />
      
      {/* Advanced Search Button */}
      <Button
        onClick={openDrawer}
        label="Advanced Search"
        startIcon={<FilterListIcon />}
      />
      
      {/* Share Search Button */}
      {Object.keys(state.currentFilters).length > 0 && (
        <Button
          onClick={() => {
            const url = getShareableURL();
            navigator.clipboard.writeText(url);
            // You could show a notification here
          }}
          label="Share Search"
          startIcon={<ShareIcon />}
        />
      )}
      
      {/* Export Results Button */}
      {state.lastSearchResults && (
        <Button
          onClick={() => exportResults('csv')}
          label="Export CSV"
          startIcon={<DownloadIcon />}
          disabled={state.isLoading}
        />
      )}
    </TopToolbar>
  );

  // Custom aside with saved searches
  const CustomAside = () => (
    <Box sx={{ width: 250, p: 2 }}>
      <h3>Saved Searches</h3>
      {savedSearches.map(savedSearch => (
        <Button
          key={savedSearch.id}
          onClick={() => {
            updateFilters(savedSearch.filters);
            performSearch(savedSearch.filters);
          }}
          variant="text"
          fullWidth
          sx={{ justifyContent: 'flex-start', mb: 1 }}
        >
          {savedSearch.name}
        </Button>
      ))}
    </Box>
  );

  return (
    <>
      <List
        title="Security Events"
        actions={<CustomToolbar />}
        aside={<CustomAside />}
        // Apply any filters from advanced search
        filter={state.currentFilters}
        // Show loading state
        loading={state.isLoading}
      >
        {/* Show search summary if results available */}
        {state.lastSearchResults && (
          <Box sx={{ p: 2 }}>
            <Alert severity="info">
              Found {state.lastSearchResults.totalCount.toLocaleString()} results in {state.lastSearchResults.queryMetadata.queryTime}ms
              {state.lastSearchResults.queryMetadata.usedFullTextSearch && ' (using full-text search)'}
            </Alert>
          </Box>
        )}
        
        {/* Show error if any */}
        {state.error && (
          <Box sx={{ p: 2 }}>
            <Alert severity="error">
              {state.error}
            </Alert>
          </Box>
        )}

        <Datagrid>
          <DateField source="timestamp" showTime />
          <TextField source="eventType" />
          <ChipField source="riskLevel" />
          <TextField source="machine" />
          <TextField source="user" />
          <TextField source="source" />
          <TextField source="message" />
          <NumberField source="confidence" />
          <FunctionField
            label="MITRE Techniques"
            render={(record: any) =>
              record.mitreTechniques?.length > 0
                ? record.mitreTechniques.join(', ')
                : '-'
            }
          />
          <ChipField source="status" />
        </Datagrid>
      </List>

      {/* Advanced Search Drawer */}
      <AdvancedSearchDrawer
        open={state.isDrawerOpen}
        onClose={closeDrawer}
        onSearch={handleSearch}
        onClearAll={handleClearAll}
        initialFilters={{
          // Convert API filters back to UI format
          ...state.currentFilters,
          startDate: state.currentFilters.startDate 
            ? new Date(state.currentFilters.startDate) 
            : undefined,
          endDate: state.currentFilters.endDate 
            ? new Date(state.currentFilters.endDate) 
            : undefined,
        }}
        isLoading={state.isLoading}
        searchResults={state.lastSearchResults ? {
          total: state.lastSearchResults.totalCount,
          queryTime: state.lastSearchResults.queryMetadata.queryTime
        } : undefined}
      />
    </>
  );
};

export default SecurityEventsWithAdvancedSearch;
