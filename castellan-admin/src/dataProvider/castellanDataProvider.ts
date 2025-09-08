import { DataProvider, fetchUtils, HttpError } from 'react-admin';

// Castellan Open Source Data Provider - All features enabled
// This is the free version with no premium restrictions

// API Configuration
const API_URL = process.env.REACT_APP_CASTELLANPRO_API_URL || 'http://localhost:5000/api';
// WebSocket URL available for future use
// const WS_URL = process.env.REACT_APP_WS_URL || 'ws://localhost:5000/ws';

// HTTP Client with authentication and error handling
const httpClient = (url: string, options: fetchUtils.Options = {}) => {
    // Add authentication token if available
    const token = localStorage.getItem('auth_token');
    if (token) {
        options.headers = new Headers(options.headers);
        options.headers.set('Authorization', `Bearer ${token}`);
    }

    // Set default headers
    options.headers = new Headers(options.headers);
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
    'users': 'users',
    'settings': 'settings',
    'analytics': 'analytics',
    'reports': 'reports',
    // MITRE ATT&CK resource mappings
    'mitre/techniques': 'mitre/techniques',
    'mitre/tactics': 'mitre/tactics',
    'mitre/groups': 'mitre/groups',
    'mitre/software': 'mitre/software'
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
            apiFilters[`${key}_in`] = value.join(',');
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
    if (resource.startsWith('mitre/')) {
        const resourceType = resource.split('/')[1]; // 'techniques', 'tactics', 'groups', 'software'
        
        // MITRE endpoints return { techniques: [...] } or { tactics: [...] } etc.
        if (response[resourceType]) {
            return {
                data: response[resourceType],
                total: response.totalCount || response[resourceType].length
            };
        }
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

        const backendResource = RESOURCE_MAP[resource] || resource;
        const url = `${API_URL}/${backendResource}/${params.id}`;
        
        try {
            const { json } = await httpClient(url);
            // Backend returns { data: { id: "1", ... } }, we need to extract the nested data
            return { data: json.data || json };
        } catch (error) {
            console.error(`Error fetching ${resource} ${params.id}:`, error);
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
        
        const query = new URLSearchParams({
            page: page.toString(),
            limit: perPage.toString(),
            sort: field,
            order: order.toLowerCase(),
            [params.target]: params.id.toString(),
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

// Enhanced data provider with additional methods for custom operations
export const enhancedCastellanDataProvider = {
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
    }
};

export default enhancedCastellanDataProvider;