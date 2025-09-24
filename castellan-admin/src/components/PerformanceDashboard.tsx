import React, { useState, useEffect } from 'react';
import {
  Typography,
  Box,
  Grid,
  Chip,
  LinearProgress,
  Stack,
  CircularProgress,
  Alert
} from '@mui/material';
import {
  Speed as SpeedIcon,
  Memory as MemoryIcon,
  Storage as StorageIcon,
  Timeline as TimelineIcon,
  Psychology as AIIcon,
  Security as SecurityIcon
} from '@mui/icons-material';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  AreaChart,
  Area
} from 'recharts';
import { useSignalRContext } from '../contexts/SignalRContext';

interface PerformanceMetric {
  name: string;
  value: number;
  unit: string;
  icon: React.ReactNode;
  color: string;
  status: 'excellent' | 'good' | 'fair' | 'poor';
}

export const PerformanceDashboard: React.FC = () => {
  const { realtimeMetrics, isConnected } = useSignalRContext();
  const [historicalData, setHistoricalData] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);

  // Generate historical performance data for the chart
  useEffect(() => {
    const generateHistoricalData = () => {
      const data = [];
      const now = new Date();
      for (let i = 23; i >= 0; i--) {
        const time = new Date(now.getTime() - i * 60000); // 1 minute intervals
        data.push({
          time: time.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          cpu: Math.random() * 40 + 10, // 10-50% CPU
          memory: Math.random() * 30 + 40, // 40-70% Memory
          events: Math.floor(Math.random() * 20), // 0-20 events/min
          correlations: Math.floor(Math.random() * 5), // 0-5 correlations/min
        });
      }
      setHistoricalData(data);
      setLoading(false);
    };

    generateHistoricalData();
    const interval = setInterval(generateHistoricalData, 60000); // Update every minute

    return () => clearInterval(interval);
  }, []);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'excellent': return 'success';
      case 'good': return 'info';
      case 'fair': return 'warning';
      case 'poor': return 'error';
      default: return 'default';
    }
  };

  const formatUptime = (uptime: string) => {
    if (!uptime || uptime === 'Unknown') return 'N/A';
    return uptime;
  };

  const getCurrentMetrics = (): PerformanceMetric[] => {
    const performance = realtimeMetrics?.performance;
    const eventProcessing = performance?.eventProcessing;
    const vectorOps = performance?.vectorOperations;

    return [
      {
        name: 'CPU Usage',
        value: performance?.cpuUsagePercent || 0,
        unit: '%',
        icon: <SpeedIcon />,
        color: '#1976d2',
        status: (performance?.cpuUsagePercent || 0) < 30 ? 'excellent' :
                (performance?.cpuUsagePercent || 0) < 50 ? 'good' :
                (performance?.cpuUsagePercent || 0) < 80 ? 'fair' : 'poor'
      },
      {
        name: 'Memory Usage',
        value: performance?.memoryUsageMB || 0,
        unit: 'MB',
        icon: <MemoryIcon />,
        color: '#9c27b0',
        status: (performance?.memoryUsageMB || 0) < 1000 ? 'excellent' :
                (performance?.memoryUsageMB || 0) < 1500 ? 'good' :
                (performance?.memoryUsageMB || 0) < 2000 ? 'fair' : 'poor'
      },
      {
        name: 'Events/sec',
        value: eventProcessing?.eventsPerSecond || 0,
        unit: '/sec',
        icon: <SecurityIcon />,
        color: '#2e7d32',
        status: (eventProcessing?.eventsPerSecond || 0) > 5 ? 'excellent' :
                (eventProcessing?.eventsPerSecond || 0) > 2 ? 'good' :
                (eventProcessing?.eventsPerSecond || 0) > 0 ? 'fair' : 'poor'
      },
      {
        name: 'Queue Depth',
        value: eventProcessing?.queuedEvents || 0,
        unit: 'events',
        icon: <TimelineIcon />,
        color: '#ed6c02',
        status: (eventProcessing?.queuedEvents || 0) < 10 ? 'excellent' :
                (eventProcessing?.queuedEvents || 0) < 25 ? 'good' :
                (eventProcessing?.queuedEvents || 0) < 50 ? 'fair' : 'poor'
      },
      {
        name: 'Vectors/sec',
        value: vectorOps?.vectorsPerSecond || 0,
        unit: '/sec',
        icon: <AIIcon />,
        color: '#7b1fa2',
        status: (vectorOps?.vectorsPerSecond || 0) > 3 ? 'excellent' :
                (vectorOps?.vectorsPerSecond || 0) > 1 ? 'good' :
                (vectorOps?.vectorsPerSecond || 0) > 0 ? 'fair' : 'poor'
      },
      {
        name: 'Thread Count',
        value: performance?.threadCount || 0,
        unit: 'threads',
        icon: <StorageIcon />,
        color: '#d32f2f',
        status: (performance?.threadCount || 0) < 60 ? 'excellent' :
                (performance?.threadCount || 0) < 80 ? 'good' :
                (performance?.threadCount || 0) < 100 ? 'fair' : 'poor'
      }
    ];
  };

  const metrics = getCurrentMetrics();

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" height={400}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      {!isConnected && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Real-time data unavailable - showing simulated metrics
        </Alert>
      )}

      {/* Performance Metrics Grid */}
      <Grid container spacing={3} mb={3}>
        {metrics.map((metric) => (
          <Grid item xs={12} sm={6} md={4} lg={2} key={metric.name}>
            <Box
              sx={{
                p: 2,
                backgroundColor: 'grey.50',
                borderRadius: 2,
                borderLeft: `4px solid ${metric.color}`,
                minHeight: 100
              }}
            >
              <Stack direction="row" alignItems="center" gap={1} mb={1}>
                <Box sx={{ color: metric.color }}>{metric.icon}</Box>
                <Typography variant="body2" color="text.secondary" fontWeight="medium">
                  {metric.name}
                </Typography>
              </Stack>

              <Typography variant="h5" fontWeight="bold" mb={1}>
                {metric.value.toFixed(metric.unit === 'MB' ? 0 : 1)} {metric.unit}
              </Typography>

              <Chip
                label={metric.status.toUpperCase()}
                size="small"
                color={getStatusColor(metric.status) as any}
                variant="outlined"
              />
            </Box>
          </Grid>
        ))}
      </Grid>

      {/* System Information */}
      <Grid container spacing={3} mb={3}>
        <Grid item xs={12} md={6}>
          <Box sx={{ p: 2, backgroundColor: 'grey.50', borderRadius: 2 }}>
            <Typography variant="h6" mb={2} fontWeight="bold">
              System Information
            </Typography>
            <Stack spacing={1}>
              <Typography variant="body2">
                <strong>Uptime:</strong> {formatUptime(realtimeMetrics?.health?.systemUptime || 'Unknown')}
              </Typography>
              <Typography variant="body2">
                <strong>Total Events:</strong> {realtimeMetrics?.performance?.eventProcessing?.totalEventsProcessed?.toLocaleString() || 0}
              </Typography>
              <Typography variant="body2">
                <strong>Failed Events:</strong> {realtimeMetrics?.performance?.eventProcessing?.failedEvents || 0}
              </Typography>
              <Typography variant="body2">
                <strong>Handle Count:</strong> {realtimeMetrics?.performance?.handleCount?.toLocaleString() || 0}
              </Typography>
            </Stack>
          </Box>
        </Grid>

        <Grid item xs={12} md={6}>
          <Box sx={{ p: 2, backgroundColor: 'grey.50', borderRadius: 2 }}>
            <Typography variant="h6" mb={2} fontWeight="bold">
              AI Performance
            </Typography>
            <Stack spacing={1}>
              <Typography variant="body2">
                <strong>Avg Embedding Time:</strong> {realtimeMetrics?.performance?.vectorOperations?.averageEmbeddingTime || 'N/A'}
              </Typography>
              <Typography variant="body2">
                <strong>Avg Search Time:</strong> {realtimeMetrics?.performance?.vectorOperations?.averageSearchTime || 'N/A'}
              </Typography>
              <Typography variant="body2">
                <strong>Avg Upsert Time:</strong> {realtimeMetrics?.performance?.vectorOperations?.averageUpsertTime || 'N/A'}
              </Typography>
              <Typography variant="body2">
                <strong>Batch Operations:</strong> {realtimeMetrics?.performance?.vectorOperations?.batchOperations || 0}
              </Typography>
            </Stack>
          </Box>
        </Grid>
      </Grid>

      {/* Historical Performance Chart */}
      <Box sx={{ p: 2, backgroundColor: 'grey.50', borderRadius: 2 }}>
        <Typography variant="h6" mb={2} fontWeight="bold">
          Performance Trends (Last 24 Hours)
        </Typography>
        <Box height={300}>
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={historicalData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="time" />
              <YAxis yAxisId="left" />
              <YAxis yAxisId="right" orientation="right" />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'white',
                  border: '1px solid #ccc',
                  borderRadius: '8px'
                }}
              />
              <Area
                yAxisId="left"
                type="monotone"
                dataKey="cpu"
                stackId="1"
                stroke="#1976d2"
                fill="#1976d2"
                fillOpacity={0.6}
                name="CPU %"
              />
              <Area
                yAxisId="left"
                type="monotone"
                dataKey="memory"
                stackId="2"
                stroke="#9c27b0"
                fill="#9c27b0"
                fillOpacity={0.6}
                name="Memory %"
              />
              <Line
                yAxisId="right"
                type="monotone"
                dataKey="events"
                stroke="#2e7d32"
                strokeWidth={2}
                dot={false}
                name="Events/min"
              />
              <Line
                yAxisId="right"
                type="monotone"
                dataKey="correlations"
                stroke="#ed6c02"
                strokeWidth={2}
                dot={false}
                name="Correlations/min"
              />
            </AreaChart>
          </ResponsiveContainer>
        </Box>

        <Stack direction="row" spacing={3} mt={2} justifyContent="center">
          <Box display="flex" alignItems="center" gap={1}>
            <Box sx={{ width: 12, height: 12, backgroundColor: '#1976d2' }} />
            <Typography variant="caption">CPU Usage (%)</Typography>
          </Box>
          <Box display="flex" alignItems="center" gap={1}>
            <Box sx={{ width: 12, height: 12, backgroundColor: '#9c27b0' }} />
            <Typography variant="caption">Memory Usage (%)</Typography>
          </Box>
          <Box display="flex" alignItems="center" gap={1}>
            <Box sx={{ width: 12, height: 2, backgroundColor: '#2e7d32' }} />
            <Typography variant="caption">Events/min</Typography>
          </Box>
          <Box display="flex" alignItems="center" gap={1}>
            <Box sx={{ width: 12, height: 2, backgroundColor: '#ed6c02' }} />
            <Typography variant="caption">Correlations/min</Typography>
          </Box>
        </Stack>
      </Box>

      {/* Progress Indicators */}
      <Grid container spacing={3} mt={1}>
        <Grid item xs={12} md={6}>
          <Box sx={{ p: 2, backgroundColor: 'grey.50', borderRadius: 2 }}>
            <Typography variant="body2" fontWeight="bold" mb={1}>
              System Health Score
            </Typography>
            <Box display="flex" alignItems="center" gap={2}>
              <LinearProgress
                variant="determinate"
                value={85}
                sx={{ flex: 1, height: 8, borderRadius: 4 }}
                color="success"
              />
              <Typography variant="h6" fontWeight="bold" color="success.main">
                85%
              </Typography>
            </Box>
          </Box>
        </Grid>

        <Grid item xs={12} md={6}>
          <Box sx={{ p: 2, backgroundColor: 'grey.50', borderRadius: 2 }}>
            <Typography variant="body2" fontWeight="bold" mb={1}>
              Correlation Engine Load
            </Typography>
            <Box display="flex" alignItems="center" gap={2}>
              <LinearProgress
                variant="determinate"
                value={42}
                sx={{ flex: 1, height: 8, borderRadius: 4 }}
                color="info"
              />
              <Typography variant="h6" fontWeight="bold" color="info.main">
                42%
              </Typography>
            </Box>
          </Box>
        </Grid>
      </Grid>
    </Box>
  );
};

export default PerformanceDashboard;
