import React from 'react';
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
  Alert
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
      case 'completedwiththreats': return 'error';
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
  
  return (
    <Chip 
      icon={getStatusIcon(statusValue)}
      label={statusValue} 
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

// Custom component for threats summary
const ThreatsSummaryField = ({ source }: any) => {
  const record = useRecordContext();
  
  const threatsFound = record?.threatsFound || 0;
  const malware = record?.malwareDetected || 0;
  const backdoors = record?.backdoorsDetected || 0;
  const suspicious = record?.suspiciousFiles || 0;

  if (threatsFound === 0) {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <SafeIcon color="success" fontSize="small" />
        <Typography variant="body2" color="success.main">No threats detected</Typography>
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
        <ThreatIcon color="error" fontSize="small" />
        <Typography variant="body2" fontWeight="bold">{threatsFound} threats found</Typography>
      </Box>
      <Typography variant="caption" color="textSecondary">
        {malware} malware, {backdoors} backdoors, {suspicious} suspicious
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

// Scan Actions Component
const ScanActions = () => {
  const notify = useNotify();
  const refresh = useRefresh();
  
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
        setTimeout(() => refresh(), 1000);
      } else {
        notify('Failed to start full scan', { type: 'error' });
      }
    } catch (error) {
      notify('Error starting full scan', { type: 'error' });
    }
  };

  return (
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
      { id: 'Completed', name: 'Completed' },
      { id: 'CompletedWithThreats', name: 'Completed with Threats' },
      { id: 'Running', name: 'Running' },
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
        <ThreatsSummaryField source="threatsFound" label="Threats" sortable={false} />
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
        <Typography variant="h6">Threat Analysis</Typography>
        <ThreatsSummaryField source="threatsFound" />
        <RiskLevelField source="riskLevel" label="Overall Risk Level" />
      </Box>

      <TextField source="summary" label="Summary" />
      {/* Show error message if scan failed */}
      <TextField source="errorMessage" label="Error Details" />
      
      <Box component="div" sx={{ mt: 2 }}>
        <Typography variant="h6">Threat Details</Typography>
        <Typography variant="body2" color="textSecondary">
          Detailed threat information would be displayed here
        </Typography>
      </Box>
    </SimpleShowLayout>
    </Show>
  </Box>
);