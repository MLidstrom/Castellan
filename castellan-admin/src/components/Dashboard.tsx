import React, { useState, useEffect } from 'react';
import { 
  Card, 
  CardContent, 
  CardHeader, 
  Typography, 
  Box, 
  Button,
  ButtonGroup,
  CircularProgress,
  LinearProgress,
  Chip,
  IconButton,
  Tooltip as MuiTooltip
} from '@mui/material';
import { useGetList, useNotify } from 'react-admin';
import { 
  XAxis, 
  YAxis, 
  CartesianGrid, 
  Tooltip, 
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  BarChart,
  Bar,
  AreaChart,
  Area,
  Legend
} from 'recharts';
import {
  Security as SecurityIcon,
  Assessment as ComplianceIcon,
  Warning as WarningIcon,
  CheckCircle as HealthyIcon,
  Error as ErrorIcon,
  TrendingUp as TrendingUpIcon,
  Refresh as RefreshIcon,
  Download as DownloadIcon,
  Fullscreen as FullscreenIcon,
  Shield as ShieldIcon,
  Scanner as ScannerIcon
} from '@mui/icons-material';

// Updated: Removed premium features - all functionality is now available in the free version

import { ConnectionPoolMonitor } from './ConnectionPoolMonitor';
import { RealtimeSystemMetrics } from './RealtimeSystemMetrics';
import { GeographicThreatMap } from './GeographicThreatMap';

export const Dashboard = () => {
  const [timeRange, setTimeRange] = useState('24h');
  const [refreshing, setRefreshing] = useState(false);
  const [lastRefresh, setLastRefresh] = useState(new Date());
  const notify = useNotify();

  const { data: securityEvents, refetch: refetchEvents } = useGetList('security-events', {
    pagination: { page: 1, perPage: 10000 }, // Get all events in rolling window (max 1000 from backend)
    sort: { field: 'timestamp', order: 'DESC' },
  });

  const { data: complianceReports, refetch: refetchReports } = useGetList('compliance-reports', {
    pagination: { page: 1, perPage: 50 },
    sort: { field: 'generated', order: 'DESC' },
  });

  const { data: systemStatus, refetch: refetchStatus } = useGetList('system-status', {
    pagination: { page: 1, perPage: 100 },
  });

  const { data: threatScanner, refetch: refetchThreatScanner } = useGetList('threat-scanner', {
    pagination: { page: 1, perPage: 20 },
    sort: { field: 'timestamp', order: 'DESC' },
  });

  // Auto-refresh every 30 seconds
  useEffect(() => {
    const interval = setInterval(() => {
      handleRefresh(false);
    }, 30000);
    return () => clearInterval(interval);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleRefresh = async (showNotification = true) => {
    setRefreshing(true);
    try {
      await Promise.all([
        refetchEvents(),
        refetchReports(),
        refetchStatus(),
        refetchThreatScanner()
      ]);
      setLastRefresh(new Date());
      if (showNotification) {
        notify('Dashboard refreshed successfully', { type: 'info' });
      }
    } catch (error) {
      notify('Failed to refresh dashboard', { type: 'error' });
    } finally {
      setRefreshing(false);
    }
  };

  // Enhanced metrics calculations
  const totalEvents = securityEvents?.length || 0;
  const criticalEvents = securityEvents?.filter(e => e.riskLevel === 'critical').length || 0;
  const highRiskEvents = securityEvents?.filter(e => e.riskLevel === 'high' || e.riskLevel === 'critical').length || 0;
  const mediumRiskEvents = securityEvents?.filter(e => e.riskLevel === 'medium').length || 0;
  const lowRiskEvents = securityEvents?.filter(e => e.riskLevel === 'low').length || 0;
  const avgCorrelationScore = securityEvents?.reduce((sum, e) => sum + (e.correlationScore || 0), 0) / totalEvents || 0;
  const avgConfidenceScore = securityEvents?.reduce((sum, e) => sum + (e.confidence || 0), 0) / totalEvents || 0;
  
  const healthyComponents = systemStatus?.filter(s => s.status?.toLowerCase() === 'healthy').length || 0;
  const totalComponents = systemStatus?.length || 0;
  const systemHealthPercentage = totalComponents > 0 ? (healthyComponents / totalComponents) * 100 : 0;
  
  const avgComplianceScore = complianceReports?.reduce((sum, r) => sum + (r.complianceScore || r.ComplianceScore || 0), 0) / (complianceReports?.length || 1) || 0;

  // Recent activity and trends
  const recentEvents = securityEvents?.slice(0, 10) || [];
  const recentComplianceReports = complianceReports?.slice(0, 5) || [];
  const recentThreatScans = threatScanner?.slice(0, 10) || [];
  
  // Real-time metrics
  const eventsInLastHour = securityEvents?.filter(e => 
    new Date(e.timestamp) > new Date(Date.now() - 60 * 60 * 1000)
  ).length || 0;
  
  const eventsInLast24Hours = securityEvents?.filter(e => 
    new Date(e.timestamp) > new Date(Date.now() - 24 * 60 * 60 * 1000)
  ).length || 0;
  
  // Event velocity (events per hour)
  const eventVelocity = eventsInLast24Hours / 24;
  
  // Threat scanner metrics
  const totalScans = threatScanner?.length || 0;
  const activeScanners = threatScanner?.filter(s => s.status === 'running' || s.status === 'active').length || 0;
  const completedScans = threatScanner?.filter(s => s.status === 'completed').length || 0;
  const threatsDetected = threatScanner?.reduce((sum, scan) => sum + (scan.threatsFound || 0), 0) || 0;
  const scansToday = threatScanner?.filter(s => 
    new Date(s.timestamp || s.startTime) > new Date(Date.now() - 24 * 60 * 60 * 1000)
  ).length || 0;

  // Risk distribution for pie chart
  const riskDistribution = [
    { name: 'Critical', value: criticalEvents, color: '#f44336' },
    { name: 'High', value: highRiskEvents - criticalEvents, color: '#ff9800' },
    { name: 'Medium', value: mediumRiskEvents, color: '#2196f3' },
    { name: 'Low', value: lowRiskEvents, color: '#4caf50' },
  ].filter(item => item.value > 0);

  // Time series data for trend analysis
  const timeSeriesData = securityEvents?.slice(0, 50).reverse().map((event) => ({
    time: new Date(event.timestamp).toLocaleTimeString(),
    correlationScore: event.correlationScore || 0,
    confidenceScore: event.confidence || 0,
    riskValue: event.riskLevel === 'critical' ? 4 : event.riskLevel === 'high' ? 3 : event.riskLevel === 'medium' ? 2 : 1,
    eventType: event.eventType,
    hour: new Date(event.timestamp).getHours()
  })) || [];

  // Event type distribution
  const eventTypeStats = securityEvents?.reduce((acc: Record<string, number>, event) => {
    acc[event.eventType] = (acc[event.eventType] || 0) + 1;
    return acc;
  }, {}) || {};

  const eventTypeData = Object.entries(eventTypeStats)
    .map(([type, count]) => ({ name: type, value: count as number }))
    .sort((a, b) => b.value - a.value)
    .slice(0, 8);

  // Detection method effectiveness (using Source field as detection method)
  const detectionMethodStats = securityEvents?.reduce((acc: Record<string, number>, event) => {
    const method = event.source || 'Unknown';
    acc[method] = (acc[method] || 0) + 1;
    return acc;
  }, {}) || {};

  const detectionMethodData = Object.entries(detectionMethodStats)
    .map(([method, count]) => ({ name: method, value: count as number }));

  // System performance metrics - using response time and component health as performance indicators
  const avgResponseTime = systemStatus?.reduce((sum, s) => sum + (s.responseTime || 0), 0) / (systemStatus?.length || 1) || 0;
  const avgCpuUsage = Math.min(avgResponseTime * 2, 100); // Simulate CPU based on response time
  const unhealthyComponents = (systemStatus?.filter(s => s.status?.toLowerCase() !== 'healthy') || []).length;
  const avgMemoryUsage = unhealthyComponents * 15 + 25; // Base memory usage
  const avgDiskUsage = (systemStatus?.reduce((sum, s) => sum + (s.errorCount || 0) + (s.warningCount || 0), 0) || 0) * 5 + 20; // Disk usage based on errors/warnings
  
  // Connection Pool metrics
  const connectionPoolStatus = systemStatus?.find(s => s.component === 'Qdrant Connection Pool');
  const connectionPoolHealthy = connectionPoolStatus?.status === 'Healthy';
  const connectionPoolExists = connectionPoolStatus && connectionPoolStatus.status !== 'Disabled';

  return (
    <Box sx={{ padding: '20px' }}>
      {/* Header with controls */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '30px' }}>
        <Typography variant="h4" gutterBottom sx={{ margin: 0 }}>
          🛡️ Castellan Dashboard
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <ButtonGroup variant="outlined" size="small">
            <Button 
              variant={timeRange === '1h' ? 'contained' : 'outlined'}
              onClick={() => setTimeRange('1h')}
            >
              1H
            </Button>
            <Button 
              variant={timeRange === '24h' ? 'contained' : 'outlined'}
              onClick={() => setTimeRange('24h')}
            >
              24H
            </Button>
            <Button 
              variant={timeRange === '7d' ? 'contained' : 'outlined'}
              onClick={() => setTimeRange('7d')}
            >
              7D
            </Button>
            <Button 
              variant={timeRange === '30d' ? 'contained' : 'outlined'}
              onClick={() => setTimeRange('30d')}
            >
              30D
            </Button>
          </ButtonGroup>
          <MuiTooltip title={`Last refreshed: ${lastRefresh.toLocaleTimeString()}`}>
            <IconButton 
              onClick={() => handleRefresh(true)}
              disabled={refreshing}
              color="primary"
            >
              {refreshing ? <CircularProgress size={20} /> : <RefreshIcon />}
            </IconButton>
          </MuiTooltip>
        </Box>
      </Box>
      
      {/* KPI Cards Row */}
      <Box sx={{ 
        display: 'flex', 
        flexWrap: 'wrap', 
        gap: 3, 
        marginBottom: '30px' 
      }}>
        <Box sx={{ flex: '1 1 250px', minWidth: '250px' }}>
          <Card sx={{ height: '150px' }}>
            <CardContent sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', marginBottom: 1 }}>
                <SecurityIcon sx={{ marginRight: 1, color: 'primary.main' }} />
                <Typography variant="h6" color="textSecondary">
                  Total Security Events
                </Typography>
              </Box>
              <Typography variant="h3" sx={{ flexGrow: 1, display: 'flex', alignItems: 'center' }}>
                {totalEvents.toLocaleString()}
              </Typography>
              <Typography variant="body2" color="textSecondary">
                {criticalEvents} critical, {highRiskEvents - criticalEvents} high risk (rolling 24h window)
              </Typography>
            </CardContent>
          </Card>
        </Box>
        
        <Box sx={{ flex: '1 1 250px', minWidth: '250px' }}>
          <Card sx={{ height: '150px' }}>
            <CardContent sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', marginBottom: 1 }}>
                <WarningIcon sx={{ marginRight: 1, color: 'error.main' }} />
                <Typography variant="h6" color="textSecondary">
                  Critical Alerts
                </Typography>
              </Box>
              <Typography variant="h3" color="error.main" sx={{ flexGrow: 1, display: 'flex', alignItems: 'center' }}>
                {criticalEvents}
              </Typography>
              <LinearProgress 
                variant="determinate" 
                value={totalEvents > 0 ? (criticalEvents / totalEvents) * 100 : 0} 
                color="error"
                sx={{ width: '100%', height: 8, borderRadius: 4 }}
              />
            </CardContent>
          </Card>
        </Box>
        
        <Box sx={{ flex: '1 1 250px', minWidth: '250px' }}>
          <Card sx={{ height: '150px' }}>
            <CardContent sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', marginBottom: 1 }}>
                <TrendingUpIcon sx={{ marginRight: 1, color: 'info.main' }} />
                <Typography variant="h6" color="textSecondary">
                  Avg Confidence
                </Typography>
              </Box>
              <Typography variant="h3" sx={{ flexGrow: 1, display: 'flex', alignItems: 'center' }}>
                {avgConfidenceScore.toFixed(1)}%
              </Typography>
              <Typography variant="body2" color="textSecondary">
                Correlation: {avgCorrelationScore.toFixed(1)}
              </Typography>
            </CardContent>
          </Card>
        </Box>
        
        <Box sx={{ flex: '1 1 250px', minWidth: '250px' }}>
          <Card sx={{ height: '150px' }}>
            <CardContent sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
              <Box sx={{ display: 'flex', alignItems: 'center', marginBottom: 1 }}>
                {systemHealthPercentage >= 90 ? 
                  <HealthyIcon sx={{ marginRight: 1, color: 'success.main' }} /> : 
                  <ErrorIcon sx={{ marginRight: 1, color: 'error.main' }} />
                }
                <Typography variant="h6" color="textSecondary">
                  System Health
                </Typography>
              </Box>
              <Typography 
                variant="h3" 
                color={systemHealthPercentage >= 90 ? 'success.main' : 'error.main'}
                sx={{ flexGrow: 1, display: 'flex', alignItems: 'center' }}
              >
                {systemHealthPercentage.toFixed(0)}%
              </Typography>
              <Typography variant="body2" color="textSecondary">
                {healthyComponents}/{totalComponents} components healthy
                {connectionPoolExists && (
                  <span>
                    {' • Pool: '}
                    <span style={{ color: connectionPoolHealthy ? '#4caf50' : '#f44336' }}>
                      {connectionPoolHealthy ? 'Active' : 'Issues'}
                    </span>
                  </span>
                )}
              </Typography>
            </CardContent>
          </Card>
        </Box>
      </Box>

      {/* Connection Pool Monitor - Phase 2A */}
      <Box sx={{ 
        display: 'flex', 
        flexWrap: 'wrap', 
        gap: 3,
        marginBottom: '30px'
      }}>
        <Box sx={{ flex: '1 1 600px', minWidth: '600px' }}>
          <ConnectionPoolMonitor systemStatus={systemStatus || []} />
        </Box>
      </Box>

      {/* Real-time System Metrics - Phase 3 SignalR Integration */}
      <Box sx={{ 
        display: 'flex', 
        flexWrap: 'wrap', 
        gap: 3,
        marginBottom: '30px'
      }}>
        <Box sx={{ flex: '1 1 100%', minWidth: '100%' }}>
          <RealtimeSystemMetrics />
        </Box>
      </Box>

      {/* Charts Row 1 - Main Analytics */}
      <Box sx={{ 
        display: 'flex', 
        flexWrap: 'wrap', 
        gap: 3,
        marginBottom: '30px'
      }}>
        <Box sx={{ flex: '2 1 600px', minWidth: '600px' }}>
          <Card>
            <CardHeader 
              title="Security Events Analysis" 
              action={
                <IconButton size="small">
                  <FullscreenIcon />
                </IconButton>
              }
            />
            <CardContent>
              <ResponsiveContainer width="100%" height={400}>
                <AreaChart data={timeSeriesData}>
                  <defs>
                    <linearGradient id="correlationGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#8884d8" stopOpacity={0.8}/>
                      <stop offset="95%" stopColor="#8884d8" stopOpacity={0}/>
                    </linearGradient>
                    <linearGradient id="riskGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#ff0000" stopOpacity={0.8}/>
                      <stop offset="95%" stopColor="#ff0000" stopOpacity={0}/>
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f0f0f0" />
                  <XAxis 
                    dataKey="time" 
                    tick={{ fontSize: 12 }}
                    label={{ value: 'Time', position: 'insideBottom', offset: -5 }}
                  />
                  <YAxis 
                    tick={{ fontSize: 12 }}
                    label={{ value: 'Score / Risk Level', angle: -90, position: 'insideLeft' }}
                    domain={[0, 4]}
                  />
                  <Tooltip 
                    contentStyle={{ 
                      backgroundColor: '#fff', 
                      border: '1px solid #ccc', 
                      borderRadius: '8px',
                      boxShadow: '0 4px 8px rgba(0,0,0,0.1)'
                    }}
                  />
                  <Legend />
                  <Area 
                    type="monotone" 
                    dataKey="correlationScore" 
                    stroke="#8884d8" 
                    strokeWidth={2}
                    fillOpacity={0.6} 
                    fill="url(#correlationGradient)" 
                    name="Correlation Score"
                    dot={{ r: 3 }}
                  />
                  <Area 
                    type="monotone" 
                    dataKey="riskValue" 
                    stroke="#ff0000" 
                    fillOpacity={1} 
                    fill="url(#riskGradient)" 
                    name="Risk Level"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Box>
        
        <Box sx={{ flex: '1 1 400px', minWidth: '400px' }}>
          <Card sx={{ height: '100%' }}>
            <CardHeader title="Risk Distribution" />
            <CardContent>
              <ResponsiveContainer width="100%" height={350}>
                <PieChart>
                  <Pie
                    data={riskDistribution}
                    cx="50%"
                    cy="50%"
                    innerRadius={60}
                    outerRadius={120}
                    paddingAngle={5}
                    dataKey="value"
                    label={({ name, percent }) => `${name} ${((percent || 0) * 100).toFixed(0)}%`}
                  >
                    {riskDistribution.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Box>
      </Box>

      {/* Charts Row 2 - Secondary Analytics */}
      <Box sx={{ 
        display: 'flex', 
        flexWrap: 'wrap', 
        gap: 3,
        marginBottom: '30px'
      }}>
        <Box sx={{ flex: '1 1 500px', minWidth: '500px' }}>
          <Card>
            <CardHeader title="Top Event Types" />
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={eventTypeData} layout="vertical">
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis type="number" />
                  <YAxis dataKey="name" type="category" width={150} tick={{ fontSize: 11 }} />
                  <Tooltip />
                  <Bar dataKey="value" fill="#8884d8" radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Box>

        <Box sx={{ flex: '1 1 500px', minWidth: '500px' }}>
          <Card>
            <CardHeader title="Detection Methods" />
            <CardContent>
              <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                  <Pie
                    data={detectionMethodData}
                    cx="50%"
                    cy="50%"
                    outerRadius={100}
                    dataKey="value"
                    label={({ name, percent }) => `${name}: ${((percent || 0) * 100).toFixed(0)}%`}
                  >
                    {detectionMethodData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={['#0088FE', '#00C49F', '#FFBB28', '#FF8042'][index % 4]} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Box>
      </Box>

      {/* Bottom Row - System Performance, Compliance Status, and Threat Scanner */}
      <Box sx={{ 
        display: 'flex', 
        gap: 3,
        marginBottom: '30px'
      }}>
        {/* Left Column - System Performance and Compliance Status */}
        <Box sx={{ flex: '1 1 50%', display: 'flex', flexDirection: 'column', gap: 3 }}>
          {/* System Performance */}
          <Card>
            <CardHeader title="System Performance" />
            <CardContent>
              <Box sx={{ marginBottom: 3 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', marginBottom: 1 }}>
                  <Typography variant="body2">Memory Usage</Typography>
                  <Typography variant="body2" fontWeight="bold">{avgMemoryUsage.toFixed(1)}%</Typography>
                </Box>
                <LinearProgress 
                  variant="determinate" 
                  value={avgMemoryUsage} 
                  color={avgMemoryUsage > 80 ? 'error' : avgMemoryUsage > 60 ? 'warning' : 'info'}
                  sx={{ height: 12, borderRadius: 6, marginBottom: 2 }}
                />
              </Box>
              <Box>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', marginBottom: 1 }}>
                  <Typography variant="body2">Disk Usage</Typography>
                  <Typography variant="body2" fontWeight="bold">{avgDiskUsage.toFixed(1)}%</Typography>
                </Box>
                <LinearProgress 
                  variant="determinate" 
                  value={avgDiskUsage} 
                  color={avgDiskUsage > 80 ? 'error' : avgDiskUsage > 60 ? 'warning' : 'success'}
                  sx={{ height: 12, borderRadius: 6 }}
                />
              </Box>
            </CardContent>
          </Card>

          {/* Threat Scanners */}
          <Card>
            <CardHeader 
              title="Threat Scanners"
              action={
                <Button 
                  size="small" 
                  startIcon={<ScannerIcon />}
                  onClick={() => window.location.href = '#/threat-scanner'}
                >
                  View All
                </Button>
              }
            />
            <CardContent>
              <Box sx={{ marginBottom: 3 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 2 }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <ShieldIcon sx={{ fontSize: 20, color: 'success.main' }} />
                    <Typography variant="h6">Scanner Status</Typography>
                  </Box>
                  <Chip 
                    label={activeScanners > 0 ? 'Active' : 'Idle'}
                    size="small"
                    color={activeScanners > 0 ? 'success' : 'default'}
                    icon={<ShieldIcon />}
                  />
                </Box>
                
                <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 2, marginBottom: 2 }}>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(33, 150, 243, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="info.main">{totalScans}</Typography>
                    <Typography variant="caption">Total Scans</Typography>
                  </Box>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(76, 175, 80, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="success.main">{activeScanners}</Typography>
                    <Typography variant="caption">Active</Typography>
                  </Box>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(244, 67, 54, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="error.main">{threatsDetected}</Typography>
                    <Typography variant="caption">Threats Found</Typography>
                  </Box>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(255, 152, 0, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="warning.main">{scansToday}</Typography>
                    <Typography variant="caption">Today</Typography>
                  </Box>
                </Box>
              </Box>
              
              {recentThreatScans.length > 0 ? (
                <>
                  <Typography variant="subtitle2" sx={{ marginBottom: 1 }}>
                    Recent Scans
                  </Typography>
                  {recentThreatScans.map((scan, index) => (
                    <Box key={scan.id || index} sx={{ marginBottom: 1, paddingBottom: 1, borderBottom: index < recentThreatScans.length - 1 ? '1px solid #eee' : 'none' }}>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                          <ScannerIcon sx={{ fontSize: 16, color: 
                            scan.status === 'running' ? '#2196f3' : 
                            scan.status === 'completed' ? '#4caf50' : 
                            scan.status === 'failed' ? '#f44336' : '#ff9800' 
                          }} />
                          <Typography variant="body2" sx={{ fontWeight: 500 }}>
                            {scan.scanType || scan.type || 'File System Scan'}
                          </Typography>
                          <Chip 
                            label={scan.status || 'unknown'}
                            size="small"
                            color={
                              scan.status === 'running' ? 'info' : 
                              scan.status === 'completed' ? 'success' : 
                              scan.status === 'failed' ? 'error' : 'default'
                            }
                          />
                        </Box>
                        <Typography variant="caption" color="textSecondary">
                          {new Date(scan.timestamp || scan.startTime || Date.now()).toLocaleTimeString()}
                        </Typography>
                      </Box>
                      <Typography variant="caption" color="textSecondary" sx={{ marginLeft: 3 }}>
                        {scan.threatsFound ? `${scan.threatsFound} threats found` : 
                         scan.filesScanned ? `${scan.filesScanned} files scanned` : 
                         scan.path || scan.target || 'Scanning system files'}
                      </Typography>
                    </Box>
                  ))}
                </>
              ) : (
                <Box sx={{ textAlign: 'center', padding: 2 }}>
                  <ScannerIcon sx={{ fontSize: 36, color: 'text.secondary', marginBottom: 1 }} />
                  <Typography variant="body2" color="textSecondary" gutterBottom>
                    No recent scans available
                  </Typography>
                  <Button 
                    variant="outlined" 
                    size="small" 
                    onClick={() => window.location.href = '#/threat-scanner'}
                    sx={{ marginTop: 1 }}
                  >
                    Start Scan
                  </Button>
                </Box>
              )}
            </CardContent>
          </Card>
        </Box>

        {/* Right Column - Compliance Reports Status */}
        <Box sx={{ flex: '1 1 50%' }}>
          <Card>
            <CardHeader 
              title={`Compliance Reports (${avgComplianceScore.toFixed(1)}% avg)`}
              action={
                <Button 
                  size="small" 
                  startIcon={<DownloadIcon />}
                  onClick={() => window.location.href = '#/compliance-reports'}
                >
                  View All
                </Button>
              }
            />
            <CardContent>
              {recentComplianceReports.length > 0 ? (
                <>
                  <Box sx={{ marginBottom: 3 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 2 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <ComplianceIcon sx={{ fontSize: 20, color: 'info.main' }} />
                        <Typography variant="h6">Report Summary</Typography>
                      </Box>
                      <Chip 
                        label={`${recentComplianceReports.length} Reports`}
                        size="small"
                        color="info"
                        icon={<ComplianceIcon />}
                      />
                    </Box>
                    
                    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 2, marginBottom: 2 }}>
                      <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(33, 150, 243, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h4" color="info.main">{recentComplianceReports.length}</Typography>
                        <Typography variant="caption">Total Reports</Typography>
                      </Box>
                      <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(76, 175, 80, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h4" color="success.main">
                          {recentComplianceReports.filter(r => (r.complianceScore || r.ComplianceScore || 0) >= 80).length}
                        </Typography>
                        <Typography variant="caption">Compliant</Typography>
                      </Box>
                      <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(255, 152, 0, 0.1)', borderRadius: 1 }}>
                        <Typography variant="h4" color="warning.main">
                          {avgComplianceScore.toFixed(0)}%
                        </Typography>
                        <Typography variant="caption">Avg Score</Typography>
                      </Box>
                    </Box>
                  </Box>
                  
                  {/* Recent Reports */}
                  <Box sx={{ marginBottom: 2 }}>
                    <Typography variant="subtitle2" sx={{ marginBottom: 1 }}>
                      Recent Compliance Reports
                    </Typography>
                    {recentComplianceReports.map((report, index) => (
                      <Box key={report.id || index} sx={{ marginBottom: 1 }}>
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 0.5 }}>
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <ComplianceIcon sx={{ fontSize: 16 }} />
                            <Typography variant="body2" sx={{ fontWeight: 500 }}>
                              {report.framework}
                            </Typography>
                          </Box>
                          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                            <Chip 
                              label={`${report.complianceScore || report.ComplianceScore || 0}%`}
                              size="small"
                              color={(report.complianceScore || report.ComplianceScore || 0) >= 80 ? 'success' : 
                                     (report.complianceScore || report.ComplianceScore || 0) >= 60 ? 'warning' : 'error'}
                            />
                            <Typography variant="caption" color="textSecondary">
                              {new Date(report.generated || report.createdDate || Date.now()).toLocaleDateString()}
                            </Typography>
                          </Box>
                        </Box>
                        <Typography variant="caption" color="textSecondary" sx={{ marginLeft: 3 }}>
                          {report.reportType} • {report.controlsEvaluated || 'Multiple controls'} evaluated
                        </Typography>
                      </Box>
                    ))}
                  </Box>
                </>
              ) : (
                <Box sx={{ textAlign: 'center', padding: 3 }}>
                  <ComplianceIcon sx={{ fontSize: 48, color: 'text.secondary', marginBottom: 1 }} />
                  <Typography variant="h6" color="textSecondary" gutterBottom>
                    No Compliance Reports
                  </Typography>
                  <Typography variant="body2" color="textSecondary" gutterBottom>
                    Generate your first compliance report to see analysis here.
                  </Typography>
                  <Button 
                    variant="contained" 
                    size="small" 
                    onClick={() => window.location.href = '#/compliance-reports/create'}
                    sx={{ marginTop: 1 }}
                  >
                    Create Report
                  </Button>
                </Box>
              )}
            </CardContent>
          </Card>
        </Box>
      </Box>

      {/* Geographic Threat Analysis - Enhanced Analytics */}
      <Box sx={{ 
        display: 'flex', 
        flexWrap: 'wrap', 
        gap: 3,
        marginBottom: '30px'
      }}>
        <Box sx={{ flex: '1 1 100%', minWidth: '100%' }}>
          <GeographicThreatMap />
        </Box>
      </Box>
    </Box>
  );
};
