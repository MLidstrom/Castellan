import { DataProvider } from 'react-admin';

/**
 * Simplified Data Provider - No Caching
 *
 * This provider does NOT cache data - React Query handles all caching.
 * It only provides:
 * - Request deduplication (prevents duplicate in-flight requests)
 * - API transformations (delegated to base provider)
 * - Consistent interface for React Admin
 *
 * All caching, TTL, background refresh, and invalidation is managed by React Query.
 *
 * @see reactQueryConfig.ts for cache configuration
 * @see enhancedDataProvider.ts (deprecated) - Previous cache implementation
 */
export class SimplifiedDataProvider implements DataProvider {
    private pendingRequests = new Map<string, Promise<any>>();

    constructor(private baseProvider: DataProvider) {}

    /**
     * Get list of resources - NO caching, React Query handles it
     */
    async getList(resource: string, params: any): Promise<any> {
        const requestKey = this.generateRequestKey('getList', resource, params);

        // Request deduplication only - if same request is already in-flight, reuse it
        const pending = this.pendingRequests.get(requestKey);
        if (pending) {
            console.log(`[SimplifiedDataProvider] Reusing in-flight request: ${resource}`);
            return pending;
        }

        const promise = this.baseProvider.getList(resource, params)
            .finally(() => {
                // Clean up pending request when done
                this.pendingRequests.delete(requestKey);
            });

        this.pendingRequests.set(requestKey, promise);
        return promise;
    }

    /**
     * Get single record - NO caching
     */
    async getOne(resource: string, params: any): Promise<any> {
        const requestKey = this.generateRequestKey('getOne', resource, params);

        const pending = this.pendingRequests.get(requestKey);
        if (pending) {
            console.log(`[SimplifiedDataProvider] Reusing in-flight request: ${resource}/${params.id}`);
            return pending;
        }

        const promise = this.baseProvider.getOne(resource, params)
            .finally(() => {
                this.pendingRequests.delete(requestKey);
            });

        this.pendingRequests.set(requestKey, promise);
        return promise;
    }

    /**
     * Get many records - NO caching
     */
    async getMany(resource: string, params: any): Promise<any> {
        const requestKey = this.generateRequestKey('getMany', resource, params);

        const pending = this.pendingRequests.get(requestKey);
        if (pending) {
            console.log(`[SimplifiedDataProvider] Reusing in-flight request: ${resource} (${params.ids.length} records)`);
            return pending;
        }

        const promise = this.baseProvider.getMany(resource, params)
            .finally(() => {
                this.pendingRequests.delete(requestKey);
            });

        this.pendingRequests.set(requestKey, promise);
        return promise;
    }

    /**
     * Get many references - NO caching
     */
    async getManyReference(resource: string, params: any): Promise<any> {
        const requestKey = this.generateRequestKey('getManyReference', resource, params);

        const pending = this.pendingRequests.get(requestKey);
        if (pending) {
            console.log(`[SimplifiedDataProvider] Reusing in-flight request: ${resource} references`);
            return pending;
        }

        const promise = this.baseProvider.getManyReference(resource, params)
            .finally(() => {
                this.pendingRequests.delete(requestKey);
            });

        this.pendingRequests.set(requestKey, promise);
        return promise;
    }

    /**
     * Create record - NO caching needed
     * Note: React Query will automatically invalidate relevant caches
     */
    async create(resource: string, params: any): Promise<any> {
        return this.baseProvider.create(resource, params);
    }

    /**
     * Update record - NO caching needed
     * Note: React Query will automatically invalidate relevant caches
     */
    async update(resource: string, params: any): Promise<any> {
        return this.baseProvider.update(resource, params);
    }

    /**
     * Update many records - NO caching needed
     */
    async updateMany(resource: string, params: any): Promise<any> {
        return this.baseProvider.updateMany(resource, params);
    }

    /**
     * Delete record - NO caching needed
     */
    async delete(resource: string, params: any): Promise<any> {
        return this.baseProvider.delete(resource, params);
    }

    /**
     * Delete many records - NO caching needed
     */
    async deleteMany(resource: string, params: any): Promise<any> {
        return this.baseProvider.deleteMany(resource, params);
    }

    /**
     * Generate unique request key for deduplication
     */
    private generateRequestKey(method: string, resource: string, params: any): string {
        // Create stable key by sorting params
        const sortedParams = this.sortObject(params);
        return `${method}:${resource}:${JSON.stringify(sortedParams)}`;
    }

    /**
     * Sort object keys recursively for stable key generation
     */
    private sortObject(obj: any): any {
        if (obj === null || typeof obj !== 'object') {
            return obj;
        }

        if (Array.isArray(obj)) {
            return obj.map(item => this.sortObject(item));
        }

        const sortedObj: any = {};
        Object.keys(obj).sort().forEach(key => {
            sortedObj[key] = this.sortObject(obj[key]);
        });

        return sortedObj;
    }

    /**
     * Get statistics about pending requests (for debugging)
     */
    public getStats(): { pendingRequests: number; requestKeys: string[] } {
        return {
            pendingRequests: this.pendingRequests.size,
            requestKeys: Array.from(this.pendingRequests.keys()),
        };
    }
}

/**
 * Factory function to create simplified data provider with Proxy for custom methods
 *
 * This wraps the SimplifiedDataProvider in a Proxy to support custom methods
 * from the base provider (like getTimelineData, getDashboardMetrics, etc.)
 */
export const createSimplifiedDataProvider = (baseProvider: DataProvider): DataProvider => {
    const simplifiedProvider = new SimplifiedDataProvider(baseProvider);

    // Create a Proxy to handle custom methods not defined in SimplifiedDataProvider
    return new Proxy(simplifiedProvider, {
        get(target: any, prop: string) {
            // If the property exists on the simplified provider, use it
            if (prop in target) {
                return target[prop];
            }

            // Otherwise, check if it exists on the base provider (custom methods)
            if (prop in baseProvider && typeof (baseProvider as any)[prop] === 'function') {
                console.log(`[SimplifiedDataProvider] Proxying custom method: ${prop}`);

                // Just pass through to base provider - no caching/deduplication for custom methods
                // React Query will handle caching for these
                return (baseProvider as any)[prop].bind(baseProvider);
            }

            return undefined;
        }
    }) as DataProvider;
};
