import { useState, useCallback, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
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
  /**
   * Debounce delay for search in milliseconds
   */
  debounceMs?: number;
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
    initialFilters = {},
    debounceMs = 300
  } = options;

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
  const [debounceTimer, setDebounceTimer] = useState<number | null>(null);

  // Load initial filters from URL on mount
  useEffect(() => {
    if (syncWithURL) {
      const filters = advancedSearchService.queryParamsToFilters(searchParams);
      if (Object.keys(filters).length > 0) {
        setState(prev => ({ ...prev, currentFilters: filters }));
      }
    }
    // Load saved searches on mount
    const loadSavedSearches = async () => {
      try {
        const response = await advancedSearchService.getSavedSearches();
        setSavedSearches(response.savedSearches);
      } catch (error) {
        console.error('Failed to load saved searches:', error);
      }
    };
    loadSavedSearches();
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
        lastSearchResults: results
      }));
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Search failed';
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: errorMessage
      }));
    }
  }, []);

  const debouncedSearch = useCallback((filters: AdvancedSearchRequest) => {
    if (debounceTimer) {
      window.clearTimeout(debounceTimer);
    }

    const timer = window.setTimeout(() => {
      performSearch(filters);
    }, debounceMs);

    setDebounceTimer(timer);
  }, [debounceTimer, debounceMs, performSearch]);

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

  // Filter management
  const updateFilters = useCallback((newFilters: Partial<AdvancedSearchRequest>) => {
    setState(prev => {
      const updatedFilters = { ...prev.currentFilters, ...newFilters };
      return { ...prev, currentFilters: updatedFilters };
    });
  }, []);

  const setFilters = useCallback((filters: AdvancedSearchRequest) => {
    setState(prev => ({ ...prev, currentFilters: filters }));
  }, []);

  // Saved searches
  const refreshSavedSearches = useCallback(async () => {
    try {
      const response = await advancedSearchService.getSavedSearches();
      setSavedSearches(response.savedSearches);
    } catch (error) {
      console.error('Failed to load saved searches:', error);
    }
  }, []);

  const loadSavedSearch = useCallback((savedSearch: SavedSearch) => {
    setFilters(savedSearch.filters);
    performSearch(savedSearch.filters);
  }, [setFilters, performSearch]);

  const saveCurrentSearch = useCallback(async (name: string, description?: string) => {
    try {
      const savedSearch = await advancedSearchService.saveSearch(
        name,
        description || '',
        state.currentFilters
      );
      setSavedSearches(prev => [...prev, savedSearch]);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to save search';
      setState(prev => ({ ...prev, error: errorMessage }));
      throw error;
    }
  }, [state.currentFilters]);

  const deleteSavedSearch = useCallback(async (searchId: string) => {
    try {
      await advancedSearchService.deleteSavedSearch(searchId);
      setSavedSearches(prev => prev.filter(s => s.id !== searchId));
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to delete search';
      setState(prev => ({ ...prev, error: errorMessage }));
      throw error;
    }
  }, []);

  // Export
  const exportResults = useCallback(async (format: 'csv' | 'json' | 'xlsx' = 'csv') => {
    try {
      setState(prev => ({ ...prev, isLoading: true }));
      
      const blob = await advancedSearchService.exportSearchResults(state.currentFilters, format);
      
      // Create download link
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `security-events-export.${format}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      setState(prev => ({ ...prev, isLoading: false }));
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Export failed';
      setState(prev => ({
        ...prev,
        isLoading: false,
        error: errorMessage
      }));
    }
  }, [state.currentFilters]);

  // URL sharing
  const getShareableURL = useCallback(() => {
    const params = advancedSearchService.filtersToQueryParams(state.currentFilters);
    const url = new URL(window.location.href);
    url.search = params.toString();
    return url.toString();
  }, [state.currentFilters]);

  const loadFiltersFromURL = useCallback(() => {
    const filters = advancedSearchService.queryParamsToFilters(searchParams);
    if (Object.keys(filters).length > 0) {
      setState(prev => ({ ...prev, currentFilters: filters }));
    }
  }, [searchParams]);

  // Cleanup debounce timer on unmount
  useEffect(() => {
    return () => {
      if (debounceTimer) {
        window.clearTimeout(debounceTimer);
      }
    };
  }, [debounceTimer]);

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
