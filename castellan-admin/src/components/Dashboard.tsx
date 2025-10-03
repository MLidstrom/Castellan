import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
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
  Tooltip as MuiTooltip,
  Badge,
  Stack,
  Skeleton
} from '@mui/material';
import { useNotify } from 'react-admin';
import { useNavigate } from 'react-router-dom';
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
  Warning as WarningIcon,
  CheckCircle as HealthyIcon,
  Error as ErrorIcon,
  TrendingUp as TrendingUpIcon,
  Refresh as RefreshIcon,
  Download as DownloadIcon,
  Fullscreen as FullscreenIcon,
  Shield as ShieldIcon,
  Scanner as ScannerIcon,
  Circle as ConnectionIcon,
  SignalWifi4Bar as ConnectedIcon,
  SignalWifiOff as DisconnectedIcon
} from '@mui/icons-material';

import { ConnectionPoolMonitor } from './ConnectionPoolMonitor';
import { RealtimeSystemMetrics } from './RealtimeSystemMetrics';
import { GeographicThreatMap } from './GeographicThreatMap';
import { PerformanceDashboard } from './PerformanceDashboard';
import { ThreatIntelligenceHealthDashboard } from './ThreatIntelligenceHealthDashboard';
import { ApiDiagnostic } from './ApiDiagnostic';
import { YaraSummaryCard } from './YaraSummaryCard';

// Import SignalR context for persistent connection
import { useSignalRContext } from '../contexts/SignalRContext';
import { SystemMetricsUpdate, ConsolidatedDashboardData } from '../hooks/useSignalR';

// Import Dashboard data caching context
import { useDashboardDataContext } from '../contexts/DashboardDataContext';

// API Configuration - Use same base URL as data provider
const API_URL = process.env.REACT_APP_CASTELLANPRO_API_URL || 'http://localhost:5000/api';

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
  const [initialLoad, setInitialLoad] = useState(true);
  const notify = useNotify();
  const navigate = useNavigate();

  const authToken = localStorage.getItem('auth_token');

  // Debug authentication (only on first render)
  const hasLoggedAuth = useRef(false);
  if (!hasLoggedAuth.current) {
    console.log('🔐 Auth Token:', authToken ? 'Present (' + authToken.substring(0, 20) + '...)' : 'Missing');
    if (!authToken) {
      console.warn('⚠️ No authentication token found! API calls may fail.');
    }
    hasLoggedAuth.current = true;
  }
  
  // Test backend connectivity
  useEffect(() => {
    const testBackend = async () => {
      if (!authToken) return;
      try {
        console.log('🔧 Testing backend connectivity...');
        const response = await fetch(`${API_URL}/system-status`, { 
          method: 'GET',
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        });
        console.log('🚑 Backend health check:', response.status);
      } catch (error) {
        console.error('🚨 Backend connectivity failed:', error);
      }
    };
    testBackend();
  }, [authToken]);

  // Use SignalR context for persistent real-time connection
  const {
    connectionState,
    isConnected,
    realtimeMetrics,
    consolidatedDashboardData: contextDashboardData,
    triggerSystemUpdate,
    joinDashboardUpdates,
    leaveDashboardUpdates,
    requestDashboardData
  } = useSignalRContext();

  // State for consolidated dashboard data - now using SignalR real-time updates
  // Initialize loading to false if we already have data from context
  const [consolidatedData, setConsolidatedData] = useState<ConsolidatedDashboardData | null>(contextDashboardData);
  const [dashboardDataLoading, setDashboardDataLoading] = useState(!contextDashboardData);
  const [dashboardDataError, setDashboardDataError] = useState<string | null>(null);

  // Legacy state for backward compatibility with existing components
  const [securityEventsData, setSecurityEventsData] = useState<{ events: SecurityEvent[], total: number }>({ events: [], total: 0 });
  const [systemStatus, setSystemStatus] = useState<SystemStatus[]>([]);
  const [threatScanner, setThreatScanner] = useState<ThreatScan[]>([]);
  const [scanInProgress, setScanInProgress] = useState(false);
  const [scanProgress, setScanProgress] = useState<any>(null);
  const [currentScanType, setCurrentScanType] = useState<string>('');

  // Update consolidated data when SignalR provides it
  useEffect(() => {
    if (contextDashboardData) {
      console.log('📡 Received consolidated dashboard data from context:', contextDashboardData);
      setConsolidatedData(contextDashboardData);
      setDashboardDataLoading(false);
      setDashboardDataError(null);
      setInitialLoad(false); // Important: Mark initial load as complete

      // Update legacy state for backward compatibility
      setSecurityEventsData({
        events: contextDashboardData.securityEvents.recentEvents.map(event => ({
          id: event.id,
          timestamp: event.timestamp,
          eventType: event.eventType,
          riskLevel: event.riskLevel as 'critical' | 'high' | 'medium' | 'low',
          source: event.source,
          machine: event.machine
        })),
        total: contextDashboardData.securityEvents.totalEvents
      });


      setSystemStatus(contextDashboardData.systemStatus.components.map(component => ({
        id: component.component,
        component: component.component,
        status: component.status.toLowerCase(),
        responseTime: component.responseTime,
        errorCount: component.status.toLowerCase() === 'healthy' ? 0 : 1,
        warningCount: 0,
        lastCheck: component.lastCheck,
        uptime: '0m',
        details: component.status
      })));

      setThreatScanner(contextDashboardData.threatScanner.recentScans.map(scan => ({
        id: scan.id,
        status: scan.status.toLowerCase(),
        threatsFound: scan.threatsFound,
        timestamp: scan.timestamp
      })));

      setLastRefresh(new Date());
      console.log('✅ Dashboard data updated from consolidated SignalR context');
    }
  }, [contextDashboardData]);

  // Join dashboard updates on connection and cleanup on unmount
  useEffect(() => {
    if (connectionState === 'Connected') {
      console.log('🔗 Dashboard: Joining dashboard updates and requesting initial data');
      joinDashboardUpdates();
      requestDashboardData(timeRange);
    }

    // Cleanup function runs when component unmounts or dependencies change
    return () => {
      if (connectionState === 'Connected') {
        console.log('🔌 Dashboard: Leaving dashboard updates');
        leaveDashboardUpdates();
      }
    };
  }, [connectionState, timeRange, joinDashboardUpdates, leaveDashboardUpdates, requestDashboardData]);

  // Check scan progress function
  const checkScanProgress = async () => {
    try {
      // Only log when scan is in progress to reduce noise
      if (scanInProgress) {
        console.log('🔍 Checking scan progress...');
      }
      const response = await fetch('http://localhost:5000/api/threat-scanner/progress', {
        headers: {
          'Authorization': `Bearer ${authToken}`,
        },
      });

      if (response.ok) {
        const data = await response.json();
        // Only log progress data when there's actual progress or status changes
        if (data.progress && scanInProgress) {
          console.log('📊 Progress API response data:', data);
        }

        if (data.progress) {
          console.log('✅ Progress data found:', data.progress);
          setScanInProgress(true);
          setScanProgress(data.progress);

          // Check if scan completed
          if (data.progress.status === 'Completed' ||
              data.progress.status === 'CompletedWithThreats' ||
              data.progress.status === 'Failed' ||
              data.progress.status === 'Cancelled') {
            console.log('🏁 Scan completed with status:', data.progress.status);
            setScanInProgress(false);
            setCurrentScanType('');
            setRefreshing(true); // Refresh dashboard data
            return false; // Stop polling
          }
          return true; // Continue polling
        } else {
          // Only log when transitioning from in-progress to no-progress
          if (scanInProgress) {
            console.log('❌ No progress data in response');
          }
          setScanInProgress(false);
          setScanProgress(null);
          setCurrentScanType('');
          return false;
        }
      } else {
        console.log('❌ Progress API failed with status:', response.status);
      }
      return false;
    } catch (error) {
      console.error('❌ Error checking scan progress:', error);
      return false;
    }
  };

  // Threat Scanner handlers
  const handleQuickScan = async () => {
    try {
      const response = await fetch('http://localhost:5000/api/threat-scanner/quick-scan', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json',
        },
      });

      if (response.ok) {
        notify('Quick scan started successfully', { type: 'success' });
        setScanInProgress(true);
        setCurrentScanType('Quick Scan');
        checkScanProgress(); // Start checking progress immediately
      } else {
        notify('Failed to start quick scan', { type: 'error' });
      }
    } catch (error) {
      notify('Error starting quick scan', { type: 'error' });
    }
  };

  const handleFullScan = async () => {
    console.log('🚀 Full scan button clicked!'); // Debug log
    try {
      const response = await fetch('http://localhost:5000/api/threat-scanner/full-scan?async=true', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json',
        },
      });

      console.log('🔄 Full scan response:', response.status); // Debug log
      if (response.ok) {
        notify('Full scan started successfully', { type: 'success' });
        console.log('✅ Setting scan in progress and checking initial progress...');
        setScanInProgress(true);
        setCurrentScanType('Full Scan');

        // Wait a moment for the backend to initialize the scan, then check progress
        setTimeout(() => {
          console.log('⏰ Delayed progress check (after 1 second)...');
          checkScanProgress();
        }, 1000);
      } else {
        notify('Failed to start full scan', { type: 'error' });
      }
    } catch (error) {
      console.error('❌ Full scan error:', error); // Debug log
      notify('Error starting full scan', { type: 'error' });
    }
  };

  // Check for scan on initial load and poll for scan progress
  const [hasCheckedInitialScan, setHasCheckedInitialScan] = useState(false);

  useEffect(() => {
    // Only check for existing scan once on initial load
    if (!hasCheckedInitialScan && !scanInProgress && authToken) {
      console.log('🔍 Initial scan progress check...');
      checkScanProgress();
      setHasCheckedInitialScan(true);
    }

    let interval: NodeJS.Timeout | null = null;

    if (scanInProgress) {
      console.log('⏱️ Starting progress polling (every 2 seconds)...');
      interval = setInterval(async () => {
        const shouldContinue = await checkScanProgress();
        if (!shouldContinue && interval) {
          console.log('🛑 Stopping progress polling');
          clearInterval(interval);
        }
      }, 2000); // Poll every 2 seconds
    }

    return () => {
      if (interval) {
        console.log('🧹 Cleaning up progress polling interval');
        clearInterval(interval);
      }
    };
  }, [scanInProgress, authToken, hasCheckedInitialScan]);


  // Use dashboard data caching context (kept for compatibility with other components)
  const {
    getCachedData,
    setCachedData,
    isCacheValid,
    clearCache
  } = useDashboardDataContext();

  const [lastRealTimeUpdate, setLastRealTimeUpdate] = useState<Date | null>(null);

  // Enhanced polling system as SignalR fallback
  const [pollingEnabled, setPollingEnabled] = useState(true);
  const [pollingInterval, setPollingInterval] = useState<NodeJS.Timeout | null>(null);

  // Connection state is already provided by the consolidated SignalR context

  // Update last real-time update when metrics change
  useEffect(() => {
    if (realtimeMetrics) {
      setLastRealTimeUpdate(new Date());

      // Auto-update derived system status data from real-time metrics
      if (realtimeMetrics.health?.components) {
        const statusArray = Object.entries(realtimeMetrics.health.components).map(([component, health]) => ({
          id: component,
          component,
          status: health.isHealthy ? 'healthy' : 'error',
          responseTime: health.responseTimeMs || 0,
          errorCount: health.isHealthy ? 0 : 1,
          warningCount: 0,
          lastCheck: health.lastCheck || new Date().toISOString(),
          uptime: realtimeMetrics.health.systemUptime || '0m',
          details: health.details || health.status
        }));
        setSystemStatus(statusArray);
      }
    }
  }, [realtimeMetrics]);
  
  // Enhanced polling system for real-time updates
  const fetchSystemMetrics = useCallback(async () => {
    if (!authToken || isConnected) return; // Skip if no auth or SignalR is working
    
    try {
      console.log('🔄 Polling system metrics...');
      const response = await fetch(`${API_URL}/system-status`, {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });
      
      if (response.ok) {
        const data = await response.json();
        // Simulate SignalR system metrics format
        const mockSystemMetrics: SystemMetricsUpdate = {
          timestamp: new Date().toISOString(),
          health: {
            isHealthy: data.data?.every((item: any) => item.isHealthy) ?? true,
            totalComponents: data.data?.length ?? 0,
            healthyComponents: data.data?.filter((item: any) => item.isHealthy)?.length ?? 0,
            systemUptime: data.data?.[0]?.uptime ?? 'Unknown',
            components: data.data?.reduce((acc: any, item: any) => {
              acc[item.component] = {
                isHealthy: item.isHealthy,
                status: item.status,
                lastCheck: item.lastCheck,
                responseTimeMs: item.responseTime,
                details: item.details
              };
              return acc;
            }, {}) ?? {}
          },
          performance: {
            cpuUsagePercent: Math.random() * 100, // Mock data - replace with real API
            memoryUsageMB: 1024 + Math.random() * 512,
            threadCount: 50 + Math.floor(Math.random() * 20),
            handleCount: 1000 + Math.floor(Math.random() * 500),
            eventProcessing: {
              eventsPerSecond: Math.floor(Math.random() * 10),
              totalEventsProcessed: Math.floor(Math.random() * 1000),
              queuedEvents: Math.floor(Math.random() * 50),
              failedEvents: Math.floor(Math.random() * 5)
            },
            vectorOperations: {
              vectorsPerSecond: Math.floor(Math.random() * 5),
              averageEmbeddingTime: '50ms',
              averageUpsertTime: '25ms',
              averageSearchTime: '10ms',
              batchOperations: Math.floor(Math.random() * 10)
            }
          },
          threatIntelligence: {
            isEnabled: true,
            services: {},
            totalQueries: 0,
            cacheHits: 0,
            cacheHitRate: 0,
            lastQuery: ''
          },
          cache: {
            embedding: { totalEntries: 0, hits: 0, misses: 0, hitRate: 0, memoryUsageMB: 0 },
            threatIntelligence: { totalHashes: 0, cachedResults: 0, cacheUtilization: 0, oldestEntry: '', expiredEntries: 0 },
            general: { totalMemoryUsageMB: 256, activeCaches: 1, memoryPressure: 0, evictedEntries: 0 }
          },
          activeScans: {
            hasActiveScan: false,
            queuedScans: 0,
            recentScans: []
          }
        };
        
        // Direct state updates to avoid callback dependency
        // Note: realtimeMetrics is now managed by SignalR context
        setLastRealTimeUpdate(new Date());
        
        // Update system status from metrics
        if (mockSystemMetrics.health?.components) {
          const statusArray = Object.entries(mockSystemMetrics.health.components).map(([component, health]) => ({
            id: component,
            component,
            status: health.isHealthy ? 'healthy' : 'error',
            responseTime: health.responseTimeMs || 0,
            errorCount: health.isHealthy ? 0 : 1,
            warningCount: 0,
            lastCheck: health.lastCheck || new Date().toISOString(),
            uptime: mockSystemMetrics.health.systemUptime || '0m',
            details: health.details || health.status
          }));
          setSystemStatus(statusArray);
        }
        console.log('✅ Polling update successful');
      } else {
        console.warn('⚠️ Polling failed:', response.status);
      }
    } catch (error) {
      console.error('❌ Polling error:', error);
    }
  }, [authToken, isConnected]);
  
  // Set up polling interval
  useEffect(() => {
    if (pollingEnabled && !isConnected && authToken) {
      console.log('📡 Starting enhanced polling mode (30s interval)');
      
      // Initial fetch
      fetchSystemMetrics();
      
      // Set up interval
      const interval = setInterval(fetchSystemMetrics, 30000); // 30 seconds
      setPollingInterval(interval);
      
      return () => {
        if (interval) {
          clearInterval(interval);
          console.log('🚫 Stopped polling');
        }
      };
    } else if (pollingInterval) {
      clearInterval(pollingInterval);
      setPollingInterval(null);
    }
  }, [pollingEnabled, isConnected, authToken]);

  // Fallback to REST API when SignalR is not available
  useEffect(() => {
    const fetchFallbackData = async () => {
      if (!authToken || connectionState === 'Connected') return;

      console.log('🔄 SignalR unavailable, using REST API fallback');
      setDashboardDataLoading(true);

      try {
        const response = await fetch(`${API_URL}/dashboarddata/consolidated?timeRange=${timeRange}`, {
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        });

        if (response.ok) {
          const data = await response.json();
          setConsolidatedData(data);
          setDashboardDataError(null);

          // Update legacy state for backward compatibility
          setSecurityEventsData({
            events: data.securityEvents.recentEvents.map((event: any) => ({
              id: event.id,
              timestamp: event.timestamp,
              eventType: event.eventType,
              riskLevel: event.riskLevel as 'critical' | 'high' | 'medium' | 'low',
              source: event.source,
              machine: event.machine
            })),
            total: data.securityEvents.totalEvents
          });


          setSystemStatus(data.systemStatus.components.map((component: any) => ({
            id: component.component,
            component: component.component,
            status: component.status.toLowerCase(),
            responseTime: component.responseTime,
            errorCount: component.status.toLowerCase() === 'healthy' ? 0 : 1,
            warningCount: 0,
            lastCheck: component.lastCheck,
            uptime: '0m',
            details: component.status
          })));

          setThreatScanner(data.threatScanner.recentScans.map((scan: any) => ({
            id: scan.id,
            status: scan.status.toLowerCase(),
            threatsFound: scan.threatsFound,
            timestamp: scan.timestamp
          })));

          console.log('✅ Fallback data loaded successfully');
        } else {
          throw new Error(`REST API failed: ${response.status}`);
        }
      } catch (error) {
        console.error('❌ Fallback data fetch failed:', error);
        setDashboardDataError(error instanceof Error ? error.message : 'Failed to fetch data');
      } finally {
        setDashboardDataLoading(false);
        setInitialLoad(false);
      }
    };

    fetchFallbackData();
  }, [authToken, timeRange, connectionState]);

  // Extract derived data
  const securityEvents = securityEventsData?.events || [];
  const securityEventsTotal = securityEventsData?.total || 0;
  
  // Debug logging for consolidated data
  useEffect(() => {
    if (consolidatedData) {
      console.group('📊 Consolidated Dashboard Data Status');
      console.log('Security Events:', {
        totalEvents: consolidatedData.securityEvents.totalEvents,
        recentEventsCount: consolidatedData.securityEvents.recentEvents.length,
        riskLevelCounts: consolidatedData.securityEvents.riskLevelCounts
      });
      console.log('System Status:', {
        totalComponents: consolidatedData.systemStatus.totalComponents,
        healthyComponents: consolidatedData.systemStatus.healthyComponents,
        componentsCount: consolidatedData.systemStatus.components.length
      });
      console.log('Threat Scanner:', {
        totalScans: consolidatedData.threatScanner.totalScans,
        activeScans: consolidatedData.threatScanner.activeScans,
        threatsFound: consolidatedData.threatScanner.threatsFound
      });
      console.log('Last Updated:', consolidatedData.lastUpdated);
      console.groupEnd();
    }
  }, [consolidatedData]);
  
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

  // Refresh all data - now uses consolidated SignalR or REST API fallback
  const handleRefresh = async () => {
    setRefreshing(true);
    setLastRefresh(new Date());

    try {
      if (connectionState === 'Connected') {
        // Request fresh data via SignalR
        console.log('📡 Requesting fresh dashboard data via SignalR...');
        await requestDashboardData(timeRange);
        notify('Dashboard data refreshed via SignalR', { type: 'success' });
      } else {
        // Fallback to REST API
        console.log('🔄 Refreshing via REST API fallback...');
        const response = await fetch(`${API_URL}/dashboarddata/refresh`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        });

        if (response.ok) {
          // Re-fetch data after cache invalidation
          const dataResponse = await fetch(`${API_URL}/dashboarddata/consolidated?timeRange=${timeRange}`, {
            headers: {
              'Authorization': `Bearer ${authToken}`,
              'Content-Type': 'application/json'
            }
          });

          if (dataResponse.ok) {
            const data = await dataResponse.json();
            setConsolidatedData(data);
            notify('Dashboard data refreshed', { type: 'success' });
          }
        } else {
          throw new Error('Failed to refresh data');
        }
      }
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

  // Chart data - memoized to prevent unnecessary re-renders and use consolidated data
  const securityEventsChartData = useMemo(() => {
    if (consolidatedData?.securityEvents.riskLevelCounts) {
      return Object.entries(consolidatedData.securityEvents.riskLevelCounts)
        .map(([name, value]) => ({ name, value }));
    }

    // Fallback to legacy data processing
    if (!securityEvents || securityEvents.length === 0) return [];
    const riskCounts = securityEvents.reduce((acc: any, event) => {
      const risk = event.riskLevel || event.severity || 'unknown';
      acc[risk] = (acc[risk] || 0) + 1;
      return acc;
    }, {});
    return Object.entries(riskCounts).map(([name, value]) => ({ name, value }));
  }, [consolidatedData, securityEvents]);

  const systemHealthChartData = useMemo(() => {
    if (consolidatedData?.systemStatus) {
      const healthy = consolidatedData.systemStatus.healthyComponents;
      const total = consolidatedData.systemStatus.totalComponents;
      const unhealthy = total - healthy;

      const result = [];
      if (healthy > 0) result.push({ name: 'Healthy', value: healthy });
      if (unhealthy > 0) result.push({ name: 'Unhealthy', value: unhealthy });
      return result;
    }

    // Fallback to legacy data processing
    if (!systemStatus || systemStatus.length === 0) return [];
    const healthData = systemStatus.reduce((acc: any, status) => {
      const health = status.status === 'healthy' ? 'Healthy' :
                    status.status === 'warning' ? 'Warning' : 'Error';
      acc[health] = (acc[health] || 0) + 1;
      return acc;
    }, {});
    return Object.entries(healthData).map(([name, value]) => ({ name, value }));
  }, [consolidatedData, systemStatus]);

  const threatScanChartData = useMemo(() => {
    if (consolidatedData?.threatScanner.recentScans) {
      return consolidatedData.threatScanner.recentScans.slice(0, 10).map((scan, index) => ({
        name: `${scan.scanType} ${index + 1}`,
        threats: scan.threatsFound,
        status: scan.status
      }));
    }

    // Fallback to legacy data processing
    if (!threatScanner || threatScanner.length === 0) return [];
    return threatScanner.map((scan, index) => ({
      name: `Scan ${index + 1}`,
      threats: scan.threatsFound || 0,
      status: scan.status
    }));
  }, [consolidatedData, threatScanner]);

  // Stable label renderer for pie chart to prevent blinking
  const renderPieLabel = useCallback(({ name, percent }: any) => {
    return `${name} ${percent ? (percent * 100).toFixed(0) : 0}%`;
  }, []);

  // Remove blocking initial overlay; allow structure to render immediately with skeletons

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
          {/* Connection Status Indicator */}
          <MuiTooltip title={`Dashboard real-time: ${connectionState}${lastRealTimeUpdate ? ` | Last update: ${lastRealTimeUpdate.toLocaleTimeString()}` : ''}`}>
            <Box display="flex" alignItems="center" gap={1}>
              {connectionState === 'Connected' ? (
                <ConnectedIcon color="success" fontSize="small" />
              ) : connectionState === 'Connecting' || connectionState === 'Reconnecting' ? (
                <CircularProgress size={16} />
              ) : (
                <DisconnectedIcon color="error" fontSize="small" />
              )}
              <Typography variant="caption" color="text.secondary">
                {isConnected ? 'Live' : 'Offline'}
              </Typography>
            </Box>
          </MuiTooltip>
          
          {/* Refresh Button - now adaptive */}
          <Button
            startIcon={<RefreshIcon />}
            onClick={handleRefresh}
            disabled={refreshing}
            variant={isConnected ? "outlined" : "contained"}
            color={isConnected ? "primary" : "warning"}
          >
            {refreshing ? 'Refreshing...' : isConnected ? 'Update Now' : 'Manual Refresh'}
          </Button>
          
          <Typography variant="caption" color="text.secondary">
            Last updated: {(lastRealTimeUpdate || lastRefresh).toLocaleTimeString()}
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
            {dashboardDataLoading ? (
              <Box>
                <Skeleton variant="text" width="60%" height={48} sx={{ mb: 0.5 }} />
                <Skeleton variant="text" width="40%" />
              </Box>
            ) : dashboardDataError ? (
              <Typography color="error" variant="body2">
                Error: {dashboardDataError}
              </Typography>
            ) : (
              <Box>
                <Typography variant="h4" color="primary">
                  {consolidatedData?.securityEvents.totalEvents || securityEventsData.total}
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
            avatar={<HealthyIcon color="success" />}
            title="System Health"
            titleTypographyProps={{ variant: 'h6' }}
          />
          <CardContent>
            {dashboardDataLoading ? (
              <Box>
                <Skeleton variant="text" width="50%" height={48} sx={{ mb: 0.5 }} />
                <Skeleton variant="text" width="45%" />
              </Box>
            ) : dashboardDataError ? (
              <Typography color="error" variant="body2">
                Error: {dashboardDataError}
              </Typography>
            ) : (
              <Box>
                <Typography variant="h4" color="success.main">
                  {consolidatedData
                    ? `${consolidatedData.systemStatus.healthyComponents}/${consolidatedData.systemStatus.totalComponents}`
                    : (() => {
                        const healthyCount = systemStatus.filter(s => s.status && s.status.toLowerCase() === 'healthy').length;
                        return `${healthyCount}/${systemStatus.length}`;
                      })()
                  }
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Healthy Services
                </Typography>
              </Box>
            )}
          </CardContent>
        </Card>

        <Card
          sx={{
            minWidth: 200,
            flex: 1,
            cursor: 'pointer',
            '&:hover': {
              boxShadow: 3,
              backgroundColor: 'action.hover'
            }
          }}
          onClick={() => navigate('/threat-scanner')}
        >
          <CardHeader
            avatar={<ScannerIcon color="warning" />}
            title="Threat Scans"
            titleTypographyProps={{ variant: 'h6' }}
          />
          <CardContent>
            {dashboardDataLoading ? (
              <Box>
                <Skeleton variant="text" width="40%" height={48} sx={{ mb: 0.5 }} />
                <Skeleton variant="text" width="35%" />
              </Box>
            ) : dashboardDataError ? (
              <Typography color="error" variant="body2">
                Error: {dashboardDataError}
              </Typography>
            ) : (
              <Box>
                <Typography variant="h4" color="warning.main">
                  {consolidatedData?.threatScanner.totalScans || threatScanner.length}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Total Scans
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
            <Box display="flex" gap={3} alignItems="center">
              {/* Pie Chart - Left Side */}
              <Box width="40%" height={300} flexShrink={0}>
                {dashboardDataLoading ? (
                  <Skeleton
                    variant="circular"
                    width={200}
                    height={200}
                    sx={{ mx: 'auto', mt: 3 }}
                  />
                ) : securityEventsChartData.length > 0 ? (
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie
                        data={securityEventsChartData}
                        cx="50%"
                        cy="50%"
                        labelLine={false}
                        label={renderPieLabel}
                        outerRadius={80}
                        fill="#8884d8"
                        dataKey="value"
                        isAnimationActive={false}
                      >
                        {securityEventsChartData.map((entry, index) => (
                          <Cell key={`cell-${entry.name}`} fill={COLORS[index % COLORS.length]} />
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

              {/* Risk Level Details - Right Side */}
              <Box flex={1}>
                {!dashboardDataLoading && securityEventsChartData.length > 0 && (
                  <Stack spacing={1.5}>
                    {[...securityEventsChartData]
                      .sort((a, b) => {
                        const order = { critical: 0, high: 1, medium: 2, low: 3, unknown: 4 };
                        return (order[a.name.toLowerCase() as keyof typeof order] ?? 999) -
                               (order[b.name.toLowerCase() as keyof typeof order] ?? 999);
                      })
                      .map((entry) => {
                        const value = typeof entry.value === 'number' ? entry.value : 0;
                        const total = securityEventsChartData.reduce((sum, e) => sum + (typeof e.value === 'number' ? e.value : 0), 0);
                        const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
                        const riskLevelKey = entry.name.toLowerCase() as keyof typeof riskLevelColors;
                        const color = riskLevelColors[riskLevelKey] || '#757575';

                        return (
                          <Box
                            key={entry.name}
                            display="flex"
                            justifyContent="space-between"
                            alignItems="center"
                            sx={{
                              py: 1,
                              px: 2,
                              borderRadius: 1,
                              bgcolor: 'action.hover'
                            }}
                          >
                            <Box display="flex" alignItems="center" gap={1.5}>
                              <Box
                                sx={{
                                  width: 12,
                                  height: 12,
                                  borderRadius: '50%',
                                  bgcolor: color
                                }}
                              />
                              <Typography variant="body2" fontWeight={500} sx={{ textTransform: 'capitalize' }}>
                                {entry.name}
                              </Typography>
                            </Box>
                            <Typography variant="body2" fontWeight={600}>
                              {value.toLocaleString()} ({percentage}%)
                            </Typography>
                          </Box>
                        );
                      })}
                  </Stack>
                )}
              </Box>
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
              systemStatusData={systemStatus}
              threatScannerData={threatScanner}
              securityEventsError={dashboardDataError}
              systemStatusError={dashboardDataError}
              threatScannerError={dashboardDataError}
            />
          </CardContent>
        </Card>

        <Card
          sx={{
            flex: 1,
            cursor: 'pointer',
            '&:hover': {
              boxShadow: 3,
              backgroundColor: 'action.hover'
            }
          }}
          onClick={() => navigate('/yara-rules')}
        >
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
            <RealtimeSystemMetrics 
              metrics={realtimeMetrics} 
              connectionStatus={connectionState}
            />
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
          <CardHeader title="Performance" />
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

    </Box>
  );
});

