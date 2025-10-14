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
  const [granularity, setGranularity] = useState<Granularity>('hour');
  const [from, setFrom] = useState<Date>(() => {
    const d = new Date(); d.setHours(d.getHours()-24); return d;
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
  const riskLevelStats = (() => {
    const s: any = statsQuery.data || {};
    const riskLevels: any = s.eventsByRiskLevel || {};
    return {
      critical: riskLevels.Critical ?? riskLevels.critical ?? 0,
      high: riskLevels.High ?? riskLevels.high ?? 0,
      medium: riskLevels.Medium ?? riskLevels.medium ?? 0,
      low: riskLevels.Low ?? riskLevels.low ?? 0,
      info: riskLevels.Info ?? riskLevels.info ?? 0,
    };
  })();

  const topEventTypes = (() => {
    const s: any = statsQuery.data || {};
    const types: any = s.eventsByType || {};
    return Object.entries(types)
      .map(([type, count]) => ({ type, count: count as number }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 5);
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
            <p className="text-gray-600 dark:text-gray-400 mt-1">Security events over time (24-hour rolling window)</p>
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
              {statsQuery.isLoading ? (
                <div className="space-y-3">
                  {[...Array(4)].map((_, i) => (
                    <div key={i} className="h-5 rounded bg-gray-200 dark:bg-gray-700 animate-pulse" />
                  ))}
                </div>
              ) : (
                <div className="space-y-4">
                  <div>
                    <div className="flex justify-between mb-3">
                      <span className="text-sm font-semibold text-gray-700 dark:text-gray-300">Total Events</span>
                      <span className="text-lg font-bold text-gray-900 dark:text-gray-100">{totalEvents.toLocaleString()}</span>
                    </div>
                    <div className="space-y-2 text-sm">
                      {riskLevelStats.critical > 0 && (
                        <div className="flex justify-between items-center">
                          <span className="flex items-center gap-2">
                            <span className="h-2 w-2 rounded-full bg-red-500"></span>
                            <span className="text-gray-600 dark:text-gray-400">Critical</span>
                          </span>
                          <span className="text-gray-900 dark:text-gray-100 font-medium">{riskLevelStats.critical}</span>
                        </div>
                      )}
                      {riskLevelStats.high > 0 && (
                        <div className="flex justify-between items-center">
                          <span className="flex items-center gap-2">
                            <span className="h-2 w-2 rounded-full bg-orange-500"></span>
                            <span className="text-gray-600 dark:text-gray-400">High</span>
                          </span>
                          <span className="text-gray-900 dark:text-gray-100 font-medium">{riskLevelStats.high}</span>
                        </div>
                      )}
                      {riskLevelStats.medium > 0 && (
                        <div className="flex justify-between items-center">
                          <span className="flex items-center gap-2">
                            <span className="h-2 w-2 rounded-full bg-yellow-500"></span>
                            <span className="text-gray-600 dark:text-gray-400">Medium</span>
                          </span>
                          <span className="text-gray-900 dark:text-gray-100 font-medium">{riskLevelStats.medium}</span>
                        </div>
                      )}
                      {riskLevelStats.low > 0 && (
                        <div className="flex justify-between items-center">
                          <span className="flex items-center gap-2">
                            <span className="h-2 w-2 rounded-full bg-green-500"></span>
                            <span className="text-gray-600 dark:text-gray-400">Low</span>
                          </span>
                          <span className="text-gray-900 dark:text-gray-100 font-medium">{riskLevelStats.low}</span>
                        </div>
                      )}
                      {riskLevelStats.info > 0 && (
                        <div className="flex justify-between items-center">
                          <span className="flex items-center gap-2">
                            <span className="h-2 w-2 rounded-full bg-blue-500"></span>
                            <span className="text-gray-600 dark:text-gray-400">Info</span>
                          </span>
                          <span className="text-gray-900 dark:text-gray-100 font-medium">{riskLevelStats.info}</span>
                        </div>
                      )}
                    </div>
                  </div>

                  {topEventTypes.length > 0 && (
                    <div className="pt-4 border-t border-gray-200 dark:border-gray-700">
                      <div className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3">Top Event Types</div>
                      <div className="space-y-2 text-sm">
                        {topEventTypes.map(({ type, count }) => (
                          <div key={type} className="flex justify-between items-center">
                            <span className="text-gray-600 dark:text-gray-400 truncate mr-2">{type}</span>
                            <span className="text-gray-900 dark:text-gray-100 font-medium whitespace-nowrap">{count}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}


