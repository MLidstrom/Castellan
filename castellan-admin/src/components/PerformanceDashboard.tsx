import React, { useState, useEffect } from 'react';
import {
  Card,
  CardContent,
  CardHeader,
  Typography,
  Box,
  Button,
  ButtonGroup,
  Chip,
  CircularProgress,
  LinearProgress,
  Tooltip as MuiTooltip,
  IconButton,
  Alert,
  AlertTitle,
  Grid
} from '@mui/material';
import {
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  LineChart,
  Line,
  AreaChart,
  Area,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  Legend
} from 'recharts';
import {
  Speed as PerformanceIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  CheckCircle as HealthyIcon,
  Memory as MemoryIcon,
  Storage as DiskIcon,
  Router as NetworkIcon,
  Security as SecurityIcon,
  BugReport as ThreatIcon,
  Assessment as ComplianceIcon,
  Refresh as RefreshIcon,
  Timeline as TimelineIcon
} from '@mui/icons-material';
import { useCachedApi } from '../hooks/useCachedApi';
import { CACHE_KEYS, CACHE_TTL } from '../utils/cacheManager';

// API Response Interfaces based on actual endpoints
interface SecurityEvent {
  id: string;
  timestamp: string;
  eventType: string;
  severity?: 'critical' | 'high' | 'medium' | 'low';
  riskLevel?: number | string;
  description?: string;
  message?: string;
  source?: string;
  [key: string]: any;
}
interface ComplianceReport {
  id: string;
  generated: string;
  type: string;
  status: string;
  score: number;
  findings: number;
  criticalFindings: number;
}

interface SystemStatus {
  id: string;
  component: string;
  status: 'healthy' | 'warning' | 'error';
  lastCheck: string;
  uptime: number;
  responseTime: number;
  details: string;
}

interface ThreatScanResult {
  id: string;
  timestamp: string;
  scanType: string;
  threatsFound: number;
  riskScore: number;
  status: string;
  duration: number;
}

// Derived dashboard metrics from API data
interface DashboardMetrics {
  security: {
    totalEvents: number;
    criticalEvents: number;
    avgRiskLevel: number;
    recentEvents: SecurityEvent[];
  };
  compliance: {
    avgScore: number;
    totalReports: number;
    criticalFindings: number;
    recentReports: ComplianceReport[];
  };
  system: {
    healthyComponents: number;
    totalComponents: number;
    avgResponseTime: number;
    avgUptime: number;
    systemStatus: SystemStatus[];
  };
  threats: {
    totalScans: number;
    threatsDetected: number;
    avgRiskScore: number;
    recentScans: ThreatScanResult[];
  };
}

export const PerformanceDashboard: React.FC = () => {
  const [timeRange, setTimeRange] = useState<'1h' | '6h' | '24h' | '7d'>('1h');
  const [dashboardMetrics, setDashboardMetrics] = useState<DashboardMetrics | null>(null);

  const authToken = localStorage.getItem('auth_token');

  // Cached API calls for performance dashboard data
  const securityEventsApi = useCachedApi(
    async () => {
      const response = await fetch('/api/security-events?sort=timestamp&order=desc', {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
      const data = await response.json();
      return data.data || data || [];
    },
    {
      cacheKey: `${CACHE_KEYS.PERFORMANCE_METRICS}_security_${timeRange}`,
      cacheTtl: CACHE_TTL.NORMAL_REFRESH,
      dependencies: [timeRange]
    }
  );

  const complianceReportsApi = useCachedApi(
    async () => {
      const response = await fetch('/api/compliance-reports?sort=generated&order=desc', {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
      const data = await response.json();
      return data.data || data || [];
    },
    {
      cacheKey: `${CACHE_KEYS.PERFORMANCE_METRICS}_compliance_${timeRange}`,
      cacheTtl: CACHE_TTL.SLOW_REFRESH,
      dependencies: [timeRange]
    }
  );

  const systemStatusApi = useCachedApi(
    async () => {
      const response = await fetch('/api/system-status', {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
      const data = await response.json();
      return data.data || data || [];
    },
    {
      cacheKey: `${CACHE_KEYS.PERFORMANCE_METRICS}_system_${timeRange}`,
      cacheTtl: CACHE_TTL.NORMAL_REFRESH,
      dependencies: [timeRange]
    }
  );

  const threatScannerApi = useCachedApi(
    async () => {
      const response = await fetch('/api/threat-scanner?sort=timestamp&order=desc', {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
      const data = await response.json();
      return data.data || data || [];
    },
    {
      cacheKey: `${CACHE_KEYS.PERFORMANCE_METRICS}_threats_${timeRange}`,
      cacheTtl: CACHE_TTL.NORMAL_REFRESH,
      dependencies: [timeRange]
    }
  );

  const fetchData = async (endpoint: string, params?: Record<string, string>) => {
    const url = new URL(`/api/${endpoint}`, window.location.origin);
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        url.searchParams.append(key, value);
      });
    }

    const response = await fetch(url.toString(), {
      headers: {
        'Authorization': `Bearer ${authToken}`,
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return await response.json();
  };

  // Helper function to ensure numeric values
  const ensureNumber = (value: any): number => {
    if (typeof value === 'number' && !isNaN(value)) return value;
    if (typeof value === 'string') {
      const parsed = parseFloat(value);
      return isNaN(parsed) ? 0 : parsed;
    }
    return 0;
  };

  const calculateMetrics = (security: SecurityEvent[], compliance: ComplianceReport[], system: SystemStatus[], threats: ThreatScanResult[]): DashboardMetrics => {
    // Ensure arrays are valid
    const safeSecurityEvents = Array.isArray(security) ? security.filter(e => e && e.id) : [];
    const safeComplianceReports = Array.isArray(compliance) ? compliance.filter(r => r && r.id) : [];
    const safeSystemStatus = Array.isArray(system) ? system.filter(s => s && s.id) : [];
    const safeThreatScans = Array.isArray(threats) ? threats.filter(t => t && t.id) : [];
    
    // Sanitize system status data to ensure numeric values
    const sanitizedSystem = safeSystemStatus.map(s => ({
      ...s,
      responseTime: ensureNumber(s.responseTime),
      uptime: ensureNumber(s.uptime)
    }));

    return {
      security: {
        totalEvents: safeSecurityEvents.length,
        criticalEvents: safeSecurityEvents.filter(e => e.severity === 'critical' || e.severity === 'high' || (typeof e.riskLevel === 'string' && (e.riskLevel === 'critical' || e.riskLevel === 'high'))).length,
        avgRiskLevel: safeSecurityEvents.length > 0 ? safeSecurityEvents.reduce((sum, e) => {
          const riskValue = typeof e.riskLevel === 'string' ? 
            (['low', 'medium', 'high', 'critical'].indexOf(e.riskLevel) + 1) : ensureNumber(e.riskLevel);
          return sum + riskValue;
        }, 0) / safeSecurityEvents.length : 0,
        recentEvents: safeSecurityEvents.slice(0, 10)
      },
      compliance: {
        avgScore: safeComplianceReports.length > 0 ? safeComplianceReports.reduce((sum, r) => sum + ensureNumber(r.score), 0) / safeComplianceReports.length : 0,
        totalReports: safeComplianceReports.length,
        criticalFindings: safeComplianceReports.reduce((sum, r) => sum + ensureNumber(r.criticalFindings), 0),
        recentReports: safeComplianceReports.slice(0, 5)
      },
      system: {
        healthyComponents: sanitizedSystem.filter(s => s.status === 'healthy').length,
        totalComponents: sanitizedSystem.length,
        avgResponseTime: sanitizedSystem.length > 0 ? sanitizedSystem.reduce((sum, s) => sum + s.responseTime, 0) / sanitizedSystem.length : 0,
        avgUptime: sanitizedSystem.length > 0 ? sanitizedSystem.reduce((sum, s) => sum + s.uptime, 0) / sanitizedSystem.length : 0,
        systemStatus: sanitizedSystem
      },
      threats: {
        totalScans: safeThreatScans.length,
        threatsDetected: safeThreatScans.reduce((sum, t) => sum + ensureNumber(t.threatsFound), 0),
        avgRiskScore: safeThreatScans.length > 0 ? safeThreatScans.reduce((sum, t) => sum + ensureNumber(t.riskScore), 0) / safeThreatScans.length : 0,
        recentScans: safeThreatScans.slice(0, 10).map(t => ({
          ...t,
          riskScore: ensureNumber(t.riskScore),
          threatsFound: ensureNumber(t.threatsFound),
          duration: ensureNumber(t.duration)
        }))
      }
    };
  };

  // Update metrics when cached data changes
  useEffect(() => {
    const security = securityEventsApi.data || [];
    const compliance = complianceReportsApi.data || [];
    const system = systemStatusApi.data || [];
    const threats = threatScannerApi.data || [];
    
    if (security.length > 0 || compliance.length > 0 || system.length > 0 || threats.length > 0) {
      setDashboardMetrics(calculateMetrics(security, compliance, system, threats));
    }
  }, [
    securityEventsApi.data,
    complianceReportsApi.data, 
    systemStatusApi.data,
    threatScannerApi.data
  ]);

  const loadAllData = async () => {
    try {
      await Promise.all([
        securityEventsApi.refetch(),
        complianceReportsApi.refetch(),
        systemStatusApi.refetch(),
        threatScannerApi.refetch()
      ]);
    } catch (err) {
      console.error('Dashboard refresh error:', err);
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const formatDuration = (uptime: string | number) => {
    if (typeof uptime === 'string') return uptime;
    const hours = Math.floor(uptime / 3600);
    const minutes = Math.floor((uptime % 3600) / 60);
    return `${hours}h ${minutes}m`;
  };

  const loading = securityEventsApi.loading || complianceReportsApi.loading || 
                  systemStatusApi.loading || threatScannerApi.loading;
  
  const error = securityEventsApi.error || complianceReportsApi.error || 
                systemStatusApi.error || threatScannerApi.error;

  if (loading && !dashboardMetrics) {
    return (
      <Card>
        <CardHeader title="Performance Dashboard" />
        <CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
            <CircularProgress />
            <Typography sx={{ ml: 2 }}>Loading cached performance data...</Typography>
          </Box>
        </CardContent>
      </Card>
    );
  }

  if (error && !dashboardMetrics) {
    return (
      <Card>
        <CardHeader title="Performance Dashboard" />
        <CardContent>
          <Alert severity="error">
            <AlertTitle>Error Loading Performance Data</AlertTitle>
            {error}
          </Alert>
          <Button onClick={loadAllData} variant="contained" sx={{ mt: 2 }}>
            Retry
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <Box sx={{ mb: 4 }}>
      {/* Header with controls */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5" gutterBottom sx={{ margin: 0, display: 'flex', alignItems: 'center', gap: 1 }}>
          <PerformanceIcon /> Performance Dashboard
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
              variant={timeRange === '6h' ? 'contained' : 'outlined'}
              onClick={() => setTimeRange('6h')}
            >
              6H
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
          </ButtonGroup>
          <IconButton onClick={loadAllData} color="primary">
            <RefreshIcon />
          </IconButton>
        </Box>
      </Box>

      {/* System Overview Cards */}
      {dashboardMetrics && (
        <Grid container spacing={3} sx={{ mb: 3 }}>
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                  <HealthyIcon sx={{ 
                    color: dashboardMetrics.system.totalComponents > 0 && 
                           (dashboardMetrics.system.healthyComponents / dashboardMetrics.system.totalComponents) >= 0.8 
                           ? 'success.main' : 'warning.main', 
                    mr: 1 
                  }} />
                  <Typography variant="h6" color="textSecondary">System Health</Typography>
                </Box>
                <Typography variant="h3" color={
                  dashboardMetrics.system.totalComponents > 0 && 
                  (dashboardMetrics.system.healthyComponents / dashboardMetrics.system.totalComponents) >= 0.8 
                  ? 'success.main' : 'warning.main'
                }>
                  {dashboardMetrics.system.totalComponents > 0 ? 
                    Math.round((dashboardMetrics.system.healthyComponents / dashboardMetrics.system.totalComponents) * 100) : 0}%
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  {dashboardMetrics.system.healthyComponents}/{dashboardMetrics.system.totalComponents} components healthy
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                  <SecurityIcon sx={{ color: 'error.main', mr: 1 }} />
                  <Typography variant="h6" color="textSecondary">Security Events</Typography>
                </Box>
                <Typography variant="h3" color="error.main">
                  {dashboardMetrics.security.criticalEvents}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  {dashboardMetrics.security.totalEvents} total events • Avg risk: {(typeof dashboardMetrics.security.avgRiskLevel === 'number' ? dashboardMetrics.security.avgRiskLevel.toFixed(1) : 'N/A')}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                  <ComplianceIcon sx={{ color: 'info.main', mr: 1 }} />
                  <Typography variant="h6" color="textSecondary">Compliance Score</Typography>
                </Box>
                <Typography variant="h3" color="info.main">
                  {(typeof dashboardMetrics.compliance.avgScore === 'number' ? dashboardMetrics.compliance.avgScore.toFixed(0) : 'N/A')}%
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  {dashboardMetrics.compliance.totalReports} reports • {dashboardMetrics.compliance.criticalFindings} critical findings
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          
          <Grid item xs={12} sm={6} md={3}>
            <Card>
              <CardContent>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                  <ThreatIcon sx={{ color: 'warning.main', mr: 1 }} />
                  <Typography variant="h6" color="textSecondary">Threat Scans</Typography>
                </Box>
                <Typography variant="h3" color="warning.main">
                  {dashboardMetrics.threats.threatsDetected}
                </Typography>
                <Typography variant="body2" color="textSecondary">
                  {dashboardMetrics.threats.totalScans} scans • Risk score: {(typeof dashboardMetrics.threats.avgRiskScore === 'number' ? dashboardMetrics.threats.avgRiskScore.toFixed(1) : 'N/A')}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      {/* Recent Security Events */}
      {dashboardMetrics && dashboardMetrics.security.recentEvents.length > 0 && (
        <Box sx={{ mb: 3 }}>
          <Typography variant="h6" sx={{ mb: 2 }}>Recent Security Events</Typography>
          {dashboardMetrics.security.recentEvents.slice(0, 5).map((event) => (
            <Alert 
              key={event.id}
              severity={event.severity === 'critical' || event.severity === 'high' ? 'error' : 
                       event.severity === 'medium' ? 'warning' : 'info'}
              sx={{ mb: 1 }}
            >
              <AlertTitle>{event.eventType || 'Security Event'} - {(event.severity?.toUpperCase?.() || (typeof event.riskLevel === 'string' ? event.riskLevel.toUpperCase() : 'INFO'))}</AlertTitle>
              {event.description || event.message || 'Security event detected'} (Risk Level: {event.riskLevel || 'unknown'}, Source: {event.source || 'system'})
            </Alert>
          ))}
        </Box>
      )}

      {/* System Status Overview */}
      {dashboardMetrics && (
        <Grid container spacing={3} sx={{ mb: 3 }}>
          <Grid item xs={12} md={8}>
            <Card>
              <CardHeader title="System Components Status" />
              <CardContent>
                <ResponsiveContainer width="100%" height={400}>
                  <BarChart data={dashboardMetrics.system.systemStatus.slice(0, 10)}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis 
                      dataKey="component" 
                      tick={{ fontSize: 12 }}
                      angle={-45}
                      textAnchor="end"
                      height={80}
                    />
                    <YAxis />
                    <Tooltip 
                      formatter={(value: any, name: string) => [
                        typeof value === 'number' ? value.toFixed(2) : (value || 'N/A'), 
                        name
                      ]}
                    />
                    <Legend />
                    <Bar dataKey="responseTime" fill="#8884d8" name="Response Time (ms)" />
                    <Bar dataKey="uptime" fill="#82ca9d" name="Uptime (hours)" />
                  </BarChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          </Grid>
          
          <Grid item xs={12} md={4}>
            <Card>
              <CardHeader title="Component Health" />
              <CardContent>
                <ResponsiveContainer width="100%" height={400}>
                  <PieChart>
                    <Pie
                      data={[
                        { name: 'Healthy', value: dashboardMetrics.system.healthyComponents, fill: '#4caf50' },
                        { name: 'Warning', value: dashboardMetrics.system.systemStatus.filter(s => s.status === 'warning').length, fill: '#ff9800' },
                        { name: 'Error', value: dashboardMetrics.system.systemStatus.filter(s => s.status === 'error').length, fill: '#f44336' }
                      ]}
                      cx="50%"
                      cy="50%"
                      labelLine={false}
                      label={({ name, percent }) => `${name} ${percent ? (percent * 100).toFixed(0) : '0'}%`}
                      outerRadius={80}
                      fill="#8884d8"
                      dataKey="value"
                    >
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      {/* Recent Activity Summary */}
      {dashboardMetrics && (
        <Grid container spacing={3} sx={{ mb: 3 }}>
          <Grid item xs={12} md={6}>
            <Card>
              <CardHeader title="Recent Compliance Reports" />
              <CardContent>
                {dashboardMetrics.compliance.recentReports.length > 0 ? (
                  dashboardMetrics.compliance.recentReports.slice(0, 3).map((report) => (
                    <Box key={report.id} sx={{ mb: 2, p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                        <Typography variant="subtitle2" fontWeight="bold">{report.type}</Typography>
                        <Chip 
                          label={report.status} 
                          color={report.status === 'passed' ? 'success' : report.status === 'failed' ? 'error' : 'warning'}
                          size="small"
                        />
                      </Box>
                      <Typography variant="body2" color="textSecondary" sx={{ mb: 1 }}>
                        Score: {report.score}% • {report.findings} findings ({report.criticalFindings} critical)
                      </Typography>
                      <Typography variant="caption" color="textSecondary">
                        Generated: {new Date(report.generated).toLocaleString()}
                      </Typography>
                    </Box>
                  ))
                ) : (
                  <Typography variant="body2" color="textSecondary">
                    No recent compliance reports available
                  </Typography>
                )}
              </CardContent>
            </Card>
          </Grid>
          
          <Grid item xs={12} md={6}>
            <Card>
              <CardHeader title="Recent Threat Scans" />
              <CardContent>
                {dashboardMetrics.threats.recentScans.length > 0 ? (
                  dashboardMetrics.threats.recentScans.slice(0, 3).map((scan) => (
                    <Box key={scan.id} sx={{ mb: 2, p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
                      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                        <Typography variant="subtitle2" fontWeight="bold">{scan.scanType}</Typography>
                        <Chip 
                          label={`${scan.threatsFound} threats`}
                          color={scan.threatsFound > 0 ? 'error' : 'success'}
                          size="small"
                        />
                      </Box>
                      <Typography variant="body2" color="textSecondary" sx={{ mb: 1 }}>
                        Risk Score: {(typeof scan.riskScore === 'number' ? scan.riskScore.toFixed(1) : 'N/A')} • Duration: {scan.duration}ms
                      </Typography>
                      <Typography variant="caption" color="textSecondary">
                        Scanned: {new Date(scan.timestamp).toLocaleString()}
                      </Typography>
                    </Box>
                  ))
                ) : (
                  <Typography variant="body2" color="textSecondary">
                    No recent threat scans available
                  </Typography>
                )}
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}
    </Box>
  );
};
