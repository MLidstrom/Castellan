import { API_URL } from './constants';
import { AuthService } from './auth';
import { navigationService } from './navigation';

/**
 * ✅ FIX 5.2: CSRF Token Support
 *
 * Retrieves CSRF token from cookie or meta tag.
 * Backend requirements:
 * - Set CSRF token in cookie: Set-Cookie: XSRF-TOKEN=<token>
 * - OR include meta tag: <meta name="csrf-token" content="<token>">
 * - Validate X-XSRF-TOKEN header on state-changing requests (POST, PUT, PATCH, DELETE)
 */
function getCsrfToken(): string | null {
  // Try to get from cookie first (standard for SPA backends)
  const cookieMatch = document.cookie.match(/XSRF-TOKEN=([^;]+)/);
  if (cookieMatch) {
    return decodeURIComponent(cookieMatch[1]);
  }

  // Fallback to meta tag (common in server-rendered apps)
  const metaTag = document.querySelector<HTMLMetaElement>('meta[name="csrf-token"]');
  if (metaTag) {
    return metaTag.content;
  }

  return null;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = AuthService.getToken();

  // ✅ FIX 1.2a: Safe header merging with proper types
  const baseHeaders: Record<string, string> = {
    'Content-Type': 'application/json'
  };

  // ✅ FIX 1.2b: Convert init.headers to Record<string, string> safely
  let customHeaders: Record<string, string> = {};
  if (init?.headers) {
    if (init.headers instanceof Headers) {
      init.headers.forEach((value, key) => {
        customHeaders[key] = value;
      });
    } else if (Array.isArray(init.headers)) {
      init.headers.forEach(([key, value]) => {
        customHeaders[key] = value;
      });
    } else {
      customHeaders = init.headers as Record<string, string>;
    }
  }

  const headers: Record<string, string> = {
    ...baseHeaders,
    ...customHeaders
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  // ✅ FIX 5.2: Add CSRF token for state-changing requests
  const method = (init?.method || 'GET').toUpperCase();
  const isStateMutating = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(method);
  if (isStateMutating) {
    const csrfToken = getCsrfToken();
    if (csrfToken) {
      headers['X-XSRF-TOKEN'] = csrfToken;
    }
  }

  const res = await fetch(`${API_URL}${path}`, { ...init, headers });

  // ✅ FIX 3.3: Handle 401 Unauthorized or 403 Forbidden - use navigation service
  if (res.status === 401 || res.status === 403) {
    console.log(`[API] Authentication failed (${res.status}), redirecting to login`);
    AuthService.logout();
    navigationService.toLogin(); // ✅ Use navigation service instead of window.location
    throw new Error(`Authentication failed (${res.status}) - redirecting to login`);
  }

  if (!res.ok) {
    const errorText = await res.text();
    throw new Error(`API ${res.status}: ${errorText}`);
  }

  return res.json();
}

export const Api = {
  getDashboardConsolidated: (timeRange = '24h') => request(`/dashboarddata/consolidated?timeRange=${timeRange}`),
  getRecentSecurityEvents: (limit = 8) => request(`/security-events?limit=${limit}&sort=timestamp&order=desc`),
  getSystemStatus: () => request(`/system-status`),
  getMalwareStatus: () => request(`/malware-rules/status`).catch(() => null as any),
  getTimeline: (granularity: 'day' | 'hour', fromISO: string, toISO: string) =>
    request(`/timeline?granularity=${encodeURIComponent(granularity)}&from=${encodeURIComponent(fromISO)}&to=${encodeURIComponent(toISO)}`),
  getTimelineStats: (fromISO: string, toISO: string) =>
    request(`/timeline/stats?startTime=${encodeURIComponent(fromISO)}&endTime=${encodeURIComponent(toISO)}`),
  
  // MITRE ATT&CK API methods
  getMitreTechniques: (params: {
    page?: number;
    perPage?: number;
    search?: string;
    tactic?: string;
    platform?: string;
    sort?: string;
    order?: string;
  } = {}) => {
    const queryParams = new URLSearchParams();
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.perPage) queryParams.append('perPage', params.perPage.toString());
    if (params.search) queryParams.append('search', params.search);
    if (params.tactic) queryParams.append('tactic', params.tactic);
    if (params.platform) queryParams.append('platform', params.platform);
    if (params.sort) queryParams.append('sort', params.sort);
    if (params.order) queryParams.append('order', params.order);
    
    return request(`/mitre/techniques?${queryParams.toString()}`);
  },
  getMitreStatistics: () => request(`/mitre/statistics`),
  importMitreTechniques: () => request(`/mitre/import`, { method: 'POST' }),

  // Malware Rules API methods
  getMalwareRules: (params: {
    page?: number;
    perPage?: number;
    search?: string;
    category?: string;
    threatLevel?: string;
    isValid?: boolean;
    isEnabled?: boolean;
  } = {}) => {
    const queryParams = new URLSearchParams();
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.perPage) queryParams.append('perPage', params.perPage.toString());
    if (params.search) queryParams.append('q', params.search);
    if (params.category) queryParams.append('category', params.category);
    if (params.threatLevel) queryParams.append('threatLevel', params.threatLevel);
    if (params.isValid !== undefined) queryParams.append('isValid', params.isValid.toString());
    if (params.isEnabled !== undefined) queryParams.append('isEnabled', params.isEnabled.toString());

    return request(`/malware-rules?${queryParams.toString()}`);
  },
  getMalwareStatistics: () => request(`/malware-rules/statistics`),
  toggleMalwareRule: (id: number, enabled: boolean) =>
    request(`/malware-rules/${id}/toggle`, {
      method: 'POST',
      body: JSON.stringify({ isEnabled: enabled })
    }),
  deleteMalwareRule: (id: number) => request(`/malware-rules/${id}`, { method: 'DELETE' }),
  importMalwareRules: (data: {
    ruleContent: string;
    category: string;
    author: string;
    skipDuplicates: boolean;
    enableByDefault: boolean;
  }) => request(`/malware-rules/import`, {
    method: 'POST',
    body: JSON.stringify(data)
  }),

  // Threat Scanner API methods
  getThreatScans: (params: {
    page?: number;
    perPage?: number;
    sort?: string;
    order?: string;
  } = {}) => {
    const queryParams = new URLSearchParams();
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.perPage) queryParams.append('perPage', params.perPage.toString());
    if (params.sort) queryParams.append('sort', params.sort);
    if (params.order) queryParams.append('order', params.order);

    return request(`/threat-scanner?${queryParams.toString()}`);
  },
  getScanProgress: () => request(`/threat-scanner/progress`),
  startQuickScan: () => request(`/threat-scanner/quick-scan?async=true`, { method: 'POST' }),
  startFullScan: () => request(`/threat-scanner/full-scan?async=true`, { method: 'POST' }),
  cancelScan: () => request(`/threat-scanner/cancel`, { method: 'POST' }),
  getLastScanResult: () => request(`/threat-scanner/last-result`),

  // Notification Templates API methods
  getNotificationTemplates: () => request(`/notification-templates`),
  getNotificationTemplatesByPlatform: (platform: 'teams' | 'slack') =>
    request(`/notification-templates/platform/${platform}`),
  getNotificationTemplateById: (id: string) => request(`/notification-templates/${id}`),
  createNotificationTemplate: (template: any) =>
    request(`/notification-templates`, {
      method: 'POST',
      body: JSON.stringify(template)
    }),
  updateNotificationTemplate: (id: string, template: any) =>
    request(`/notification-templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(template)
    }),
  deleteNotificationTemplate: (id: string) =>
    request(`/notification-templates/${id}`, { method: 'DELETE' }),
  createDefaultTemplates: () =>
    request(`/notification-templates/defaults`, { method: 'POST' }),
};


