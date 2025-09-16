import { useState, useCallback, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useNotify, fetchUtils } from 'react-admin';
import axios, { AxiosResponse, InternalAxiosRequestConfig, AxiosHeaders } from 'axios';
import {
  AdvancedSearchRequest,
  AdvancedSearchResponse,
  AdvancedSearchUIState,
  SearchSuggestionsResponse,
  SavedSearchesResponse,
  SavedSearch
} from '../types/advancedSearch';

// Base configuration
const API_BASE_URL = process.env.REACT_APP_CASTELLANPRO_API_URL || 'http://localhost:5000';

class AdvancedSearchService {
  private baseURL: string;

  constructor() {
    this.baseURL = `${API_BASE_URL}/api`;

    // Add authentication interceptor
    axios.interceptors.request.use((config: InternalAxiosRequestConfig) => {
      // Initialize headers if not set
      if (!config.headers) {
        config.headers = new AxiosHeaders();
      }
      
      const token = localStorage.getItem('auth_token');
      if (token) {
        config.headers.set('Authorization', `Bearer ${token}`);
      }
      
      // Add content type and accept headers
      config.headers.set('Content-Type', 'application/json');
      config.headers.set('Accept', 'application/json');
      
      return config;
    });
  }

  /**
   * Perform advanced search with filters
   */
  async search(request: AdvancedSearchRequest): Promise<AdvancedSearchResponse> {
    // Format dates as ISO strings
    const formattedRequest = {
      ...request,
      startDate: request.startDate ? new Date(request.startDate).toISOString() : undefined,
      endDate: request.endDate ? new Date(request.endDate).toISOString() : undefined
    };
    try {
      const startTime = Date.now();

      // Format the request body
      const searchRequest = {
        // Pagination
        page: 1,
        limit: 25,
        
        // Sorting
        sort: 'timestamp',
        order: 'desc',
        
        // Date range
        startDate: request.startDate,
        endDate: request.endDate,
        
        // Risk levels and other filters
        riskLevels: request.riskLevels || [],
        eventTypes: request.eventTypes || [],
        machines: request.machines || [],
        users: request.users || [],
        sources: request.sources || [],
        
        // Full text search
        fullTextQuery: request.fullTextQuery,
        useExactMatch: request.useExactMatch,
        enableFuzzySearch: request.enableFuzzySearch
      };
      
      const response: AxiosResponse<any> = await axios.post(
        `${this.baseURL}/security-events/search`,
        searchRequest,
        {
          headers: {
            'Content-Type': 'application/json'
          }
        }
      );

      // Normalize backend variations to AdvancedSearchResponse
      const raw = response.data || {};
      const items = Array.isArray(raw.data) ? raw.data : (Array.isArray(raw.results) ? raw.results : []);
      const total = typeof raw.total === 'number' ? raw.total : (typeof raw.totalCount === 'number' ? raw.totalCount : items.length);
      const page = typeof raw.page === 'number' ? raw.page : (typeof raw.currentPage === 'number' ? raw.currentPage : 1);
      const pageSize = typeof raw.pageSize === 'number' ? raw.pageSize : (typeof raw.limit === 'number' ? raw.limit : 25);
      const totalPages = typeof raw.totalPages === 'number' ? raw.totalPages : (pageSize > 0 ? Math.ceil(total / pageSize) : 1);

      const normalized: AdvancedSearchResponse = {
        results: items,
        totalCount: total,
        page,
        pageSize,
        totalPages,
        queryMetadata: {
          queryTime: Date.now() - startTime,
          usedFullTextSearch: Boolean(request.fullTextQuery),
          indexesUsed: raw.queryMetadata?.indexesUsed || [],
          filtersSummary: raw.queryMetadata?.filtersSummary || {}
        }
      };

      return normalized;
    } catch (error) {
      console.error('Advanced search failed:', error);
      throw this.handleError(error);
    }
  }

  /**
   * Get search suggestions for autocomplete
   */
  async getSuggestions(query: string, type?: string): Promise<SearchSuggestionsResponse> {
    try {
      const params = new URLSearchParams();
      params.append('q', query);
      if (type) {
        params.append('type', type);
      }

      const response: AxiosResponse<SearchSuggestionsResponse> = await axios.get(
        `${this.baseURL}/security-events/search/suggestions?${params.toString()}`
      );

      return response.data;
    } catch (error) {
      console.error('Failed to get search suggestions:', error);
      throw this.handleError(error);
    }
  }

  /**
   * Get saved searches for the current user
   */
  async getSavedSearches(): Promise<SavedSearchesResponse> {
    try {
      const response = await axios.get(`${this.baseURL}/saved-searches`);
      return response.data;
    } catch (error) {
      console.error('Failed to get saved searches:', error);
      return {
        savedSearches: []
      };
    }
  }

  /**
   * Save a search query
   */
  async saveSearch(name: string, description: string, filters: AdvancedSearchRequest): Promise<SavedSearch> {
    try {
      const response = await axios.post(
        `${this.baseURL}/saved-searches`,
        { name, description, filters }
      );
      return response.data;
    } catch (error) {
      console.error('Failed to save search:', error);
      throw this.handleError(error);
    }
  }

  /**
   * Delete a saved search
   */
  async deleteSavedSearch(searchId: string): Promise<void> {
    try {
      await axios.delete(`${this.baseURL}/saved-searches/${searchId}`);
    } catch (error) {
      console.error('Failed to delete saved search:', error);
      throw this.handleError(error);
    }
  }

  /**
   * Update a saved search
   */
  async updateSavedSearch(
    searchId: string,
    updates: Partial<Pick<SavedSearch, 'name' | 'description' | 'filters'>>
  ): Promise<SavedSearch> {
    try {
      const response = await axios.put(
        `${this.baseURL}/saved-searches/${searchId}`,
        updates
      );
      return response.data;
    } catch (error) {
      console.error('Failed to update saved search:', error);
      throw this.handleError(error);
    }
  }

  /**
   * Get filter options for dropdowns (e.g., available machines, users, etc.)
   */
  async getFilterOptions(filterType: string): Promise<{ id: string; name: string; count?: number }[]> {
    try {
      const response = await axios.get(
        `${this.baseURL}/security-events/search/filter-options/${filterType}`
      );

      return response.data;
    } catch (error) {
      console.error(`Failed to get filter options for ${filterType}:`, error);
      throw this.handleError(error);
    }
  }

  /**
   * Export search results
   */
  async exportSearchResults(
    request: AdvancedSearchRequest,
    format: 'csv' | 'json' | 'xlsx' = 'csv'
  ): Promise<Blob> {
    try {
      const response = await axios.post(
        `${this.baseURL}/security-events/search/export`,
        { ...request, format },
        {
          responseType: 'blob',
          headers: {
            'Content-Type': 'application/json'
          }
        }
      );

      return response.data;
    } catch (error) {
      console.error('Failed to export search results:', error);
      throw this.handleError(error);
    }
  }

  /**
   * Convert filters to URL query parameters for sharing/bookmarking
   */
  filtersToQueryParams(filters: AdvancedSearchRequest): URLSearchParams {
    const params = new URLSearchParams();

    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        if (Array.isArray(value)) {
          if (value.length > 0) {
            params.append(key, value.join(','));
          }
        } else if (value instanceof Date) {
          params.append(key, value.toISOString());
        } else {
          params.append(key, value.toString());
        }
      }
    });

    return params;
  }

  /**
   * Parse URL query parameters back to filters
   */
  queryParamsToFilters(params: URLSearchParams): AdvancedSearchRequest {
    const filters: AdvancedSearchRequest = {};

    params.forEach((value, key) => {
      // Handle array fields
      const arrayFields = [
        'riskLevels',
        'eventTypes',
        'statuses',
        'machines',
        'users',
        'sources',
        'ipAddresses',
        'mitreTechniques'
      ];

      if (arrayFields.includes(key)) {
        (filters as any)[key] = value.split(',').filter(v => v.trim().length > 0);
      }
      // Handle numeric fields
      else if (key.includes('min') || key.includes('max') || key === 'page' || key === 'pageSize') {
        const numValue = parseFloat(value);
        if (!isNaN(numValue)) {
          (filters as any)[key] = numValue;
        }
      }
      // Handle boolean fields
      else if (key === 'useExactMatch' || key === 'enableFuzzySearch') {
        (filters as any)[key] = value.toLowerCase() === 'true';
      }
      // Handle date fields
      else if (key === 'startDate' || key === 'endDate') {
        (filters as any)[key] = new Date(value);
      }
      // Handle string fields
      else {
        (filters as any)[key] = value;
      }
    });

    return filters;
  }

  /**
   * Handle API errors
   */
  private handleError(error: any): Error {
    if (axios.isAxiosError(error)) {
      if (error.response) {
        // Server responded with error status
        const message = error.response.data?.message || error.response.statusText || 'API Error';
        return new Error(`${error.response.status}: ${message}`);
      } else if (error.request) {
        // Request made but no response received
        return new Error('Network error: Unable to reach the server');
      } else {
        // Something else happened
        return new Error(`Request error: ${error.message}`);
      }
    }

    return error instanceof Error ? error : new Error('Unknown error occurred');
  }
}

// Export singleton instance
export const advancedSearchService = new AdvancedSearchService();

// Custom hook for using the advanced search
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
   * Debounce time for search operations in milliseconds
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