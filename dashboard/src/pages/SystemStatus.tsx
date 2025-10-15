import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Activity,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Info,
  Server,
  Database,
  Shield,
  Cloud,
  Zap,
  HardDrive,
  RefreshCw,
  TrendingUp,
  Signal
} from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { LoadingSpinner } from '../components/LoadingSpinner';

interface SystemStatus {
  id: string;
  component: string;
  status: string;
  isHealthy: boolean;
  lastCheck: string | Date;
  responseTime: number;
  uptime: string;
  details: string;
  errorCount: number;
  warningCount: number;
}

// Helper functions
const getStatusColor = (status: string, isHealthy: boolean) => {
  if (isHealthy) return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200 border-green-300';
  
  switch (status?.toLowerCase()) {
    case 'error':
    case 'failed':
    case 'down': 
      return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200 border-red-300';
    case 'warning':
    case 'degraded': 
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200 border-yellow-300';
    default: 
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200 border-blue-300';
  }
};

const getStatusIcon = (status: string, isHealthy: boolean) => {
  if (isHealthy) return <CheckCircle className="h-4 w-4" />;
  
  switch (status?.toLowerCase()) {
    case 'error':
    case 'failed':
    case 'down': 
      return <XCircle className="h-4 w-4" />;
    case 'warning':
    case 'degraded': 
      return <AlertTriangle className="h-4 w-4" />;
    default: 
      return <Info className="h-4 w-4" />;
  }
};

const getComponentIcon = (component: string) => {
  const comp = component?.toLowerCase() || '';
  if (comp.includes('qdrant') || comp.includes('vector') || comp.includes('database')) 
    return <Database className="h-5 w-5" />;
  if (comp.includes('ollama') || comp.includes('llm') || comp.includes('ai')) 
    return <Zap className="h-5 w-5" />;
  if (comp.includes('security') || comp.includes('detector') || comp.includes('yara')) 
    return <Shield className="h-5 w-5" />;
  if (comp.includes('cloud') || comp.includes('connector') || comp.includes('threat')) 
    return <Cloud className="h-5 w-5" />;
  if (comp.includes('event') || comp.includes('log') || comp.includes('collector')) 
    return <Activity className="h-5 w-5" />;
  if (comp.includes('storage') || comp.includes('disk')) 
    return <HardDrive className="h-5 w-5" />;
  if (comp.includes('performance') || comp.includes('monitor')) 
    return <TrendingUp className="h-5 w-5" />;
  return <Server className="h-5 w-5" />;
};

const getResponseTimeColor = (responseTime: number) => {
  if (responseTime <= 50) return 'text-green-600 dark:text-green-400';
  if (responseTime <= 150) return 'text-blue-600 dark:text-blue-400';
  if (responseTime <= 500) return 'text-yellow-600 dark:text-yellow-400';
  return 'text-red-600 dark:text-red-400';
};

const getResponseTimeLabel = (responseTime: number) => {
  if (responseTime <= 50) return 'Excellent';
  if (responseTime <= 150) return 'Good';
  if (responseTime <= 500) return 'Fair';
  return 'Poor';
};

const getUptimeColor = (uptime: string) => {
  const percentage = parseFloat(uptime.replace('%', ''));
  if (percentage >= 99) return 'text-green-600 dark:text-green-400';
  if (percentage >= 95) return 'text-blue-600 dark:text-blue-400';
  if (percentage >= 90) return 'text-yellow-600 dark:text-yellow-400';
  return 'text-red-600 dark:text-red-400';
};

export function SystemStatusPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!loading && !token) {
      navigate('/login');
    }
  }, [token, loading, navigate]);

  const statusQuery = useQuery({
    queryKey: ['system-status'],
    queryFn: async () => {
      const response = await fetch('/api/system-status', {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error('Failed to fetch system status');
      const data = await response.json();
      console.log('[SystemStatus] Raw API response:', data);
      
      // Handle different response formats
      const statusData = data.data || data;
      
      // Ensure we have an array
      if (Array.isArray(statusData)) {
        return statusData;
      } else if (Array.isArray(data)) {
        return data;
      } else {
        console.warn('[SystemStatus] Response is not an array:', statusData);
        return [];
      }
    },
    enabled: !loading && !!token,
    refetchInterval: 30000, // Auto-refresh every 30 seconds
  });

  const statuses: SystemStatus[] = Array.isArray(statusQuery.data) ? statusQuery.data : [];

  // Calculate summary statistics
  const summary = {
    total: statuses.length,
    healthy: statuses.filter(s => s.isHealthy).length,
    warning: statuses.filter(s => s.status?.toLowerCase() === 'warning' || s.warningCount > 0).length,
    error: statuses.filter(s => !s.isHealthy && s.status?.toLowerCase() === 'error').length,
    avgResponseTime: statuses.length > 0
      ? Math.round(statuses.reduce((sum, s) => sum + s.responseTime, 0) / statuses.length)
      : 0
  };

  if (statusQuery.isLoading) {
    return <LoadingSpinner />;
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Header */}
      <div className="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div className="px-8 py-6 flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold text-gray-900 dark:text-white">System Status</h1>
            <p className="text-gray-600 dark:text-gray-400 mt-1">Platform health and component monitoring</p>
          </div>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 text-green-600 dark:text-green-400">
              <Signal className="h-5 w-5" />
              <span className="text-sm font-medium">Live Monitoring</span>
            </div>
            <button
              onClick={() => statusQuery.refetch()}
              disabled={statusQuery.isRefetching}
              className="px-3 py-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 flex items-center gap-2"
            >
              <RefreshCw className={`h-4 w-4 ${statusQuery.isRefetching ? 'animate-spin' : ''}`} />
              Refresh
            </button>
          </div>
        </div>
      </div>

      <div className="p-8">
        {/* Summary Cards */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-6 mb-8">
          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <div className="flex items-center justify-between mb-2">
              <Server className="h-8 w-8 text-blue-600 dark:text-blue-400" />
              <span className="text-2xl font-bold text-gray-900 dark:text-white">{summary.total}</span>
            </div>
            <p className="text-sm text-gray-600 dark:text-gray-400">Total Components</p>
          </div>

          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <div className="flex items-center justify-between mb-2">
              <CheckCircle className="h-8 w-8 text-green-600 dark:text-green-400" />
              <span className="text-2xl font-bold text-green-600 dark:text-green-400">{summary.healthy}</span>
            </div>
            <p className="text-sm text-gray-600 dark:text-gray-400">Healthy</p>
          </div>

          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <div className="flex items-center justify-between mb-2">
              <AlertTriangle className="h-8 w-8 text-yellow-600 dark:text-yellow-400" />
              <span className="text-2xl font-bold text-yellow-600 dark:text-yellow-400">{summary.warning}</span>
            </div>
            <p className="text-sm text-gray-600 dark:text-gray-400">Warnings</p>
          </div>

          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <div className="flex items-center justify-between mb-2">
              <XCircle className="h-8 w-8 text-red-600 dark:text-red-400" />
              <span className="text-2xl font-bold text-red-600 dark:text-red-400">{summary.error}</span>
            </div>
            <p className="text-sm text-gray-600 dark:text-gray-400">Errors</p>
          </div>

          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <div className="flex items-center justify-between mb-2">
              <Zap className="h-8 w-8 text-purple-600 dark:text-purple-400" />
              <span className="text-2xl font-bold text-purple-600 dark:text-purple-400">{summary.avgResponseTime}ms</span>
            </div>
            <p className="text-sm text-gray-600 dark:text-gray-400">Avg Response</p>
          </div>
        </div>

        {/* Component List */}
        {statusQuery.isLoading ? (
          <div className="space-y-4">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="h-32 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 animate-pulse" />
            ))}
          </div>
        ) : statuses.length === 0 ? (
          <div className="text-center py-12 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700">
            <Server className="h-12 w-12 text-gray-400 mx-auto mb-4" />
            <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">No Components Found</h3>
            <p className="text-gray-600 dark:text-gray-300">System status data is not available</p>
          </div>
        ) : (
          <div className="space-y-4">
            {statuses.map((status) => (
              <div
                key={status.id}
                className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6 hover:shadow-lg transition-shadow"
              >
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    {/* Header */}
                    <div className="flex items-center gap-3 mb-3">
                      <div className="text-gray-600 dark:text-gray-400">
                        {getComponentIcon(status.component)}
                      </div>
                      <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                        {status.component}
                      </h3>
                      <span className={`px-3 py-1 rounded-full text-xs font-semibold border flex items-center gap-1 ${getStatusColor(status.status, status.isHealthy)}`}>
                        {getStatusIcon(status.status, status.isHealthy)}
                        {status.status}
                      </span>
                    </div>

                    {/* Details */}
                    <p className="text-sm text-gray-600 dark:text-gray-300 mb-4">
                      {status.details}
                    </p>

                    {/* Metrics */}
                    <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                      <div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">Last Check</span>
                        <p className="text-sm font-medium text-gray-900 dark:text-white mt-1">
                          {new Date(status.lastCheck).toLocaleTimeString()}
                        </p>
                      </div>

                      <div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">Response Time</span>
                        <p className={`text-sm font-semibold mt-1 ${getResponseTimeColor(status.responseTime)}`}>
                          {status.responseTime}ms
                          <span className="text-xs ml-1 font-normal">({getResponseTimeLabel(status.responseTime)})</span>
                        </p>
                      </div>

                      <div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">Uptime</span>
                        <p className={`text-sm font-semibold mt-1 ${getUptimeColor(status.uptime)}`}>
                          {status.uptime}
                        </p>
                      </div>

                      <div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">Errors</span>
                        <p className={`text-sm font-semibold mt-1 ${status.errorCount > 0 ? 'text-red-600 dark:text-red-400' : 'text-green-600 dark:text-green-400'}`}>
                          {status.errorCount}
                        </p>
                      </div>

                      <div>
                        <span className="text-xs text-gray-500 dark:text-gray-400">Warnings</span>
                        <p className={`text-sm font-semibold mt-1 ${status.warningCount > 0 ? 'text-yellow-600 dark:text-yellow-400' : 'text-green-600 dark:text-green-400'}`}>
                          {status.warningCount}
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
