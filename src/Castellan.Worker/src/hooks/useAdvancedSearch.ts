import { useState, useCallback, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useNotify } from 'react-admin';
import {
  AdvancedSearchRequest,
  AdvancedSearchResponse,
  AdvancedSearchUIState,
  SavedSearch
} from '../types/advancedSearch';
import { advancedSearchService } from '../services/advancedSearchService';

export interface UseAdvancedSearchOptions {
  /**
   * Whether to automatically sync filters with URL search params
   */
  syncWithURL?: boolean;
  /**
   * Initial filters to use
   */
  initialFilters?: AdvancedSearchRequest;
}

export interface UseAdvancedSearchReturn {
  // State
  state: AdvancedSearchUIState;
  
  // Actions
  openDrawer: () => void;
  closeDrawer: () => void;
  toggleDrawer: () => void;
  
  // Search operations
  performSearch: (filters: AdvancedSearchRequest) => Promise<void>;
  clearSearch: () => void;
  resetFilters: () => void;
  
  // Filter management
  updateFilters: (filters: Partial<AdvancedSearchRequest>) => void;
  setFilters: (filters: AdvancedSearchRequest) => void;
  
  // Saved searches
  savedSearches: SavedSearch[];
  loadSavedSearch: (savedSearch: SavedSearch) => void;
  saveCurrentSearch: (name: string, description?: string) => Promise<void>;
  deleteSavedSearch: (searchId: string) => Promise<void>;
  refreshSavedSearches: () => Promise<void>;
  
  // Export
  exportResults: (format?: 'csv' | 'json' | 'xlsx') => Promise<void>;
  
  // URL sharing
  getShareableURL: () => string;
  loadFiltersFromURL: () => void;
}

export function useAdvancedSearch(options: UseAdvancedSearchOptions = {}): UseAdvancedSearchReturn {
  const {
    syncWithURL = true,
    initialFilters = {}
  } = options;

  const notify = useNotify();
  const [searchParams, setSearchParams] = useSearchParams();
  
  // Initialize state
  const [state, setState] = useState<AdvancedSearchUIState>({
    isDrawerOpen: false,
    isLoading: false,
    currentFilters: initialFilters,
    lastSearchResults: undefined,
    error: undefined
  });

  const [savedSearches, setSavedSearches] = useState<SavedSearch[]>([]);

  // Load initial filters from URL on mount
  useEffect(() => {
    if (syncWithURL) {
      const filters = advancedSearchService.queryParamsToFilters(searchParams);
      if (Object.keys(filters).length > 0) {
        setState(prev => ({ ...prev, currentFilters: filters }));
      }
    }

    // Load saved searches on mount
    refreshSavedSearches();
  }, [syncWithURL, searchParams]);

  // Sync filters to URL when they change
  useEffect(() => {
    if (syncWithURL && Object.keys(state.currentFilters).length > 0) {
      const params = advancedSearchService.filtersToQueryParams(state.currentFilters);
      setSearchParams(params, { replace: true });
    }
  }, [state.currentFilters, syncWithURL, setSearchParams]);

  // Drawer controls
  const openDrawer = useCallback(() => {
    setState(prev => ({ ...prev, isDrawerOpen: true }));
  }, []);

  const closeDrawer = useCallback(() => {
    setState(prev => ({ ...prev, isDrawerOpen: false }));
  }, []);

  const toggleDrawer = useCallback(() => {
    setState(prev => ({ ...prev, isDrawerOpen: !prev.isDrawerOpen }));
  }, []);

  // Filter management
  const updateFilters = useCallback((newFilters: Partial<AdvancedSearchRequest>) => {
    setState(prev => ({
      ...prev,
      currentFilters: {
        ...prev.currentFilters,
        ...newFilters
      }
    }));
  }, []);

  const setFilters = useCallback((filters: AdvancedSearchRequest) => {
    setState(prev => ({
      ...prev,
      currentFilters: filters
    }));
  }, []);

  // Search operations
  const performSearch = useCallback(async (filters: AdvancedSearchRequest) => {
    setState(prev => ({ 
      ...prev, 
      isLoading: true, 
      error: undefined,
      currentFilters: filters 
    }));

    try {
      const results = await advancedSearchService.search(filters);
      setState(prev => ({
        ...prev,
        isLoading: false,
        lastSearchResults: results,
        error: undefined
      }));
    } catch (error) {
      console.error('Search error:', error);
      const errorMessage = error instanceof Error ? error.message : 'Search failed';
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: errorMessage
      }));
      notify(errorMessage, { type: 'error' });
    }
  }, [notify]);

  const clearSearch = useCallback(() => {
    setState(prev => ({
      ...prev,
      lastSearchResults: undefined,
      error: undefined
    }));
  }, []);

  const resetFilters = useCallback(() => {
    setState(prev => ({
      ...prev,
      currentFilters: initialFilters
    }));
    if (syncWithURL) {
      setSearchParams({}, { replace: true });
    }
  }, [initialFilters, syncWithURL, setSearchParams]);

  // Saved searches management
  const refreshSavedSearches = useCallback(async () => {
    try {
      const { savedSearches: searches } = await advancedSearchService.getSavedSearches();
      setSavedSearches(searches);
    } catch (error) {
      console.error('Failed to refresh saved searches:', error);
      notify('Failed to load saved searches', { type: 'error' });
    }
  }, [notify]);

  const loadSavedSearch = useCallback((savedSearch: SavedSearch) => {
    setFilters(savedSearch.filters);
  }, [setFilters]);

  const saveCurrentSearch = useCallback(async (name: string, description: string = '') => {
    try {
      await advancedSearchService.saveSearch(name, description, state.currentFilters);
      await refreshSavedSearches();
      notify('Search saved successfully', { type: 'success' });
    } catch (error) {
      console.error('Failed to save search:', error);
      notify('Failed to save search', { type: 'error' });
      throw error;
    }
  }, [state.currentFilters, refreshSavedSearches, notify]);

  const deleteSavedSearch = useCallback(async (searchId: string) => {
    try {
      await advancedSearchService.deleteSavedSearch(searchId);
      await refreshSavedSearches();
      notify('Search deleted successfully', { type: 'success' });
    } catch (error) {
      console.error('Failed to delete saved search:', error);
      notify('Failed to delete saved search', { type: 'error' });
      throw error;
    }
  }, [refreshSavedSearches, notify]);

  // Export functionality
  const exportResults = useCallback(async (format: 'csv' | 'json' | 'xlsx' = 'csv') => {
    try {
      setState(prev => ({ ...prev, isLoading: true }));
      const blob = await advancedSearchService.exportSearchResults(state.currentFilters, format);
      
      // Create download link
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `search-results.${format}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
      
      setState(prev => ({ ...prev, isLoading: false }));
      notify('Export completed successfully', { type: 'success' });
    } catch (error) {
      console.error('Export error:', error);
      setState(prev => ({ ...prev, isLoading: false }));
      notify('Failed to export results', { type: 'error' });
    }
  }, [state.currentFilters, notify]);

  // URL sharing
  const getShareableURL = useCallback(() => {
    const params = advancedSearchService.filtersToQueryParams(state.currentFilters);
    const url = new URL(window.location.href);
    url.search = params.toString();
    return url.toString();
  }, [state.currentFilters]);

  const loadFiltersFromURL = useCallback(() => {
    if (searchParams) {
      const filters = advancedSearchService.queryParamsToFilters(searchParams);
      setFilters(filters);
    }
  }, [searchParams, setFilters]);

  return {
    state,
    openDrawer,
    closeDrawer,
    toggleDrawer,
    performSearch,
    clearSearch,
    resetFilters,
    updateFilters,
    setFilters,
    savedSearches,
    loadSavedSearch,
    saveCurrentSearch,
    deleteSavedSearch,
    refreshSavedSearches,
    exportResults,
    getShareableURL,
    loadFiltersFromURL
  };
}