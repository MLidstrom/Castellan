import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Api } from '../services/api';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { useSignalR } from '../contexts/SignalRContext';
import { SignalRStatus } from '../components/SignalRStatus';
import { LoadingSpinner } from '../components/LoadingSpinner';
import {
  Shield,
  PlayCircle,
  XCircle,
  CheckCircle,
  AlertTriangle,
  Clock,
  HardDrive,
  FileSearch,
  Activity,
  Loader2,
  Filter,
  Eye
} from 'lucide-react';

interface ThreatScan {
  id: string;
  scanType: string;
  status: string;
  startTime: string;
  endTime?: string;
  duration: number;
  filesScanned: number;
  directoriesScanned: number;
  bytesScanned: number;
  threatsFound: number;
  malwareDetected: number;
  backdoorsDetected: number;
  suspiciousFiles: number;
  riskLevel: string;
  summary: string;
  errorMessage?: string;
}

interface ScanProgress {
  scanId: string;
  status: string;
  filesScanned: number;
  totalEstimatedFiles: number;
  directoriesScanned: number;
  threatsFound: number;
  currentFile: string;
  currentDirectory: string;
  percentComplete: number;
  startTime: string;
  elapsedTime: string;
  estimatedTimeRemaining?: string;
  bytesScanned: number;
  scanPhase: string;
}

export function ThreatScannerPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { hub } = useSignalR();
  const [progressDialogOpen, setProgressDialogOpen] = useState(false);
  const [scanType, setScanType] = useState('');
  const [scanInProgress, setScanInProgress] = useState(false);
  const [selectedScanId, setSelectedScanId] = useState<string | null>(null);

  // Filter states
  const [filterScanType, setFilterScanType] = useState<string>('');
  const [filterStatus, setFilterStatus] = useState<string>('');
  const [filterRiskLevel, setFilterRiskLevel] = useState<string>('');

  useEffect(() => {
    if (!loading && !token) {
      navigate('/login');
    }
  }, [token, loading, navigate]);

  // Query for scan history
  const scansQuery = useQuery({
    queryKey: ['threat-scans'],
    queryFn: () => Api.getThreatScans(),
    enabled: !loading && !!token,
    refetchInterval: 5000, // Refresh every 5 seconds
  });

  // Query for current scan progress
  const progressQuery = useQuery({
    queryKey: ['scan-progress'],
    queryFn: () => Api.getScanProgress(),
    enabled: !loading && !!token,
    refetchInterval: 2000, // Poll every 2 seconds
  });

  // Check if scan is in progress
  useEffect(() => {
    if ((progressQuery.data as any)?.progress) {
      const status = (progressQuery.data as any).progress.status?.toLowerCase();
      setScanInProgress(status === 'running' || status === 'initializing');
      setScanType((progressQuery.data as any).progress.scanType || 'Scan');
    } else {
      setScanInProgress(false);
    }
  }, [progressQuery.data]);

  // Setup SignalR connection for real-time updates
  useEffect(() => {
    if (!hub) return;

    // Listen for scan progress updates
    hub.on('ScanProgressUpdate', () => {
      queryClient.invalidateQueries({ queryKey: ['scan-progress'] });
    });

    // Listen for scan completion
    hub.on('ScanCompleted', () => {
      queryClient.invalidateQueries({ queryKey: ['threat-scans'] });
      queryClient.invalidateQueries({ queryKey: ['scan-progress'] });
    });
  }, [hub, queryClient]);

  const handleQuickScan = async () => {
    try {
      await Api.startQuickScan();
      setScanType('Quick Scan');
      setProgressDialogOpen(true);
      queryClient.invalidateQueries({ queryKey: ['threat-scans'] });
      queryClient.invalidateQueries({ queryKey: ['scan-progress'] });
    } catch (error) {
      console.error('Failed to start quick scan:', error);
    }
  };

  const handleFullScan = async () => {
    try {
      await Api.startFullScan();
      setScanType('Full Scan');
      setProgressDialogOpen(true);
      queryClient.invalidateQueries({ queryKey: ['threat-scans'] });
      queryClient.invalidateQueries({ queryKey: ['scan-progress'] });
    } catch (error) {
      console.error('Failed to start full scan:', error);
    }
  };

  const handleCancelScan = async () => {
    try {
      await Api.cancelScan();
      queryClient.invalidateQueries({ queryKey: ['scan-progress'] });
      queryClient.invalidateQueries({ queryKey: ['threat-scans'] });
      setProgressDialogOpen(false);
    } catch (error) {
      console.error('Failed to cancel scan:', error);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return 'text-green-600 bg-green-50 dark:bg-green-900/20 dark:text-green-400';
      case 'completedwiththreats': return 'text-orange-600 bg-orange-50 dark:bg-orange-900/20 dark:text-orange-400';
      case 'running': return 'text-blue-600 bg-blue-50 dark:bg-blue-900/20 dark:text-blue-400';
      case 'failed': return 'text-red-600 bg-red-50 dark:bg-red-900/20 dark:text-red-400';
      case 'cancelled': return 'text-gray-600 bg-gray-50 dark:bg-gray-700/20 dark:text-gray-400';
      default: return 'text-gray-600 bg-gray-50 dark:bg-gray-700/20 dark:text-gray-400';
    }
  };

  const getStatusText = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return 'Clean';
      case 'completedwiththreats': return 'Findings Detected';
      case 'running': return 'Scanning';
      case 'failed': return 'Failed';
      case 'cancelled': return 'Cancelled';
      default: return status;
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return <CheckCircle className="h-4 w-4" />;
      case 'completedwiththreats': return <AlertTriangle className="h-4 w-4" />;
      case 'running': return <Activity className="h-4 w-4 animate-spin" />;
      case 'failed': return <XCircle className="h-4 w-4" />;
      case 'cancelled': return <XCircle className="h-4 w-4" />;
      default: return <Shield className="h-4 w-4" />;
    }
  };

  const getRiskColor = (level: string) => {
    switch (level?.toLowerCase()) {
      case 'critical': return 'text-red-600 dark:text-red-400';
      case 'high': return 'text-orange-600 dark:text-orange-400';
      case 'medium': return 'text-yellow-600 dark:text-yellow-400';
      case 'low': return 'text-green-600 dark:text-green-400';
      default: return 'text-gray-600 dark:text-gray-400';
    }
  };

  const formatDuration = (minutes: number) => {
    if (minutes < 1) return `${Math.round(minutes * 60)}s`;
    if (minutes < 60) return `${Math.round(minutes)}m`;
    return `${Math.round(minutes / 60)}h ${Math.round(minutes % 60)}m`;
  };

  const scans = (scansQuery.data as any)?.data || [];

  // Filter scans based on selected filters
  const filteredScans = scans.filter((scan: ThreatScan) => {
    if (filterScanType && scan.scanType !== filterScanType) return false;
    if (filterStatus && scan.status.toLowerCase() !== filterStatus.toLowerCase()) return false;
    if (filterRiskLevel && scan.riskLevel !== filterRiskLevel) return false;
    return true;
  });

  // Get selected scan for detail view
  const selectedScan = selectedScanId ? scans.find((s: ThreatScan) => s.id === selectedScanId) : null;

  if (scansQuery.isLoading) {
    return <LoadingSpinner />;
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Header */}
      <div className="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div className="px-8 py-6">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Threat Scanner</h1>
              <p className="text-gray-600 dark:text-gray-400 mt-1">Security scanning and malware detection</p>
            </div>
            <div className="flex items-center space-x-3">
              {scanInProgress && (
                <button
                  onClick={() => setProgressDialogOpen(true)}
                  className="flex items-center space-x-2 px-4 py-2 bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-400 rounded-lg hover:bg-blue-100 dark:hover:bg-blue-900/40 transition-colors"
                >
                  <Loader2 className="h-4 w-4 animate-spin" />
                  <span className="text-sm font-medium">{scanType} in progress...</span>
                </button>
              )}
              <SignalRStatus />
              <button
                onClick={handleQuickScan}
                disabled={scanInProgress}
                className="flex items-center space-x-2 px-4 py-2 border border-blue-600 text-blue-600 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-900/20 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <PlayCircle className="h-5 w-5" />
                <span className="font-medium">Quick Scan</span>
              </button>
              <button
                onClick={handleFullScan}
                disabled={scanInProgress}
                className="flex items-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <Shield className="h-5 w-5" />
                <span className="font-medium">Full Scan</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* Scan Progress Dialog */}
      {progressDialogOpen && (progressQuery.data as any)?.progress && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
          <div className="bg-white dark:bg-gray-800 rounded-xl shadow-2xl max-w-2xl w-full mx-4 max-h-[90vh] overflow-y-auto">
            <div className="p-6 border-b border-gray-200 dark:border-gray-700">
              <div className="flex items-center justify-between">
                <div className="flex items-center space-x-3">
                  <Shield className="h-6 w-6 text-blue-600" />
                  <h2 className="text-xl font-bold text-gray-900 dark:text-white">{scanType} Progress</h2>
                </div>
                <button onClick={() => setProgressDialogOpen(false)} className="text-gray-400 hover:text-gray-600">
                  <XCircle className="h-6 w-6" />
                </button>
              </div>
            </div>

            <div className="p-6 space-y-4">
              <ScanProgressComponent progress={(progressQuery.data as any).progress} onCancel={handleCancelScan} />
            </div>

            <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-end">
              <button
                onClick={() => setProgressDialogOpen(false)}
                className="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-900 dark:text-white rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
              >
                Hide
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Scan Detail Dialog */}
      {selectedScan && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
          <div className="bg-white dark:bg-gray-800 rounded-xl shadow-2xl max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto">
            <div className="p-6 border-b border-gray-200 dark:border-gray-700">
              <div className="flex items-center justify-between">
                <div className="flex items-center space-x-3">
                  <Shield className="h-6 w-6 text-blue-600" />
                  <h2 className="text-xl font-bold text-gray-900 dark:text-white">Scan Details</h2>
                </div>
                <button onClick={() => setSelectedScanId(null)} className="text-gray-400 hover:text-gray-600">
                  <XCircle className="h-6 w-6" />
                </button>
              </div>
            </div>

            <div className="p-6 space-y-6">
              <ScanDetailView scan={selectedScan} />
            </div>

            <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-end">
              <button
                onClick={() => setSelectedScanId(null)}
                className="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-900 dark:text-white rounded-lg hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Scan History */}
      <div className="p-8">
        <div className="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700">
          <div className="p-6 border-b border-gray-200 dark:border-gray-700">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-gray-900 dark:text-white">Scan History</h2>
              <div className="flex items-center space-x-2">
                <Filter className="h-5 w-5 text-gray-400" />
                <span className="text-sm text-gray-600 dark:text-gray-400">Filters</span>
              </div>
            </div>

            {/* Filter Controls */}
            <div className="mt-4 grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Scan Type</label>
                <select
                  value={filterScanType}
                  onChange={(e) => setFilterScanType(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                >
                  <option value="">All Types</option>
                  <option value="QuickScan">Quick Scan</option>
                  <option value="FullScan">Full Scan</option>
                  <option value="DirectoryScan">Directory Scan</option>
                  <option value="FileScan">File Scan</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Status</label>
                <select
                  value={filterStatus}
                  onChange={(e) => setFilterStatus(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                >
                  <option value="">All Statuses</option>
                  <option value="Completed">Clean</option>
                  <option value="CompletedWithThreats">Findings Detected</option>
                  <option value="Running">Scanning</option>
                  <option value="Failed">Failed</option>
                  <option value="Cancelled">Cancelled</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Risk Level</label>
                <select
                  value={filterRiskLevel}
                  onChange={(e) => setFilterRiskLevel(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                >
                  <option value="">All Levels</option>
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                  <option value="Critical">Critical</option>
                </select>
              </div>
            </div>
          </div>

          {scansQuery.isLoading ? (
            <div className="p-12 text-center">
              <Loader2 className="h-8 w-8 animate-spin mx-auto text-gray-400" />
              <p className="mt-4 text-gray-600 dark:text-gray-400">Loading scan history...</p>
            </div>
          ) : filteredScans.length === 0 ? (
            <div className="p-12 text-center">
              <Shield className="h-12 w-12 mx-auto text-gray-400" />
              <h3 className="mt-4 text-lg font-medium text-gray-900 dark:text-white">
                {scans.length === 0 ? 'No scan history yet' : 'No matching scans found'}
              </h3>
              <p className="mt-2 text-gray-600 dark:text-gray-400">
                {scans.length === 0 ? 'Start a security scan to check for threats' : 'Try adjusting your filters'}
              </p>
              <div className="mt-6 flex items-center justify-center space-x-3">
                <button
                  onClick={handleQuickScan}
                  className="flex items-center space-x-2 px-4 py-2 border border-blue-600 text-blue-600 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-900/20 transition-colors"
                >
                  <PlayCircle className="h-5 w-5" />
                  <span>Quick Scan</span>
                </button>
                <button
                  onClick={handleFullScan}
                  className="flex items-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
                >
                  <Shield className="h-5 w-5" />
                  <span>Full Scan</span>
                </button>
              </div>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead className="bg-gray-50 dark:bg-gray-700/50">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Type</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Status</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Started</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Duration</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Files</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Results</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Risk</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
                  {filteredScans.map((scan: ThreatScan) => (
                    <tr key={scan.id} className="hover:bg-gray-50 dark:hover:bg-gray-700/30 transition-colors">
                      <td className="px-6 py-4 whitespace-nowrap">
                        <span className="text-sm font-medium text-gray-900 dark:text-white">{scan.scanType}</span>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className={`inline-flex items-center space-x-2 px-2.5 py-1 rounded-full text-xs font-medium ${getStatusColor(scan.status)}`}>
                          {getStatusIcon(scan.status)}
                          <span>{getStatusText(scan.status)}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                        {new Date(scan.startTime).toLocaleString()}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                        {formatDuration(scan.duration)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-600 dark:text-gray-400">
                        {scan.filesScanned.toLocaleString()}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        {scan.threatsFound === 0 ? (
                          <div className="flex items-center space-x-2 text-sm text-green-600 dark:text-green-400">
                            <CheckCircle className="h-4 w-4" />
                            <span>Clean</span>
                          </div>
                        ) : (
                          <div className="text-sm">
                            <div className="flex items-center space-x-2 text-orange-600 dark:text-orange-400 font-medium">
                              <AlertTriangle className="h-4 w-4" />
                              <span>{scan.threatsFound} finding{scan.threatsFound !== 1 ? 's' : ''}</span>
                            </div>
                            <div className="text-xs text-gray-600 dark:text-gray-400 mt-1">
                              {scan.malwareDetected + scan.backdoorsDetected > 0 && (
                                <span className="font-medium text-red-600 dark:text-red-400">
                                  {scan.malwareDetected + scan.backdoorsDetected} threat{scan.malwareDetected + scan.backdoorsDetected !== 1 ? 's' : ''}
                                </span>
                              )}
                              {scan.malwareDetected + scan.backdoorsDetected > 0 && scan.suspiciousFiles > 0 && ', '}
                              {scan.suspiciousFiles > 0 && `${scan.suspiciousFiles} flagged`}
                            </div>
                          </div>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <span className={`text-sm font-medium ${getRiskColor(scan.riskLevel)}`}>{scan.riskLevel}</span>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <button
                          onClick={() => setSelectedScanId(scan.id)}
                          className="flex items-center space-x-1 text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300"
                        >
                          <Eye className="h-4 w-4" />
                          <span className="text-sm font-medium">View</span>
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// Scan Progress Component
function ScanProgressComponent({ progress, onCancel }: { progress: ScanProgress; onCancel: () => void }) {
  const isRunning = progress.status?.toLowerCase() === 'running';

  return (
    <div className="space-y-4">
      {/* Progress Bar */}
      <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-sm text-gray-600 dark:text-gray-400">{progress.scanPhase}</span>
          <span className="text-sm font-bold text-gray-900 dark:text-white">{progress.percentComplete.toFixed(1)}%</span>
        </div>
        <div className="w-full bg-gray-200 dark:bg-gray-600 rounded-full h-2">
          <div
            className="bg-blue-600 h-2 rounded-full transition-all duration-300"
            style={{ width: `${progress.percentComplete}%` }}
          ></div>
        </div>
        <div className="mt-3 space-y-1">
          <p className="text-xs text-gray-600 dark:text-gray-400 truncate">
            <span className="font-medium">Current file:</span> {progress.currentFile || 'Initializing...'}
          </p>
          <p className="text-xs text-gray-600 dark:text-gray-400 truncate">
            <span className="font-medium">Directory:</span> {progress.currentDirectory || 'N/A'}
          </p>
        </div>
      </div>

      {/* Statistics */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4 text-center">
          <FileSearch className="h-6 w-6 mx-auto text-blue-600 dark:text-blue-400 mb-2" />
          <div className="text-2xl font-bold text-gray-900 dark:text-white">{progress.filesScanned.toLocaleString()}</div>
          <div className="text-xs text-gray-600 dark:text-gray-400">Files Scanned</div>
          <div className="text-xs text-gray-500 dark:text-gray-500 mt-1">of {progress.totalEstimatedFiles.toLocaleString()}</div>
        </div>

        <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4 text-center">
          <HardDrive className="h-6 w-6 mx-auto text-blue-600 dark:text-blue-400 mb-2" />
          <div className="text-2xl font-bold text-gray-900 dark:text-white">{progress.directoriesScanned.toLocaleString()}</div>
          <div className="text-xs text-gray-600 dark:text-gray-400">Directories</div>
        </div>

        <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4 text-center">
          <AlertTriangle className={`h-6 w-6 mx-auto mb-2 ${progress.threatsFound > 0 ? 'text-orange-600 dark:text-orange-400' : 'text-green-600 dark:text-green-400'}`} />
          <div className={`text-2xl font-bold ${progress.threatsFound > 0 ? 'text-orange-600 dark:text-orange-400' : 'text-green-600 dark:text-green-400'}`}>
            {progress.threatsFound.toLocaleString()}
          </div>
          <div className="text-xs text-gray-600 dark:text-gray-400">Findings</div>
        </div>

        <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4 text-center">
          <Clock className="h-6 w-6 mx-auto text-blue-600 dark:text-blue-400 mb-2" />
          <div className="text-2xl font-bold text-gray-900 dark:text-white">{progress.elapsedTime}</div>
          <div className="text-xs text-gray-600 dark:text-gray-400">Elapsed</div>
          {progress.estimatedTimeRemaining && (
            <div className="text-xs text-gray-500 dark:text-gray-500 mt-1">~{progress.estimatedTimeRemaining} left</div>
          )}
        </div>
      </div>

      {/* Data Scanned */}
      <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
        <p className="text-sm text-gray-600 dark:text-gray-400">
          <span className="font-medium">Data Scanned:</span> {(progress.bytesScanned / (1024 * 1024 * 1024)).toFixed(2)} GB
        </p>
      </div>

      {/* Cancel Button */}
      {isRunning && (
        <button
          onClick={onCancel}
          className="w-full px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition-colors font-medium"
        >
          Cancel Scan
        </button>
      )}
    </div>
  );
}

// Scan Detail View Component
function ScanDetailView({ scan }: { scan: ThreatScan }) {
  const getRiskColor = (level: string) => {
    switch (level?.toLowerCase()) {
      case 'critical': return 'text-red-600 dark:text-red-400';
      case 'high': return 'text-orange-600 dark:text-orange-400';
      case 'medium': return 'text-yellow-600 dark:text-yellow-400';
      case 'low': return 'text-green-600 dark:text-green-400';
      default: return 'text-gray-600 dark:text-gray-400';
    }
  };

  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return 'text-green-600 bg-green-50 dark:bg-green-900/20 dark:text-green-400';
      case 'completedwiththreats': return 'text-orange-600 bg-orange-50 dark:bg-orange-900/20 dark:text-orange-400';
      case 'running': return 'text-blue-600 bg-blue-50 dark:bg-blue-900/20 dark:text-blue-400';
      case 'failed': return 'text-red-600 bg-red-50 dark:bg-red-900/20 dark:text-red-400';
      case 'cancelled': return 'text-gray-600 bg-gray-50 dark:bg-gray-700/20 dark:text-gray-400';
      default: return 'text-gray-600 bg-gray-50 dark:bg-gray-700/20 dark:text-gray-400';
    }
  };

  const getStatusText = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return 'Clean';
      case 'completedwiththreats': return 'Findings Detected';
      case 'running': return 'Scanning';
      case 'failed': return 'Failed';
      case 'cancelled': return 'Cancelled';
      default: return status;
    }
  };

  const formatBytes = (bytes: number) => {
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  };

  return (
    <div className="space-y-6">
      {/* Basic Information */}
      <div>
        <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-4">Basic Information</h3>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="text-sm font-medium text-gray-600 dark:text-gray-400">Scan ID</label>
            <p className="text-sm text-gray-900 dark:text-white font-mono">{scan.id}</p>
          </div>
          <div>
            <label className="text-sm font-medium text-gray-600 dark:text-gray-400">Scan Type</label>
            <p className="text-sm text-gray-900 dark:text-white">{scan.scanType}</p>
          </div>
          <div>
            <label className="text-sm font-medium text-gray-600 dark:text-gray-400">Status</label>
            <div className={`inline-flex items-center space-x-2 px-2.5 py-1 rounded-full text-xs font-medium ${getStatusColor(scan.status)}`}>
              <span>{getStatusText(scan.status)}</span>
            </div>
          </div>
          <div>
            <label className="text-sm font-medium text-gray-600 dark:text-gray-400">Risk Level</label>
            <p className={`text-sm font-bold ${getRiskColor(scan.riskLevel)}`}>{scan.riskLevel}</p>
          </div>
          <div>
            <label className="text-sm font-medium text-gray-600 dark:text-gray-400">Start Time</label>
            <p className="text-sm text-gray-900 dark:text-white">{new Date(scan.startTime).toLocaleString()}</p>
          </div>
          <div>
            <label className="text-sm font-medium text-gray-600 dark:text-gray-400">End Time</label>
            <p className="text-sm text-gray-900 dark:text-white">{scan.endTime ? new Date(scan.endTime).toLocaleString() : 'N/A'}</p>
          </div>
        </div>
      </div>

      {/* Scan Statistics */}
      <div>
        <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-4">Scan Statistics</h3>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
            <div className="text-sm font-medium text-gray-600 dark:text-gray-400">Files Scanned</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-white mt-1">{scan.filesScanned.toLocaleString()}</div>
          </div>
          <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
            <div className="text-sm font-medium text-gray-600 dark:text-gray-400">Directories</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-white mt-1">{scan.directoriesScanned.toLocaleString()}</div>
          </div>
          <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
            <div className="text-sm font-medium text-gray-600 dark:text-gray-400">Data Scanned</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-white mt-1">{formatBytes(scan.bytesScanned)}</div>
          </div>
          <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
            <div className="text-sm font-medium text-gray-600 dark:text-gray-400">Duration</div>
            <div className="text-2xl font-bold text-gray-900 dark:text-white mt-1">{Math.round(scan.duration)} min</div>
          </div>
        </div>
      </div>

      {/* Security Analysis */}
      <div>
        <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-4">Security Analysis</h3>
        {scan.threatsFound === 0 ? (
          <div className="bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg p-4">
            <div className="flex items-center space-x-3">
              <CheckCircle className="h-6 w-6 text-green-600 dark:text-green-400" />
              <div>
                <p className="text-sm font-bold text-green-900 dark:text-green-300">System Clean</p>
                <p className="text-xs text-green-700 dark:text-green-400">No security findings detected</p>
              </div>
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="bg-orange-50 dark:bg-orange-900/20 border border-orange-200 dark:border-orange-800 rounded-lg p-4">
              <div className="flex items-center space-x-3">
                <AlertTriangle className="h-6 w-6 text-orange-600 dark:text-orange-400" />
                <div>
                  <p className="text-sm font-bold text-orange-900 dark:text-orange-300">
                    {scan.threatsFound} Security Finding{scan.threatsFound !== 1 ? 's' : ''} Detected
                  </p>
                  <p className="text-xs text-orange-700 dark:text-orange-400">
                    {scan.malwareDetected + scan.backdoorsDetected > 0 && (
                      <span className="font-medium">
                        {scan.malwareDetected + scan.backdoorsDetected} threat{scan.malwareDetected + scan.backdoorsDetected !== 1 ? 's' : ''} ({scan.malwareDetected} malware, {scan.backdoorsDetected} backdoors)
                      </span>
                    )}
                    {scan.malwareDetected + scan.backdoorsDetected > 0 && scan.suspiciousFiles > 0 && ', '}
                    {scan.suspiciousFiles > 0 && `${scan.suspiciousFiles} suspicious file${scan.suspiciousFiles !== 1 ? 's' : ''}`}
                  </p>
                </div>
              </div>
            </div>

            <div className="grid grid-cols-3 gap-4">
              <div className="bg-red-50 dark:bg-red-900/20 rounded-lg p-4 text-center border border-red-200 dark:border-red-800">
                <div className="text-2xl font-bold text-red-600 dark:text-red-400">{scan.malwareDetected}</div>
                <div className="text-xs text-red-700 dark:text-red-400 font-medium">Malware</div>
              </div>
              <div className="bg-red-50 dark:bg-red-900/20 rounded-lg p-4 text-center border border-red-200 dark:border-red-800">
                <div className="text-2xl font-bold text-red-600 dark:text-red-400">{scan.backdoorsDetected}</div>
                <div className="text-xs text-red-700 dark:text-red-400 font-medium">Backdoors</div>
              </div>
              <div className="bg-yellow-50 dark:bg-yellow-900/20 rounded-lg p-4 text-center border border-yellow-200 dark:border-yellow-800">
                <div className="text-2xl font-bold text-yellow-600 dark:text-yellow-400">{scan.suspiciousFiles}</div>
                <div className="text-xs text-yellow-700 dark:text-yellow-400 font-medium">Suspicious</div>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Summary */}
      {scan.summary && (
        <div>
          <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-4">Summary</h3>
          <div className="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
            <p className="text-sm text-gray-900 dark:text-white">{scan.summary}</p>
          </div>
        </div>
      )}

      {/* Error Message */}
      {scan.errorMessage && (
        <div>
          <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-4">Error Details</h3>
          <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-4">
            <p className="text-sm text-red-900 dark:text-red-300 font-mono">{scan.errorMessage}</p>
          </div>
        </div>
      )}
    </div>
  );
}
