import { config, logger } from '../config/environment';

// Enhanced HTTP client with retry logic, timeout, and error handling
export class HTTPClient {
    private baseURL: string;
    private timeout: number;
    private retryAttempts: number;
    private retryDelay: number;

    constructor(baseURL?: string) {
        this.baseURL = baseURL || config.apiUrl;
        this.timeout = config.api.timeout;
        this.retryAttempts = config.api.retryAttempts;
        this.retryDelay = config.api.retryDelay;
    }

    // Create an AbortController for timeout functionality
    private createTimeoutController(timeout: number): AbortController {
        const controller = new AbortController();
        setTimeout(() => controller.abort(), timeout);
        return controller;
    }

    // Sleep utility for retry delays
    private sleep(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    // Enhanced fetch with retry logic
    async fetch(url: string, options: RequestInit = {}): Promise<Response> {
        const fullURL = url.startsWith('http') ? url : `${this.baseURL}${url}`;
        
        // Add authentication headers
        const token = localStorage.getItem('auth_token');
        const headers = new Headers(options.headers);
        
        if (token && !headers.has('Authorization')) {
            headers.set('Authorization', `Bearer ${token}`);
        }
        
        if (!headers.has('Content-Type') && options.method !== 'GET') {
            headers.set('Content-Type', 'application/json');
        }
        
        headers.set('Accept', 'application/json');

        let lastError: Error | null = null;
        
        for (let attempt = 0; attempt <= this.retryAttempts; attempt++) {
            try {
                const controller = this.createTimeoutController(this.timeout);
                
                logger.debug(`HTTP ${options.method || 'GET'} ${fullURL} (attempt ${attempt + 1})`);
                
                const response = await fetch(fullURL, {
                    ...options,
                    headers,
                    signal: controller.signal
                });

                // Handle authentication errors
                if (response.status === 401) {
                    localStorage.removeItem('auth_token');
                    localStorage.removeItem('user_permissions');
                    throw new Error('Authentication required');
                }

                // Handle client errors (don't retry)
                if (response.status >= 400 && response.status < 500 && response.status !== 408) {
                    const errorData = await response.json().catch(() => ({}));
                    throw new Error(errorData.message || `HTTP ${response.status}: ${response.statusText}`);
                }

                // Handle server errors (retry on 5xx and 408)
                if (response.status >= 500 || response.status === 408) {
                    const errorData = await response.json().catch(() => ({}));
                    lastError = new Error(errorData.message || `HTTP ${response.status}: ${response.statusText}`);
                    
                    if (attempt < this.retryAttempts) {
                        logger.warn(`Server error ${response.status}, retrying in ${this.retryDelay}ms...`);
                        await this.sleep(this.retryDelay * Math.pow(2, attempt)); // Exponential backoff
                        continue;
                    }
                    
                    throw lastError;
                }

                logger.debug(`HTTP ${options.method || 'GET'} ${fullURL} completed with status ${response.status}`);
                return response;

            } catch (error) {
                lastError = error instanceof Error ? error : new Error('Unknown error');

                // Don't retry on abort (timeout) or client errors
                if (error instanceof Error && (
                    error.name === 'AbortError' ||
                    error.message.includes('Authentication required') ||
                    error.message.includes('HTTP 4')
                )) {
                    logger.error(`HTTP ${options.method || 'GET'} ${fullURL} failed:`, error.message);
                    throw error;
                }

                // Retry on network errors
                if (attempt < this.retryAttempts) {
                    logger.warn(`Network error, retrying in ${this.retryDelay}ms...`);
                    await this.sleep(this.retryDelay * Math.pow(2, attempt));
                    continue;
                }

                logger.error(`HTTP ${options.method || 'GET'} ${fullURL} failed after ${this.retryAttempts + 1} attempts:`, error);
                throw lastError;
            }
        }

        throw lastError || new Error('Request failed');
    }

    // Convenience methods
    async get(url: string, options: RequestInit = {}): Promise<Response> {
        return this.fetch(url, { ...options, method: 'GET' });
    }

    async post(url: string, data?: any, options: RequestInit = {}): Promise<Response> {
        return this.fetch(url, {
            ...options,
            method: 'POST',
            body: data ? JSON.stringify(data) : undefined
        });
    }

    async put(url: string, data?: any, options: RequestInit = {}): Promise<Response> {
        return this.fetch(url, {
            ...options,
            method: 'PUT',
            body: data ? JSON.stringify(data) : undefined
        });
    }

    async delete(url: string, options: RequestInit = {}): Promise<Response> {
        return this.fetch(url, { ...options, method: 'DELETE' });
    }

    async patch(url: string, data?: any, options: RequestInit = {}): Promise<Response> {
        return this.fetch(url, {
            ...options,
            method: 'PATCH',
            body: data ? JSON.stringify(data) : undefined
        });
    }

    // JSON convenience methods
    async getJSON<T = any>(url: string, options: RequestInit = {}): Promise<T> {
        const response = await this.get(url, options);
        return response.json();
    }

    async postJSON<T = any>(url: string, data?: any, options: RequestInit = {}): Promise<T> {
        const response = await this.post(url, data, options);
        return response.json();
    }

    async putJSON<T = any>(url: string, data?: any, options: RequestInit = {}): Promise<T> {
        const response = await this.put(url, data, options);
        return response.json();
    }

    async patchJSON<T = any>(url: string, data?: any, options: RequestInit = {}): Promise<T> {
        const response = await this.patch(url, data, options);
        return response.json();
    }

    // File upload method
    async uploadFile(url: string, file: File, additionalData?: Record<string, any>): Promise<Response> {
        const formData = new FormData();
        formData.append('file', file);
        
        if (additionalData) {
            Object.keys(additionalData).forEach(key => {
                formData.append(key, additionalData[key]);
            });
        }

        // Don't set Content-Type for FormData, let browser set it with boundary
        const headers = new Headers();
        const token = localStorage.getItem('auth_token');
        if (token) {
            headers.set('Authorization', `Bearer ${token}`);
        }

        return this.fetch(url, {
            method: 'POST',
            headers,
            body: formData
        });
    }
}

// Default HTTP client instance
export const httpClient = new HTTPClient();

// Export for convenience
export default httpClient;