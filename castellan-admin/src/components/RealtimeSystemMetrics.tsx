import React from 'react';
import { 
  Card, 
  CardContent, 
  CardHeader, 
  Typography, 
  Box, 
  Button,
  Chip,
  CircularProgress,
  LinearProgress,
  Tooltip as MuiTooltip
} from '@mui/material';
import { useCachedApi } from '../hooks/useCachedApi';
import { CACHE_KEYS, CACHE_TTL } from '../utils/cacheManager';

interface SystemMetric {
  id: number;
  component: string;
  status: string;
  isHealthy: boolean;
  lastCheck: string;
  responseTime: number;
  uptime: string;
  details: string;
  errorCount: number;
  totalRequests: number;
  avgResponseTime: number;
  cpuUsage?: number;
  memoryUsage?: number;
  diskUsage?: number;
  networkIO?: number;
}


export const RealtimeSystemMetrics: React.FC = () => {
  const authToken = localStorage.getItem('auth_token');

  // Cached API call for system metrics
  const systemMetricsApi = useCachedApi(
    async () => {
      console.log('🚀 Fetching System Metrics...');
      const response = await fetch('/api/system-status', {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });
      
      if (!response.ok) {
        console.error('❌ System Metrics API Error:', { status: response.status, statusText: response.statusText });
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      
      const data = await response.json();
      console.log('🔍 System Metrics API Response:', { status: response.status, dataLength: data?.data?.length || 'no array', sample: data });
      
      // Transform the data to match expected structure if needed
      if (Array.isArray(data.data) || Array.isArray(data)) {
        // If API returns raw array, transform to expected structure
        const metricsArray = data.data || data;
        return {
          data: metricsArray,
          lastUpdated: new Date().toISOString(),
          systemHealth: 'healthy',
          overallStatus: 'healthy' as const
        };
      }
      
      return data;
    },
    {
      cacheKey: CACHE_KEYS.REALTIME_METRICS,
      cacheTtl: CACHE_TTL.FAST_REFRESH, // 30 seconds for real-time data
      refreshInterval: 10000, // Refresh every 10 seconds
      dependencies: []
    }
  );

  const metricsData = systemMetricsApi.data;
  const loading = systemMetricsApi.loading;
  const error = systemMetricsApi.error;

  const fetchSystemMetrics = () => {
    systemMetricsApi.refetch();
  };


  if (loading && !metricsData) {
    return (
      <Card sx={{ width: '100%' }}>
        <CardHeader title="System Metrics" />
        <CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'center' }}>
            <CircularProgress />
            <Typography sx={{ ml: 2 }}>Loading cached system metrics...</Typography>
          </Box>
        </CardContent>
      </Card>
    );
  }

  if (error && !metricsData) {
    return (
      <Card sx={{ width: '100%' }}>
        <CardHeader title="System Metrics" />
        <CardContent>
          <Typography color="error">
            Error: {error}
          </Typography>
          <Button 
            variant="contained" 
            color="primary" 
            onClick={fetchSystemMetrics}
            sx={{ mt: 2 }}
          >
            Retry
          </Button>
        </CardContent>
      </Card>
    );
  }

  if (!metricsData) {
    return (
      <Card sx={{ width: '100%' }}>
        <CardHeader title="System Metrics" />
        <CardContent>
          <Typography>No system metrics data available.</Typography>
        </CardContent>
      </Card>
    );
  }

  return (
    <Box sx={{ mb: 4 }}>
      {/* Overall System Health */}
      <Card sx={{ mb: 2 }}>
        <CardHeader 
          title="System Health Overview" 
          action={
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Chip 
                label={systemMetricsApi.isStale ? 'Stale Data' : 'Fresh'}
                color={systemMetricsApi.isStale ? 'warning' : 'success'}
                size="small"
              />
              <Chip 
                label={(metricsData.overallStatus?.toUpperCase?.() || 'HEALTHY')}
                color={metricsData.overallStatus?.toLowerCase() === 'healthy' ? 'success' : 
                  metricsData.overallStatus?.toLowerCase() === 'warning' ? 'warning' : 'error'}
              />
            </Box>
          }
          subheader={`Last updated: ${systemMetricsApi.lastUpdated ? systemMetricsApi.lastUpdated.toLocaleTimeString() : 'Unknown'}`}
        />
      </Card>

      {/* Component Metrics Grid */}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr', lg: '1fr 1fr 1fr' }, gap: 2 }}>
        {(metricsData.data || []).length > 0 ? (metricsData.data || []).map((metric: SystemMetric) => (
          <Card key={metric.id}>
            <CardHeader 
              title={metric.component}
              action={
                <Chip 
                  label={metric.status} 
                  color={metric.status?.toLowerCase() === 'healthy' ? 'success' : 
                    metric.status?.toLowerCase() === 'warning' ? 'warning' : 'error'}
                />
              }
              sx={{ pb: 1 }}
            />
            <CardContent>
              {/* Response Time */}
              <Box sx={{ mb: 2 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                  <Typography variant="body2">Response Time</Typography>
                  <Typography variant="body2">{metric.responseTime}ms</Typography>
                </Box>
                <LinearProgress 
                  variant="determinate" 
                  value={Math.min((metric.responseTime / 1000) * 100, 100)} 
                />
              </Box>

              {/* Uptime */}
              <Box sx={{ mb: 2 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                  <Typography variant="body2">Uptime</Typography>
                  <Typography variant="body2">{metric.uptime}</Typography>
                </Box>
              </Box>

              {/* Error Rate */}
              {metric.totalRequests > 0 && (
                <Box sx={{ mb: 2 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                    <Typography variant="body2">Error Rate</Typography>
                    <Typography variant="body2">{((metric.errorCount / metric.totalRequests) * 100).toFixed(1)}%</Typography>
                  </Box>
                  <LinearProgress 
                    variant="determinate" 
                    value={(metric.errorCount / metric.totalRequests) * 100} 
                  />
                </Box>
              )}

              {/* System Resource Metrics */}
              {metric.cpuUsage !== undefined && (
                <Box sx={{ mb: 2 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                    <Typography variant="body2">CPU Usage</Typography>
                    <Typography variant="body2">{metric.cpuUsage.toFixed(1)}%</Typography>
                  </Box>
                  <LinearProgress variant="determinate" value={metric.cpuUsage} />
                </Box>
              )}

              {metric.memoryUsage !== undefined && (
                <Box sx={{ mb: 2 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5 }}>
                    <Typography variant="body2">Memory Usage</Typography>
                    <Typography variant="body2">{metric.memoryUsage.toFixed(1)}%</Typography>
                  </Box>
                  <LinearProgress variant="determinate" value={metric.memoryUsage} />
                </Box>
              )}

              {/* Details */}
              <MuiTooltip title={metric.details}>
                <Typography variant="caption" noWrap sx={{ display: 'block', color: 'text.secondary', mb: 1 }}>
                  {metric.details}
                </Typography>
              </MuiTooltip>

              {/* Last Check */}
              <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                Last check: {new Date(metric.lastCheck).toLocaleString()}
              </Typography>
            </CardContent>
          </Card>
        )) : (
          <Card>
            <CardContent>
              <Box sx={{ textAlign: 'center', p: 2 }}>
                <Typography color="textSecondary">
                  No system metrics data available
                </Typography>
                <Button 
                  variant="contained" 
                  color="primary" 
                  onClick={fetchSystemMetrics}
                  sx={{ mt: 2 }}
                >
                  Refresh
                </Button>
              </Box>
            </CardContent>
          </Card>
        )}
      </Box>
    </Box>
  );
};

export default RealtimeSystemMetrics;
