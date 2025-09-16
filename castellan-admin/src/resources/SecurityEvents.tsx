import React, { useState, useEffect } from 'react';
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
  TopToolbar,
  FilterButton,
  ExportButton,
  CreateButton,
  Button,
  // ReferenceField, // Not used
} from 'react-admin';
import { Chip, Box, Typography, Tooltip, CircularProgress, Alert } from '@mui/material';
import {
  Security as SecurityIcon,
  FilterList as FilterListIcon,
  Share as ShareIcon,
  Download as DownloadIcon,
  SignalWifi4Bar as ConnectedIcon,
  SignalWifiOff as DisconnectedIcon
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

// Cache for MITRE technique lookups to avoid repeated API calls
const mitreTechniqueCache: { [key: string]: MitreTechnique } = {};

// Hook to fetch MITRE technique data from the database
const useMitreTechniques = () => {
  const [techniques, setTechniques] = useState<{ [key: string]: MitreTechnique }>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dataProvider = useDataProvider();
  
  // Function to create a fallback technique entry
  const createFallbackTechnique = (techniqueId: string): MitreTechnique => {
    const displayName = getTechniqueDisplayName(techniqueId);
    const tacticMap: { [key: string]: string } = {
      'T1552': 'Credential Access',
      'T1059': 'Execution',
      'T1078': 'Initial Access, Persistence, Privilege Escalation',
      'T1055': 'Defense Evasion, Privilege Escalation',
      'T1003': 'Credential Access',
      'T1021': 'Lateral Movement',
      'T1547': 'Persistence, Privilege Escalation'
    };
    
    // Get base technique ID (before the dot)
    const baseTechniqueId = techniqueId.split('.')[0];
    
    return {
      id: 0,
      techniqueId,
      name: displayName || `MITRE ATT&CK Technique: ${techniqueId}`,
      description: `MITRE ATT&CK Technique ${techniqueId}: ${displayName || 'No description available'}`,
      tactic: tacticMap[baseTechniqueId] || 'Unknown',
      platform: 'Windows, Linux, macOS, Cloud',
      createdAt: new Date().toISOString()
    };
  };

  const fetchTechniques = async (techniqueIds: string[]) => {
    if (techniqueIds.length === 0) return;
    
    // Filter out techniques we already have and invalid IDs
    const missingTechniques = techniqueIds
      .filter(id => !techniques[id] && id && id !== 'undefined')
      .filter(id => id.startsWith('T') && !id.startsWith('TA')); // Only get techniques, not tactics
      
    if (missingTechniques.length === 0) return;

    setLoading(true);
    setError(null);

    try {
      // Process techniques in parallel but with batching
      const batchSize = 5;
      const batches = [];
      for (let i = 0; i < missingTechniques.length; i += batchSize) {
        batches.push(missingTechniques.slice(i, i + batchSize));
      }

      const newTechniques = {};
      
      for (const batch of batches) {
        const batchResults = await Promise.all(
          batch.map(async (techniqueId) => {
            try {
              const response = await dataProvider.getOne('mitre/techniques', { id: techniqueId });
              return { [techniqueId]: response.data };
            } catch (err: any) {
              // Create fallback with proper tactic mapping
              const baseTechniqueId = techniqueId.split('.')[0];
              const tacticMap: { [key: string]: string } = {
                'T1552': 'Credential Access',
                'T1059': 'Execution',
                'T1078': 'Initial Access, Persistence, Privilege Escalation',
                'T1055': 'Defense Evasion, Privilege Escalation',
                'T1003': 'Credential Access',
                'T1021': 'Lateral Movement',
                'T1547': 'Persistence, Privilege Escalation'
              };

              const displayName = getTechniqueDisplayName(techniqueId);
              const fallback = {
                id: 0,
                techniqueId,
                name: displayName || `MITRE ATT&CK Technique: ${techniqueId}`,
                description: `${displayName || techniqueId}: ${tacticMap[baseTechniqueId] || 'Unknown Tactic'}`,
                tactic: tacticMap[baseTechniqueId] || 'Unknown',
                platform: 'Windows, Linux, macOS, Cloud',
                createdAt: new Date().toISOString()
              };

              console.warn(`MITRE technique ${techniqueId} not found in database - may need to import MITRE data`);
              return { [techniqueId]: fallback };
            }
          })
        );

        // Merge batch results
        Object.assign(newTechniques, ...batchResults);
      }
      
      setTechniques(prev => ({ ...prev, ...newTechniques }));
    } catch (err) {
      console.error('Error fetching MITRE techniques:', err);
      setError('Failed to load technique descriptions');
    } finally {
      setLoading(false);
    }
  };

  return { techniques, loading, error, fetchTechniques };
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

// Custom component for MITRE ATT&CK techniques with database-driven tooltips
const MitreTechniquesField = ({ source }: any) => {
  const record = useRecordContext();
  const techniques = record?.[source] || record?.mitreAttack || record?.MitreAttack || [];
  const { techniques: mitreData, loading, error, fetchTechniques } = useMitreTechniques();
  
  // Extract technique IDs and fetch data when component mounts or techniques change
  useEffect(() => {
    if (techniques.length > 0) {
      // Filter out tactic IDs (TA*) and only fetch technique IDs (T*)
      const techniqueIds = techniques
        .map((t: string) => t?.trim().toUpperCase())
        .filter(Boolean)
        .filter((id: string) => id.startsWith('T') && !id.startsWith('TA')); // Only techniques, not tactics
      
      if (techniqueIds.length > 0) {
        fetchTechniques(techniqueIds);
      }
    }
  }, [techniques.join(','), fetchTechniques]);

  // Helper function to get description for a technique/tactic from database
  const getTechniqueTooltip = (technique: string): React.ReactNode => {
    const cleanTechnique = technique?.trim().toUpperCase() || '';
    
    // Check if this is a tactic (TA*) or technique (T*)
    if (cleanTechnique.startsWith('TA')) {
      // Handle tactic IDs - provide static information
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
      
      const tacticName = tacticNames[cleanTechnique] || 'Unknown Tactic';
      return (
        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 'bold' }}>
            {tacticName}
          </Typography>
          <Typography variant="body2" sx={{ mt: 0.5 }}>
            MITRE ATT&CK Tactic: {cleanTechnique}
          </Typography>
        </Box>
      );
    }
    
    // Handle technique IDs
    const mitreInfo = mitreData[cleanTechnique];
    
    if (loading) {
      return (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CircularProgress size={16} />
          <span>Loading technique details...</span>
        </Box>
      );
    }
    
    if (error) {
      return `${cleanTechnique} - Error loading details`;
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
          {mitreInfo.platform && (
            <Typography variant="caption" sx={{ display: 'block', fontStyle: 'italic' }}>
              Platform: {mitreInfo.platform}
            </Typography>
          )}
        </Box>
      );
    }
    
    return `MITRE ATT&CK Technique: ${cleanTechnique}`;
  };

  const getAdditionalTechniquesTooltip = (additionalTechniques: string[]): React.ReactNode => {
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
    
    const techniquesList = additionalTechniques.map((t: string) => {
      const cleanTechnique = t?.trim().toUpperCase() || '';
      
      if (cleanTechnique.startsWith('TA')) {
        // Handle tactics
        const tacticName = tacticNames[cleanTechnique] || 'Unknown Tactic';
        return `${cleanTechnique}: ${tacticName}`;
      } else {
        // Handle techniques
        const mitreInfo = mitreData[cleanTechnique];
        return mitreInfo ? `${cleanTechnique}: ${mitreInfo.name}` : cleanTechnique;
      }
    });

    return (
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 'bold', mb: 1 }}>
          Additional Techniques:
        </Typography>
        {techniquesList.map((technique, index) => (
          <Typography key={index} variant="body2" sx={{ mb: 0.5 }}>
            • {technique}
          </Typography>
        ))}
      </Box>
    );
  };
  
  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
      {techniques.slice(0, 3).map((technique: string, index: number) => (
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
      {techniques.length > 3 && (
        <Tooltip
          title={getAdditionalTechniquesTooltip(techniques.slice(3))}
          arrow
          placement="top"
          componentsProps={{
            tooltip: {
              sx: { maxWidth: 400 }
            }
          }}
        >
          <Chip 
            label={`+${techniques.length - 3} more`}
            variant="outlined"
            size="small"
            color="primary"
          />
        </Tooltip>
      )}
    </Box>
  );
};

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

  // SignalR connection for real-time updates
  const { isConnected: signalRConnected, connectionState } = useSignalRContext();

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

export const SecurityEventShow = () => (
  <Box>
    <SecurityEventsHeader />
    <Show title=" ">
      <SimpleShowLayout>
      <TextField source="id" />
      <TextField source="eventId" />
      <TextField source="eventType" />
      <RiskLevelField source="riskLevel" label="Risk Level" />
      <NumberField source="correlationScore" options={{ minimumFractionDigits: 2, maximumFractionDigits: 2 }} />
      <NumberField source="confidence" options={{ minimumFractionDigits: 0, maximumFractionDigits: 0 }} />
      <TextField source="ipAddresses" label="IP Addresses" />
      <TextField source="machine" />
      <TextField source="user" />
      <TextField source="mitreAttack" label="MITRE Techniques" />
      <TextField source="source" label="Source" />
      <TextField source="message" label="Message" />
      <TextField source="recommendedActions" />
      <TextField source="enrichedIPs" label="IP Enrichment" />
      <DateField source="timestamp" showTime />
    </SimpleShowLayout>
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

