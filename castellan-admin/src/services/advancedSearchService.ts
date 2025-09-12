import axios, { AxiosResponse } from 'axios';
import {
  AdvancedSearchRequest,
  AdvancedSearchResponse,
  SearchSuggestionsResponse,
  SavedSearchesResponse,
  SavedSearch
} from '../types/advancedSearch';

// Base configuration
const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || 'http://localhost:5000';

class AdvancedSearchService {
  private baseURL: string;

  constructor() {
    this.baseURL = `${API_BASE_URL}/api`;
  }

  /**
   * Perform advanced search with filters
   */
  async search(request: AdvancedSearchRequest): Promise<AdvancedSearchResponse> {
    try {
      const response: AxiosResponse<AdvancedSearchResponse> = await axios.post(
        `${this.baseURL}/security-events/search`,
        request,
        {
          headers: {
            'Content-Type': 'application/json'
          }
        }
      );

      return response.data;
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
      const response: AxiosResponse<SavedSearchesResponse> = await axios.get(
        `${this.baseURL}/security-events/search/saved`
      );

      return response.data;
    } catch (error) {
      console.error('Failed to get saved searches:', error);
      throw this.handleError(error);
    }
  }

  /**
   * Save a search query
   */
  async saveSearch(name: string, description: string, filters: AdvancedSearchRequest): Promise<SavedSearch> {
    try {
      const response: AxiosResponse<SavedSearch> = await axios.post(
        `${this.baseURL}/security-events/search/saved`,
        {
          name,
          description,
          filters
        },
        {
          headers: {
            'Content-Type': 'application/json'
          }
        }
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
      await axios.delete(`${this.baseURL}/security-events/search/saved/${searchId}`);
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
      const response: AxiosResponse<SavedSearch> = await axios.put(
        `${this.baseURL}/security-events/search/saved/${searchId}`,
        updates,
        {
          headers: {
            'Content-Type': 'application/json'
          }
        }
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
        } else if (typeof value === 'object') {
          params.append(key, JSON.stringify(value));
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
export default advancedSearchService;
