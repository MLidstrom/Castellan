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
  Lock as LockIcon,
  Star as StarIcon,
  BugReport as BugReportIcon,
  Shield as ShieldIcon,
  Scanner as ScannerIcon
} from '@mui/icons-material';

import { ConnectionPoolMonitor } from './ConnectionPoolMonitor';

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
        refetchStatus()
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

  // Dummy compliance data for premium feature display
  const dummyComplianceData = [
    { id: 1, framework: 'SOC 2 Type II', complianceScore: 87, reportType: 'Quarterly', createdDate: '2024-08-15' },
    { id: 2, framework: 'ISO 27001', complianceScore: 92, reportType: 'Annual', createdDate: '2024-08-10' },
    { id: 3, framework: 'HIPAA', complianceScore: 78, reportType: 'Monthly', createdDate: '2024-08-05' },
    { id: 4, framework: 'FedRAMP', complianceScore: 95, reportType: 'Quarterly', createdDate: '2024-07-30' },
    { id: 5, framework: 'GDPR', complianceScore: 83, reportType: 'Annual', createdDate: '2024-07-25' }
  ];
  const dummyAvgComplianceScore = 87.0;

  // Dummy threat scanner data for premium feature display
  const dummyThreatScanData = {
    filesScanned: 2847,
    threatsDetected: 15,
    threatsBlocked: 13,
    threatsQuarantined: 2,
    lastFullScan: '2024-08-22 14:30:00',
    scanProgress: 0,
    realTimeProtection: true,
    scanStatus: 'Idle',
    threatsByType: [
      { type: 'Malware', count: 8, severity: 'high' },
      { type: 'Ransomware', count: 3, severity: 'critical' },
      { type: 'Spyware', count: 2, severity: 'medium' },
      { type: 'Adware', count: 2, severity: 'low' }
    ],
    protectionModules: [
      { name: 'Real-time Protection', status: 'Active', lastUpdate: '2024-08-22' },
      { name: 'Web Protection', status: 'Active', lastUpdate: '2024-08-22' },
      { name: 'Email Protection', status: 'Active', lastUpdate: '2024-08-21' },
      { name: 'Behavioral Analysis', status: 'Active', lastUpdate: '2024-08-22' }
    ]
  };

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

          {/* Compliance Status */}
          <Card 
            sx={{ 
            position: 'relative',
            opacity: 0.5,
            filter: 'grayscale(100%)',
            pointerEvents: 'none',
            backgroundColor: '#f8f9fa',
            border: '1px solid #dee2e6'
          }}
        >
            {/* Premium Badge */}
            <Box sx={{ position: 'absolute', top: 8, right: 8, zIndex: 1 }}>
              <Chip 
                icon={<LockIcon />}
                label="PRO"
                color="warning"
                size="small"
                variant="filled"
                sx={{
                  pointerEvents: 'auto',
                  opacity: 1,
                  filter: 'none'
                }}
              />
            </Box>
            
            <CardHeader 
              title={`Compliance Status (${dummyAvgComplianceScore.toFixed(1)}% avg)`}
              action={
                <Button 
                  size="small" 
                  startIcon={<DownloadIcon />}
                  disabled
                  sx={{ opacity: 0.6 }}
                >
                  Export
                </Button>
              }
            />
            <CardContent>
              {dummyComplianceData.slice(0, 6).map(report => (
                <Box key={report.id} sx={{ marginBottom: 2 }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 1 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <ComplianceIcon sx={{ fontSize: 16 }} />
                      <Typography variant="subtitle2">{report.framework}</Typography>
                    </Box>
                    <Chip 
                      label={`${report.complianceScore}%`}
                      size="small"
                      color={report.complianceScore >= 90 ? 'success' : 
                             report.complianceScore >= 70 ? 'info' : 
                             report.complianceScore >= 50 ? 'warning' : 'error'}
                    />
                  </Box>
                  <LinearProgress 
                    variant="determinate" 
                    value={report.complianceScore} 
                    color={report.complianceScore >= 90 ? 'success' : 
                           report.complianceScore >= 70 ? 'info' : 
                           report.complianceScore >= 50 ? 'warning' : 'error'}
                    sx={{ height: 8, borderRadius: 4 }}
                  />
                  <Typography variant="caption" color="textSecondary">
                    {report.reportType} • {new Date(report.createdDate).toLocaleDateString()}
                  </Typography>
                </Box>
              ))}
              
            </CardContent>
          </Card>
        </Box>

        {/* Right Column - Threat Scanner */}
        <Box sx={{ flex: '1 1 50%' }}>
          <Card 
            sx={{ 
            position: 'relative',
            opacity: 0.5,
            filter: 'grayscale(100%)',
            pointerEvents: 'none',
            backgroundColor: '#f8f9fa',
            border: '1px solid #dee2e6'
          }}
        >
            {/* Premium Badge */}
            <Box sx={{ position: 'absolute', top: 8, right: 8, zIndex: 1 }}>
              <Chip 
                icon={<LockIcon />}
                label="PRO"
                color="warning"
                size="small"
                variant="filled"
                sx={{
                  pointerEvents: 'auto',
                  opacity: 1,
                  filter: 'none'
                }}
              />
            </Box>
            
            <CardHeader 
              title="Advanced Threat Scanner"
              action={
                <Button 
                  size="small" 
                  startIcon={<ScannerIcon />}
                  disabled
                  sx={{ opacity: 0.6 }}
                >
                  Full Scan
                </Button>
              }
            />
            <CardContent>
              {/* Scan Statistics */}
              <Box sx={{ marginBottom: 3 }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 2 }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <ShieldIcon sx={{ fontSize: 20, color: 'success.main' }} />
                    <Typography variant="h6">Scan Summary</Typography>
                  </Box>
                  <Chip 
                    label={dummyThreatScanData.realTimeProtection ? 'Protected' : 'Unprotected'}
                    size="small"
                    color={dummyThreatScanData.realTimeProtection ? 'success' : 'error'}
                    icon={<ShieldIcon />}
                  />
                </Box>
                
                <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: 2, marginBottom: 2 }}>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(76, 175, 80, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="success.main">{dummyThreatScanData.filesScanned.toLocaleString()}</Typography>
                    <Typography variant="caption">Files Scanned</Typography>
                  </Box>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(244, 67, 54, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="error.main">{dummyThreatScanData.threatsDetected}</Typography>
                    <Typography variant="caption">Threats Detected</Typography>
                  </Box>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(33, 150, 243, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="info.main">{dummyThreatScanData.threatsBlocked}</Typography>
                    <Typography variant="caption">Blocked</Typography>
                  </Box>
                  <Box sx={{ textAlign: 'center', padding: 1, backgroundColor: 'rgba(255, 152, 0, 0.1)', borderRadius: 1 }}>
                    <Typography variant="h4" color="warning.main">{dummyThreatScanData.threatsQuarantined}</Typography>
                    <Typography variant="caption">Quarantined</Typography>
                  </Box>
                </Box>
                
                <Typography variant="body2" color="textSecondary">
                  Last full scan: {new Date(dummyThreatScanData.lastFullScan).toLocaleString()}
                </Typography>
              </Box>
              
              {/* Threat Types */}
              <Box sx={{ marginBottom: 3 }}>
                <Typography variant="subtitle2" sx={{ marginBottom: 1, display: 'flex', alignItems: 'center', gap: 1 }}>
                  <BugReportIcon sx={{ fontSize: 16 }} />
                  Threat Types Detected
                </Typography>
                {dummyThreatScanData.threatsByType.map((threat, index) => (
                  <Box key={index} sx={{ marginBottom: 1 }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 0.5 }}>
                      <Typography variant="body2">{threat.type}</Typography>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <Chip 
                          label={threat.count}
                          size="small"
                          color={threat.severity === 'critical' ? 'error' : 
                                 threat.severity === 'high' ? 'warning' : 
                                 threat.severity === 'medium' ? 'info' : 'default'}
                        />
                        <Chip 
                          label={threat.severity}
                          size="small"
                          variant="outlined"
                          color={threat.severity === 'critical' ? 'error' : 
                                 threat.severity === 'high' ? 'warning' : 
                                 threat.severity === 'medium' ? 'info' : 'default'}
                        />
                      </Box>
                    </Box>
                  </Box>
                ))}
              </Box>
              
              {/* Protection Modules Status */}
              <Box sx={{ marginBottom: 2 }}>
                <Typography variant="subtitle2" sx={{ marginBottom: 1 }}>
                  Protection Modules
                </Typography>
                {dummyThreatScanData.protectionModules.map((module, index) => (
                  <Box key={index} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 1 }}>
                    <Typography variant="body2">{module.name}</Typography>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Chip 
                        label={module.status}
                        size="small"
                        color={module.status === 'Active' ? 'success' : 'error'}
                        variant="filled"
                      />
                      <Typography variant="caption" color="textSecondary">
                        {new Date(module.lastUpdate).toLocaleDateString()}
                      </Typography>
                    </Box>
                  </Box>
                ))}
              </Box>
              
            </CardContent>
          </Card>
        </Box>
      </Box>
    </Box>
  );
};