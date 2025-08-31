import React from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  NumberField,
  EditButton,
  ShowButton,
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
  // ReferenceField, // Not used
} from 'react-admin';
import { Chip, Box, Typography } from '@mui/material';
import { Security as SecurityIcon } from '@mui/icons-material';

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

// Custom component for MITRE ATT&CK techniques
const MitreTechniquesField = ({ source }: any) => {
  const record = useRecordContext();
  const techniques = record?.[source] || record?.mitreAttack || record?.MitreAttack || [];
  
  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
      {techniques.slice(0, 3).map((technique: string, index: number) => (
        <Chip 
          key={index}
          label={technique.trim()}
          variant="outlined"
          size="small"
        />
      ))}
      {techniques.length > 3 && (
        <Chip 
          label={`+${techniques.length - 3} more`}
          variant="outlined"
          size="small"
          color="primary"
        />
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
      <ShowButton />
      <EditButton />
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