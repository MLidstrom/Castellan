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
  Stack
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
  Assessment as ComplianceIcon,
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
import { YaraDashboardWidgetSimple } from './YaraDashboardWidgetSimple';
import { YaraSummaryCard } from './YaraSummaryCard';

// Import SignalR context for persistent connection
import { useSignalRContext } from '../contexts/SignalRContext';
import { SystemMetricsUpdate } from '../hooks/useSignalR';

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
  const [initialLoad, setInitialLoad] = useState(true);
  const notify = useNotify();
  const navigate = useNavigate();

  const authToken = localStorage.getItem('auth_token');

  // Debug authentication
  console.log('🔐 Auth Token:', authToken ? 'Present (' + authToken.substring(0, 20) + '...)' : 'Missing');
  
  if (!authToken) {
    console.warn('⚠️ No authentication token found! API calls may fail.');
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
  const [scanInProgress, setScanInProgress] = useState(false);
  const [scanProgress, setScanProgress] = useState<any>(null);
  const [currentScanType, setCurrentScanType] = useState<string>('');

  // Check scan progress function
  const checkScanProgress = async () => {
    try {
      console.log('🔍 Checking scan progress...');
      const response = await fetch('http://localhost:5000/api/threat-scanner/progress', {
        headers: {
          'Authorization': `Bearer ${authToken}`,
        },
      });

      console.log('📡 Progress API response status:', response.status);
      if (response.ok) {
        const data = await response.json();
        console.log('📊 Progress API response data:', data);

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
          console.log('❌ No progress data in response');
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
    console.log('🔄 useEffect triggered - scanInProgress:', scanInProgress, 'authToken:', !!authToken);

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
        console.log('🔄 Polling scan progress...');
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

  // Use SignalR context for persistent real-time connection
  const {
    connectionState,
    isConnected: signalRConnected,
    realtimeMetrics,
    triggerSystemUpdate
  } = useSignalRContext();

  // Use dashboard data caching context
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

  // Set connection state based on SignalR or polling
  const isConnected = signalRConnected || pollingEnabled;

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
        setSystemStatusLoading(false);
        setSystemStatusError(null);
      }
    }
  }, [realtimeMetrics]);
  
  // Enhanced polling system for real-time updates
  const fetchSystemMetrics = useCallback(async () => {
    if (!authToken || signalRConnected) return; // Skip if no auth or SignalR is working
    
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
          setSystemStatusLoading(false);
          setSystemStatusError(null);
        }
        console.log('✅ Polling update successful');
      } else {
        console.warn('⚠️ Polling failed:', response.status);
      }
    } catch (error) {
      console.error('❌ Polling error:', error);
    }
  }, [authToken, signalRConnected]);
  
  // Set up polling interval
  useEffect(() => {
    if (pollingEnabled && !signalRConnected && authToken) {
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
  }, [pollingEnabled, signalRConnected, authToken]);

  // Track previous timeRange to detect changes
  const prevTimeRangeRef = useRef(timeRange);

  // ⚡ Optimized: Fetch all dashboard data with caching
  useEffect(() => {
    const fetchAllDashboardData = async () => {
      if (!authToken) return;

      // Clear cache if timeRange changed (new filters need fresh data)
      if (prevTimeRangeRef.current !== timeRange && timeRange !== 'refresh-trigger') {
        console.log(`🔄 Time range changed from ${prevTimeRangeRef.current} to ${timeRange} - clearing cache`);
        clearCache();
        prevTimeRangeRef.current = timeRange;
      }

      // Check if we have valid cached data first (only if timeRange didn't change)
      const cachedData = getCachedData();
      if (cachedData && isCacheValid(30000) && timeRange !== 'refresh-trigger') { // Cache valid for 30 seconds
        console.log('📦 Using cached dashboard data');

        // Set data from cache
        setSecurityEventsData({
          events: cachedData.securityEvents || [],
          total: cachedData.securityEvents?.length || 0
        });
        setComplianceReports(cachedData.complianceReports || []);
        setSystemStatus(cachedData.systemStatus || []);
        setThreatScanner(cachedData.threatScanner || []);

        // Set loading states to false immediately
        setSecurityEventsLoading(false);
        setComplianceReportsLoading(false);
        setSystemStatusLoading(false);
        setThreatScannerLoading(false);
        setInitialLoad(false);

        // Clear any previous errors
        setSecurityEventsError(null);
        setComplianceReportsError(null);
        setSystemStatusError(null);
        setThreatScannerError(null);

        console.log('✅ Dashboard loaded from cache');
        return;
      }

      console.log('🚀 Fetching Dashboard Data in Parallel...');

      // Set all loading states to true
      setSecurityEventsLoading(true);
      setComplianceReportsLoading(true);
      setSystemStatusLoading(true);
      setThreatScannerLoading(true);

      // Clear previous errors
      setSecurityEventsError(null);
      setComplianceReportsError(null);
      setSystemStatusError(null);
      setThreatScannerError(null);

      const headers = {
        'Authorization': `Bearer ${authToken}`,
        'Content-Type': 'application/json'
      };

      try {
        // Execute all API calls in parallel
        const [
          securityEventsResponse,
          complianceReportsResponse,
          systemStatusResponse,
          threatScannerResponse
        ] = await Promise.all([
          fetch(`${API_URL}/security-events?sort=timestamp&order=desc`, { headers }),
          fetch(`${API_URL}/compliance-reports?sort=generated&order=desc`, { headers }),
          fetch(`${API_URL}/system-status`, { headers }),
          fetch(`${API_URL}/threat-scanner?sort=timestamp&order=desc`, { headers })
        ]);

        console.log('📡 All API Response Status:', {
          securityEvents: securityEventsResponse.status,
          complianceReports: complianceReportsResponse.status,
          systemStatus: systemStatusResponse.status,
          threatScanner: threatScannerResponse.status
        });

        // Initialize data containers for caching
        let securityEventsResult: any[] = [];
        let complianceReportsResult: any[] = [];
        let systemStatusResult: any[] = [];
        let threatScannerResult: any[] = [];

        // Process Security Events
        try {
          if (securityEventsResponse.ok) {
            const securityData = await securityEventsResponse.json();
            securityEventsResult = securityData.data || securityData || [];
            const securityResult = {
              events: securityEventsResult,
              total: securityData.total || 0
            };
            console.log('✅ Security Events loaded:', securityResult.events.length);
            setSecurityEventsData(securityResult);
          } else {
            throw new Error(`Security Events HTTP ${securityEventsResponse.status}`);
          }
        } catch (error) {
          console.error('❌ Security Events Failed:', error);
          setSecurityEventsError(error instanceof Error ? error.message : 'Failed to fetch');
        }

        // Process Compliance Reports
        try {
          if (complianceReportsResponse.ok) {
            const complianceData = await complianceReportsResponse.json();
            complianceReportsResult = complianceData.data || complianceData || [];
            console.log('✅ Compliance Reports loaded:', complianceReportsResult.length);
            setComplianceReports(complianceReportsResult);
          } else {
            throw new Error(`Compliance Reports HTTP ${complianceReportsResponse.status}`);
          }
        } catch (error) {
          console.error('❌ Compliance Reports Failed:', error);
          setComplianceReportsError(error instanceof Error ? error.message : 'Failed to fetch');
        }

        // Process System Status
        try {
          if (systemStatusResponse.ok) {
            const systemData = await systemStatusResponse.json();
            systemStatusResult = systemData.data || systemData || [];
            console.log('✅ System Status loaded:', systemStatusResult.length);
            setSystemStatus(systemStatusResult);
          } else {
            throw new Error(`System Status HTTP ${systemStatusResponse.status}`);
          }
        } catch (error) {
          console.error('❌ System Status Failed:', error);
          setSystemStatusError(error instanceof Error ? error.message : 'Failed to fetch');
        }

        // Process Threat Scanner
        try {
          if (threatScannerResponse.ok) {
            const threatData = await threatScannerResponse.json();
            threatScannerResult = threatData.data || threatData || [];
            console.log('✅ Threat Scanner loaded:', threatScannerResult.length);
            setThreatScanner(threatScannerResult);
          } else {
            throw new Error(`Threat Scanner HTTP ${threatScannerResponse.status}`);
          }
        } catch (error) {
          console.error('❌ Threat Scanner Failed:', error);
          setThreatScannerError(error instanceof Error ? error.message : 'Failed to fetch');
        }

        // Cache the successfully fetched data
        setCachedData({
          securityEvents: securityEventsResult,
          complianceReports: complianceReportsResult,
          systemStatus: systemStatusResult,
          threatScanner: threatScannerResult,
          timestamp: Date.now()
        });
        console.log('💾 Dashboard data cached');

      } catch (error) {
        console.error('💥 Dashboard Data Fetch Failed:', error);
        // Set errors for all failed endpoints
        const errorMessage = error instanceof Error ? error.message : 'Network error';
        setSecurityEventsError(errorMessage);
        setComplianceReportsError(errorMessage);
        setSystemStatusError(errorMessage);
        setThreatScannerError(errorMessage);
      } finally {
        // Set all loading states to false
        setSecurityEventsLoading(false);
        setComplianceReportsLoading(false);
        setSystemStatusLoading(false);
        setThreatScannerLoading(false);
        setInitialLoad(false);
        console.log('⚡ Dashboard loading complete');
      }
    };

    fetchAllDashboardData();
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

  // Refresh all data - now integrated with SignalR and cache clearing
  const handleRefresh = async () => {
    setRefreshing(true);
    setLastRefresh(new Date());

    try {
      // Clear cache to force fresh data fetch
      clearCache();
      console.log('🗑️ Cleared dashboard cache for fresh data');

      if (isConnected) {
        // If SignalR is connected, trigger a real-time update
        console.log('📡 Triggering SignalR system update...');
        await triggerSystemUpdate();
        notify('Real-time data refresh triggered', { type: 'info' });

        // Also trigger useEffect to fetch fresh data
        const currentRange = timeRange;
        setTimeRange('refresh-trigger');
        setTimeout(() => setTimeRange(currentRange), 100);
      } else {
        // Fallback to manual API calls when SignalR is not connected
        console.log('🔄 SignalR disconnected - falling back to manual refresh');
        notify('Refreshing data manually (real-time unavailable)', { type: 'warning' });

        // Re-trigger the existing useEffect hooks by toggling timeRange briefly
        const currentRange = timeRange;
        setTimeRange('refresh-trigger');
        setTimeout(() => setTimeRange(currentRange), 100);
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

  // Chart data - memoized to prevent unnecessary re-renders
  const securityEventsChartData = useMemo(() => processSecurityEventsForChart(), [securityEvents]);
  const complianceChartData = useMemo(() => processComplianceData(), [complianceReports]);
  const systemHealthChartData = useMemo(() => processSystemHealthData(), [systemStatus]);
  const threatScanChartData = useMemo(() => processThreatScanData(), [threatScanner]);

  // Stable label renderer for pie chart to prevent blinking
  const renderPieLabel = useCallback(({ name, percent }: any) => {
    return `${name} ${percent ? (percent * 100).toFixed(0) : 0}%`;
  }, []);

  // Show loading overlay on initial load
  if (initialLoad) {
    return (
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          height: '50vh',
          gap: 2
        }}
      >
        <CircularProgress size={50} />
        <Typography variant="h6" color="text.secondary">
          Loading Dashboard...
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Fetching security data in parallel
        </Typography>
      </Box>
    );
  }

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
          <MuiTooltip title={`Real-time connection: ${connectionState}${lastRealTimeUpdate ? ` | Last update: ${lastRealTimeUpdate.toLocaleTimeString()}` : ''}`}>
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
                  {(() => {
                    const healthyCount = systemStatus.filter(s => s.status && s.status.toLowerCase() === 'healthy').length;
                    console.log('🔍 System Health Debug:', {
                      totalServices: systemStatus.length,
                      healthyCount,
                      statuses: systemStatus.map(s => s.status)
                    });
                    return `${healthyCount}/${systemStatus.length}`;
                  })()}
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
            <Box height={300}>
              {securityEventsChartData.length > 0 ? (
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

