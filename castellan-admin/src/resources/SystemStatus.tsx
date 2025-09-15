import React from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  ShowButton,
  Show,
  SimpleShowLayout,
  Filter,
  SelectInput,
  TextInput,
  NumberField,
  useRecordContext,
  RefreshButton,
  TopToolbar,
} from 'react-admin';
import { 
  Chip, 
  Box, 
  Typography, 
  IconButton,
  Tooltip 
} from '@mui/material';
import {
  CheckCircle as HealthyIcon,
  Error as ErrorIcon,
  Warning as WarningIcon,
  Info as InfoIcon,
  Memory as MemoryIcon,
  Storage as StorageIcon,
  Speed as PerformanceIcon,
  Computer as SystemIcon,
  Cloud as CloudIcon,
  Security as SecurityIcon,
  DataObject as DatabaseIcon,
  Refresh as RefreshIcon,
  SignalWifi4Bar as ConnectedIcon,
  SignalWifiOff as DisconnectedIcon
} from '@mui/icons-material';

// Import SignalR context for real-time updates
import { useSignalRContext } from '../contexts/SignalRContext';

// Custom header component with shield icon
const SystemStatusHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      System Status
    </Typography>
  </Box>
);

// Custom component for component health status  
interface FieldProps {
  source?: string;
  label?: string;
  [key: string]: any;
}

const HealthStatusField = ({ source, label, ...props }: FieldProps) => {
  const record = useRecordContext();
  const getHealthIcon = (status: string, isHealthy: boolean) => {
    if (isHealthy) return <HealthyIcon fontSize="small" color="success" />;
    
    switch (status?.toLowerCase()) {
      case 'error':
      case 'failed':
      case 'down': return <ErrorIcon fontSize="small" color="error" />;
      case 'warning':
      case 'degraded': return <WarningIcon fontSize="small" color="warning" />;
      default: return <InfoIcon fontSize="small" color="info" />;
    }
  };

  const getHealthColor = (status: string, isHealthy: boolean) => {
    if (isHealthy) return 'success';
    
    switch (status?.toLowerCase()) {
      case 'error':
      case 'failed':
      case 'down': return 'error';
      case 'warning':
      case 'degraded': return 'warning';
      default: return 'info';
    }
  };

  return (
    <Chip 
      icon={getHealthIcon(record?.status, record?.isHealthy)}
      label={record?.status || 'Unknown'} 
      color={getHealthColor(record?.status, record?.isHealthy) as any}
      size="small"
      variant={record?.isHealthy ? 'filled' : 'outlined'}
    />
  );
};

// Add display name for React-Admin field integration
HealthStatusField.displayName = 'HealthStatusField';

// Custom component for component type with icons
const ComponentTypeField = ({ source, label, ...props }: FieldProps) => {
  const record = useRecordContext();
  
  const getComponentIcon = (component: string) => {
    const comp = component?.toLowerCase() || '';
    if (comp.includes('pipeline') || comp.includes('worker')) return <SystemIcon fontSize="small" />;
    if (comp.includes('vector') || comp.includes('qdrant') || comp.includes('database')) return <DatabaseIcon fontSize="small" />;
    if (comp.includes('security') || comp.includes('detector')) return <SecurityIcon fontSize="small" />;
    if (comp.includes('cloud') || comp.includes('connector')) return <CloudIcon fontSize="small" />;
    if (comp.includes('performance') || comp.includes('monitor')) return <PerformanceIcon fontSize="small" />;
    if (comp.includes('memory') || comp.includes('ram')) return <MemoryIcon fontSize="small" />;
    if (comp.includes('storage') || comp.includes('disk')) return <StorageIcon fontSize="small" />;
    return <SystemIcon fontSize="small" />;
  };

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
      {getComponentIcon(record?.component)}
      <Typography variant="body2">{record?.component || 'Unknown'}</Typography>
    </Box>
  );
};

// Add display name for React-Admin field integration
ComponentTypeField.displayName = 'ComponentTypeField';

// Custom component for uptime display
const UptimeField = ({ source, label, ...props }: FieldProps) => {
  const record = useRecordContext();
  // Handle both numeric (seconds) and string (percentage) formats
  const uptime = record?.uptime;
  
  // If it's a string (percentage format), display as-is
  if (typeof uptime === 'string') {
    const getUptimeColorFromPercentage = (uptimeStr: string) => {
      const percentage = parseFloat(uptimeStr.replace('%', ''));
      if (percentage >= 99) return 'success';
      if (percentage >= 95) return 'info';
      if (percentage >= 90) return 'warning';
      return 'error';
    };
    
    return (
      <Chip 
        label={uptime}
        color={getUptimeColorFromPercentage(uptime)}
        size="small"
        variant="outlined"
      />
    );
  }
  
  // If it's a number (seconds), convert to time format
  const uptimeSeconds = uptime || 0;
  const days = Math.floor(uptimeSeconds / 86400);
  const hours = Math.floor((uptimeSeconds % 86400) / 3600);
  const minutes = Math.floor((uptimeSeconds % 3600) / 60);
  
  let displayText = '';
  if (days > 0) displayText += `${days}d `;
  if (hours > 0) displayText += `${hours}h `;
  displayText += `${minutes}m`;

  const getUptimeColor = (uptime: number) => {
    const days = uptime / 86400;
    if (days >= 30) return 'success';
    if (days >= 7) return 'info';
    if (days >= 1) return 'warning';
    return 'error';
  };

  return (
    <Chip 
      label={displayText.trim() || '0m'}
      color={getUptimeColor(uptimeSeconds)}
      size="small"
      variant="outlined"
    />
  );
};

// Add display name for React-Admin field integration
UptimeField.displayName = 'UptimeField';

// Custom component for response time display
const ResponseTimeField = ({ source, label, ...props }: FieldProps) => {
  const record = useRecordContext();
  const responseTime = record?.responseTime || 0;
  
  const getResponseColor = (value: number) => {
    if (value <= 50) return 'success';
    if (value <= 150) return 'info';
    if (value <= 500) return 'warning';
    return 'error';
  };

  const getResponseLabel = (value: number) => {
    if (value <= 50) return 'Excellent';
    if (value <= 150) return 'Good';
    if (value <= 500) return 'Fair';
    return 'Poor';
  };

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', minWidth: 120 }}>
      <Tooltip title={`${responseTime}ms - ${getResponseLabel(responseTime)}`}>
        <Chip 
          label={`${responseTime}ms`}
          color={getResponseColor(responseTime)}
          size="small"
          variant="outlined"
        />
      </Tooltip>
    </Box>
  );
};

// Add display name for React-Admin field integration
ResponseTimeField.displayName = 'ResponseTimeField';

// Custom refresh button for individual components
const RefreshComponentButton = () => {
  const record = useRecordContext();
  
  const handleRefresh = () => {
    // In a real implementation, this would trigger a refresh for the specific component
    console.log(`Refreshing component: ${record?.component || 'Unknown'}`);
  };
  
  return (
    <Tooltip title="Refresh Component Status">
      <IconButton onClick={handleRefresh} size="small">
        <RefreshIcon fontSize="small" />
      </IconButton>
    </Tooltip>
  );
};

// Filters for the list view
const SystemStatusFilters = [
  <TextInput source="component" label="Component Name" alwaysOn />,
  <SelectInput 
    source="status" 
    label="Status"
    choices={[
      { id: 'healthy', name: 'Healthy' },
      { id: 'running', name: 'Running' },
      { id: 'degraded', name: 'Degraded' },
      { id: 'warning', name: 'Warning' },
      { id: 'error', name: 'Error' },
      { id: 'down', name: 'Down' },
      { id: 'maintenance', name: 'Maintenance' },
    ]}
  />,
  <SelectInput 
    source="isHealthy" 
    label="Health Status"
    choices={[
      { id: 'true', name: 'Healthy' },
      { id: 'false', name: 'Unhealthy' },
    ]}
  />,
];

// Custom list actions with refresh all button
const SystemStatusListActions = () => {
  const { isConnected: signalRConnected, connectionState } = useSignalRContext();

  return (
    <TopToolbar>
      <RefreshButton />

      {/* SignalR Connection Status */}
      <Tooltip title={`Real-time updates: ${connectionState}`}>
        <Box sx={{ display: 'flex', alignItems: 'center', ml: 1 }}>
          {signalRConnected ? (
            <ConnectedIcon color="success" fontSize="small" />
          ) : (
            <DisconnectedIcon color="error" fontSize="small" />
          )}
          <Typography variant="caption" sx={{ ml: 0.5, color: signalRConnected ? 'success.main' : 'error.main' }}>
            {signalRConnected ? 'Live' : 'Offline'}
          </Typography>
        </Box>
      </Tooltip>
    </TopToolbar>
  );
};

export const SystemStatusList = () => (
  <Box>
    <SystemStatusHeader />
    <List 
      filters={<Filter>{SystemStatusFilters}</Filter>}
      sort={{ field: 'lastCheck', order: 'DESC' }}
      perPage={25}
      title=" "
      actions={<SystemStatusListActions />}
      // pollInterval={30000} // Auto-refresh every 30 seconds - not supported in react-admin List
    >
    <Datagrid rowClick="show" size="small">
      <ComponentTypeField source="component" label="Component" />
      <HealthStatusField source="status" label="Status" />
      <UptimeField source="uptime" label="Uptime" />
      <ResponseTimeField source="responseTime" label="Response Time" />
      <NumberField source="errorCount" label="Errors" />
      <NumberField source="warningCount" label="Warnings" />
      <DateField source="lastCheck" showTime label="Last Check" />
      <RefreshComponentButton />
      <ShowButton />
    </Datagrid>
    </List>
  </Box>
);

export const SystemStatusShow = () => (
  <Box>
    <SystemStatusHeader />
    <Show title=" ">
      <SimpleShowLayout>
      <ComponentTypeField source="component" label="Component" />
      <HealthStatusField source="status" label="Status" />
      <UptimeField source="uptime" label="Uptime" />
      <ResponseTimeField source="responseTime" label="Response Time" />
      
      <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>Monitoring Information</Typography>
      <DateField source="lastCheck" showTime label="Last Check" />
      <NumberField source="errorCount" label="Error Count" />
      <NumberField source="warningCount" label="Warning Count" />
      
      <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>Component Details</Typography>
      <TextField source="details" label="Details" />
    </SimpleShowLayout>
    </Show>
  </Box>
);