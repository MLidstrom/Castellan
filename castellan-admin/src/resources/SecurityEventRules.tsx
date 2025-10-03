import React from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  BooleanField,
  Show,
  SimpleShowLayout,
  Edit,
  SimpleForm,
  Create,
  TextInput,
  BooleanInput,
  SelectInput,
  NumberInput,
  required,
  useRecordContext,
  FunctionField,
  ChipField,
  ArrayField,
  SingleFieldList,
  NumberField,
  Button,
  useRefresh,
  useNotify,
  useDataProvider,
  TopToolbar,
  EditButton,
  ShowButton,
  FilterButton,
  CreateButton,
  ExportButton,
  useListContext,
  ArrayInput,
  SimpleFormIterator,
} from 'react-admin';
import {
  Card,
  CardContent,
  Typography,
  Chip,
  Box,
  Grid,
  Alert,
  Tooltip,
  Divider,
} from '@mui/material';
import {
  CheckCircle as EnabledIcon,
  Cancel as DisabledIcon,
  Security as SecurityIcon,
  Refresh as RefreshIcon,
  Warning as WarningIcon,
} from '@mui/icons-material';

// Event channels for SelectInput
const EVENT_CHANNELS = [
  { id: 'Security', name: 'Security' },
  { id: 'Microsoft-Windows-PowerShell/Operational', name: 'PowerShell/Operational' },
  { id: 'System', name: 'System' },
  { id: 'Application', name: 'Application' },
];

// Security event types for SelectInput
const EVENT_TYPES = [
  { id: 'AuthenticationSuccess', name: 'Authentication Success' },
  { id: 'AuthenticationFailure', name: 'Authentication Failure' },
  { id: 'PrivilegeEscalation', name: 'Privilege Escalation' },
  { id: 'ProcessCreation', name: 'Process Creation' },
  { id: 'AccountManagement', name: 'Account Management' },
  { id: 'ServiceInstallation', name: 'Service Installation' },
  { id: 'ScheduledTask', name: 'Scheduled Task' },
  { id: 'SystemStartup', name: 'System Startup' },
  { id: 'SystemShutdown', name: 'System Shutdown' },
  { id: 'SecurityPolicyChange', name: 'Security Policy Change' },
  { id: 'NetworkConnection', name: 'Network Connection' },
  { id: 'PowerShellExecution', name: 'PowerShell Execution' },
  { id: 'Unknown', name: 'Unknown' },
];

// Risk levels for SelectInput
const RISK_LEVELS = [
  { id: 'low', name: 'Low' },
  { id: 'medium', name: 'Medium' },
  { id: 'high', name: 'High' },
  { id: 'critical', name: 'Critical' },
];

// Custom field to display risk level with color coding
const RiskLevelField = React.memo(() => {
  const record = useRecordContext();
  if (!record) return null;

  const getColor = (level: string) => {
    switch (level?.toLowerCase()) {
      case 'critical': return 'error';
      case 'high': return 'warning';
      case 'medium': return 'info';
      case 'low': return 'success';
      default: return 'default';
    }
  };

  return (
    <Chip
      label={record.riskLevel?.toUpperCase() || 'UNKNOWN'}
      color={getColor(record.riskLevel)}
      size="small"
    />
  );
});

// Custom field to display enabled/disabled status
const EnabledStatusField = React.memo(() => {
  const record = useRecordContext();
  if (!record) return null;

  return (
    <Box display="flex" alignItems="center" gap={1}>
      {record.isEnabled ? (
        <>
          <EnabledIcon color="success" fontSize="small" />
          <Chip label="Enabled" color="success" size="small" />
        </>
      ) : (
        <>
          <DisabledIcon color="error" fontSize="small" />
          <Chip label="Disabled" color="error" size="small" />
        </>
      )}
    </Box>
  );
});

// Custom field to display MITRE techniques
const MitreTechniquesField = React.memo(() => {
  const record = useRecordContext();
  if (!record || !record.mitreTechniques || record.mitreTechniques.length === 0) {
    return <Typography variant="caption" color="textSecondary">None</Typography>;
  }

  return (
    <Box display="flex" gap={0.5} flexWrap="wrap">
      {record.mitreTechniques.map((technique: string, index: number) => (
        <Tooltip key={index} title={`MITRE ATT&CK: ${technique}`}>
          <Chip
            label={technique}
            size="small"
            variant="outlined"
            color="primary"
          />
        </Tooltip>
      ))}
    </Box>
  );
});

// Custom field to display recommended actions
const RecommendedActionsField = React.memo(() => {
  const record = useRecordContext();
  if (!record || !record.recommendedActions || record.recommendedActions.length === 0) {
    return <Typography variant="caption" color="textSecondary">None</Typography>;
  }

  return (
    <Box>
      <ul style={{ margin: 0, paddingLeft: '20px' }}>
        {record.recommendedActions.slice(0, 3).map((action: string, index: number) => (
          <li key={index}>
            <Typography variant="body2">{action}</Typography>
          </li>
        ))}
        {record.recommendedActions.length > 3 && (
          <Typography variant="caption" color="textSecondary">
            +{record.recommendedActions.length - 3} more...
          </Typography>
        )}
      </ul>
    </Box>
  );
});

// Cache refresh button
const RefreshCacheButton = () => {
  const notify = useNotify();
  const dataProvider = useDataProvider();
  const [loading, setLoading] = React.useState(false);

  const handleRefresh = async () => {
    setLoading(true);
    try {
      await dataProvider.create('security-event-rules/refresh-cache', { data: {} });
      notify('Rule cache refreshed successfully', { type: 'success' });
    } catch (error: any) {
      notify(`Error refreshing cache: ${error.message}`, { type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Button
      onClick={handleRefresh}
      disabled={loading}
      label="Refresh Cache"
      startIcon={<RefreshIcon />}
    >
      <RefreshIcon />
    </Button>
  );
};

// List actions with refresh cache button
const SecurityEventRulesListActions = () => (
  <TopToolbar>
    <FilterButton />
    <CreateButton />
    <ExportButton />
    <RefreshCacheButton />
  </TopToolbar>
);

// List view with filters and datagrid
export const SecurityEventRuleList = () => {
  const filters = [
    <TextInput key="eventId" source="eventId" label="Event ID" alwaysOn />,
    <SelectInput key="channel" source="channel" label="Channel" choices={EVENT_CHANNELS} />,
    <SelectInput key="eventType" source="eventType" label="Event Type" choices={EVENT_TYPES} />,
    <SelectInput key="riskLevel" source="riskLevel" label="Risk Level" choices={RISK_LEVELS} />,
    <BooleanInput key="isEnabled" source="isEnabled" label="Enabled Only" />,
  ];

  return (
    <List
      filters={filters}
      sort={{ field: 'priority', order: 'DESC' }}
      actions={<SecurityEventRulesListActions />}
    >
      <Datagrid rowClick="show">
        <NumberField source="id" label="ID" />
        <NumberField source="eventId" label="Event ID" />
        <TextField source="channel" label="Channel" />
        <TextField source="eventType" label="Event Type" />
        <FunctionField label="Risk Level" render={() => <RiskLevelField />} />
        <NumberField source="confidence" label="Confidence" />
        <FunctionField label="Status" render={() => <EnabledStatusField />} />
        <NumberField source="priority" label="Priority" />
        <FunctionField label="MITRE Techniques" render={() => <MitreTechniquesField />} />
        <DateField source="updatedAt" label="Last Updated" showTime />
        <EditButton />
        <ShowButton />
      </Datagrid>
    </List>
  );
};

// Custom show layout component for better card layout
const SecurityEventRuleShowLayout = () => {
  const record = useRecordContext();

  if (!record) return null;

  return (
    <Box sx={{ p: 2 }}>
      <Grid container spacing={3}>
        {/* Rule Configuration Card */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Box display="flex" alignItems="center" gap={1} mb={2}>
                <SecurityIcon color="primary" />
                <Typography variant="h6">Rule Configuration</Typography>
              </Box>
              <Divider sx={{ mb: 3 }} />

              <Grid container spacing={3}>
                <Grid item xs={12} md={6}>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Rule ID</Typography>
                    <Typography variant="body1" fontWeight="medium">{record.id}</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Windows Event ID</Typography>
                    <Typography variant="body1" fontWeight="medium">{record.eventId}</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Event Channel</Typography>
                    <Typography variant="body1">{record.channel}</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Security Event Type</Typography>
                    <Typography variant="body1">{record.eventType}</Typography>
                  </Box>
                </Grid>
                <Grid item xs={12} md={6}>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Risk Level</Typography>
                    <Box mt={0.5}><RiskLevelField /></Box>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Confidence Score</Typography>
                    <Typography variant="body1" fontWeight="medium">{record.confidence}%</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Priority</Typography>
                    <Typography variant="body1" fontWeight="medium">{record.priority}</Typography>
                  </Box>
                  <Box mb={2}>
                    <Typography variant="caption" color="textSecondary">Status</Typography>
                    <Box mt={0.5}><EnabledStatusField /></Box>
                  </Box>
                </Grid>
              </Grid>
            </CardContent>
          </Card>
        </Grid>

        {/* Description & Summary Card */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>Description & Summary</Typography>
              <Divider sx={{ mb: 3 }} />

              <Box mb={2}>
                <Typography variant="caption" color="textSecondary">Summary</Typography>
                <Typography variant="body1" sx={{ mt: 0.5 }}>{record.summary}</Typography>
              </Box>

              {record.description && (
                <Box>
                  <Typography variant="caption" color="textSecondary">Detailed Description</Typography>
                  <Typography variant="body2" sx={{ mt: 0.5 }} color="textSecondary">
                    {record.description}
                  </Typography>
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* MITRE ATT&CK Mapping Card */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>MITRE ATT&CK Mapping</Typography>
              <Divider sx={{ mb: 3 }} />
              <MitreTechniquesField />
            </CardContent>
          </Card>
        </Grid>

        {/* Recommended Actions Card */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>Recommended Actions for Analysts</Typography>
              <Divider sx={{ mb: 3 }} />
              <RecommendedActionsField />
            </CardContent>
          </Card>
        </Grid>

        {/* Metadata Card */}
        <Grid item xs={12}>
          <Card elevation={2}>
            <CardContent>
              <Typography variant="h6" gutterBottom>Metadata</Typography>
              <Divider sx={{ mb: 3 }} />

              <Grid container spacing={2}>
                <Grid item xs={12} sm={6} md={4}>
                  <Typography variant="caption" color="textSecondary">Created</Typography>
                  <Typography variant="body2" sx={{ mt: 0.5 }}>
                    {new Date(record.createdAt).toLocaleString()}
                  </Typography>
                </Grid>
                <Grid item xs={12} sm={6} md={4}>
                  <Typography variant="caption" color="textSecondary">Last Updated</Typography>
                  <Typography variant="body2" sx={{ mt: 0.5 }}>
                    {new Date(record.updatedAt).toLocaleString()}
                  </Typography>
                </Grid>
                <Grid item xs={12} sm={6} md={4}>
                  <Typography variant="caption" color="textSecondary">Modified By</Typography>
                  <Typography variant="body2" sx={{ mt: 0.5 }}>
                    {record.modifiedBy || 'System'}
                  </Typography>
                </Grid>
              </Grid>

              {record.tags && record.tags.length > 0 && (
                <Box mt={2}>
                  <Typography variant="caption" color="textSecondary">Tags</Typography>
                  <Box display="flex" gap={0.5} flexWrap="wrap" mt={1}>
                    {record.tags.map((tag: string, idx: number) => (
                      <Chip key={idx} label={tag} size="small" variant="outlined" />
                    ))}
                  </Box>
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};

// Show view with improved layout
export const SecurityEventRuleShow = () => (
  <Show>
    <SecurityEventRuleShowLayout />
  </Show>
);

// Create view
export const SecurityEventRuleCreate = () => (
  <Create>
    <SimpleForm>
      <Alert severity="info" sx={{ mb: 2 }}>
        Create a new security event detection rule. Rules will be cached and used for real-time event detection.
      </Alert>

      <Typography variant="h6" gutterBottom>Event Configuration</Typography>
      <NumberInput source="eventId" label="Windows Event ID" validate={required()} helperText="e.g., 4624, 4625, 4104" />
      <SelectInput source="channel" label="Event Channel" choices={EVENT_CHANNELS} validate={required()} />
      <SelectInput source="eventType" label="Security Event Type" choices={EVENT_TYPES} validate={required()} />

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Risk Assessment</Typography>
      <SelectInput source="riskLevel" label="Risk Level" choices={RISK_LEVELS} validate={required()} defaultValue="medium" />
      <NumberInput source="confidence" label="Confidence Score (0-100)" validate={required()} defaultValue={75} min={0} max={100} />
      <NumberInput source="priority" label="Priority" defaultValue={100} helperText="Higher priority rules take precedence" />

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Description</Typography>
      <TextInput source="summary" label="Summary" validate={required()} fullWidth multiline />
      <TextInput source="description" label="Detailed Description" fullWidth multiline rows={3} />

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>MITRE ATT&CK Mapping</Typography>
      <ArrayInput source="mitreTechniques" label="MITRE Techniques">
        <SimpleFormIterator inline>
          <TextInput source="" label="Technique ID" helperText="e.g., T1078, T1110" />
        </SimpleFormIterator>
      </ArrayInput>

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Analyst Guidance</Typography>
      <ArrayInput source="recommendedActions" label="Recommended Actions" validate={required()}>
        <SimpleFormIterator>
          <TextInput source="" label="Action" fullWidth />
        </SimpleFormIterator>
      </ArrayInput>

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Options</Typography>
      <BooleanInput source="isEnabled" label="Enable this rule" defaultValue={true} />
      <ArrayInput source="tags" label="Tags">
        <SimpleFormIterator inline>
          <TextInput source="" label="Tag" />
        </SimpleFormIterator>
      </ArrayInput>
    </SimpleForm>
  </Create>
);

// Edit view
export const SecurityEventRuleEdit = () => (
  <Edit>
    <SimpleForm>
      <Alert severity="warning" sx={{ mb: 2 }}>
        <Typography variant="body2">
          <strong>Note:</strong> Changes to rules require a cache refresh to take effect immediately.
          Rules are automatically refreshed every 5 minutes, or use the "Refresh Cache" button in the list view.
        </Typography>
      </Alert>

      <Typography variant="h6" gutterBottom>Event Configuration</Typography>
      <NumberField source="id" label="Rule ID" />
      <NumberInput source="eventId" label="Windows Event ID" validate={required()} />
      <SelectInput source="channel" label="Event Channel" choices={EVENT_CHANNELS} validate={required()} />
      <SelectInput source="eventType" label="Security Event Type" choices={EVENT_TYPES} validate={required()} />

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Risk Assessment</Typography>
      <SelectInput source="riskLevel" label="Risk Level" choices={RISK_LEVELS} validate={required()} />
      <NumberInput source="confidence" label="Confidence Score (0-100)" validate={required()} min={0} max={100} />
      <NumberInput source="priority" label="Priority" helperText="Higher priority rules take precedence" />

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Description</Typography>
      <TextInput source="summary" label="Summary" validate={required()} fullWidth multiline />
      <TextInput source="description" label="Detailed Description" fullWidth multiline rows={3} />

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>MITRE ATT&CK Mapping</Typography>
      <ArrayInput source="mitreTechniques" label="MITRE Techniques">
        <SimpleFormIterator inline>
          <TextInput source="" label="Technique ID" />
        </SimpleFormIterator>
      </ArrayInput>

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Analyst Guidance</Typography>
      <ArrayInput source="recommendedActions" label="Recommended Actions" validate={required()}>
        <SimpleFormIterator>
          <TextInput source="" label="Action" fullWidth />
        </SimpleFormIterator>
      </ArrayInput>

      <Typography variant="h6" gutterBottom sx={{ mt: 2 }}>Options</Typography>
      <BooleanInput source="isEnabled" label="Enable this rule" />
      <ArrayInput source="tags" label="Tags">
        <SimpleFormIterator inline>
          <TextInput source="" label="Tag" />
        </SimpleFormIterator>
      </ArrayInput>

      <Divider sx={{ my: 2 }} />
      <Typography variant="caption" color="textSecondary">
        Last modified by: <TextField source="modifiedBy" />
      </Typography>
    </SimpleForm>
  </Edit>
);
