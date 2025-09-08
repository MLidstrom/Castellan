import React, { useState, useEffect } from 'react';
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

interface SystemMetricsData {
  data: SystemMetric[];
  lastUpdated: string;
  systemHealth: string;
  overallStatus: 'healthy' | 'warning' | 'critical';
}

export const RealtimeSystemMetrics: React.FC = () => {
  const [metricsData, setMetricsData] = useState<SystemMetricsData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchSystemMetrics = async () => {
    try {
      const token = localStorage.getItem('authToken');
      const response = await fetch('/api/system-status', {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      
      const data = await response.json();
      setMetricsData(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch system metrics');
      console.error('Error fetching system metrics:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSystemMetrics();
    
    // Set up real-time updates every 10 seconds
    const interval = setInterval(fetchSystemMetrics, 10000);
    
    return () => clearInterval(interval);
  }, []);

  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'healthy': return 'success.main';
      case 'warning': return 'warning.main';
      case 'critical': 
      case 'error': return 'error.main';
      default: return 'text.secondary';
    }
  };

  if (loading) {
    return (
      <Card sx={{ width: '100%' }}>
        <CardHeader title="System Metrics" />
        <CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'center' }}>
            <CircularProgress />
          </Box>
        </CardContent>
      </Card>
    );
  }

  if (error) {
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
            <Chip 
              label={metricsData.overallStatus?.toUpperCase() || 'HEALTHY'} 
              color={metricsData.overallStatus?.toLowerCase() === 'healthy' ? 'success' : 
                metricsData.overallStatus?.toLowerCase() === 'warning' ? 'warning' : 'error'}
            />
          }
          subheader={`Last updated: ${new Date(metricsData.lastUpdated).toLocaleTimeString()}`}
        />
      </Card>

      {/* Component Metrics Grid */}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr', lg: '1fr 1fr 1fr' }, gap: 2 }}>
        {metricsData.data.map((metric) => (
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
        ))}
      </Box>
    </Box>
  );
};

export default RealtimeSystemMetrics;
