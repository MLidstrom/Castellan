import React, { useState, useEffect } from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  NumberField,
  ShowButton,
  Show,
  SimpleShowLayout,
  Filter,
  SelectInput,
  TextInput,
  useRecordContext,
  Button,
  TopToolbar,
  useNotify,
  useRefresh,
} from 'react-admin';
import { 
  Chip, 
  Box, 
  LinearProgress, 
  Typography, 
  Card,
  CardContent,
  CardHeader,
  IconButton,
  Alert,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Paper
} from '@mui/material';
import { 
  Security as SecurityIcon,
  BugReport as ThreatIcon,
  Warning as WarningIcon,
  CheckCircle as SafeIcon,
  Cancel as CancelIcon,
  PlayArrow as ScanIcon,
  Refresh as RefreshIcon,
  Timeline as AnalyticsIcon
} from '@mui/icons-material';

// Custom header component with shield icon
const ThreatScannerHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      Threat Scanner
    </Typography>
  </Box>
);

// Custom component for scan status with color coding
const ScanStatusField = ({ source }: any) => {
  const record = useRecordContext();
  
  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return 'success';
      case 'completedwiththreats': return 'warning'; // Changed from error to warning
      case 'running': return 'info';
      case 'failed': return 'error';
      case 'cancelled': return 'warning';
      default: return 'default';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return <SafeIcon fontSize="small" />;
      case 'completedwiththreats': return <WarningIcon fontSize="small" />;
      case 'running': return <RefreshIcon fontSize="small" />;
      case 'failed': return <CancelIcon fontSize="small" />;
      case 'cancelled': return <CancelIcon fontSize="small" />;
      default: return <SecurityIcon fontSize="small" />;
    }
  };

  const statusValue = record?.status || record?.Status || 'Unknown';
  
  // Convert status to user-friendly text
  const getDisplayText = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'completed': return 'Clean';
      case 'completedwiththreats': return 'Findings Detected';
      case 'running': return 'Scanning';
      case 'failed': return 'Failed';
      case 'cancelled': return 'Cancelled';
      case 'notstartedyet': return 'Pending';
      default: return status;
    }
  };
  
  return (
    <Chip 
      icon={getStatusIcon(statusValue)}
      label={getDisplayText(statusValue)} 
      color={getStatusColor(statusValue)}
      size="small"
    />
  );
};

// Custom component for scan type
const ScanTypeField = ({ source }: any) => {
  const record = useRecordContext();
  
  const getTypeColor = (type: string) => {
    switch (type?.toLowerCase()) {
      case 'fullscan': return 'primary';
      case 'quickscan': return 'info';
      case 'directoryscan': return 'secondary';
      case 'filescan': return 'default';
      default: return 'default';
    }
  };

  const typeValue = record?.scanType || record?.ScanType || 'Unknown';
  
  return (
    <Chip 
      label={typeValue} 
      color={getTypeColor(typeValue)}
      size="small"
      variant="outlined"
    />
  );
};

// Custom component for risk level
const RiskLevelField = ({ source }: any) => {
  const record = useRecordContext();
  
  const getRiskColor = (level: string) => {
    switch (level?.toLowerCase()) {
      case 'critical': return 'error';
      case 'high': return 'warning';
      case 'medium': return 'info';
      case 'low': return 'success';
      default: return 'default';
    }
  };

  const riskValue = record?.riskLevel || record?.RiskLevel || 'Unknown';
  
  return (
    <Chip 
      label={riskValue} 
      color={getRiskColor(riskValue)}
      size="small"
    />
  );
};

// Custom component for security findings summary
const FindingsSummaryField = ({ source }: any) => {
  const record = useRecordContext();
  
  const findingsCount = record?.threatsFound || 0; // API still uses threatsFound
  const malware = record?.malwareDetected || 0;
  const backdoors = record?.backdoorsDetected || 0;
  const suspicious = record?.suspiciousFiles || 0;
  
  // Calculate actual threats (high-risk items)
  const actualThreats = malware + backdoors;

  if (findingsCount === 0) {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <SafeIcon color="success" fontSize="small" />
        <Typography variant="body2" color="success.main">Clean - No issues found</Typography>
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
        {actualThreats > 0 ? (
          <ThreatIcon color="error" fontSize="small" />
        ) : (
          <WarningIcon color="warning" fontSize="small" />
        )}
        <Typography variant="body2" fontWeight="bold">
          {findingsCount} security finding{findingsCount !== 1 ? 's' : ''}
        </Typography>
      </Box>
      <Typography variant="caption" color="textSecondary">
        {actualThreats > 0 ? (
          <><strong>{actualThreats} threat{actualThreats !== 1 ? 's' : ''}</strong> ({malware} malware, {backdoors} backdoors), </>
        ) : (
          'No actual threats, '
        )}
        {suspicious} flagged item{suspicious !== 1 ? 's' : ''}
      </Typography>
    </Box>
  );
};

// Custom component for scan duration
const DurationField = ({ source }: any) => {
  const record = useRecordContext();
  const duration = record?.duration || 0; // in minutes
  
  const formatDuration = (minutes: number) => {
    if (minutes < 1) return `${Math.round(minutes * 60)}s`;
    if (minutes < 60) return `${Math.round(minutes)}m`;
    return `${Math.round(minutes / 60)}h ${Math.round(minutes % 60)}m`;
  };

  return (
    <Typography variant="body2">
      {formatDuration(duration)}
    </Typography>
  );
};

// Progress tracking interface
interface ScanProgress {
  scanId: string;
  status: string;
  filesScanned: number;
  totalEstimatedFiles: number;
  directoriesScanned: number;
  threatsFound: number; // API field name
  findingsCount?: number; // Cleaner terminology
  currentFile: string;
  currentDirectory: string;
  percentComplete: number;
  startTime: string;
  elapsedTime: string;
  estimatedTimeRemaining?: string;
  bytesScanned: number;
  scanPhase: string;
}

// Progress Dialog Component
const ScanProgressDialog = ({ open, onClose, scanType }: { open: boolean; onClose: () => void; scanType: string }) => {
  const [progress, setProgress] = useState<ScanProgress | null>(null);
  const [error, setError] = useState<string | null>(null);
  const notify = useNotify();
  const refresh = useRefresh();
  
  const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || 'http://localhost:5000';
  
  useEffect(() => {
    if (!open) {
      setProgress(null);
      setError(null);
      return;
    }
    
    const pollProgress = async () => {
      try {
        const response = await fetch(`${API_BASE_URL}/api/threat-scanner/progress`, {
          headers: {
            'Authorization': `Bearer ${localStorage.getItem('auth_token')}`,
          },
        });
        
        if (response.ok) {
          const data = await response.json();
          if (data.progress) {
            setProgress(data.progress);
            
            // Check if scan is completed
            if (data.progress.status === 'Completed' || data.progress.status === 'CompletedWithThreats') {
              const findings = data.progress.threatsFound;
              notify(
                `${scanType} completed! Found ${findings} security finding${findings !== 1 ? 's' : ''}.`,
                { type: findings > 0 ? 'info' : 'success' }
              );
              setTimeout(() => {
                refresh();
                onClose();
              }, 2000);
              return;
            }
            
            if (data.progress.status === 'Failed' || data.progress.status === 'Cancelled') {
              setError(`Scan ${data.progress.status.toLowerCase()}`);
              setTimeout(() => onClose(), 3000);
              return;
            }
          } else {
            setError('No scan in progress');
            setTimeout(() => onClose(), 2000);
            return;
          }
        } else {
          setError('Failed to fetch progress');
        }
      } catch (err) {
        setError('Error fetching progress');
      }
    };
    
    // Initial poll
    pollProgress();
    
    // Set up polling interval
    const interval = setInterval(pollProgress, 2000);
    
    return () => clearInterval(interval);
  }, [open, API_BASE_URL, notify, refresh, onClose, scanType]);
  
  const handleCancel = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/threat-scanner/cancel`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`,
        },
      });
      
      if (response.ok) {
        notify('Scan cancellation requested', { type: 'info' });
      }
    } catch (err) {
      notify('Failed to cancel scan', { type: 'error' });
    }
  };
  
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <ScanIcon color="primary" />
          <Typography variant="h6">{scanType} Progress</Typography>
        </Box>
      </DialogTitle>
      
      <DialogContent>
        {error ? (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        ) : progress ? (
          <Box sx={{ spacing: 2 }}>
            {/* Progress Bar */}
            <Paper elevation={1} sx={{ p: 2, mb: 2 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
                <Typography variant="body2" color="textSecondary">
                  {progress.scanPhase}
                </Typography>
                <Typography variant="body2" fontWeight="bold">
                  {progress.percentComplete.toFixed(1)}%
                </Typography>
              </Box>
              <LinearProgress 
                variant="determinate" 
                value={progress.percentComplete} 
                sx={{ height: 8, borderRadius: 4, mb: 2 }}
              />
              
              {/* Current File Info */}
              <Box sx={{ mb: 2 }}>
                <Typography variant="body2" color="textSecondary" noWrap>
                  <strong>Current file:</strong> {progress.currentFile || 'Initializing...'}
                </Typography>
                <Typography variant="body2" color="textSecondary" noWrap>
                  <strong>Directory:</strong> {progress.currentDirectory || 'N/A'}
                </Typography>
              </Box>
            </Paper>
            
            {/* Statistics Grid */}
            <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 2, mb: 2 }}>
              <Paper elevation={1} sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="h6" color="primary">
                  {progress.filesScanned.toLocaleString()}
                </Typography>
                <Typography variant="caption" color="textSecondary">
                  Files Scanned
                </Typography>
                <Typography variant="caption" display="block">
                  of {progress.totalEstimatedFiles.toLocaleString()}
                </Typography>
              </Paper>
              
              <Paper elevation={1} sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="h6" color="info.main">
                  {progress.directoriesScanned.toLocaleString()}
                </Typography>
                <Typography variant="caption" color="textSecondary">
                  Directories
                </Typography>
              </Paper>
              
              <Paper elevation={1} sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="h6" color={progress.threatsFound > 0 ? 'warning.main' : 'success.main'}>
                  {progress.threatsFound.toLocaleString()}
                </Typography>
                <Typography variant="caption" color="textSecondary">
                  Findings
                </Typography>
              </Paper>
              
              <Paper elevation={1} sx={{ p: 1.5, textAlign: 'center' }}>
                <Typography variant="h6">
                  {progress.elapsedTime}
                </Typography>
                <Typography variant="caption" color="textSecondary">
                  Elapsed
                </Typography>
                {progress.estimatedTimeRemaining && (
                  <Typography variant="caption" display="block">
                    ~{progress.estimatedTimeRemaining} left
                  </Typography>
                )}
              </Paper>
            </Box>
            
            {/* Data Scanned */}
            <Paper elevation={1} sx={{ p: 1.5 }}>
              <Typography variant="body2" color="textSecondary">
                <strong>Data Scanned:</strong> {(progress.bytesScanned / (1024 * 1024 * 1024)).toFixed(2)} GB
              </Typography>
            </Paper>
          </Box>
        ) : (
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', py: 4 }}>
            <LinearProgress sx={{ width: '100%' }} />
            <Typography variant="body2" sx={{ ml: 2 }}>Loading scan progress...</Typography>
          </Box>
        )}
      </DialogContent>
      
      <DialogActions>
        {progress && (progress.status === 'Running') && (
          <Button onClick={handleCancel} color="error" variant="outlined">
            Cancel Scan
          </Button>
        )}
        <Button onClick={onClose} variant="contained">
          {error || (progress && (progress.status === 'Completed' || progress.status === 'CompletedWithThreats')) ? 'Close' : 'Hide'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

// Scan Actions Component
const ScanActions = () => {
  const notify = useNotify();
  const refresh = useRefresh();
  const [progressOpen, setProgressOpen] = useState(false);
  const [scanType, setScanType] = useState('');
  
  // Get API base URL from environment or default to localhost:5000
  const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || 'http://localhost:5000';

  const handleQuickScan = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/threat-scanner/quick-scan`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`,
          'Content-Type': 'application/json',
        },
      });
      
      if (response.ok) {
        notify('Quick scan started successfully', { type: 'success' });
        setScanType('Quick Scan');
        setProgressOpen(true);
        setTimeout(() => refresh(), 1000);
      } else {
        notify('Failed to start quick scan', { type: 'error' });
      }
    } catch (error) {
      notify('Error starting quick scan', { type: 'error' });
    }
  };

  const handleFullScan = async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/threat-scanner/full-scan`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`,
          'Content-Type': 'application/json',
        },
      });
      
      if (response.ok) {
        notify('Full scan started successfully', { type: 'success' });
        setScanType('Full Scan');
        setProgressOpen(true);
        setTimeout(() => refresh(), 1000);
      } else {
        notify('Failed to start full scan', { type: 'error' });
      }
    } catch (error) {
      notify('Error starting full scan', { type: 'error' });
    }
  };

  return (
    <>
      <TopToolbar>
        <Button
          onClick={handleQuickScan}
          startIcon={<ScanIcon />}
          label="Quick Scan"
          variant="outlined"
          size="small"
        />
        <Button
          onClick={handleFullScan}
          startIcon={<SecurityIcon />}
          label="Full Scan"
          variant="contained"
          size="small"
        />
        <IconButton onClick={() => refresh()}>
          <RefreshIcon />
        </IconButton>
      </TopToolbar>
      
      <ScanProgressDialog 
        open={progressOpen} 
        onClose={() => setProgressOpen(false)} 
        scanType={scanType}
      />
    </>
  );
};

// Filters for the list view
const ThreatScannerFilters = [
  <SelectInput 
    source="scanType" 
    label="Scan Type"
    choices={[
      { id: 'QuickScan', name: 'Quick Scan' },
      { id: 'FullScan', name: 'Full Scan' },
      { id: 'DirectoryScan', name: 'Directory Scan' },
      { id: 'FileScan', name: 'File Scan' },
    ]}
    alwaysOn
  />,
  <SelectInput 
    source="status" 
    label="Status"
    choices={[
      { id: 'Completed', name: 'Clean' },
      { id: 'CompletedWithThreats', name: 'Findings Detected' },
      { id: 'Running', name: 'Scanning' },
      { id: 'Failed', name: 'Failed' },
      { id: 'Cancelled', name: 'Cancelled' },
    ]}
  />,
  <SelectInput 
    source="riskLevel" 
    label="Risk Level"
    choices={[
      { id: 'Low', name: 'Low' },
      { id: 'Medium', name: 'Medium' },
      { id: 'High', name: 'High' },
      { id: 'Critical', name: 'Critical' },
    ]}
  />,
];

export const ThreatScannerList = () => (
  <Box>
    <ThreatScannerHeader />
    <List 
      filters={<Filter>{ThreatScannerFilters}</Filter>}
      sort={{ field: 'startTime', order: 'DESC' }}
      perPage={25}
      title=" "
      actions={<ScanActions />}
    >
      <Datagrid rowClick="show" size="small">
        <TextField source="id" label="Scan ID" />
        <ScanTypeField source="scanType" label="Type" />
        <ScanStatusField source="status" label="Status" />
        <DateField source="startTime" showTime label="Started" />
        <DurationField source="duration" label="Duration" />
        <NumberField source="filesScanned" label="Files" />
        <FindingsSummaryField source="threatsFound" label="Results" sortable={false} />
        <RiskLevelField source="riskLevel" label="Risk" />
        <ShowButton />
      </Datagrid>
    </List>
  </Box>
);

export const ThreatScannerShow = () => (
  <Box>
    <ThreatScannerHeader />
    <Show title=" ">
      <SimpleShowLayout>
      <TextField source="id" label="Scan ID" />
      <ScanTypeField source="scanType" label="Scan Type" />
      <ScanStatusField source="status" label="Status" />
      <DateField source="startTime" showTime label="Start Time" />
      <DateField source="endTime" showTime label="End Time" />
      <DurationField source="duration" label="Duration" />
      
      <Box component="div" sx={{ mt: 2, mb: 2 }}>
        <Typography variant="h6">Scan Statistics</Typography>
        <Box sx={{ display: 'flex', gap: 4, mt: 1 }}>
          <Box>
            <Typography variant="body2" color="textSecondary">Files Scanned</Typography>
            <Typography variant="h4">{}</Typography>
          </Box>
          <Box>
            <Typography variant="body2" color="textSecondary">Directories</Typography>
            <NumberField source="directoriesScanned" />
          </Box>
          <Box>
            <Typography variant="body2" color="textSecondary">Data Scanned</Typography>
            <NumberField source="bytesScanned" options={{ notation: 'compact' }} />
          </Box>
        </Box>
      </Box>

      <Box component="div" sx={{ mt: 2, mb: 2 }}>
        <Typography variant="h6">Security Analysis</Typography>
        <FindingsSummaryField source="threatsFound" />
        <RiskLevelField source="riskLevel" label="Overall Risk Level" />
      </Box>

      <TextField source="summary" label="Summary" />
      {/* Show error message if scan failed */}
      <TextField source="errorMessage" label="Error Details" />
      
      <Box component="div" sx={{ mt: 2 }}>
        <Typography variant="h6">Security Findings</Typography>
        <Typography variant="body2" color="textSecondary">
          Detailed security analysis results would be displayed here
        </Typography>
      </Box>
    </SimpleShowLayout>
    </Show>
  </Box>
);