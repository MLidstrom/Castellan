import { API_URL } from './constants';
import { AuthService } from './auth';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = AuthService.getToken();
  const headers: Record<string, string> = { 'Content-Type': 'application/json', ...(init?.headers as any || {}) };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${API_URL}${path}`, { ...init, headers });

  // Handle 401 Unauthorized - clear token and redirect to login
  if (res.status === 401) {
    AuthService.logout();
    window.location.href = '/login';
    throw new Error('Unauthorized - redirecting to login');
  }

  if (!res.ok) throw new Error(`API ${res.status}: ${await res.text()}`);
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
};


