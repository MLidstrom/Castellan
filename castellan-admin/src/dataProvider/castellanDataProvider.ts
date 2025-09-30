import { DataProvider, fetchUtils, HttpError } from 'react-admin';

// Castellan Open Source Data Provider - All features enabled
// This is the free version with no premium restrictions

// API Configuration
const API_URL = process.env.REACT_APP_CASTELLANPRO_API_URL || 'http://localhost:5000/api';
// WebSocket URL available for future use
// const WS_URL = process.env.REACT_APP_WS_URL || 'ws://localhost:5000/ws';

// HTTP Client with authentication and error handling
const httpClient = (url: string, options: fetchUtils.Options = {}) => {
    // Initialize headers object once
    options.headers = new Headers(options.headers);
    
    // Add authentication token if available
    const token = localStorage.getItem('auth_token');
    if (token) {
        options.headers.set('Authorization', `Bearer ${token}`);
    }

    // Set default headers
    options.headers.set('Content-Type', 'application/json');
    options.headers.set('Accept', 'application/json');

    return fetchUtils.fetchJson(url, options).catch((error) => {
        // Handle authentication errors
        if (error.status === 401) {
            localStorage.removeItem('auth_token');
            localStorage.removeItem('user_permissions');
            window.location.href = '/login';
            return Promise.reject(new HttpError('Authentication required', 401));
        }

        // Handle network errors
        if (error.status === 0 || !error.status) {
            return Promise.reject(new HttpError('Network error - backend server may be unavailable', 500));
        }

        // Handle other HTTP errors
        const message = error.body?.message || error.statusText || 'An error occurred';
        return Promise.reject(new HttpError(message, error.status, error.body));
    });
};

// Resource mapping between frontend and backend
const RESOURCE_MAP: Record<string, string> = {
    'security-events': 'security-events',
    'compliance-reports': 'compliance-reports',
    'system-status': 'system-status',
    'threat-scanner': 'threat-scanner',
    'notification-settings': 'notifications/config',
    'configuration': 'settings/threat-intelligence',
    'users': 'users',
    'settings': 'settings',
    'analytics': 'analytics',
    'reports': 'reports',
    // Correlation Engine mappings - v0.6.0
    'correlation': 'correlation',
    'correlation/statistics': 'correlation/statistics',
    'correlation/analyze': 'correlation/analyze',
    'correlation/attack-chains': 'correlation/attack-chains',
    // MITRE ATT&CK resource mappings
    'mitre-techniques': 'mitre/techniques',
    'mitre/techniques': 'mitre/techniques',
    'mitre/tactics': 'mitre/tactics',
    'mitre/groups': 'mitre/groups',
    'mitre/software': 'mitre/software',
    // YARA Rule Engine resource mappings
    'yara-rules': 'yara-rules',
    'yara-matches': 'yara-rules/matches',
    'yara-categories': 'yara-rules/categories',
    // Export resource mappings
    'export/formats': 'export/formats',
    'export/security-events': 'export/security-events',
    'export/stats': 'export/stats',
    // Timeline resource mappings
    'timeline': 'timeline',
    'timeline/events': 'timeline/events',
    'timeline/heatmap': 'timeline/heatmap',
    'timeline/stats': 'timeline/stats',
    'timeline/anomalies': 'timeline/anomalies',
    // Search Management resource mappings - v0.5.0
    'saved-searches': 'saved-searches',
    'search-history': 'search-history'
};

// Transform react-admin filter format to backend API format
const transformFilters = (filters: any) => {
    const apiFilters: any = {};
    
    Object.keys(filters).forEach(key => {
        const value = filters[key];
        
        // Handle different filter types
        if (key.startsWith('filter_')) {
            // Advanced search filters
            const fieldName = key.replace('filter_', '');
            if (value && typeof value === 'object' && value.operator && value.value !== undefined) {
                apiFilters[`${fieldName}_${value.operator}`] = value.value;
            }
        } else if (Array.isArray(value)) {
            // Array filters (e.g., risk levels)
            apiFilters[key] = value.join(',');
        } else if (value instanceof Date) {
            // Format dates as ISO strings
            apiFilters[key] = value.toISOString();
        } else if (value !== null && value !== undefined && value !== '') {
            // Simple filters
            apiFilters[key] = value;
        }
    });

    return apiFilters;
};

// Transform backend response to react-admin format
const transformResponse = (resource: string, response: any) => {
    // Handle MITRE-specific response formats
    if (resource.startsWith('mitre-') || resource.startsWith('mitre/')) {
        // Extract resource type from either 'mitre-techniques' or 'mitre/techniques'
        let resourceType: string;
        if (resource.includes('/')) {
            resourceType = resource.split('/')[1]; // 'techniques', 'tactics', etc.
        } else {
            // For 'mitre-techniques', extract 'technique' and add 's'
            const baseName = resource.replace('mitre-', '').replace(/s$/, ''); // Remove trailing 's' if present
            resourceType = baseName + 's'; // Add 's' back: 'techniques', 'tactics', etc.
        }

        // MITRE endpoints return { techniques: [...], totalCount: 823 }
        let data = response[resourceType] || response.data || [];

        // Ensure data is an array
        if (!Array.isArray(data)) {
            console.warn('[transformResponse] MITRE data is not an array:', data);
            data = [];
        }

        console.log('[transformResponse] MITRE data before transform:', data.slice(0, 2));

        // Ensure MITRE records have an 'id' field (React Admin requirement)
        // Use techniqueId as the ID since the backend API expects it
        data = data.map((item: any) => {
            return {
                ...item,
                id: item.techniqueId || item.tacticId || item.groupId || item.softwareId || item.id
            };
        });

        console.log('[transformResponse] MITRE data after transform:', data.slice(0, 2));

        return {
            data: data,
            total: response.totalCount || data.length
        };
    }

    // Handle standard response formats
    if (response.data) {
        return {
            data: response.data,
            total: response.total || response.data.length
        };
    }

    // Handle array responses
    if (Array.isArray(response)) {
        return {
            data: response,
            total: response.length
        };
    }

    // Handle single object responses
    return {
        data: [response],
        total: 1
    };
};

// Main Data Provider
export const castellanDataProvider: DataProvider = {
    // Get list of resources with pagination, sorting, and filtering
    getList: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            console.log(`[DataProvider] Returning empty data for disabled premium resource: ${resource}`);
            return {
                data: [],
                total: 0
            };
        }

        // Special handling for configuration resource list
        if (resource === 'configuration') {
            return {
                data: [{
                    id: 'threat-intelligence',
                    name: 'Threat Intelligence Configuration',
                    description: 'Configure threat intelligence providers and settings'
                }],
                total: 1
            };
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
        
        // Use smaller page sizes for MITRE resources to prevent timeouts
        let { page, perPage } = params.pagination || { page: 1, perPage: 10 };
        if (resource.startsWith('mitre/')) {
            perPage = Math.min(perPage, 25); // Limit MITRE queries to 25 items max
        }
        
        const { field, order } = params.sort || { field: 'id', order: 'ASC' };
        
        // Build query parameters
        const query = new URLSearchParams({
            page: page.toString(),
            limit: perPage.toString(),
            sort: field,
            order: order.toLowerCase(),
            ...transformFilters(params.filter)
        });

        const url = `${API_URL}/${backendResource}?${query}`;
        
        try {
            console.log(`[DataProvider] Making request to: ${url}`);
            const { json } = await httpClient(url);
            console.log(`[DataProvider] Raw response:`, json);
            const transformed = transformResponse(resource, json);
            console.log(`[DataProvider] Transformed response:`, transformed);
            
            return {
                data: transformed.data,
                total: transformed.total
            };
        } catch (error) {
            console.error(`Error fetching ${resource}:`, error);
            throw error;
        }
    },

    // Get one resource by ID
    getOne: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            console.log(`[DataProvider] Returning null data for disabled premium resource: ${resource}/${params.id}`);
            throw new Error(`${resource} is a premium feature not available in CastellanFree`);
        }

        // Special handling for configuration resource
        if (resource === 'configuration') {
            if (params.id === 'threat-intelligence') {
                console.log('[DataProvider] Getting threat intelligence configuration from backend API');
                const backendResource = RESOURCE_MAP[resource] || resource;
                const url = `${API_URL}/${backendResource}`;

                try {
                    const { json } = await httpClient(url);
                    console.log('[DataProvider] Configuration loaded from backend:', json);

                    // Backend returns nested in { data: ... }
                    const backendData = json.data || json;

                    // Helper to convert PascalCase object to camelCase
                    const toCamelCase = (obj: any) => {
                        if (!obj || typeof obj !== 'object') return obj;

                        const result: any = {};
                        for (const key in obj) {
                            const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
                            result[camelKey] = obj[key];
                        }
                        return result;
                    };

                    // Transform the entire config object
                    const frontendData = {
                        id: backendData.Id || backendData.id || 'threat-intelligence',
                        virusTotal: backendData.VirusTotal ? toCamelCase(backendData.VirusTotal) :
                            backendData.virusTotal || { enabled: false, apiKey: '', rateLimitPerMinute: 4, cacheEnabled: true, cacheTtlMinutes: 60 },
                        malwareBazaar: backendData.MalwareBazaar ? toCamelCase(backendData.MalwareBazaar) :
                            backendData.malwareBazaar || { enabled: false, rateLimitPerMinute: 10, cacheEnabled: true, cacheTtlMinutes: 30 },
                        alienVaultOtx: backendData.AlienVaultOtx ? toCamelCase(backendData.AlienVaultOtx) :
                            backendData.alienVaultOtx || { enabled: false, apiKey: '', rateLimitPerMinute: 10, cacheEnabled: true, cacheTtlMinutes: 60 }
                    };

                    console.log('[DataProvider] Transformed to frontend format:', frontendData);
                    return { data: frontendData };
                } catch (error) {
                    console.error('[DataProvider] Error loading configuration from backend:', error);
                    // Return defaults on error
                    return {
                        data: {
                            id: 'threat-intelligence',
                            virusTotal: { enabled: false, apiKey: '', rateLimitPerMinute: 4, cacheEnabled: true, cacheTtlMinutes: 60 },
                            malwareBazaar: { enabled: false, rateLimitPerMinute: 10, cacheEnabled: true, cacheTtlMinutes: 30 },
                            alienVaultOtx: { enabled: false, apiKey: '', rateLimitPerMinute: 10, cacheEnabled: true, cacheTtlMinutes: 60 }
                        }
                    };
                }
            } else if (params.id === 'notifications') {
                console.log('[DataProvider] Getting notification configuration');
                const url = `${API_URL}/notifications/config`;

                try {
                    const { json } = await httpClient(url);
                    console.log('[DataProvider] Notification configuration loaded:', json);
                    return { data: { id: 'notifications', ...json.data || json } };
                } catch (error) {
                    console.error('[DataProvider] Error loading notification configuration:', error);
                    // Return defaults on error
                    return {
                        data: {
                            id: 'notifications',
                            teams: { enabled: false, webhookUrl: '', notificationTypes: {} },
                            slack: { enabled: false, webhookUrl: '', channel: '', notificationTypes: {} }
                        }
                    };
                }
            } else if (params.id === 'ip-enrichment') {
                console.log('[DataProvider] Getting IP enrichment configuration');
                const url = `${API_URL}/settings/ip-enrichment`;

                try {
                    const { json } = await httpClient(url);
                    console.log('[DataProvider] IP enrichment configuration loaded:', json);
                    return { data: { id: 'ip-enrichment', ...json.data || json } };
                } catch (error) {
                    console.error('[DataProvider] Error loading IP enrichment configuration:', error);
                    // Return defaults on error
                    return {
                        data: {
                            id: 'ip-enrichment',
                            enabled: true,
                            provider: 'MaxMind',
                            maxMind: {
                                licenseKey: '',
                                accountId: '',
                                autoUpdate: false,
                                updateFrequencyDays: 7,
                                lastUpdate: null,
                                databasePaths: { city: '', asn: '', country: '' }
                            },
                            ipInfo: { apiKey: '' },
                            enrichment: { cacheMinutes: 60, maxCacheEntries: 10000, enrichPrivateIPs: false, timeoutMs: 5000 },
                            highRiskCountries: ['CN', 'RU', 'KP', 'IR', 'SY'],
                            highRiskASNs: []
                        }
                    };
                }
            }
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}/${params.id}`;

        console.log(`[DataProvider.getOne] Fetching ${resource} with ID ${params.id} from ${url}`);

        try {
            const { json } = await httpClient(url);
            console.log(`[DataProvider.getOne] Raw response:`, json);

            // Backend returns { data: { id: "1", ... } }, we need to extract the nested data
            let data = json.data || json;

            // Handle MITRE resources - ensure they use techniqueId as the id
            if (resource.startsWith('mitre-') || resource.startsWith('mitre/')) {
                console.log(`[DataProvider.getOne] MITRE data before transform:`, data);
                // Always use techniqueId as id to match what we send in the request
                data = {
                    ...data,
                    id: data.techniqueId || data.tacticId || data.groupId || data.softwareId || params.id
                };
                console.log(`[DataProvider.getOne] MITRE data after transform:`, data);
            }

            return { data };
        } catch (error) {
            // Enhanced error handling for specific cases
            if (error instanceof HttpError && error.status === 404) {
                if (resource.startsWith('mitre-') || resource.startsWith('mitre/')) {
                    console.warn(`MITRE resource ${params.id} not found in database - may need to import MITRE data`);
                } else {
                    console.warn(`${resource} with ID ${params.id} not found`);
                }
            } else {
                console.error(`Error fetching ${resource} ${params.id}:`, error);
            }
            throw error;
        }
    },

    // Get many resources by IDs
    getMany: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            console.log(`[DataProvider] Returning empty data for disabled premium resource getMany: ${resource}`);
            return { data: [] };
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
        const query = new URLSearchParams({
            ids: params.ids.join(',')
        });
        
        const url = `${API_URL}/${backendResource}/batch?${query}`;
        
        try {
            const { json } = await httpClient(url);
            const transformed = transformResponse(resource, json);
            return { data: transformed.data };
        } catch (error) {
            console.error(`Error fetching multiple ${resource}:`, error);
            throw error;
        }
    },

    // Get many resources with reference
    getManyReference: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            console.log(`[DataProvider] Returning empty data for disabled premium resource getManyReference: ${resource}`);
            return {
                data: [],
                total: 0
            };
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
    const { page, perPage } = params.pagination || { page: 1, perPage: 10 };
    const { field, order } = params.sort || { field: 'id', order: 'ASC' };
    
    // Security events use POST instead of GET
    if (resource === 'security-events') {
      const url = `${API_URL}/${backendResource}/search`;
      const transformedFilters = transformFilters(params.filter);
      
      try {
        const { json } = await httpClient(url, {
          method: 'POST',
          body: JSON.stringify({
            ...transformedFilters,
            page,
            limit: perPage,
            sort: field,
            order: order.toLowerCase()
          })
        });
        
        return {
          data: json.data || [],
          total: json.total || 0
        };
      } catch (error) {
        console.error(`Error fetching ${resource}:`, error);
        throw error;
      }
    }
    
    // Other resources use GET
    const query = new URLSearchParams({
        page: page.toString(),
        limit: perPage.toString(),
        sort: field,
        order: order.toLowerCase(),
        ...transformFilters(params.filter)
    });

    const url = `${API_URL}/${backendResource}?${query}`;
        
        try {
            const { json } = await httpClient(url);
            const transformed = transformResponse(resource, json);
            
            return {
                data: transformed.data,
                total: transformed.total
            };
        } catch (error) {
            console.error(`Error fetching ${resource} references:`, error);
            throw error;
        }
    },

    // Create new resource
    create: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            throw new Error(`${resource} is a premium feature not available in CastellanFree`);
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}`;
        
        try {
            const { json } = await httpClient(url, {
                method: 'POST',
                body: JSON.stringify(params.data),
            });
            
            return { data: json };
        } catch (error) {
            console.error(`Error creating ${resource}:`, error);
            throw error;
        }
    },

    // Update existing resource
    update: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            throw new Error(`${resource} is a premium feature not available in CastellanFree`);
        }

        // Special handling for configuration resource
        if (resource === 'configuration') {
            if (params.id === 'threat-intelligence') {
                console.log('[DataProvider] Saving threat intelligence configuration to backend API:', params.data);
                const backendResource = RESOURCE_MAP[resource] || resource;
                const url = `${API_URL}/${backendResource}`;

                // Transform camelCase to PascalCase for backend
                const backendData = {
                    Id: params.data.id || 'threat-intelligence',
                    VirusTotal: params.data.virusTotal ? {
                        Enabled: params.data.virusTotal.enabled,
                        ApiKey: params.data.virusTotal.apiKey,
                        RateLimitPerMinute: params.data.virusTotal.rateLimitPerMinute,
                        CacheEnabled: params.data.virusTotal.cacheEnabled,
                        CacheTtlMinutes: params.data.virusTotal.cacheTtlMinutes
                    } : undefined,
                    MalwareBazaar: params.data.malwareBazaar ? {
                        Enabled: params.data.malwareBazaar.enabled,
                        RateLimitPerMinute: params.data.malwareBazaar.rateLimitPerMinute,
                        CacheEnabled: params.data.malwareBazaar.cacheEnabled,
                        CacheTtlMinutes: params.data.malwareBazaar.cacheTtlMinutes
                    } : undefined,
                    AlienVaultOtx: params.data.alienVaultOtx ? {
                        Enabled: params.data.alienVaultOtx.enabled,
                        ApiKey: params.data.alienVaultOtx.apiKey,
                        RateLimitPerMinute: params.data.alienVaultOtx.rateLimitPerMinute,
                        CacheEnabled: params.data.alienVaultOtx.cacheEnabled,
                        CacheTtlMinutes: params.data.alienVaultOtx.cacheTtlMinutes
                    } : undefined
                };

                try {
                    const { json } = await httpClient(url, {
                        method: 'PUT',
                        body: JSON.stringify(backendData),
                    });

                    console.log('[DataProvider] Configuration saved to backend:', json);

                    // Just return the data we sent, since the backend save was successful
                    // The backend returns the same data we sent, so we can just use params.data
                    return {
                        data: {
                            id: 'threat-intelligence',
                            ...params.data
                        }
                    };
                } catch (error) {
                    console.error('[DataProvider] Error saving configuration to backend:', error);
                    throw error;
                }
            } else if (params.id === 'notifications') {
                console.log('[DataProvider] Saving notification configuration:', params.data);

                // The notifications endpoint expects the ID in the URL path
                const url = `${API_URL}/notifications/config/${params.data.id || params.id}`;

                try {
                    const { json } = await httpClient(url, {
                        method: 'PUT',
                        body: JSON.stringify(params.data),
                    });

                    console.log('[DataProvider] Notification configuration saved:', json);
                    // Return the data we sent since save was successful
                    return {
                        data: {
                            id: params.id,
                            ...params.data
                        }
                    };
                } catch (error) {
                    console.error('[DataProvider] Error saving notification configuration:', error);
                    throw error;
                }
            } else if (params.id === 'ip-enrichment') {
                console.log('[DataProvider] Saving IP enrichment configuration:', params.data);
                const url = `${API_URL}/settings/ip-enrichment`;

                try {
                    const { json } = await httpClient(url, {
                        method: 'PUT',
                        body: JSON.stringify(params.data),
                    });

                    console.log('[DataProvider] IP enrichment configuration saved:', json);
                    // Return the data we sent since save was successful
                    return {
                        data: {
                            id: 'ip-enrichment',
                            ...params.data
                        }
                    };
                } catch (error) {
                    console.error('[DataProvider] Error saving IP enrichment configuration:', error);
                    throw error;
                }
            }
        }

        // Special handling for saved-searches usage tracking
        if (resource === 'saved-searches' && params.meta?.action === 'use') {
            const url = `${API_URL}/saved-searches/${params.id}/use`;
            
            try {
                const { json } = await httpClient(url, {
                    method: 'POST',
                });
                
                return { data: json };
            } catch (error) {
                console.error(`Error recording usage for saved search ${params.id}:`, error);
                throw error;
            }
        }
        
        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}/${params.id}`;
        
        try {
            const { json } = await httpClient(url, {
                method: 'PUT',
                body: JSON.stringify(params.data),
            });
            
            return { data: json };
        } catch (error) {
            console.error(`Error updating ${resource} ${params.id}:`, error);
            throw error;
        }
    },

    // Update many resources
    updateMany: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            throw new Error(`${resource} is a premium feature not available in CastellanFree`);
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}/batch`;
        
        try {
            await httpClient(url, {
                method: 'PUT',
                body: JSON.stringify({
                    ids: params.ids,
                    data: params.data
                }),
            });
            
            return { data: params.ids };
        } catch (error) {
            console.error(`Error updating multiple ${resource}:`, error);
            throw error;
        }
    },

    // Delete resource
    delete: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            throw new Error(`${resource} is a premium feature not available in CastellanFree`);
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}/${params.id}`;
        
        try {
            await httpClient(url, {
                method: 'DELETE',
            });
            
            return { data: (params.previousData || { id: params.id }) as any };
        } catch (error) {
            console.error(`Error deleting ${resource} ${params.id}:`, error);
            throw error;
        }
    },

    // Delete many resources
    deleteMany: async (resource, params) => {
        // Handle disabled premium resources for CastellanFree
        const premiumResources: string[] = []; // All features enabled in open source version
        if (premiumResources.includes(resource)) {
            throw new Error(`${resource} is a premium feature not available in CastellanFree`);
        }

        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}/batch`;
        
        try {
            await httpClient(url, {
                method: 'DELETE',
                body: JSON.stringify({ ids: params.ids }),
            });
            
            return { data: params.ids };
        } catch (error) {
            console.error(`Error deleting multiple ${resource}:`, error);
            throw error;
        }
    },
};

// Base enhanced data provider with additional methods for custom operations
const baseEnhancedCastellanDataProvider = {
    ...castellanDataProvider,

    // Custom method for dashboard metrics
    getDashboardMetrics: async () => {
        const url = `${API_URL}/dashboard/metrics`;
        try {
            const { json } = await httpClient(url);
            return json;
        } catch (error) {
            console.error('Error fetching dashboard metrics:', error);
            throw error;
        }
    },

    // Custom method for system health
    getSystemHealth: async () => {
        const url = `${API_URL}/system/health`;
        try {
            const { json } = await httpClient(url);
            return json;
        } catch (error) {
            console.error('Error fetching system health:', error);
            throw error;
        }
    },

    // Custom method for compliance reports export
    exportComplianceReport: async (reportId: string, format: 'csv' | 'pdf' | 'json' = 'csv') => {
        const url = `${API_URL}/compliance-reports/${reportId}/export?format=${format}`;
        try {
            const response = await httpClient(url);
            return response;
        } catch (error) {
            console.error('Error exporting compliance report:', error);
            throw error;
        }
    },

    // Custom method for file uploads
    uploadFile: async (resource: string, file: File, metadata?: any) => {
        const backendResource = RESOURCE_MAP[resource] || resource;
        const formData = new FormData();
        formData.append('file', file);
        
        if (metadata) {
            Object.keys(metadata).forEach(key => {
                formData.append(key, metadata[key]);
            });
        }

        const token = localStorage.getItem('auth_token');
        const headers: HeadersInit = {};
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }

        const url = `${API_URL}/${backendResource}/upload`;
        
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers,
                body: formData,
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new HttpError(
                    errorData.message || 'Upload failed',
                    response.status,
                    errorData
                );
            }

            const json = await response.json();
            return json;
        } catch (error) {
            console.error('Error uploading file:', error);
            throw error;
        }
    },

    // Custom method for advanced search
    advancedSearch: async (resource: string, query: string, filters: any[], options?: any) => {
        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}/search`;
        
        try {
            const { json } = await httpClient(url, {
                method: 'POST',
                body: JSON.stringify({
                    query,
                    filters,
                    options
                }),
            });
            
            const transformed = transformResponse(resource, json);
            return {
                data: transformed.data,
                total: transformed.total
            };
        } catch (error) {
            console.error(`Error performing advanced search on ${resource}:`, error);
            throw error;
        }
    },

    // Health check method
    healthCheck: async () => {
        const url = `${API_URL}/health`;
        try {
            const { json } = await httpClient(url);
            return {
                status: 'healthy',
                ...json
            };
        } catch (error) {
            return {
                status: 'unhealthy',
                error: error instanceof Error ? error.message : 'Unknown error'
            };
        }
    },

    // Test notification channel connections
    testNotificationConnection: async (channel: 'teams' | 'slack', webhookUrl: string) => {
        const url = `${API_URL}/notifications/${channel}/test`;
        try {
            const { json } = await httpClient(url, {
                method: 'POST',
                body: JSON.stringify({ webhookUrl }),
            });
            return {
                success: true,
                data: json
            };
        } catch (error) {
            console.error(`Error testing ${channel} connection:`, error);
            return {
                success: false,
                error: error instanceof Error ? error.message : 'Unknown error'
            };
        }
    },

    // Get notification channel health status
    getNotificationHealth: async () => {
        const url = `${API_URL}/notifications/health`;
        try {
            const { json } = await httpClient(url);
            return json;
        } catch (error) {
            console.error('Error fetching notification health:', error);
            throw error;
        }
    },

    // Custom method for generic API calls (used by MITRE components)
    custom: async ({ url, method = 'GET', body }: { url: string; method?: string; body?: any }) => {
        if (!url) {
            throw new Error('URL is required for custom API call');
        }

        const fullUrl = url.startsWith('http') ? url : `${API_URL}/${url}`;

        try {
            const options: fetchUtils.Options = { method };
            if (body) {
                options.body = JSON.stringify(body);
            }

            const { json } = await httpClient(fullUrl, options);
            return { data: json };
        } catch (error) {
            console.error(`Error making custom API call to ${fullUrl}:`, error);
            throw error;
        }
    },

    // Timeline-specific API methods
    getTimelineData: async (params: {
        granularity?: 'minute' | 'hour' | 'day' | 'week' | 'month';
        from?: string;
        to?: string;
        eventTypes?: string[];
        riskLevels?: string[];
    }) => {
        const query = new URLSearchParams();
        if (params.granularity) query.set('granularity', params.granularity);
        if (params.from) query.set('from', params.from);
        if (params.to) query.set('to', params.to);
        if (params.eventTypes?.length) query.set('eventTypes', params.eventTypes.join(','));
        if (params.riskLevels?.length) query.set('riskLevels', params.riskLevels.join(','));
        
        const url = `${API_URL}/timeline?${query.toString()}`;
        
        try {
            console.log(`[TimelineDataProvider] Getting timeline data: ${url}`);
            const { json } = await httpClient(url);
            console.log(`[TimelineDataProvider] Response received:`, json);

            // Handle empty or null response (204 No Content)
            if (!json || (Array.isArray(json) && json.length === 0)) {
                console.log(`[TimelineDataProvider] No data returned, returning empty array`);
                return { data: [], total: 0 };
            }

            return {
                data: json.data || json || [],
                total: json.total || (json.data ? json.data.length : 0)
            };
        } catch (error) {
            console.error('Error fetching timeline data:', error);
            throw error;
        }
    },

    getTimelineEvents: async (params: {
        timeStart?: string;
        timeEnd?: string;
        eventTypes?: string[];
        riskLevels?: string[];
        page?: number;
        limit?: number;
    }) => {
        const query = new URLSearchParams();
        if (params.timeStart) query.set('timeStart', params.timeStart);
        if (params.timeEnd) query.set('timeEnd', params.timeEnd);
        if (params.eventTypes?.length) query.set('eventTypes', params.eventTypes.join(','));
        if (params.riskLevels?.length) query.set('riskLevels', params.riskLevels.join(','));
        if (params.page) query.set('page', params.page.toString());
        if (params.limit) query.set('limit', params.limit.toString());
        
        const url = `${API_URL}/timeline/events?${query.toString()}`;
        
        try {
            console.log(`[TimelineDataProvider] Getting timeline events: ${url}`);
            const { json } = await httpClient(url);
            return {
                data: json.data || json,
                total: json.total || (json.data ? json.data.length : 0)
            };
        } catch (error) {
            console.error('Error fetching timeline events:', error);
            throw error;
        }
    },

    getTimelineHeatmap: async (params: {
        granularity?: 'hour' | 'day' | 'week';
        from?: string;
        to?: string;
    }) => {
        const query = new URLSearchParams();
        if (params.granularity) query.set('granularity', params.granularity);
        if (params.from) query.set('from', params.from);
        if (params.to) query.set('to', params.to);
        
        const url = `${API_URL}/timeline/heatmap?${query.toString()}`;
        
        try {
            console.log(`[TimelineDataProvider] Getting timeline heatmap: ${url}`);
            const { json } = await httpClient(url);
            return {
                data: json.data || json,
                total: json.total || (json.data ? json.data.length : 0)
            };
        } catch (error) {
            console.error('Error fetching timeline heatmap:', error);
            throw error;
        }
    },

    getTimelineStats: async (params?: {
        from?: string;
        to?: string;
    }) => {
        const query = new URLSearchParams();
        if (params?.from) query.set('from', params.from);
        if (params?.to) query.set('to', params.to);

        const url = `${API_URL}/timeline/stats?${query.toString()}`;
        
        try {
            console.log(`[TimelineDataProvider] Getting timeline stats: ${url}`);
            const { json } = await httpClient(url);
            console.log(`[TimelineDataProvider] Stats response:`, json);

            // Handle empty response
            if (!json) {
                console.log(`[TimelineDataProvider] No stats data returned`);
                return { data: { totalEvents: 0, highRisk: 0, mediumRisk: 0, lowRisk: 0 } };
            }

            return {
                data: json.data || json
            };
        } catch (error) {
            console.error('Error fetching timeline stats:', error);
            throw error;
        }
    },

    getTimelineAnomalies: async (params?: {
        from?: string;
        to?: string;
        threshold?: number;
    }) => {
        const query = new URLSearchParams();
        if (params?.from) query.set('from', params.from);
        if (params?.to) query.set('to', params.to);
        if (params?.threshold) query.set('threshold', params.threshold.toString());
        
        const url = `${API_URL}/timeline/anomalies?${query.toString()}`;
        
        try {
            console.log(`[TimelineDataProvider] Getting timeline anomalies: ${url}`);
            const { json } = await httpClient(url);
            return {
                data: json.data || json,
                total: json.total || (json.data ? json.data.length : 0)
            };
        } catch (error) {
            console.error('Error fetching timeline anomalies:', error);
            throw error;
        }
    }
};

// Enhanced data provider without caching for development
export const enhancedCastellanDataProvider = baseEnhancedCastellanDataProvider;

export default enhancedCastellanDataProvider;
