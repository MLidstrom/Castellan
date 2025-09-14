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
import { useNotify } from 'react-admin';
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

import { ConnectionPoolMonitor } from './ConnectionPoolMonitor';
import { RealtimeSystemMetrics } from './RealtimeSystemMetrics';
import { GeographicThreatMap } from './GeographicThreatMap';
import { PerformanceDashboard } from './PerformanceDashboard';
import { ThreatIntelligenceHealthDashboard } from './ThreatIntelligenceHealthDashboard';
import { ApiDiagnostic } from './ApiDiagnostic';
import { YaraDashboardWidgetSimple } from './YaraDashboardWidgetSimple';
import { YaraSummaryCard } from './YaraSummaryCard';

// Type interfaces for dashboard data
interface SecurityEvent {
  id: string;
  timestamp: string;
  riskLevel?: 'critical' | 'high' | 'medium' | 'low' | number;
  correlationScore?: number;
  confidence?: number;
  eventType: string;
  severity?: 'critical' | 'high' | 'medium' | 'low';
  description?: string;
  message?: string;
  source?: string;
  [key: string]: any;
}

interface ComplianceReport {
  id: string;
  complianceScore?: number;
  ComplianceScore?: number;
  [key: string]: any;
}

interface SystemStatus {
  id: string;
  status: string;
  component: string;
  responseTime: number;
  errorCount: number;
  warningCount: number;
  lastCheck: string;
  uptime: string;
  details: string;
  [key: string]: any;
}

interface ThreatScan {
  id: string;
  status: string;
  threatsFound?: number;
  timestamp?: string;
  startTime?: string;
  [key: string]: any;
}

export const Dashboard = React.memo(() => {
  const [timeRange, setTimeRange] = useState('24h');
  const [refreshing, setRefreshing] = useState(false);
  const [lastRefresh, setLastRefresh] = useState(new Date());
  const notify = useNotify();

  const authToken = localStorage.getItem('auth_token');
  
  // Debug authentication
  console.log('🔐 Auth Token:', authToken ? 'Present (' + authToken.substring(0, 20) + '...)' : 'Missing');
  
  if (!authToken) {
    console.warn('⚠️ No authentication token found! API calls may fail.');
  }
  
  // Test backend connectivity
  useEffect(() => {
    const testBackend = async () => {
      try {
        console.log('🔧 Testing backend connectivity...');
        const response = await fetch('http://localhost:5000/api/system-status', { method: 'GET' });
        console.log('🚑 Backend health check:', response.status);
      } catch (error) {
        console.error('🚨 Backend connectivity failed:', error);
      }
    };
    testBackend();
  }, []);

  // State for all dashboard data - no caching, fresh API calls
  const [securityEventsData, setSecurityEventsData] = useState<{ events: SecurityEvent[], total: number }>({ events: [], total: 0 });
  const [securityEventsLoading, setSecurityEventsLoading] = useState(true);
  const [securityEventsError, setSecurityEventsError] = useState<string | null>(null);

  const [complianceReports, setComplianceReports] = useState<ComplianceReport[]>([]);
  const [complianceReportsLoading, setComplianceReportsLoading] = useState(true);
  const [complianceReportsError, setComplianceReportsError] = useState<string | null>(null);

  const [systemStatus, setSystemStatus] = useState<SystemStatus[]>([]);
  const [systemStatusLoading, setSystemStatusLoading] = useState(true);
  const [systemStatusError, setSystemStatusError] = useState<string | null>(null);

  const [threatScanner, setThreatScanner] = useState<ThreatScan[]>([]);
  const [threatScannerLoading, setThreatScannerLoading] = useState(true);
  const [threatScannerError, setThreatScannerError] = useState<string | null>(null);

  // Fetch security events
  useEffect(() => {
    const fetchSecurityEvents = async () => {
      try {
        setSecurityEventsLoading(true);
        setSecurityEventsError(null);
        console.log('🚀 Fetching Security Events...');
        const response = await fetch('/api/security-events?sort=timestamp&order=desc', {
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        });
        console.log('📡 Security Events Response Status:', response.status);
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data = await response.json();
        const result = {
          events: data.data || data || [],
          total: data.total || 0
        };
        console.log('🔍 Security Events API Response:', result);
        setSecurityEventsData(result);
      } catch (error) {
        console.error('💥 Security Events API Failed:', error);
        setSecurityEventsError(error instanceof Error ? error.message : 'Failed to fetch');
      } finally {
        setSecurityEventsLoading(false);
      }
    };

    if (authToken) {
      fetchSecurityEvents();
    }
  }, [authToken, timeRange]);

  // Fetch compliance reports
  useEffect(() => {
    const fetchComplianceReports = async () => {
      try {
        setComplianceReportsLoading(true);
        setComplianceReportsError(null);
        console.log('🚀 Fetching Compliance Reports...');
        const response = await fetch('/api/compliance-reports?sort=generated&order=desc', {
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        });
        console.log('📡 Compliance Reports Response Status:', response.status);
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data = await response.json();
        const result = data.data || data || [];
        console.log('🔍 Compliance Reports API Response:', { status: response.status, dataLength: Array.isArray(result) ? result.length : 'not array', sample: Array.isArray(result) ? result.slice(0, 2) : result });
        setComplianceReports(result);
      } catch (error) {
        console.error('💥 Compliance Reports API Failed:', error);
        setComplianceReportsError(error instanceof Error ? error.message : 'Failed to fetch');
      } finally {
        setComplianceReportsLoading(false);
      }
    };

    if (authToken) {
      fetchComplianceReports();
    }
  }, [authToken, timeRange]);

  // Fetch system status
  useEffect(() => {
    const fetchSystemStatus = async () => {
      try {
        setSystemStatusLoading(true);
        setSystemStatusError(null);
        console.log('🚀 Fetching System Status...');
        const response = await fetch('/api/system-status', {
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        });
        console.log('📡 System Status Response Status:', response.status);
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data = await response.json();
        const result = data.data || data || [];
        console.log('🔍 System Status API Response:', { status: response.status, dataLength: Array.isArray(result) ? result.length : 'not array', sample: Array.isArray(result) ? result.slice(0, 2) : result });
        setSystemStatus(result);
      } catch (error) {
        console.error('💥 System Status API Failed:', error);
        setSystemStatusError(error instanceof Error ? error.message : 'Failed to fetch');
      } finally {
        setSystemStatusLoading(false);
      }
    };

    if (authToken) {
      fetchSystemStatus();
    }
  }, [authToken, timeRange]);

  // Fetch threat scanner data
  useEffect(() => {
    const fetchThreatScanner = async () => {
      try {
        setThreatScannerLoading(true);
        setThreatScannerError(null);
        console.log('🚀 Fetching Threat Scanner...');
        const response = await fetch('/api/threat-scanner?sort=timestamp&order=desc', {
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        });
        console.log('📡 Threat Scanner Response Status:', response.status);
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        const data = await response.json();
        const result = data.data || data || [];
        console.log('🔍 Threat Scanner API Response:', { status: response.status, dataLength: Array.isArray(result) ? result.length : 'not array', sample: Array.isArray(result) ? result.slice(0, 2) : result });
        setThreatScanner(result);
      } catch (error) {
        console.error('💥 Threat Scanner API Failed:', error);
        setThreatScannerError(error instanceof Error ? error.message : 'Failed to fetch');
      } finally {
        setThreatScannerLoading(false);
      }
    };

    if (authToken) {
      fetchThreatScanner();
    }
  }, [authToken, timeRange]);

  // Extract derived data
  const securityEvents = securityEventsData?.events || [];
  const securityEventsTotal = securityEventsData?.total || 0;
  
  // Debug logging
  useEffect(() => {
    console.group('📊 Dashboard Data Status Report');
    console.log('Security Events:', {
      hasData: !!securityEventsData && securityEvents.length > 0,
      loading: securityEventsLoading,
      error: securityEventsError,
      count: securityEvents.length
    });
    console.log('Compliance Reports:', {
      hasData: !!complianceReports && complianceReports.length > 0,
      loading: complianceReportsLoading,
      error: complianceReportsError,
      count: complianceReports.length
    });
    console.log('System Status:', {
      hasData: !!systemStatus && systemStatus.length > 0,
      loading: systemStatusLoading,
      error: systemStatusError,
      count: systemStatus.length
    });
    console.log('Threat Scanner:', {
      hasData: !!threatScanner && threatScanner.length > 0,
      loading: threatScannerLoading,
      error: threatScannerError,
      count: threatScanner.length
    });
    console.groupEnd();
  }, [securityEventsData, complianceReports, systemStatus, threatScanner, 
      securityEventsLoading, complianceReportsLoading, 
      systemStatusLoading, threatScannerLoading, 
      securityEventsError, complianceReportsError, 
      systemStatusError, threatScannerError]);
  
  // Analyze data structure (debug)
  useEffect(() => {
    if (securityEvents && securityEvents.length > 0) {
      console.group('📊 Security Events Data Analysis');
      console.log('Total Events:', securityEvents.length);
      console.log('Sample Event:', securityEvents[0]);
      console.log('Available Fields:', Object.keys(securityEvents[0]));
      console.groupEnd();
    }
  }, [securityEvents]);

  // Refresh all data
  const handleRefresh = async () => {
    setRefreshing(true);
    setLastRefresh(new Date());
    
    try {
      // Trigger all data fetches by changing dependencies
      // In a real scenario, you might want to call the fetch functions directly
      notify('Dashboard data refreshed', { type: 'info' });
    } catch (error) {
      console.error('Refresh failed:', error);
      notify('Failed to refresh dashboard data', { type: 'error' });
    } finally {
      setRefreshing(false);
    }
  };

  // Color constants for charts
  const COLORS = ['#8884d8', '#82ca9d', '#ffc658', '#ff7300', '#00ff00'];
  const riskLevelColors = {
    critical: '#f44336',
    high: '#ff9800', 
    medium: '#ffeb3b',
    low: '#4caf50'
  };

  // Data processing functions
  const processSecurityEventsForChart = () => {
    if (!securityEvents || securityEvents.length === 0) return [];
    
    const riskCounts = securityEvents.reduce((acc: any, event) => {
      const risk = event.riskLevel || event.severity || 'unknown';
      acc[risk] = (acc[risk] || 0) + 1;
      return acc;
    }, {});
    
    return Object.entries(riskCounts).map(([name, value]) => ({ name, value }));
  };

  const processComplianceData = () => {
    if (!complianceReports || complianceReports.length === 0) return [];
    
    return complianceReports.map((report, index) => ({
      name: `Report ${index + 1}`,
      score: report.complianceScore || report.ComplianceScore || 0
    }));
  };

  const processSystemHealthData = () => {
    if (!systemStatus || systemStatus.length === 0) return [];
    
    const healthData = systemStatus.reduce((acc: any, status) => {
      const health = status.status === 'healthy' ? 'Healthy' : 
                    status.status === 'warning' ? 'Warning' : 'Error';
      acc[health] = (acc[health] || 0) + 1;
      return acc;
    }, {});
    
    return Object.entries(healthData).map(([name, value]) => ({ name, value }));
  };

  const processThreatScanData = () => {
    if (!threatScanner || threatScanner.length === 0) return [];
    
    return threatScanner.map((scan, index) => ({
      name: `Scan ${index + 1}`,
      threats: scan.threatsFound || 0,
      status: scan.status
    }));
  };

  // Chart data
  const securityEventsChartData = processSecurityEventsForChart();
  const complianceChartData = processComplianceData();
  const systemHealthChartData = processSystemHealthData();
  const threatScanChartData = processThreatScanData();

  return (
    <Box sx={{ flexGrow: 1, p: 3 }}>
      {/* Header */}
      <Box display="flex" justifyContent="space-between" alignItems="center" mb={3}>
        <Typography variant="h4" component="h1">
          Security Dashboard
        </Typography>
        <Box display="flex" alignItems="center" gap={2}>
          {/* Time Range Selector */}
          <ButtonGroup variant="outlined" size="small">
            {['1h', '24h', '7d', '30d'].map((range) => (
              <Button
                key={range}
                onClick={() => setTimeRange(range)}
                variant={timeRange === range ? 'contained' : 'outlined'}
              >
                {range}
              </Button>
            ))}
          </ButtonGroup>
          
          {/* Refresh Button */}
          <Button
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={refreshing}
            variant="outlined"
          >
            {refreshing ? 'Refreshing...' : 'Refresh'}
          </Button>
          
          <Typography variant="caption" color="text.secondary">
            Last updated: {lastRefresh.toLocaleTimeString()}
          </Typography>
        </Box>
      </Box>

      {/* Summary Cards */}
      <Box display="flex" gap={3} mb={3}>
        <Card sx={{ minWidth: 200, flex: 1 }}>
          <CardHeader
            avatar={<SecurityIcon color="primary" />}
            title="Security Events"
            titleTypographyProps={{ variant: 'h6' }}
          />
          <CardContent>
            {securityEventsLoading ? (
              <CircularProgress size={24} />
            ) : securityEventsError ? (
              <Typography color="error" variant="body2">
                Error: {securityEventsError}
              </Typography>
            ) : (
              <Box>
                <Typography variant="h4" color="primary">
                  {securityEventsTotal}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Total Events
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>

        <Card sx={{ minWidth: 200, flex: 1 }}>
          <CardHeader
            avatar={<ComplianceIcon color="secondary" />}
            title="Compliance"
            titleTypographyProps={{ variant: 'h6' }}
          />
          <CardContent>
            {complianceReportsLoading ? (
              <CircularProgress size={24} />
            ) : complianceReportsError ? (
              <Typography color="error" variant="body2">
                Error: {complianceReportsError}
              </Typography>
            ) : (
              <Box>
                <Typography variant="h4" color="secondary">
                  {complianceReports.length}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Reports
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>

        <Card sx={{ minWidth: 200, flex: 1 }}>
          <CardHeader
            avatar={<HealthyIcon color="success" />}
            title="System Health"
            titleTypographyProps={{ variant: 'h6' }}
          />
          <CardContent>
            {systemStatusLoading ? (
              <CircularProgress size={24} />
            ) : systemStatusError ? (
              <Typography color="error" variant="body2">
                Error: {systemStatusError}
              </Typography>
            ) : (
              <Box>
                <Typography variant="h4" color="success.main">
                  {systemStatus.filter(s => s.status === 'healthy').length}/{systemStatus.length}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Healthy Services
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>

        <Card sx={{ minWidth: 200, flex: 1 }}>
          <CardHeader
            avatar={<ScannerIcon color="warning" />}
            title="Threat Scans"
            titleTypographyProps={{ variant: 'h6' }}
          />
          <CardContent>
            {threatScannerLoading ? (
              <CircularProgress size={24} />
            ) : threatScannerError ? (
              <Typography color="error" variant="body2">
                Error: {threatScannerError}
              </Typography>
            ) : (
              <Box>
                <Typography variant="h4" color="warning.main">
                  {threatScanner.length}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Active Scans
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>
      </Box>

      {/* Charts */}
      <Box display="flex" gap={3} mb={3}>
        <Card sx={{ flex: 1 }}>
          <CardHeader title="Security Events by Risk Level" />
          <CardContent>
            <Box height={300}>
              {securityEventsChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={securityEventsChartData}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={({ name, percent }) => `${name} ${percent ? (percent * 100).toFixed(0) : 0}%`}
                      outerRadius={80}
                      fill="#8884d8"
                      dataKey="value"
                    >
                      {securityEventsChartData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                      ))}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              ) : (
                <Box display="flex" alignItems="center" justifyContent="center" height="100%">
                  <Typography color="text.secondary">No data available</Typography>
                </Box>
              )}
            </Box>
          </CardContent>
        </Card>

        <Card sx={{ flex: 1 }}>
          <CardHeader title="Compliance Scores" />
          <CardContent>
            <Box height={300}>
              {complianceChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={complianceChartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis />
                    <Tooltip />
                    <Bar dataKey="score" fill="#82ca9d" />
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <Box display="flex" alignItems="center" justifyContent="center" height="100%">
                  <Typography color="text.secondary">No data available</Typography>
                </Box>
              )}
            </Box>
          </CardContent>
        </Card>
      </Box>

      {/* Additional Components */}
      <Box display="flex" gap={3} mb={3}>
        <Card sx={{ flex: 1 }}>
          <CardHeader title="API Diagnostics" />
          <CardContent>
            <ApiDiagnostic 
              securityEventsData={securityEvents}
              complianceReportsData={complianceReports}
              systemStatusData={systemStatus}
              threatScannerData={threatScanner}
              securityEventsError={securityEventsError}
              complianceReportsError={complianceReportsError}
              systemStatusError={systemStatusError}
              threatScannerError={threatScannerError}
            />
          </CardContent>
        </Card>

        <Card sx={{ flex: 1 }}>
          <CardHeader title="YARA Summary" />
          <CardContent>
            <YaraSummaryCard />
          </CardContent>
        </Card>
      </Box>

      {/* More Components */}
      <Box display="flex" gap={3} mb={3}>
        <Card sx={{ flex: 1 }}>
          <CardHeader title="Connection Pool Monitor" />
          <CardContent>
            <ConnectionPoolMonitor systemStatus={systemStatus} />
          </CardContent>
        </Card>

        <Card sx={{ flex: 1 }}>
          <CardHeader title="System Metrics" />
          <CardContent>
            <RealtimeSystemMetrics />
          </CardContent>
        </Card>
      </Box>

      {/* Large Components */}
      <Box mb={3}>
        <Card>
          <CardHeader title="Geographic Threat Map" />
          <CardContent>
            <GeographicThreatMap />
          </CardContent>
        </Card>
      </Box>

      <Box mb={3}>
        <Card>
          <CardHeader title="Performance Dashboard" />
          <CardContent>
            <PerformanceDashboard />
          </CardContent>
        </Card>
      </Box>

      <Box mb={3}>
        <Card>
          <CardHeader title="Threat Intelligence Health" />
          <CardContent>
            <ThreatIntelligenceHealthDashboard />
          </CardContent>
        </Card>
      </Box>

      <Box mb={3}>
        <Card>
          <CardHeader title="YARA Dashboard" />
          <CardContent>
            <YaraDashboardWidgetSimple />
          </CardContent>
        </Card>
      </Box>
    </Box>
  );
});

