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
  // ReferenceField, // Not used
} from 'react-admin';
import { Chip, Box, Typography, Tooltip, CircularProgress } from '@mui/material';
import { Security as SecurityIcon } from '@mui/icons-material';

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

// Hook to fetch MITRE technique data from the database
const useMitreTechniques = () => {
  const [techniques, setTechniques] = useState<{ [key: string]: MitreTechnique }>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dataProvider = useDataProvider();

  const fetchTechniques = async (techniqueIds: string[]) => {
    if (techniqueIds.length === 0) return;
    
    // Filter out techniques we already have
    const missingTechniques = techniqueIds.filter(id => !techniques[id]);
    if (missingTechniques.length === 0) return;

    setLoading(true);
    setError(null);

    try {
      // Fetch techniques from the MITRE API
      const promises = missingTechniques.map(async (techniqueId) => {
        try {
          const response = await dataProvider.getOne('mitre/techniques', { id: techniqueId });
          return { [techniqueId]: response.data };
        } catch (err) {
          // If technique not found in database, return a fallback
          console.warn(`Failed to fetch MITRE technique ${techniqueId}:`, err);
          return { 
            [techniqueId]: {
              id: 0,
              techniqueId,
              name: techniqueId,
              description: `MITRE ATT&CK Technique: ${techniqueId} (Not found in database)`,
              tactic: 'Not Available',
              platform: 'Not Available',
              createdAt: new Date().toISOString()
            }
          };
        }
      });

      const results = await Promise.all(promises);
      const newTechniques = results.reduce((acc, curr) => ({ ...acc, ...curr }), {});
      
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

// Custom header component with shield icon
const SecurityEventsHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      Security Events
    </Typography>
  </Box>
);

// Custom component for risk level with color coding
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

  // Get the value from the record using the source field
  const riskLevel = record?.[source] || record?.riskLevel || record?.RiskLevel || 'Unknown';

  return (
    <Chip 
      label={riskLevel} 
      color={getRiskColor(riskLevel)}
      size="small"
    />
  );
};

// Custom component for MITRE ATT&CK techniques with database-driven tooltips
const MitreTechniquesField = ({ source }: any) => {
  const record = useRecordContext();
  const techniques = record?.[source] || record?.mitreAttack || record?.MitreAttack || [];
  const { techniques: mitreData, loading, error, fetchTechniques } = useMitreTechniques();
  
  // Extract technique IDs and fetch data when component mounts or techniques change
  useEffect(() => {
    if (techniques.length > 0) {
      const techniqueIds = techniques.map((t: string) => t.trim().toUpperCase());
      fetchTechniques(techniqueIds);
    }
  }, [techniques.join(','), fetchTechniques]);

  // Helper function to get description for a technique from database
  const getTechniqueTooltip = (technique: string): React.ReactNode => {
    const cleanTechnique = technique.trim().toUpperCase();
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
    const techniquesList = additionalTechniques.map((t: string) => {
      const cleanTechnique = t.trim().toUpperCase();
      const mitreInfo = mitreData[cleanTechnique];
      return mitreInfo ? `${cleanTechnique}: ${mitreInfo.name}` : cleanTechnique;
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

// Filters for the list view
const SecurityEventFilters = [
  <TextInput source="eventType" label="Event Type" alwaysOn />,
  <SelectInput 
    source="riskLevel" 
    label="Risk Level"
    choices={[
      { id: 'low', name: 'Low' },
      { id: 'medium', name: 'Medium' },
      { id: 'high', name: 'High' },
      { id: 'critical', name: 'Critical' },
    ]}
    alwaysOn
  />,
  <TextInput source="machine" label="Machine" />,
  <TextInput source="user" label="User" />,
  <TextInput source="source" label="Source" />,
];

export const SecurityEventList = () => (
  <Box>
    <SecurityEventsHeader />
    <List 
      filters={<Filter>{SecurityEventFilters}</Filter>}
      sort={{ field: 'timestamp', order: 'DESC' }}
      perPage={25}
      title=" "
    >
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
  </Box>
);

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