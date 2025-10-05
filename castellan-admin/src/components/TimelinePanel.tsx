import React, { useMemo, useState } from 'react';
import { Card, CardContent, CardHeader, Grid, Typography, Box, Divider } from '@mui/material';
import { useDataProvider, Title } from 'react-admin';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { TimelineToolbar, Granularity } from './TimelineToolbar';
import { TimelineChart, TimelinePoint } from './TimelineChart';
import { getResourceCacheConfig, queryKeys } from '../config/reactQueryConfig';

const toLocalDateTimeInput = (d: Date) => {
  // yyyy-MM-ddTHH:mm
  const pad = (n: number) => `${n}`.padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

export const TimelinePanel: React.FC = () => {
  const dataProvider: any = useDataProvider();
  const [granularity, setGranularity] = useState<Granularity>('day');

  const now = useMemo(() => new Date(), []);
  const defaultFrom = useMemo(() => {
    const d = new Date(now);
    d.setDate(d.getDate() - 7);
    return toLocalDateTimeInput(d);
  }, [now]);
  const defaultTo = useMemo(() => toLocalDateTimeInput(now), [now]);

  const [from, setFrom] = useState<string>(defaultFrom);
  const [to, setTo] = useState<string>(defaultTo);

  // Get cache config for timeline resource
  const cacheConfig = getResourceCacheConfig('timeline');

  // Build query params for consistent cache keys
  const timelineParams = useMemo(() => ({
    granularity,
    from: new Date(from).toISOString(),
    to: new Date(to).toISOString(),
  }), [granularity, from, to]);

  const statsParams = useMemo(() => ({
    from: new Date(from).toISOString(),
    to: new Date(to).toISOString(),
  }), [from, to]);

  // React Query for timeline data - CACHED with instant snapshots!
  const { data: timelineResponse, isLoading: timelineLoading, error: timelineError, refetch: refetchTimeline } = useQuery({
    queryKey: queryKeys.custom('timeline', 'data', timelineParams),
    queryFn: async () => {
      const { data: series } = await dataProvider.getTimelineData(timelineParams);

      // Normalize series -> TimelinePoint[]
      const points: TimelinePoint[] = (Array.isArray(series) ? series : series?.data || []).map((p: any) => ({
        timestamp: p.timestamp || p.time || p.bucket || p.key || new Date().toISOString(),
        count: p.count ?? p.total ?? 0,
      }));

      return points;
    },
    placeholderData: keepPreviousData, // Show previous data while fetching - instant feel!
    ...cacheConfig, // Apply centralized cache config (30s fresh, 30min memory, 60s polling)
  });

  // React Query for timeline stats - CACHED with instant snapshots!
  const { data: stats, isLoading: statsLoading, error: statsError, refetch: refetchStats } = useQuery({
    queryKey: queryKeys.custom('timeline', 'stats', statsParams),
    queryFn: async () => {
      const statsResp = await dataProvider.getTimelineStats(statsParams);
      return statsResp.data || statsResp;
    },
    placeholderData: keepPreviousData, // Show previous data while fetching - instant feel!
    ...cacheConfig,
  });

  // Combine loading states
  const loading = timelineLoading || statsLoading;
  const error = timelineError || statsError;
  const timelineData = timelineResponse || [];

  // Refresh function for toolbar
  const handleRefresh = () => {
    refetchTimeline();
    refetchStats();
  };

  return (
    <Box>
      <Title title="Timeline" />
      <TimelineToolbar
        granularity={granularity}
        setGranularity={setGranularity}
        from={from}
        to={to}
        setFrom={setFrom}
        setTo={setTo}
        onRefresh={handleRefresh}
      />

      <Grid container spacing={2} sx={{ p: 2 }}>
        <Grid item xs={12} md={8}>
          <Card>
            <CardHeader title="Security Events Over Time" subheader={`Granularity: ${granularity}`} />
            <Divider />
            <CardContent>
              <TimelineChart data={timelineData} loading={loading} />
              {error && (
                <Typography color="error" variant="body2" sx={{ mt: 1 }}>
                  {error instanceof Error ? error.message : 'Failed to load timeline'}
                </Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={4}>
          <Card>
            <CardHeader title="Summary" />
            <Divider />
            <CardContent>
              {stats ? (
                <Box>
                  <Typography variant="body2">Total Events: {stats.totalEvents ?? stats.total ?? '—'}</Typography>
                  <Typography variant="body2">High Risk: {stats.highRisk ?? '—'}</Typography>
                  <Typography variant="body2">Medium Risk: {stats.mediumRisk ?? '—'}</Typography>
                  <Typography variant="body2">Low Risk: {stats.lowRisk ?? '—'}</Typography>
                  {stats.topEventTypes && Array.isArray(stats.topEventTypes) && (
                    <Box sx={{ mt: 1 }}>
                      <Typography variant="subtitle2">Top Event Types</Typography>
                      {stats.topEventTypes.slice(0, 5).map((t: any, idx: number) => (
                        <Typography key={idx} variant="body2">• {t.name || t.type}: {t.count}</Typography>
                      ))}
                    </Box>
                  )}
                </Box>
              ) : (
                <Typography variant="body2">No stats available.</Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};
