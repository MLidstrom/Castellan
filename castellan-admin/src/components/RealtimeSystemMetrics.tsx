import React from 'react';
import {
  Card,
  CardContent,
  CardHeader,
  Typography,
  Box,
  Grid,
  LinearProgress,
  Chip,
  List,
  ListItem,
  ListItemText,
  ListItemIcon
} from '@mui/material';
import {
  Memory as MemoryIcon,
  Speed as CpuIcon,
  Timeline as UptimeIcon,
  CheckCircle as HealthyIcon,
  Error as ErrorIcon
} from '@mui/icons-material';
import { SystemMetricsUpdate } from '../hooks/useSignalR';
import { useSignalRContext } from '../contexts/SignalRContext';

interface RealtimeSystemMetricsProps {
  // Optional props for backward compatibility, will use context if not provided
  metrics?: SystemMetricsUpdate | null;
  connectionStatus?: 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';
}

export const RealtimeSystemMetrics: React.FC<RealtimeSystemMetricsProps> = ({
  metrics: propMetrics,
  connectionStatus: propConnectionStatus
}) => {
  // Use SignalR context for persistent connection data
  const { realtimeMetrics: contextMetrics, connectionState: contextConnectionStatus } = useSignalRContext();

  // Use props if provided, otherwise fall back to context
  const metrics = propMetrics !== undefined ? propMetrics : contextMetrics;
  const connectionStatus = propConnectionStatus !== undefined ? propConnectionStatus : contextConnectionStatus;
  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 MB';
    const mb = bytes;
    return `${mb.toFixed(1)} MB`;
  };
  
  const formatUptime = (uptime: string) => {
    if (!uptime) return 'Unknown';
    return uptime;
  };
  
  
  
  if (!metrics) {
    return (
      <Card sx={{ width: '100%' }}>
        <CardHeader 
          title="System Metrics" 
          subheader={`Status: ${connectionStatus}`}
        />
        <CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: 200 }}>
            <Typography color="text.secondary">
              {connectionStatus === 'Connected' ? 'Waiting for metrics data...' : 'Real-time connection required'}
            </Typography>
          </Box>
        </CardContent>
      </Card>
    );
  }
  
  return (
    <Card sx={{ width: '100%' }}>
      <CardHeader 
        title="Live System Metrics" 
        subheader={`Last updated: ${new Date(metrics.timestamp).toLocaleTimeString()}`}
        action={
          <Chip 
            label={connectionStatus}
            color={connectionStatus === 'Connected' ? 'success' : 'default'}
            size="small"
          />
        }
      />
      <CardContent>
        <Grid container spacing={3}>
          {/* Performance Metrics */}
          <Grid item xs={12} md={6}>
            <Typography variant="h6" gutterBottom>Performance</Typography>
            
            {/* CPU Usage */}
            <Box sx={{ mb: 2 }}>
              <Box display="flex" alignItems="center" mb={1}>
                <CpuIcon fontSize="small" sx={{ mr: 1 }} />
                <Typography variant="body2">CPU Usage</Typography>
                <Typography variant="body2" sx={{ ml: 'auto' }}>
                  {metrics.performance.cpuUsagePercent.toFixed(1)}%
                </Typography>
              </Box>
              <LinearProgress 
                variant="determinate" 
                value={metrics.performance.cpuUsagePercent} 
                color={metrics.performance.cpuUsagePercent > 80 ? 'error' : 'primary'}
              />
            </Box>
            
            {/* Memory Usage */}
            <Box sx={{ mb: 2 }}>
              <Box display="flex" alignItems="center" mb={1}>
                <MemoryIcon fontSize="small" sx={{ mr: 1 }} />
                <Typography variant="body2">Memory Usage</Typography>
                <Typography variant="body2" sx={{ ml: 'auto' }}>
                  {formatBytes(metrics.performance.memoryUsageMB)}
                </Typography>
              </Box>
            </Box>
            
            {/* System Uptime */}
            <Box sx={{ mb: 2 }}>
              <Box display="flex" alignItems="center" mb={1}>
                <UptimeIcon fontSize="small" sx={{ mr: 1 }} />
                <Typography variant="body2">System Uptime</Typography>
                <Typography variant="body2" sx={{ ml: 'auto' }}>
                  {formatUptime(metrics.health.systemUptime)}
                </Typography>
              </Box>
            </Box>
          </Grid>
          
          {/* Component Health */}
          <Grid item xs={12} md={6}>
            <Typography variant="h6" gutterBottom>
              Component Health ({metrics.health.healthyComponents}/{metrics.health.totalComponents})
            </Typography>
            <List dense>
              {Object.entries(metrics.health.components).map(([name, component]) => (
                <ListItem key={name} sx={{ py: 0.5 }}>
                  <ListItemIcon sx={{ minWidth: 30 }}>
                    {component.isHealthy ? (
                      <HealthyIcon fontSize="small" color="success" />
                    ) : (
                      <ErrorIcon fontSize="small" color="error" />
                    )}
                  </ListItemIcon>
                  <ListItemText
                    primary={
                      <Box display="flex" justifyContent="space-between" alignItems="center">
                        <Typography variant="body2">{name}</Typography>
                        <Typography variant="caption" color="text.secondary">
                          {component.responseTimeMs}ms
                        </Typography>
                      </Box>
                    }
                    secondary={
                      <Typography 
                        variant="caption" 
                        color={component.isHealthy ? 'success.main' : 'error.main'}
                      >
                        {component.status}
                      </Typography>
                    }
                  />
                </ListItem>
              ))}
            </List>
          </Grid>
          
          {/* Event Processing Stats */}
          {metrics.performance.eventProcessing && (
            <Grid item xs={12}>
              <Typography variant="h6" gutterBottom>Event Processing</Typography>
              <Box display="flex" gap={3} flexWrap="wrap">
                <Chip 
                  label={`${metrics.performance.eventProcessing.eventsPerSecond}/s`}
                  variant="outlined"
                  size="small"
                />
                <Chip 
                  label={`${metrics.performance.eventProcessing.totalEventsProcessed} Total`}
                  variant="outlined"
                  size="small"
                />
                <Chip 
                  label={`${metrics.performance.eventProcessing.queuedEvents} Queued`}
                  variant="outlined"
                  size="small"
                  color={metrics.performance.eventProcessing.queuedEvents > 100 ? 'warning' : 'default'}
                />
                {metrics.performance.eventProcessing.failedEvents > 0 && (
                  <Chip 
                    label={`${metrics.performance.eventProcessing.failedEvents} Failed`}
                    variant="outlined"
                    size="small"
                    color="error"
                  />
                )}
              </Box>
            </Grid>
          )}
        </Grid>
      </CardContent>
    </Card>
  );
};

export default RealtimeSystemMetrics;
