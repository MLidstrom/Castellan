import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Api } from '../services/api';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';

type Granularity = 'day' | 'hour';

interface Bucket { label: string; count: number; }
interface TimelineResponseItem { timestamp: string; count: number; }

function toIsoString(d: Date) { return d.toISOString(); }
function pad(n: number) { return n.toString().padStart(2, '0'); }
function toLocalLabel(iso: string) {
  const d = new Date(iso);
  return `${d.getMonth()+1}/${d.getDate()}/${d.getFullYear()}, ${((d.getHours()+11)%12+1)}:${pad(d.getMinutes())}:${pad(d.getSeconds())} ${d.getHours()>=12?'PM':'AM'}`;
}

export function TimelinePage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!loading && !token) {
      navigate('/login');
    }
  }, [token, loading, navigate]);
  const [granularity, setGranularity] = useState<Granularity>('day');
  const [from, setFrom] = useState<Date>(() => {
    const d = new Date(); d.setDate(d.getDate()-7); return d;
  });
  const [to, setTo] = useState<Date>(() => new Date());

  const fromIso = useMemo(() => toIsoString(from), [from]);
  const toIso = useMemo(() => toIsoString(to), [to]);

  const timelineQuery = useQuery({
    queryKey: ['timeline', granularity, fromIso, toIso],
    queryFn: async () => {
      const res = await Api.getTimeline(granularity, fromIso, toIso) as Array<TimelineResponseItem> | any;
      return Array.isArray(res) ? res : (res.data || []);
    },
    enabled: !loading && !!token,
    staleTime: 15000,
  });

  const statsQuery = useQuery({
    queryKey: ['timeline-stats', fromIso, toIso],
    queryFn: () => Api.getTimelineStats(fromIso, toIso),
    enabled: !loading && !!token,
    staleTime: 15000,
  });

  const buckets: Bucket[] = useMemo(() => {
    const items = (timelineQuery.data as Array<TimelineResponseItem>) || [];
    return items.map(i => ({ label: toLocalLabel(i.timestamp), count: i.count }));
  }, [timelineQuery.data]);

  const totalEvents = (() => {
    const s: any = statsQuery.data || {};
    return s.total ?? s.totalEvents ?? buckets.reduce((sum, b) => sum + b.count, 0);
  })();
  const { high, medium, low } = (() => {
    const s: any = statsQuery.data || {};
    const counts: any = (s.riskLevelCounts
      || (s.securityEvents && s.securityEvents.riskLevelCounts)
      || s.counts
      || s.risk
      || {});
    return {
      high: s.high ?? s.highRisk ?? counts.HIGH ?? counts.high ?? 0,
      medium: s.medium ?? s.mediumRisk ?? counts.MEDIUM ?? counts.medium ?? 0,
      low: s.low ?? s.lowRisk ?? counts.LOW ?? counts.low ?? 0,
    };
  })();

  // Simple inputs; can be replaced with date pickers later
  const onRefresh = () => { /* react-query refetch auto by key change */ };

  useEffect(() => { /* ensure effect to satisfy hooks lint order */ }, []);

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Page header */}
      <div className="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div className="px-8 py-6 flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Timeline</h1>
            <p className="text-gray-600 dark:text-gray-400 mt-1">Security events over time</p>
          </div>
          <div className="flex items-center gap-2 text-green-600 dark:text-green-400">
            <div className="h-2 w-2 rounded-full bg-green-500 animate-pulse"></div>
            <span className="text-sm font-medium">Live Monitoring</span>
          </div>
        </div>
      </div>

      {/* Controls */}
      <div className="p-8 pt-6">
        <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-4 mb-6 flex items-end gap-6">
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400">Granularity</label>
            <select value={granularity} onChange={(e)=>setGranularity(e.target.value as Granularity)} className="mt-1 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm">
              <option value="day">Day</option>
              <option value="hour">Hour</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400">From</label>
            <input type="datetime-local" value={new Date(from).toISOString().slice(0,16)} onChange={(e)=>setFrom(new Date(e.target.value))} className="mt-1 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm" />
          </div>
          <div>
            <label className="block text-xs text-gray-500 dark:text-gray-400">To</label>
            <input type="datetime-local" value={new Date(to).toISOString().slice(0,16)} onChange={(e)=>setTo(new Date(e.target.value))} className="mt-1 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-3 py-2 text-sm" />
          </div>
          <button onClick={onRefresh} className="h-9 px-4 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-sm">REFRESH</button>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left panel: bars */}
        <div className="lg:col-span-2">
          <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
            <h3 className="text-xl font-semibold text-gray-900 dark:text-white mb-2">Security Events Over Time</h3>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-6">Granularity: {granularity}</p>
            <div className="space-y-3">
              {timelineQuery.isLoading && (
                [...Array(4)].map((_,i)=> (
                  <div key={i} className="h-5 rounded bg-gray-200 dark:bg-gray-700 animate-pulse" />
                ))
              )}
              {!timelineQuery.isLoading && buckets.map((b, idx) => (
                <div key={idx} className="flex items-center gap-4">
                  <div className="w-72 text-sm text-gray-700 dark:text-gray-300">{b.label}</div>
                  <div className="flex-1">
                    <div className="h-3 w-full bg-gray-200 dark:bg-gray-700 rounded-full">
                      <div className="h-3 bg-blue-600 rounded-full" style={{ width: `${Math.min(100, (b.count / Math.max(1, Math.max(...buckets.map(x=>x.count))))*100)}%` }}></div>
                    </div>
                  </div>
                  <div className="w-10 text-right text-sm text-gray-600 dark:text-gray-400">{b.count}</div>
                </div>
              ))}
            </div>
          </div>
        </div>
          {/* Right panel: summary */}
          <div>
            <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
              <h3 className="text-xl font-semibold text-gray-900 dark:text-white mb-4">Summary</h3>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between"><span className="text-gray-600 dark:text-gray-400">Total Events:</span><span className="text-gray-900 dark:text-gray-100 font-medium">{totalEvents}</span></div>
                <div className="flex justify-between"><span className="text-gray-600 dark:text-gray-400">High Risk:</span><span className="text-gray-900 dark:text-gray-100 font-medium">{high || '—'}</span></div>
                <div className="flex justify-between"><span className="text-gray-600 dark:text-gray-400">Medium Risk:</span><span className="text-gray-900 dark:text-gray-100 font-medium">{medium || '—'}</span></div>
                <div className="flex justify-between"><span className="text-gray-600 dark:text-gray-400">Low Risk:</span><span className="text-gray-900 dark:text-gray-100 font-medium">{low || '—'}</span></div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}


