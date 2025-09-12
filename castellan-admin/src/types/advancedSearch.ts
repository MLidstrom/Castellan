// Advanced Search Request/Response Types for Castellan

export interface AdvancedSearchRequest {
  // Full-text search
  fullTextQuery?: string;
  useExactMatch?: boolean;
  enableFuzzySearch?: boolean;

  // Date filtering
  startDate?: string; // ISO date string
  endDate?: string; // ISO date string

  // Category filtering
  riskLevels?: string[];
  eventTypes?: string[];
  statuses?: string[];

  // Entity filtering
  machines?: string[];
  users?: string[];
  sources?: string[];
  ipAddresses?: string[];

  // MITRE ATT&CK
  mitreTechniques?: string[];

  // Score filtering
  minConfidence?: number;
  maxConfidence?: number;
  minCorrelationScore?: number;
  maxCorrelationScore?: number;
  minBurstScore?: number;
  maxBurstScore?: number;
  minAnomalyScore?: number;
  maxAnomalyScore?: number;

  // Pagination and sorting
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface SecurityEventSearchResult {
  id: string;
  timestamp: string;
  eventType: string;
  riskLevel: string;
  machine: string;
  user?: string;
  source: string;
  message: string;
  description?: string;
  ipAddress?: string;
  confidence: number;
  correlationScore?: number;
  burstScore?: number;
  anomalyScore?: number;
  mitreTechniques?: string[];
  status: string;
  metadata?: Record<string, any>;
}

export interface AdvancedSearchResponse {
  results: SecurityEventSearchResult[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  queryMetadata: {
    queryTime: number;
    usedFullTextSearch: boolean;
    indexesUsed: string[];
    filtersSummary: Record<string, any>;
  };
}

export interface SearchSuggestion {
  type: 'machine' | 'user' | 'eventType' | 'riskLevel' | 'mitreTechnique';
  value: string;
  displayName: string;
  count?: number;
}

export interface SavedSearch {
  id: string;
  name: string;
  description?: string;
  filters: AdvancedSearchRequest;
  createdAt: string;
  lastUsed?: string;
  useCount: number;
}

export interface SearchSuggestionsResponse {
  suggestions: SearchSuggestion[];
}

export interface SavedSearchesResponse {
  savedSearches: SavedSearch[];
}

// UI State Types
export interface AdvancedSearchUIState {
  isDrawerOpen: boolean;
  isLoading: boolean;
  currentFilters: AdvancedSearchRequest;
  lastSearchResults?: AdvancedSearchResponse;
  error?: string;
}

// Filter Helper Types
export type FilterKey = keyof AdvancedSearchRequest;

export interface FilterOption {
  id: string;
  name: string;
  count?: number;
}

export interface DateRange {
  start?: Date;
  end?: Date;
}

export interface NumericRange {
  min: number;
  max: number;
}

// Export convenience types
export type { AdvancedSearchFilters } from '../components/AdvancedSearchDrawer';
export type SearchFilters = AdvancedSearchRequest;
