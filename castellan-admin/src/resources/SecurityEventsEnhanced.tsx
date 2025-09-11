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
  Filter,
  useRecordContext,
  useDataProvider,
  DateInput,
  SearchInput,
  AutocompleteArrayInput,
  ReferenceArrayInput,
  FilterButton,
  CreateButton,
  ExportButton,
  TopToolbar,
  SelectColumnsButton,
  FilterList,
  FilterListItem,
  FilterLiveSearch,
  SavedQueriesList,
} from 'react-admin';
import { 
  Chip, 
  Box, 
  Typography, 
  Tooltip, 
  Paper,
  Stack,
  Divider,
  Card,
  CardContent,
  Button,
  IconButton,
  Collapse,
  Grid,
} from '@mui/material';
import { 
  Security as SecurityIcon,
  ExpandMore as ExpandMoreIcon,
  ExpandLess as ExpandLessIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  CheckCircle as CheckIcon,
  Info as InfoIcon,
  Search as SearchIcon,
  FilterList as FilterListIcon,
  Download as DownloadIcon,
  ViewColumn as ViewColumnIcon,
} from '@mui/icons-material';
import CustomExportButton from '../components/CustomExportButton';

// Hook to fetch MITRE technique data (reusing from original)
const useMitreTechniques = () => {
  const [techniques, setTechniques] = useState<{ [key: string]: any }>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dataProvider = useDataProvider();

  const fetchTechniques = async (techniqueIds: string[]) => {
    if (techniqueIds.length === 0) return;
    
    const missingTechniques = techniqueIds.filter(id => !techniques[id]);
    if (missingTechniques.length === 0) return;

    setLoading(true);
    setError(null);

    try {
      const promises = missingTechniques.map(async (techniqueId) => {
        try {
          if (!techniqueId || techniqueId === 'undefined') {
            return null;
          }
          
          const response = await dataProvider.getOne('mitre/techniques', { id: techniqueId });
          return { [techniqueId]: response.data };
        } catch (err: any) {
          if (err.status !== 0 && err.status !== 500) {
            console.warn(`MITRE technique ${techniqueId} not found - using fallback data`);
          }
          return { 
            [techniqueId]: {
              id: 0,
              techniqueId,
              name: techniqueId,
              description: `MITRE ATT&CK Technique: ${techniqueId}`,
              tactic: 'Unknown',
              platform: 'Multiple',
              createdAt: new Date().toISOString()
            }
          };
        }
      });

      const results = await Promise.all(promises);
      const validResults = results.filter(r => r !== null);
      const newTechniques = validResults.reduce((acc, curr) => ({ ...acc, ...curr }), {});
      
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

// Custom header component
const SecurityEventsHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      Security Events
    </Typography>
    <Typography variant="body2" sx={{ ml: 2, color: 'text.secondary' }}>
      Advanced Search & Filtering
    </Typography>
  </Box>
);

// Risk level field with color coding
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

  const getRiskIcon = (level: string) => {
    switch (level?.toLowerCase()) {
      case 'critical': return <ErrorIcon fontSize="small" />;
      case 'high': return <WarningIcon fontSize="small" />;
      case 'medium': return <InfoIcon fontSize="small" />;
      case 'low': return <CheckIcon fontSize="small" />;
      default: return undefined;
    }
  };

  const riskLevel = record?.[source] || record?.riskLevel || record?.RiskLevel || 'Unknown';

  return (
    <Chip 
      label={riskLevel} 
      color={getRiskColor(riskLevel)}
      size="small"
      icon={getRiskIcon(riskLevel)}
    />
  );
};

// MITRE Techniques field (simplified from original)
const MitreTechniquesField = ({ source }: any) => {
  const record = useRecordContext();
  const techniques = record?.[source] || record?.mitreAttack || [];
  
  if (!techniques || techniques.length === 0) {
    return <Typography variant="body2" color="text.secondary">None</Typography>;
  }
  
  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
      {techniques.slice(0, 2).map((technique: string, index: number) => (
        <Chip 
          key={index}
          label={technique.trim()}
          variant="outlined"
          size="small"
        />
      ))}
      {techniques.length > 2 && (
        <Chip 
          label={`+${techniques.length - 2} more`}
          variant="outlined"
          size="small"
          color="primary"
        />
      )}
    </Box>
  );
};

// Event type choices for filters
const EVENT_TYPE_CHOICES = [
  { id: 'Failed Login', name: 'Failed Login' },
  { id: 'Successful Login', name: 'Successful Login' },
  { id: 'Privilege Escalation', name: 'Privilege Escalation' },
  { id: 'Malware Detection', name: 'Malware Detection' },
  { id: 'Suspicious Process', name: 'Suspicious Process' },
  { id: 'Registry Modification', name: 'Registry Modification' },
  { id: 'Network Anomaly', name: 'Network Anomaly' },
  { id: 'Data Exfiltration', name: 'Data Exfiltration' },
  { id: 'Command Execution', name: 'Command Execution' },
  { id: 'Service Creation', name: 'Service Creation' },
];

const RISK_LEVEL_CHOICES = [
  { id: 'low', name: 'Low' },
  { id: 'medium', name: 'Medium' },
  { id: 'high', name: 'High' },
  { id: 'critical', name: 'Critical' },
];

const STATUS_CHOICES = [
  { id: 'new', name: 'New' },
  { id: 'investigating', name: 'Investigating' },
  { id: 'resolved', name: 'Resolved' },
  { id: 'false_positive', name: 'False Positive' },
];


// Enhanced filters for the list view
const SecurityEventFilters = [
  // Primary search bar - full text search
  <SearchInput source="search" alwaysOn placeholder="Search all fields..." />,
  
  // Multi-select filters
  <AutocompleteArrayInput source="eventTypes" label="Event Types" choices={EVENT_TYPE_CHOICES} />,
  <AutocompleteArrayInput source="riskLevels" label="Risk Levels" choices={RISK_LEVEL_CHOICES} />,
  
  // Date range
  <DateInput source="startDate" label="From Date" />,
  <DateInput source="endDate" label="To Date" />,
  
  // Additional text filters
  <TextInput source="machine" label="Machine" />,
  <TextInput source="user" label="User" />,
  <TextInput source="source" label="Source" />,
  
  // Status and MITRE
  <SelectInput source="status" label="Status" choices={STATUS_CHOICES} />,
  <TextInput source="mitreTechnique" label="MITRE Technique" />,
];

// Aside filters for better organization
const SecurityEventFilterSidebar = () => (
  <Card sx={{ order: -1, mr: 2, mt: 8, width: 250 }}>
    <CardContent>
      <FilterLiveSearch source="search" label="Search" />
      
      <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>
        Risk Level
      </Typography>
      <FilterList label="Risk Level" icon={<WarningIcon />}>
        {RISK_LEVEL_CHOICES.map(choice => (
          <FilterListItem
            key={choice.id}
            label={choice.name}
            value={{ riskLevel: choice.id }}
          />
        ))}
      </FilterList>
      
      <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>
        Status
      </Typography>
      <FilterList label="Status" icon={<InfoIcon />}>
        {STATUS_CHOICES.map(choice => (
          <FilterListItem
            key={choice.id}
            label={choice.name}
            value={{ status: choice.id }}
          />
        ))}
      </FilterList>
      
      <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>
        Date Range
      </Typography>
      <FilterList label="Date Range" icon={<SecurityIcon />}>
        <FilterListItem
          label="Last 24 Hours"
          value={{ 
            startDate: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
            endDate: new Date().toISOString()
          }}
        />
        <FilterListItem
          label="Last 7 Days"
          value={{ 
            startDate: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
            endDate: new Date().toISOString()
          }}
        />
        <FilterListItem
          label="Last 30 Days"
          value={{ 
            startDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
            endDate: new Date().toISOString()
          }}
        />
      </FilterList>
      
      <SavedQueriesList />
    </CardContent>
  </Card>
);

// Custom list actions toolbar
const ListActions = () => (
  <TopToolbar>
    <FilterButton />
    <CreateButton />
    <CustomExportButton />
    <SelectColumnsButton />
  </TopToolbar>
);

// Enhanced Security Event List with advanced filtering
export const SecurityEventList = () => (
  <Box>
    <SecurityEventsHeader />
    <List 
      filters={SecurityEventFilters}
      sort={{ field: 'timestamp', order: 'DESC' }}
      perPage={25}
      title=" "
      actions={<ListActions />}
      aside={<SecurityEventFilterSidebar />}
      sx={{
        '& .RaList-main': {
          '& .MuiPaper-root': {
            boxShadow: 'none',
            borderRadius: 0,
          },
        },
      }}
    >
      <Datagrid 
        rowClick="show" 
        size="small"
        sx={{
          '& .RaDatagrid-headerCell': {
            fontWeight: 600,
          },
        }}
      >
        <TextField source="id" label="ID" />
        <TextField source="eventType" label="Event Type" sortable={false} />
        <RiskLevelField source="riskLevel" label="Risk Level" />
        <NumberField 
          source="correlationScore" 
          label="Correlation"
          options={{ minimumFractionDigits: 2, maximumFractionDigits: 2 }} 
        />
        <NumberField 
          source="confidence" 
          label="Confidence %"
          options={{ minimumFractionDigits: 0, maximumFractionDigits: 0 }} 
        />
        <TextField source="machine" label="Machine" sortable={false} />
        <TextField source="user" label="User" sortable={false} />
        <MitreTechniquesField source="mitreAttack" label="MITRE" sortable={false} />
        <TextField source="source" label="Source" sortable={false} />
        <DateField source="timestamp" label="Time" showTime />
      </Datagrid>
    </List>
  </Box>
);

// Show view (unchanged from original)
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

// Edit view (unchanged from original)
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
          choices={RISK_LEVEL_CHOICES}
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

// Create view
export const SecurityEventCreate = () => (
  <Box>
    <SecurityEventsHeader />
    <Create title=" ">
      <SimpleForm>
        <TextInput source="eventType" validate={required()} />
        <SelectInput 
          source="riskLevel" 
          validate={required()}
          choices={RISK_LEVEL_CHOICES}
        />
        <TextInput source="machine" />
        <TextInput source="user" />
        <TextInput source="source" label="Source" />
        <TextInput 
          source="message" 
          label="Message"
          multiline
          rows={3}
          sx={{ width: '100%' }}
          validate={required()}
        />
        <TextInput source="mitreAttack" label="MITRE Techniques (comma-separated)" />
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
