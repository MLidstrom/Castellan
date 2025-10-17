import { useEffect, useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { MetricCard } from '../shared/MetricCard';
import { RecentActivity } from '../shared/RecentActivity';
import { ThreatDistribution } from '../shared/ThreatDistribution';
import { LoadingSpinner } from '../components/LoadingSpinner';
import { SignalRStatus } from '../components/SignalRStatus';
import { AlertTriangle, Shield, Search, TrendingUp, Server, Zap, Scan } from 'lucide-react';
import { Api } from '../services/api';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { useSignalR } from '../contexts/SignalRContext';

export function DashboardPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { hub } = useSignalR();

  useEffect(() => {
    if (!loading && !token) {
      navigate('/login');
    }
  }, [token, loading, navigate]);

  const dashboardQuery = useQuery({
    queryKey: ['dashboard', 'consolidated', '24h'],
    queryFn: () => Api.getDashboardConsolidated('24h'),
    refetchInterval: 30000,
    enabled: !loading && !!token,
  });

  // Wire SignalR live updates
  useEffect(() => {
    if (!hub) return;

    // Dashboard consolidated updates push
    hub.on('DashboardUpdate', (payload: any) => {
      queryClient.setQueryData(['dashboard', 'consolidated', '24h'], payload);
    });
    // Security events may impact recent activity
    hub.on('SecurityEvent', () => {
      queryClient.invalidateQueries({ queryKey: ['security-events'] });
    });
    // System status live changes
    hub.on('SystemStatusUpdate', () => {
      queryClient.invalidateQueries({ queryKey: ['system-status'] });
    });
  }, [hub, queryClient]);

  const normalized = useMemo(() => {
    const raw = dashboardQuery.data || {} as any;
    const root = raw.data && typeof raw.data === 'object' ? raw.data : raw;

    // Mapping to the provided consolidated shape
    const se = root.securityEvents || {};
    const totalEvents = se.totalEvents ?? 0;
    const recentEvents = Array.isArray(se.recentEvents) ? se.recentEvents : [];
    const riskCounts = se.riskLevelCounts || {};
    const critical = riskCounts.CRITICAL ?? riskCounts.critical ?? 0;

    // Heuristic for open events: recent events with status OPEN/INVESTIGATING
    const open = recentEvents.filter((e: any) => {
      const s = (e.status || '').toUpperCase();
      return s === 'OPEN' || s === 'INVESTIGATING';
    }).length;

    // Last 24h delta is not present; show recentEvents length as approximation
    const last24h = recentEvents.length ?? 0;

    // Events/24h: Total events in 24-hour window (Castellan scope)
    const events24h = totalEvents;

    // Malware detection: now included in consolidated data
    const malware = root.malware || root.yara || {};
    const enabledRules = malware.enabledRules ?? 0;
    const totalRules = malware.totalRules ?? 0;
    const recentMatches = malware.recentMatches ?? 0;

    // Threat scans: extract from threatScanner data in consolidated response
    const ts = root.threatScanner || {};
    const totalScans = ts.totalScans ?? 0;
    const lastScanResult = ts.lastScanResult ?? 'N/A';
    const lastScanStatus = ts.lastScanStatus ?? 'unknown';

    // System status: compute from systemStatus summary
    const ss = root.systemStatus || {};
    let status: string = 'UNKNOWN';
    const healthyComponents = ss.healthyComponents ?? 0;
    const totalComponents = ss.totalComponents ?? 0;
    if (typeof ss.totalComponents === 'number' && typeof ss.healthyComponents === 'number') {
      status = ss.totalComponents > 0 && ss.healthyComponents === ss.totalComponents ? 'OPERATIONAL' : 'DEGRADED';
    }

    const threatDistribution = Object.keys(riskCounts).map((k) => ({ severity: k, count: riskCounts[k] as number }));

    // Recent activity: now included in consolidated data
    const recentActivity = Array.isArray(root.recentActivity) ? root.recentActivity : [];

    return {
      events: { open, last24h, critical, events24h },
      malware: { enabledRules, totalRules, recentMatches },
      threatScans: { totalScans, lastScanResult, lastScanStatus },
      system: { status, healthyComponents, totalComponents },
      threatDistribution,
      recentActivity,
    };
  }, [dashboardQuery.data]);

  // Debug once to inspect shape
  useEffect(() => {
    if (dashboardQuery.data) {
      console.group('[Dashboard] Consolidated data');
      console.log(dashboardQuery.data);
      console.groupEnd();
    }
  }, [dashboardQuery.data]);

  if (dashboardQuery.isLoading) {
    return <LoadingSpinner />;
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div className="px-8 py-6">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Dashboard</h1>
              <p className="text-gray-600 dark:text-gray-400 mt-1">Real-time security platform overview</p>
            </div>
            <SignalRStatus />
          </div>
        </div>
      </div>

      <div className="p-8">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <MetricCard title="Open Events" value={normalized.events.open} change={{ value: normalized.events.last24h, period: 'last 24h' }} icon={AlertTriangle} color={normalized.events.critical > 0 ? 'red' : 'green'} description="Security events requiring attention" />
          <MetricCard title="Critical Threats" value={normalized.events.critical} icon={Shield} color="red" description="High-priority security incidents" />
          <MetricCard title="Malware Rules" value={`${normalized.malware.enabledRules}/${normalized.malware.totalRules}`} change={{ value: normalized.malware.recentMatches, period: 'matches today' }} icon={Search} color="blue" description="Active malware detection rules" />
          <MetricCard title="Threat Scans" value={normalized.threatScans.totalScans} change={{ value: normalized.threatScans.lastScanResult, period: 'last result' }} icon={Scan} color={normalized.threatScans.lastScanStatus === 'clean' ? 'green' : normalized.threatScans.lastScanStatus === 'threat' ? 'red' : 'gray'} description="Threat intelligence scans completed" />
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <MetricCard title="Events/24h" value={normalized.events.events24h} icon={TrendingUp} color="blue" description="Total events in 24-hour window" />
          <MetricCard title="System Status" value={`${normalized.system.healthyComponents}/${normalized.system.totalComponents}`} icon={Server} color={normalized.system.healthyComponents === normalized.system.totalComponents && normalized.system.totalComponents > 0 ? 'green' : 'yellow'} description="Healthy components / Total components" />
          <MetricCard title="Response Time" value="2.3min" icon={Zap} color="green" description="Average incident response time" />
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          <div className="lg:col-span-2">
            <RecentActivity events={normalized.recentActivity ?? []} isLoading={dashboardQuery.isLoading} />
          </div>
          <ThreatDistribution data={normalized.threatDistribution ?? []} isLoading={dashboardQuery.isLoading} />
        </div>
      </div>
    </div>
  );
}


