import { useEffect, useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Api } from '../services/api';
import { MetricCard } from '../shared/MetricCard';
import { AlertTriangle, Shield, Activity } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { SignalRService } from '../services/signalr';

type Severity = 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW' | 'UNKNOWN';

interface SecurityEvent {
  id: string;
  title?: string;
  eventType?: string;
  description?: string;
  severity?: Severity;
  status?: string;
  timestamp?: string | Date;
  source?: string;
  target?: string;
  riskScore?: number;
}

function severityBadge(sev: Severity | undefined) {
  switch (sev) {
    case 'CRITICAL':
      return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
    case 'HIGH':
      return 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200';
    case 'MEDIUM':
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200';
    case 'LOW':
      return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-200';
  }
}

export function SecurityEventsPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!loading && !token) {
      // If not authenticated, send to login
      navigate('/login');
    }
  }, [token, loading, navigate]);

  const eventsQuery = useQuery({
    queryKey: ['security-events', { limit: 25 }],
    queryFn: () => Api.getRecentSecurityEvents(25),
    refetchInterval: 30000,
    enabled: !loading && !!token,
  });

  // Pull consolidated to ensure header boxes match backend-wide counts
  const consolidatedQuery = useQuery({
    queryKey: ['dashboard', 'consolidated', '24h'],
    queryFn: () => Api.getDashboardConsolidated('24h'),
    refetchInterval: 30000,
    enabled: !loading && !!token,
    staleTime: 15000,
  });

  // Live updates via SignalR (invalidate security-events on new event)
  useEffect(() => {
    if (loading || !token) return;
    const hub = new SignalRService();
    let mounted = true;
    (async () => {
      try {
        await hub.start();
        if (!mounted) return;
        hub.on('connected', () => {});
        hub.on('SecurityEvent', () => {
          queryClient.invalidateQueries({ queryKey: ['security-events'] });
        });
      } catch (e) {
        console.warn('[SignalR] connection failed (security-events)', e);
      }
    })();
    return () => {
      mounted = false;
      hub.stop().catch(() => undefined);
    };
  }, [loading, token, queryClient]);

  const events: SecurityEvent[] = useMemo(() => {
    const raw = (eventsQuery.data as any) || {};
    // Accept either { data, total } or array
    return Array.isArray(raw) ? raw : (raw.data || []);
  }, [eventsQuery.data]);

  const summary = useMemo(() => {
    const consolidated = (consolidatedQuery.data as any) || {};
    const root = consolidated.data && typeof consolidated.data === 'object' ? consolidated.data : consolidated;
    const se = root.securityEvents || {};

    // Total Events: authoritative from consolidated when present; fallback to list/total
    const totalFromConsolidated = se.totalEvents;
    const totalFromList = events?.length || (((eventsQuery.data as any)?.total) ?? 0) || 0;
    const total = typeof totalFromConsolidated === 'number' ? totalFromConsolidated : totalFromList;

    // Critical: authoritative from consolidated riskLevelCounts
    const riskCounts = se.riskLevelCounts || {};
    const criticalFromConsolidated = riskCounts.CRITICAL ?? riskCounts.critical;

    // Compute open and average risk from current page’s events (approximation)
    let open = 0, riskSum = 0;
    events?.forEach((e) => {
      const status = (e.status || '').toUpperCase();
      if (status === 'OPEN' || status === 'INVESTIGATING') open++;
      riskSum += e.riskScore || 0;
    });
    const avgRisk = (events?.length ?? 0) > 0 ? Math.round(riskSum / (events?.length ?? 1)) : 0;
    const critical = typeof criticalFromConsolidated === 'number' ? criticalFromConsolidated : (events?.filter(e => (e.severity || '').toUpperCase() === 'CRITICAL').length ?? 0);

    return { total, open, critical, avgRisk };
  }, [events, eventsQuery.data, consolidatedQuery.data]);

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Header */}
      <div className="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div className="px-8 py-6 flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Security Events</h1>
            <p className="text-gray-600 dark:text-gray-400 mt-1">Real-time threat detection and incident response</p>
          </div>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 text-green-600 dark:text-green-400">
              <div className="h-2 w-2 rounded-full bg-green-500 animate-pulse"></div>
              <span className="text-sm font-medium">Live Monitoring</span>
            </div>
            <button className="px-3 py-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700">Filters</button>
          </div>
        </div>
      </div>

      <div className="p-8">
        {/* Summary row */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <MetricCard title="Total Events" value={summary.total} icon={Activity} color="blue" description="All security events detected" />
          <MetricCard title="Open Events" value={summary.open} icon={AlertTriangle} color={summary.critical > 0 ? 'red' : 'yellow'} description="Events requiring attention" />
          <MetricCard title="Critical Threats" value={summary.critical} icon={Shield} color="red" description="High-priority security incidents" />
          <MetricCard title="Average Risk" value={summary.avgRisk} icon={Activity} color={summary.avgRisk >= 75 ? 'red' : summary.avgRisk >= 50 ? 'yellow' : 'green'} description="Mean risk score across all events" />
        </div>

        {/* Events list */}
        <div className="space-y-5">
          {eventsQuery.isLoading && (
            <div className="space-y-3">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="h-28 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 animate-pulse" />
              ))}
            </div>
          )}

          {!eventsQuery.isLoading && events?.map((e) => {
            const sev = (e.severity || 'MEDIUM') as Severity;
            const status = (e.status || '').toUpperCase();
            return (
              <div key={e.id} className={`rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-5`}> 
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-2">
                      <span className={`text-xs font-semibold px-2 py-1 rounded-full ${severityBadge(sev)}`}>{sev}</span>
                      <div className="text-gray-900 dark:text-gray-100 font-semibold">
                        {e.title || e.eventType || 'Security Event'}
                      </div>
                      {status && (
                        <span className="text-xs font-medium text-blue-600 dark:text-blue-400">{status}</span>
                      )}
                    </div>
                    <p className="text-sm text-gray-700 dark:text-gray-300 mb-3">{e.description || '—'}</p>
                    <div className="flex flex-wrap items-center gap-6 text-xs text-gray-500 dark:text-gray-400">
                      <div className="flex items-center gap-2">
                        <span className="h-1.5 w-1.5 rounded-full bg-gray-400"></span>
                        {new Date(e.timestamp || Date.now()).toLocaleString()}
                      </div>
                      {e.source && <div>Source: <span className="text-gray-700 dark:text-gray-300 font-medium">{e.source}</span></div>}
                      {e.target && <div>Target: <span className="text-gray-700 dark:text-gray-300 font-medium">{e.target}</span></div>}
                      <div>Risk: <span className={`${(e.riskScore || 0) >= 75 ? 'text-red-600' : (e.riskScore || 0) >= 50 ? 'text-yellow-600' : 'text-green-600'} font-semibold`}>{e.riskScore ?? 0}/100</span></div>
                    </div>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}


