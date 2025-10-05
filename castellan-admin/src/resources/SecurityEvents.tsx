import React, { useState, useEffect, useMemo } from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  NumberField,
  Edit,
  Create,
  Show,
  SimpleForm,
  SimpleShowLayout,
  TextInput,
  SelectInput,
  required,
  // ChipField, // Removed - using custom component
  Filter,
  useRecordContext,
  useDataProvider,
  useGetMany,
  TopToolbar,
  FilterButton,
  ExportButton,
  CreateButton,
  Button,
  // ReferenceField, // Not used
} from 'react-admin';
import { Chip, Box, Typography, Tooltip, CircularProgress, Alert, Card, CardContent, Grid, Divider } from '@mui/material';
import {
  Security as SecurityIcon,
  FilterList as FilterListIcon,
  Share as ShareIcon,
  Download as DownloadIcon,
  SignalWifi4Bar as ConnectedIcon,
  SignalWifiOff as DisconnectedIcon,
  AccountTree as CorrelationIcon,
  Timeline as TimelineIcon,
  Policy as PolicyIcon
} from '@mui/icons-material';

// Import our advanced search components
import { AdvancedSearchDrawer } from '../components/AdvancedSearchDrawer';
import { useAdvancedSearch } from '../services/advancedSearch';
import type { AdvancedSearchFilters } from '../components/AdvancedSearchDrawer';

// Import SignalR context for real-time updates
import { useSignalRContext } from '../contexts/SignalRContext';

// Interface for MITRE technique data from the database
interface MitreTechnique {
  id: number;
  techniqueId: string;
  name: string;
  description: string;
  tactic: string;
  platform: string;
  createdAt: string;
}

// Helper function to provide friendly names for common MITRE techniques
function getTechniqueDisplayName(techniqueId: string): string {
  const commonTechniques: { [key: string]: string } = {
    // Credential Access techniques
    'T1552': 'Unsecured Credentials',
    'T1552.6': 'Unsecured Credentials: Group Policy Preferences',
    'T1552.8': 'Unsecured Credentials: Cloud Instance Metadata API',
    'T1552.9': 'Unsecured Credentials: Container API',
    'T1003': 'OS Credential Dumping',
    
    // Execution techniques
    'T1059': 'Command and Scripting Interpreter',
    'T1059.001': 'PowerShell',
    'T1059.003': 'Windows Command Shell',
    'T1059.004': 'Unix Shell',
    'T1059.005': 'Visual Basic',
    'T1059.006': 'Python',
    'T1059.007': 'JavaScript',
    'T1059.008': 'Network Device CLI',
    
    // Initial Access & Persistence
    'T1078': 'Valid Accounts',
    'T1078.001': 'Default Accounts',
    'T1078.002': 'Domain Accounts',
    'T1078.003': 'Local Accounts',
    'T1078.004': 'Cloud Accounts',
    'T1547': 'Boot or Logon Autostart Execution',
    
    // Process & Service techniques
    'T1055': 'Process Injection',
    'T1055.001': 'Dynamic-link Library Injection',
    'T1055.002': 'Portable Executable Injection',
    'T1055.003': 'Thread Execution Hijacking',
    'T1055.004': 'Asynchronous Procedure Call',
    'T1055.005': 'Thread Local Storage',
    'T1021': 'Remote Services',
    'T1021.001': 'Remote Desktop Protocol',
    'T1021.002': 'SMB/Windows Admin Shares',
    'T1021.003': 'Distributed Component Object Model',
    'T1021.004': 'SSH',
    'T1021.005': 'VNC',
    'T1021.006': 'Windows Remote Management'
  };
  
  return commonTechniques[techniqueId] || techniqueId;
}

// Helper function to create fallback technique when not found in database
const createFallbackTechnique = (techniqueId: string): MitreTechnique => {
  const displayName = getTechniqueDisplayName(techniqueId);
  const tacticMap: { [key: string]: string } = {
    'T1552': 'Credential Access',
    'T1059': 'Execution',
    'T1078': 'Initial Access, Persistence, Privilege Escalation',
    'T1055': 'Defense Evasion, Privilege Escalation',
    'T1003': 'Credential Access',
    'T1021': 'Lateral Movement',
    'T1547': 'Persistence, Privilege Escalation',
    'T1548': 'Privilege Escalation, Defense Evasion'
  };

  const baseTechniqueId = techniqueId.split('.')[0];

  return {
    id: 0, // Fallback ID
    techniqueId,
    name: displayName || `MITRE ATT&CK Technique: ${techniqueId}`,
    description: `MITRE ATT&CK Technique ${techniqueId}: ${displayName || 'No description available'}`,
    tactic: tacticMap[baseTechniqueId] || 'Unknown',
    platform: 'Windows, Linux, macOS, Cloud',
    createdAt: new Date().toISOString()
  };
};

// Custom header component with shield icon - memoized for performance
const SecurityEventsHeader = React.memo(() => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      Security Events
    </Typography>
  </Box>
));

// Custom component for risk level with color coding - memoized for performance
const RiskLevelField = React.memo(({ source }: any) => {
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

  // Get the value from the record using the source field
  const riskLevel = record?.[source] || record?.riskLevel || record?.RiskLevel || 'Unknown';

  return (
    <Chip 
      label={riskLevel} 
      color={getRiskColor(riskLevel)}
      size="small"
    />
  );
});

// Simple correlation indicator for list view
const CorrelationIndicator = React.memo(() => {
  const record = useRecordContext();
  const isCorrelated = record?.isCorrelationBased || false;
  const correlationScore = record?.correlationScore || 0;

  if (!isCorrelated && correlationScore === 0) {
    return null;
  }

  return (
    <Tooltip title={`Correlation Score: ${(correlationScore * 100).toFixed(1)}%`}>
      <CorrelationIcon
        sx={{
          color: correlationScore > 0.7 ? 'error.main' : correlationScore > 0.5 ? 'warning.main' : 'info.main',
          fontSize: 18
        }}
      />
    </Tooltip>
  );
});

// Custom component for displaying correlation context
const CorrelationField = React.memo(({ source }: any) => {
  const record = useRecordContext();

  const correlationIds = record?.correlationIds || [];
  const correlationContext = record?.correlationContext;
  const isCorrelated = record?.isCorrelationBased || false;
  const correlationScore = record?.correlationScore || 0;

  if (!isCorrelated && correlationScore === 0) {
    return null;
  }

  return (
    <Card sx={{ mt: 1, backgroundColor: 'rgba(25, 118, 210, 0.08)' }}>
      <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
        <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
          <CorrelationIcon sx={{ mr: 1, color: 'primary.main', fontSize: 20 }} />
          <Typography variant="subtitle2" color="primary">
            Correlation Information
          </Typography>
        </Box>

        {correlationContext && (
          <Typography variant="body2" sx={{ mb: 1 }}>
            {correlationContext}
          </Typography>
        )}

        <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', alignItems: 'center' }}>
          {correlationScore > 0 && (
            <Chip
              icon={<TimelineIcon />}
              label={`Score: ${(correlationScore * 100).toFixed(1)}%`}
              size="small"
              color="primary"
              variant="outlined"
            />
          )}

          {correlationIds && correlationIds.length > 0 && (
            <Chip
              label={`${correlationIds.length} correlation${correlationIds.length > 1 ? 's' : ''}`}
              size="small"
              color="info"
              variant="outlined"
            />
          )}

          {isCorrelated && (
            <Chip
              label="Correlated Event"
              size="small"
              color="secondary"
              variant="outlined"
            />
          )}
        </Box>
      </CardContent>
    </Card>
  );
});

// Optimized MITRE Techniques Field using useGetMany for batch fetching
const MitreTechniquesField = React.memo(({ source }: any) => {
  const record = useRecordContext();
  const rawTechniques = record?.[source] || record?.mitreAttack || record?.MitreAttack || [];

  // Filter and clean technique IDs - only get techniques (T*), not tactics (TA*)
  const techniqueIds = useMemo(() => {
    return rawTechniques
      .map((t: string) => t?.trim().toUpperCase())
      .filter(Boolean)
      .filter((id: string) => id.startsWith('T') && !id.startsWith('TA'));
  }, [rawTechniques.join(',')]);

  // Use useGetMany for optimized batch fetching - React Admin handles deduplication
  const { data: mitreData, isLoading, error } = useGetMany(
    'mitre/techniques',
    { ids: techniqueIds },
    {
      enabled: techniqueIds.length > 0,
      // React Query will cache these - MITRE techniques rarely change
      staleTime: 600000 // 10 minutes
    }
  );

  // Create a lookup map for quick access
  const mitreMap = useMemo(() => {
    const map: { [key: string]: MitreTechnique } = {};
    mitreData?.forEach((technique: any) => {
      // Handle both id and techniqueId as keys
      if (technique.techniqueId) {
        map[technique.techniqueId.toUpperCase()] = technique;
      }
      if (technique.id && typeof technique.id === 'string') {
        map[technique.id.toUpperCase()] = technique;
      }
    });
    return map;
  }, [mitreData]);

  // Tactic names for TA* IDs
  const tacticNames: { [key: string]: string } = {
    'TA0001': 'Initial Access',
    'TA0002': 'Execution',
    'TA0003': 'Persistence',
    'TA0004': 'Privilege Escalation',
    'TA0005': 'Defense Evasion',
    'TA0006': 'Credential Access',
    'TA0007': 'Discovery',
    'TA0008': 'Lateral Movement',
    'TA0009': 'Collection',
    'TA0010': 'Exfiltration',
    'TA0011': 'Command and Control',
    'TA0040': 'Impact',
    'TA0042': 'Resource Development',
    'TA0043': 'Reconnaissance'
  };

  // Helper to get tooltip content
  const getTechniqueTooltip = (techniqueId: string): React.ReactNode => {
    const cleanId = techniqueId?.trim().toUpperCase() || '';

    // Handle tactics (TA*)
    if (cleanId.startsWith('TA')) {
      const tacticName = tacticNames[cleanId] || 'Unknown Tactic';
      return (
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 'bold' }}>
            {tacticName}
          </Typography>
          <Typography variant="body2" sx={{ mt: 0.5 }}>
            MITRE ATT&CK Tactic: {cleanId}
          </Typography>
        </Box>
      );
    }

    // Handle techniques (T*)
    const mitreInfo = mitreMap[cleanId];

    if (isLoading) {
      return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CircularProgress size={16} />
          <span>Loading...</span>
        </Box>
      );
    }

    if (mitreInfo) {
      return (
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 'bold' }}>
            {mitreInfo.name}
          </Typography>
          <Typography variant="body2" sx={{ mt: 0.5 }}>
            {mitreInfo.description}
          </Typography>
          {mitreInfo.tactic && (
            <Typography variant="caption" sx={{ mt: 0.5, display: 'block', fontStyle: 'italic' }}>
              Tactic: {mitreInfo.tactic}
            </Typography>
          )}
        </Box>
      );
    }

    // Fallback to display name if not in database
    const fallback = createFallbackTechnique(cleanId);
    return (
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 'bold' }}>
          {fallback.name}
        </Typography>
        <Typography variant="body2" sx={{ mt: 0.5 }}>
          {fallback.description}
        </Typography>
      </Box>
    );
  };

  const getAdditionalTechniquesTooltip = (techniques: string[]): React.ReactNode => {
    return (
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 'bold', mb: 1 }}>
          Additional Techniques:
        </Typography>
        {techniques.map((techniqueId, index) => {
          const cleanId = techniqueId?.trim().toUpperCase() || '';
          const mitreInfo = mitreMap[cleanId];
          const name = mitreInfo?.name || tacticNames[cleanId] || getTechniqueDisplayName(cleanId);
          return (
            <Typography key={index} variant="body2" sx={{ mb: 0.5 }}>
              • {cleanId}: {name}
            </Typography>
          );
        })}
      </Box>
    );
  };

  if (error) {
    console.error('[MitreTechniquesField] Error loading MITRE techniques:', error);
  }

  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
      {rawTechniques.slice(0, 3).map((technique: string, index: number) => (
        <Tooltip
          key={index}
          title={getTechniqueTooltip(technique)}
          arrow
          placement="top"
          componentsProps={{
            tooltip: {
              sx: { maxWidth: 400 }
            }
          }}
        >
          <Chip
            label={technique.trim()}
            variant="outlined"
            size="small"
          />
        </Tooltip>
      ))}
      {rawTechniques.length > 3 && (
        <Tooltip
          title={getAdditionalTechniquesTooltip(rawTechniques.slice(3))}
          arrow
          placement="top"
          componentsProps={{
            tooltip: {
              sx: { maxWidth: 400 }
            }
          }}
        >
          <Chip
            label={`+${rawTechniques.length - 3} more`}
            variant="outlined"
            size="small"
            color="primary"
          />
        </Tooltip>
      )}
    </Box>
  );
});

// React Admin v5.11.1 - filters are passed as array directly to List component
const securityEventFilters = [
  <TextInput key="eventType" source="eventType" label="Event Type" alwaysOn />,
  <SelectInput 
    key="riskLevel"
    source="riskLevels" 
    label="Risk Level"
    choices={[
      { id: 'low', name: 'Low' },
      { id: 'medium', name: 'Medium' },
      { id: 'high', name: 'High' },
      { id: 'critical', name: 'Critical' },
    ]}
    alwaysOn
  />,
  <TextInput key="machine" source="machines" label="Machine" />,
  <TextInput key="user" source="users" label="User" />,
  <TextInput key="source" source="sources" label="Source" />
];

export const SecurityEventList = () => {
  // Cache clearing removed for development - no caching in use

  // Remove cache key logging to prevent render loop

  // SignalR connection for real-time updates - now consolidated for all event types
  const {
    isConnected,
    connectionState,
    latestSecurityEvents,
    latestCorrelationAlerts,
    latestYaraMatches
  } = useSignalRContext();

  // Use the advanced search hook with URL synchronization
  const {
    state,
    openDrawer,
    closeDrawer,
    performSearch,
    updateFilters,
    clearSearch,
    savedSearches,
    saveCurrentSearch,
    deleteSavedSearch,
    exportResults,
    getShareableURL
  } = useAdvancedSearch({
    syncWithURL: true,
    debounceMs: 300
  });

  // Handle search from the drawer
  const handleSearch = async (filters: AdvancedSearchFilters) => {
    // Convert UI filters to API request format
    const apiFilters = {
      ...filters,
      // Convert Date objects to ISO strings
      startDate: filters.startDate?.toISOString(),
      endDate: filters.endDate?.toISOString(),
    };

    await performSearch(apiFilters);
    closeDrawer();
  };

  // Handle clear all filters
  const handleClearAll = () => {
    clearSearch();
    updateFilters({});
  };

  // Custom toolbar with advanced search button
  const SecurityEventsActions = () => (
    <TopToolbar>
      <FilterButton />
      <CreateButton />
      <ExportButton />
      
      {/* Advanced Search Button */}
      <Button
        onClick={openDrawer}
        label="Advanced Search"
        startIcon={<FilterListIcon />}
      />
      
      {/* Share Search Button */}
      {Object.keys(state.currentFilters).length > 0 && (
        <Button
          onClick={() => {
            const url = getShareableURL();
            navigator.clipboard.writeText(url);
            // You could show a notification here
          }}
          label="Share Search"
          startIcon={<ShareIcon />}
        />
      )}
      
      {/* Export Results Button */}
      {state.lastSearchResults && (
        <Button
          onClick={() => exportResults('csv')}
          label="Export CSV"
          startIcon={<DownloadIcon />}
          disabled={state.isLoading}
        />
      )}

      {/* Real-time Connection Status */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, ml: 1 }}>
        {/* System Metrics SignalR Status */}
        <Tooltip title={`System metrics: ${connectionState}`}>
          <Box sx={{ display: 'flex', alignItems: 'center' }}>
            {isConnected ? (
              <ConnectedIcon color="success" fontSize="small" />
            ) : (
              <DisconnectedIcon color="error" fontSize="small" />
            )}
            <Typography variant="caption" sx={{ ml: 0.5, color: isConnected ? 'success.main' : 'error.main' }}>
              Metrics
            </Typography>
          </Box>
        </Tooltip>

        {/* Security Events SignalR Status */}
        <Tooltip title={`Security events: ${isConnected ? 'Connected' : 'Disconnected'}`}>
          <Box sx={{ display: 'flex', alignItems: 'center' }}>
            {isConnected ? (
              <SecurityIcon color="success" fontSize="small" />
            ) : (
              <SecurityIcon color="error" fontSize="small" />
            )}
            <Typography variant="caption" sx={{ ml: 0.5, color: isConnected ? 'success.main' : 'error.main' }}>
              Events
            </Typography>
          </Box>
        </Tooltip>

        {/* Live Indicator */}
        <Typography variant="caption" sx={{
          color: isConnected ? 'success.main' : 'error.main',
          fontWeight: 'bold'
        }}>
          {isConnected ? '🟢 LIVE' : '🔴 OFFLINE'}
        </Typography>
      </Box>
    </TopToolbar>
  );

  return (
    <Box>
      <SecurityEventsHeader />
      <List 
        filters={securityEventFilters}
        sort={{ field: 'timestamp', order: 'DESC' }}
        perPage={25}
        title=" "
        actions={<SecurityEventsActions />}
        // Apply any filters from advanced search
        filter={state.currentFilters}
        // Show loading state
        loading={state.isLoading}
      >
        {/* Show search summary if results available */}
        {state.lastSearchResults && (
          <Box sx={{ p: 2 }}>
            <Alert severity="info">
              Found {state.lastSearchResults.totalCount.toLocaleString()} results in {state.lastSearchResults.queryMetadata.queryTime}ms
              {state.lastSearchResults.queryMetadata.usedFullTextSearch && ' (using full-text search)'}
            </Alert>
          </Box>
        )}
        
        {/* Show error if any */}
        {state.error && (
          <Box sx={{ p: 2 }}>
            <Alert severity="error">
              {state.error}
            </Alert>
          </Box>
        )}

        <Datagrid rowClick="show" size="small">
          <TextField source="id" />
          <TextField source="eventType" sortable={false} />
          <RiskLevelField source="riskLevel" label="Risk Level" />
          <CorrelationIndicator />
          <NumberField source="correlationScore" options={{ minimumFractionDigits: 2, maximumFractionDigits: 2 }} />
          <NumberField source="confidence" options={{ minimumFractionDigits: 0, maximumFractionDigits: 0 }} />
          <TextField source="machine" sortable={false} label="Machine" />
          <TextField source="user" sortable={false} />
          <MitreTechniquesField source="mitreAttack" label="MITRE Techniques" sortable={false} />
          <TextField source="source" sortable={false} label="Source" />
          <DateField source="timestamp" showTime />
        </Datagrid>
      </List>

      {/* Advanced Search Drawer */}
      <AdvancedSearchDrawer
        open={state.isDrawerOpen}
        onClose={closeDrawer}
        onSearch={handleSearch}
        onClearAll={handleClearAll}
        initialFilters={{
          // Convert API filters back to UI format
          ...state.currentFilters,
          startDate: state.currentFilters.startDate 
            ? new Date(state.currentFilters.startDate) 
            : undefined,
          endDate: state.currentFilters.endDate 
            ? new Date(state.currentFilters.endDate) 
            : undefined,
        }}
        isLoading={state.isLoading}
        searchResults={state.lastSearchResults ? {
          total: state.lastSearchResults.totalCount,
          queryTime: state.lastSearchResults.queryMetadata.queryTime
        } : undefined}
      />
    </Box>
  );
};

// Component to show link to Security Event Rule
const ViewRuleButton = () => {
  const record = useRecordContext();
  const dataProvider = useDataProvider();
  const [ruleId, setRuleId] = React.useState<number | null>(null);
  const [loading, setLoading] = React.useState(false);

  useEffect(() => {
    if (!record || !record.eventId) return;

    setLoading(true);

    // The numeric Windows Event ID is already available in record.eventId
    const eventIdNum = typeof record.eventId === 'string'
      ? parseInt(record.eventId, 10)
      : record.eventId;

    // Use the source field to determine the channel (defaults to Security)
    const channel = record.source || 'Security';

    // Try to find the rule for this event - fetch all rules
    dataProvider.getList('security-event-rules', {
      pagination: { page: 1, perPage: 100 },
      sort: { field: 'eventId', order: 'ASC' },
      filter: {}
    })
    .then((response: any) => {
      // Find rule matching this event's EventId and Channel
      const matchingRule = response.data.find((rule: any) => {
        const ruleEventId = typeof rule.eventId === 'string' ? parseInt(rule.eventId, 10) : rule.eventId;
        return ruleEventId === eventIdNum &&
          (rule.channel === channel || rule.channel === 'Security');
      });
      if (matchingRule) {
        setRuleId(matchingRule.id);
      }
    })
    .catch(() => {
      // Ignore errors
    })
    .finally(() => {
      setLoading(false);
    });
  }, [record, dataProvider]);

  if (!record) return null;

  return (
    <Box sx={{ mt: 2, mb: 2 }}>
      {loading ? (
        <CircularProgress size={20} />
      ) : ruleId ? (
        <Button
          href={`/#/security-event-rules/${ruleId}/show`}
          label="View Detection Rule"
          startIcon={<PolicyIcon />}
          variant="contained"
          size="small"
        />
      ) : (
        <Typography variant="body2" color="text.secondary">
          No detection rule configured for Event ID {record.eventId}
        </Typography>
      )}
    </Box>
  );
};

// Custom show layout component for better card layout
const SecurityEventShowLayout = () => {
  const record = useRecordContext();

  if (!record) return null;

  return (
    <Box sx={{ p: 2 }}>
      <Grid container spacing={3}>
        {/* Event Overview Card */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Box display="flex" alignItems="center" gap={1} mb={2}>
                <SecurityIcon color="primary" />
                <Typography variant="h6">Event Overview</Typography>
              </Box>
              <Divider sx={{ mb: 3 }} />

              <Grid container spacing={3}>
                <Grid item xs={12} md={6}>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Event ID</Typography>
                    <Typography variant="body1" fontWeight="medium">{record.id}</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Windows Event ID</Typography>
                    <Typography variant="body1" fontWeight="medium">{record.eventId}</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Event Type</Typography>
                    <Typography variant="body1">{record.eventType}</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Source</Typography>
                    <Typography variant="body1">{record.source}</Typography>
                  </Box>
                </Grid>
                <Grid item xs={12} md={6}>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Risk Level</Typography>
                    <Box mt={0.5}><RiskLevelField source="riskLevel" /></Box>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Confidence Score</Typography>
                    <Typography variant="body1" fontWeight="medium">{record.confidence}%</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Correlation Score</Typography>
                    <Typography variant="body1" fontWeight="medium">
                      {record.correlationScore?.toFixed(2) || '0.00'}
                    </Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Timestamp</Typography>
                    <Typography variant="body1">
                      {new Date(record.timestamp).toLocaleString()}
                    </Typography>
                  </Box>
                </Grid>
              </Grid>

              <ViewRuleButton />
            </CardContent>
          </Card>
        </Grid>

        {/* System & User Information Card */}
        <Grid item xs={12} md={6}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>System & User Information</Typography>
              <Divider sx={{ mb: 3 }} />

              <Box mb={2}>
                <Typography variant="caption" color="textSecondary">Machine</Typography>
                <Typography variant="body1" fontWeight="medium">{record.machine || 'N/A'}</Typography>
              </Box>
              <Box mb={2}>
                <Typography variant="caption" color="textSecondary">User</Typography>
                <Typography variant="body1">{record.user || 'N/A'}</Typography>
              </Box>
              {record.ipAddresses && (
                <Box>
                  <Typography variant="caption" color="textSecondary">IP Addresses</Typography>
                  <Typography variant="body2" sx={{ mt: 0.5 }}>{record.ipAddresses}</Typography>
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* MITRE ATT&CK Card */}
        <Grid item xs={12} md={6}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>MITRE ATT&CK Techniques</Typography>
              <Divider sx={{ mb: 3 }} />

              {record.mitreAttack ? (
                <MitreTechniquesField source="mitreAttack" />
              ) : (
                <Typography variant="body2" color="textSecondary">No MITRE techniques identified</Typography>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Event Message Card */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>Event Message</Typography>
              <Divider sx={{ mb: 3 }} />
              <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                {record.message || 'No message available'}
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        {/* Recommended Actions Card */}
        {record.recommendedActions && (
          <Grid item xs={12}>
            <Card elevation={2}>
              <CardContent>
                <Typography variant="h6" gutterBottom>Recommended Actions</Typography>
                <Divider sx={{ mb: 3 }} />
                <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>
                  {record.recommendedActions}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* IP Enrichment Card */}
        {record.enrichedIPs && (
          <Grid item xs={12}>
            <Card elevation={2}>
              <CardContent>
                <Typography variant="h6" gutterBottom>IP Enrichment Data</Typography>
                <Divider sx={{ mb: 3 }} />
                <Typography variant="body2" component="pre" sx={{
                  whiteSpace: 'pre-wrap',
                  fontFamily: 'monospace',
                  fontSize: '0.875rem'
                }}>
                  {JSON.stringify(record.enrichedIPs, null, 2)}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* Correlation Context Card */}
        {record.correlationContext && (
          <Grid item xs={12}>
            <Card elevation={2}>
              <CardContent>
                <Typography variant="h6" gutterBottom>Correlation Context</Typography>
                <Divider sx={{ mb: 3 }} />
                <CorrelationField source="correlationContext" />
              </CardContent>
            </Card>
          </Grid>
        )}
      </Grid>
    </Box>
  );
};

export const SecurityEventShow = () => (
  <Box>
    <SecurityEventsHeader />
    <Show title=" ">
      <SecurityEventShowLayout />
    </Show>
  </Box>
);

export const SecurityEventEdit = () => (
  <Box>
    <SecurityEventsHeader />
    <Edit title=" ">
      <SimpleForm>
      <TextInput disabled source="id" />
      <TextInput disabled source="eventId" />
      <TextInput source="eventType" validate={required()} />
      <SelectInput 
        source="riskLevel" 
        validate={required()}
        choices={[
          { id: 'low', name: 'Low' },
          { id: 'medium', name: 'Medium' },
          { id: 'high', name: 'High' },
          { id: 'critical', name: 'Critical' },
        ]}
      />
      <TextInput disabled source="correlationScore" />
      <TextInput disabled source="confidence" />
      <TextInput disabled source="ipAddresses" label="IP Addresses" />
      <TextInput disabled source="machine" />
      <TextInput disabled source="user" />
      <TextInput disabled source="mitreAttack" label="MITRE Techniques" />
      <TextInput disabled source="source" label="Source" />
      <TextInput 
        source="message" 
        label="Message"
        multiline
        rows={3}
        sx={{ width: '100%' }}
      />
      <TextInput 
        source="recommendedActions" 
        multiline
        rows={3}
        sx={{ width: '100%' }}
      />
      <TextInput multiline rows={2} source="notes" label="Investigation Notes" />
    </SimpleForm>
    </Edit>
  </Box>
);

export const SecurityEventCreate = () => (
  <Box>
    <SecurityEventsHeader />
    <Create title=" ">
      <SimpleForm>
      <TextInput source="eventType" validate={required()} />
      <SelectInput 
        source="riskLevel" 
        validate={required()}
        choices={[
          { id: 'low', name: 'Low' },
          { id: 'medium', name: 'Medium' },
          { id: 'high', name: 'High' },
          { id: 'critical', name: 'Critical' },
        ]}
      />
      <TextInput source="correlationScore" validate={required()} />
      <TextInput source="confidence" validate={required()} />
      <TextInput source="machine" />
      <TextInput source="user" />
      <TextInput source="mitreAttack" label="MITRE Techniques" />
      <TextInput source="source" label="Source" validate={required()} />
      <TextInput 
        source="message" 
        label="Message"
        multiline
        rows={3}
        validate={required()}
        sx={{ width: '100%' }}
      />
      <TextInput 
        source="recommendedActions" 
        multiline
        rows={3}
        sx={{ width: '100%' }}
      />
    </SimpleForm>
    </Create>
  </Box>
);

