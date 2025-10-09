import { API_URL } from './constants';
import { AuthService } from './auth';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = AuthService.getToken();
  const headers: Record<string, string> = { 'Content-Type': 'application/json', ...(init?.headers as any || {}) };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${API_URL}${path}`, { ...init, headers });
  if (!res.ok) throw new Error(`API ${res.status}: ${await res.text()}`);
  return res.json();
}

export const Api = {
  getDashboardConsolidated: (timeRange = '24h') => request(`/dashboarddata/consolidated?timeRange=${timeRange}`),
  getRecentSecurityEvents: (limit = 8) => request(`/security-events?limit=${limit}&sort=timestamp&order=desc`),
  getSystemStatus: () => request(`/system-status`),
  getYaraStatus: () => request(`/yara-rules/status`).catch(() => null as any),
  getTimeline: (granularity: 'day' | 'hour', fromISO: string, toISO: string) =>
    request(`/timeline?granularity=${encodeURIComponent(granularity)}&from=${encodeURIComponent(fromISO)}&to=${encodeURIComponent(toISO)}`),
  getTimelineStats: (fromISO: string, toISO: string) =>
    request(`/timeline/stats?startTime=${encodeURIComponent(fromISO)}&endTime=${encodeURIComponent(toISO)}`),
};


