import React, { useState, useEffect } from 'react';
import {
  Card,
  CardContent,
  CardHeader,
  Typography,
  Box,
  Chip,
  LinearProgress,
  Grid,
  IconButton,
  Tooltip as MuiTooltip,
  Alert,
  Divider,
  CircularProgress
} from '@mui/material';
import {
  Security as SecurityIcon,
  Memory as MemoryIcon,
  Storage as StorageIcon,
  Speed as SpeedIcon,
  CloudSync as SyncIcon,
  BugReport as ThreatIcon,
  Refresh as RefreshIcon,
  SignalWifi4Bar as ConnectedIcon,
  SignalWifiOff as DisconnectedIcon,
  Warning as WarningIcon,
  CheckCircle as HealthyIcon,
  Error as ErrorIcon,
  Psychology as AIIcon,
  Shield as ShieldIcon
} from '@mui/icons-material';
import {
  useRealtimeSystemMetrics,
  SystemMetricsUpdate,
  useRealtimeThreatIntelligence,
  ThreatIntelligenceStatusUpdate
} from '../hooks/useSignalR';

export const RealtimeSystemMetrics: React.FC = () => {
  const [systemMetrics, setSystemMetrics] = useState<SystemMetricsUpdate | null>(null);
  const [threatIntelligenceStatus, setThreatIntelligenceStatus] = useState<ThreatIntelligenceStatusUpdate | null>(null);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);

  const { connectionState, isConnected, triggerSystemUpdate } = useRealtimeSystemMetrics((update) => {
    setSystemMetrics(update);
    setLastUpdate(new Date());
  });

  useRealtimeThreatIntelligence((status) => {
    setThreatIntelligenceStatus(status);
  });

  const handleManualRefresh = async () => {
    await triggerSystemUpdate();
  };

  const formatUptime = (uptimeString: string) => {
    try {
      // Parse TimeSpan format (e.g., "1.02:30:45.123")
      const parts = uptimeString.split('.');
      if (parts.length >= 2) {
        const days = parseInt(parts[0]);
        const timePart = parts[1].split(':');
        const hours = parseInt(timePart[0]);
        const minutes = parseInt(timePart[1]);
        
        if (days > 0) {
          return `${days}d ${hours}h ${minutes}m`;
        } else if (hours > 0) {
          return `${hours}h ${minutes}m`;
        } else {
          return `${minutes}m`;
        }
      }
      return uptimeString;
    } catch {
      return uptimeString;
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 MB';
    const k = 1024;
    const sizes = ['MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  };

  return (
    <Box>
      {/* Header with Connection Status */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h6" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <AIIcon />
          Real-time System Metrics
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Chip
            icon={isConnected ? <ConnectedIcon /> : <DisconnectedIcon />}
            label={connectionState}
            size="small"
            color={isConnected ? 'success' : 'error'}
            variant="outlined"
          />
          {lastUpdate && (
            <Typography variant="caption" color="textSecondary">
              Updated: {lastUpdate.toLocaleTimeString()}
            </Typography>
          )}
          <MuiTooltip title="Trigger manual update">
            <IconButton onClick={handleManualRefresh} size="small">
              <RefreshIcon />
            </IconButton>
          </MuiTooltip>
        </Box>
      </Box>

      {!isConnected && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Real-time connection is not available. Data may not be current.
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* System Health Overview */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardHeader 
              title="System Health"
              avatar={<HealthyIcon color={systemMetrics?.health.isHealthy ? 'success' : 'error'} />}
            />
            <CardContent>
              {systemMetrics ? (
                <>
                  <Box sx={{ mb: 2 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                      <Typography variant="body2">Component Health</Typography>
                      <Typography variant="body2" fontWeight="bold">
                        {systemMetrics.health.healthyComponents}/{systemMetrics.health.totalComponents}
                      </Typography>
                    </Box>
                    <LinearProgress
                      variant="determinate"
                      value={(systemMetrics.health.healthyComponents / systemMetrics.health.totalComponents) * 100}
                      color={systemMetrics.health.isHealthy ? 'success' : 'error'}
                      sx={{ height: 8, borderRadius: 4 }}
                    />
                  </Box>
                  <Typography variant="body2" color="textSecondary">
                    System Uptime: {formatUptime(systemMetrics.health.systemUptime)}
                  </Typography>
                  
                  <Divider sx={{ my: 2 }} />
                  
                  <Typography variant="subtitle2" gutterBottom>Component Status</Typography>
                  <Box sx={{ maxHeight: 200, overflow: 'auto' }}>
                    {Object.entries(systemMetrics.health.components).map(([name, component]) => (
                      <Box key={name} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', py: 0.5 }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                          {component.isHealthy ? 
                            <HealthyIcon sx={{ fontSize: 16, color: 'success.main' }} /> :
                            <ErrorIcon sx={{ fontSize: 16, color: 'error.main' }} />
                          }
                          <Typography variant="body2">{name}</Typography>
                        </Box>
                        <Chip 
                          label={component.status}
                          size="small"
                          color={component.isHealthy ? 'success' : 'error'}
                          variant="outlined"
                        />
                      </Box>
                    ))}
                  </Box>
                </>
              ) : (
                <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
                  <CircularProgress />
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Performance Metrics */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardHeader 
              title="Performance Metrics"
              avatar={<SpeedIcon color="primary" />}
            />
            <CardContent>
              {systemMetrics ? (
                <>
                  <Box sx={{ mb: 2 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                      <Typography variant="body2">Memory Usage</Typography>
                      <Typography variant="body2" fontWeight="bold">
                        {formatBytes(systemMetrics.performance.memoryUsageMB * 1024 * 1024)}
                      </Typography>
                    </Box>
                    <LinearProgress
                      variant="determinate"
                      value={Math.min((systemMetrics.performance.memoryUsageMB / 1024) * 100, 100)}
                      color="info"
                      sx={{ height: 6, borderRadius: 3, mb: 2 }}
                    />
                  </Box>

                  <Grid container spacing={2}>
                    <Grid item xs={6}>
                      <Box sx={{ textAlign: 'center', p: 1, backgroundColor: 'rgba(33, 150, 243, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h6" color="primary">
                          {systemMetrics.performance.threadCount}
                        </Typography>
                        <Typography variant="caption">Threads</Typography>
                      </Box>
                    </Grid>
                    <Grid item xs={6}>
                      <Box sx={{ textAlign: 'center', p: 1, backgroundColor: 'rgba(76, 175, 80, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h6" color="success.main">
                          {systemMetrics.performance.handleCount}
                        </Typography>
                        <Typography variant="caption">Handles</Typography>
                      </Box>
                    </Grid>
                  </Grid>

                  <Divider sx={{ my: 2 }} />
                  
                  <Typography variant="subtitle2" gutterBottom>Event Processing</Typography>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                    <Typography variant="body2">Events/sec: {systemMetrics.performance.eventProcessing.eventsPerSecond}</Typography>
                    <Typography variant="body2">Queue: {systemMetrics.performance.eventProcessing.queuedEvents}</Typography>
                  </Box>
                  <Typography variant="body2" color="textSecondary">
                    Total Processed: {systemMetrics.performance.eventProcessing.totalEventsProcessed.toLocaleString()}
                  </Typography>
                </>
              ) : (
                <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
                  <CircularProgress />
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Threat Intelligence Status */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardHeader 
              title="Threat Intelligence"
              avatar={<ShieldIcon color="secondary" />}
            />
            <CardContent>
              {systemMetrics?.threatIntelligence ? (
                <>
                  <Box sx={{ mb: 2 }}>
                    <Chip
                      label={systemMetrics.threatIntelligence.isEnabled ? 'Enabled' : 'Disabled'}
                      color={systemMetrics.threatIntelligence.isEnabled ? 'success' : 'default'}
                      size="small"
                      sx={{ mb: 1 }}
                    />
                    <Typography variant="body2" color="textSecondary">
                      Cache Hit Rate: {(systemMetrics.threatIntelligence.cacheHitRate * 100).toFixed(1)}%
                    </Typography>
                  </Box>

                  <Typography variant="subtitle2" gutterBottom>Service Status</Typography>
                  {Object.entries(systemMetrics.threatIntelligence.services).map(([name, service]) => (
                    <Box key={name} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <ThreatIcon sx={{ fontSize: 16, color: service.isHealthy ? 'success.main' : 'error.main' }} />
                        <Typography variant="body2">{name}</Typography>
                      </Box>
                      <Box sx={{ display: 'flex', gap: 1 }}>
                        <Chip
                          label={service.isHealthy ? 'Healthy' : 'Unhealthy'}
                          size="small"
                          color={service.isHealthy ? 'success' : 'error'}
                          variant="outlined"
                        />
                        <Typography variant="caption" color="textSecondary">
                          {service.apiCallsToday}/{service.rateLimit}
                        </Typography>
                      </Box>
                    </Box>
                  ))}
                </>
              ) : (
                <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
                  <CircularProgress />
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Cache Statistics */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardHeader 
              title="Cache Performance"
              avatar={<StorageIcon color="info" />}
            />
            <CardContent>
              {systemMetrics?.cache ? (
                <>
                  <Grid container spacing={2} sx={{ mb: 2 }}>
                    <Grid item xs={4}>
                      <Box sx={{ textAlign: 'center', p: 1, backgroundColor: 'rgba(33, 150, 243, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h6" color="primary">
                          {systemMetrics.cache.general.activeCaches}
                        </Typography>
                        <Typography variant="caption">Active</Typography>
                      </Box>
                    </Grid>
                    <Grid item xs={4}>
                      <Box sx={{ textAlign: 'center', p: 1, backgroundColor: 'rgba(76, 175, 80, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h6" color="success.main">
                          {(systemMetrics.cache.embedding.hitRate * 100).toFixed(0)}%
                        </Typography>
                        <Typography variant="caption">Hit Rate</Typography>
                      </Box>
                    </Grid>
                    <Grid item xs={4}>
                      <Box sx={{ textAlign: 'center', p: 1, backgroundColor: 'rgba(255, 152, 0, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h6" color="warning.main">
                          {formatBytes(systemMetrics.cache.general.totalMemoryUsageMB * 1024 * 1024)}
                        </Typography>
                        <Typography variant="caption">Memory</Typography>
                      </Box>
                    </Grid>
                  </Grid>

                  <Divider sx={{ my: 2 }} />
                  
                  <Typography variant="subtitle2" gutterBottom>Cache Details</Typography>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                    <Typography variant="body2">Embedding Cache:</Typography>
                    <Typography variant="body2">{systemMetrics.cache.embedding.totalEntries} entries</Typography>
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                    <Typography variant="body2">TI Cache:</Typography>
                    <Typography variant="body2">{systemMetrics.cache.threatIntelligence.totalHashes} hashes</Typography>
                  </Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                    <Typography variant="body2">Memory Pressure:</Typography>
                    <Typography variant="body2" color={systemMetrics.cache.general.memoryPressure > 0.8 ? 'error' : 'textSecondary'}>
                      {(systemMetrics.cache.general.memoryPressure * 100).toFixed(1)}%
                    </Typography>
                  </Box>
                </>
              ) : (
                <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
                  <CircularProgress />
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Active Scans */}
        <Grid item xs={12}>
          <Card>
            <CardHeader 
              title="Active Scans"
              avatar={<SecurityIcon color="secondary" />}
            />
            <CardContent>
              {systemMetrics?.activeScans ? (
                <>
                  {systemMetrics.activeScans.hasActiveScan && systemMetrics.activeScans.currentScan ? (
                    <Box sx={{ mb: 2 }}>
                      <Typography variant="subtitle2" gutterBottom>Current Scan</Typography>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                        <Typography variant="body2">
                          Scan ID: {systemMetrics.activeScans.currentScan.scanId}
                        </Typography>
                        <Chip label={systemMetrics.activeScans.currentScan.status} size="small" color="info" />
                      </Box>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                        <Typography variant="body2">Progress</Typography>
                        <Typography variant="body2" fontWeight="bold">
                          {systemMetrics.activeScans.currentScan.percentComplete.toFixed(1)}%
                        </Typography>
                      </Box>
                      <LinearProgress
                        variant="determinate"
                        value={systemMetrics.activeScans.currentScan.percentComplete}
                        color="primary"
                        sx={{ height: 8, borderRadius: 4, mb: 1 }}
                      />
                      <Typography variant="caption" color="textSecondary">
                        Files: {systemMetrics.activeScans.currentScan.filesScanned}/{systemMetrics.activeScans.currentScan.totalEstimatedFiles} • 
                        Phase: {systemMetrics.activeScans.currentScan.scanPhase}
                      </Typography>
                    </Box>
                  ) : (
                    <Alert severity="info" sx={{ mb: 2 }}>
                      No active scans running
                    </Alert>
                  )}

                  <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
                    <Chip
                      label={`${systemMetrics.activeScans.queuedScans} Queued`}
                      size="small"
                      variant="outlined"
                    />
                    <Chip
                      label={`${systemMetrics.activeScans.recentScans.length} Recent`}
                      size="small"
                      variant="outlined"
                    />
                  </Box>

                  {systemMetrics.activeScans.recentScans.length > 0 && (
                    <>
                      <Typography variant="subtitle2" gutterBottom>Recent Scans</Typography>
                      <Box sx={{ maxHeight: 150, overflow: 'auto' }}>
                        {systemMetrics.activeScans.recentScans.slice(0, 5).map((scan) => (
                          <Box key={scan.id} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', py: 0.5 }}>
                            <Box>
                              <Typography variant="body2">{scan.type}</Typography>
                              <Typography variant="caption" color="textSecondary">
                                {scan.filesScanned} files • {scan.threatsFound} threats
                              </Typography>
                            </Box>
                            <Box sx={{ textAlign: 'right' }}>
                              <Chip 
                                label={scan.status}
                                size="small"
                                color={scan.status === 'completed' ? 'success' : 'info'}
                                variant="outlined"
                              />
                              <Typography variant="caption" display="block" color="textSecondary">
                                {new Date(scan.startTime).toLocaleTimeString()}
                              </Typography>
                            </Box>
                          </Box>
                        ))}
                      </Box>
                    </>
                  )}
                </>
              ) : (
                <Box sx={{ display: 'flex', justifyContent: 'center', p: 2 }}>
                  <CircularProgress />
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};
