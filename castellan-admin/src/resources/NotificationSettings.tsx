import React, { useState, useEffect } from 'react';
import {
  List,
  Datagrid,
  TextField,
  BooleanField,
  Create,
  Edit,
  Show,
  SimpleForm,
  SimpleShowLayout,
  TextInput,
  BooleanInput,
  SelectInput,
  ArrayInput,
  SimpleFormIterator,
  NumberInput,
  NumberField,
  DateField,
  useDataProvider,
  useNotify,
  useRefresh,
  useRecordContext,
  SaveButton,
  Toolbar,
  ShowButton,
  EditButton,
  DeleteButton,
  TopToolbar,
  CreateButton,
  ExportButton,
} from 'react-admin';
import {
  Card,
  CardContent,
  Typography,
  Box,
  Button,
  Chip,
  Alert,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  FormControl,
  FormLabel,
  Switch,
  FormControlLabel,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  CircularProgress,
} from '@mui/material';
import {
  ExpandMore as ExpandMoreIcon,
  Notifications as NotificationsIcon,
  Send as TestIcon,
  Check as CheckIcon,
  Error as ErrorIcon,
} from '@mui/icons-material';

// Teams Configuration Component
export const TeamsConfigForm = () => {
  const record = useRecordContext();
  
  return (
    <Box>
      <Typography variant="h6" gutterBottom>
        Microsoft Teams Configuration
      </Typography>
      
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <BooleanInput 
          source="teams.enabled" 
          label="Enable Teams Notifications"
          defaultValue={false}
        />
        
        <TextInput
          source="teams.webhookUrl"
          label="Teams Webhook URL"
          fullWidth
          helperText="Incoming webhook URL from your Teams channel"
          validate={[
            (value: string) => {
              if (!value) return undefined;
              if (!value.includes('outlook.office.com') && !value.includes('teams.microsoft.com')) {
                return 'Must be a valid Microsoft Teams webhook URL';
              }
              return undefined;
            }
          ]}
        />
        
        <TextInput
          source="teams.castellanUrl"
          label="Castellan URL"
          fullWidth
          helperText="Base URL for Castellan instance (for links in notifications)"
          defaultValue="http://localhost:8080"
        />
        
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 2 }}>
          <NumberInput
            source="teams.rateLimitSettings.criticalThrottleMinutes"
            label="Critical Alert Throttle (minutes)"
            defaultValue={0}
            min={0}
            helperText="Minimum time between critical alerts"
          />
          
          <NumberInput
            source="teams.rateLimitSettings.highThrottleMinutes"
            label="High Alert Throttle (minutes)"
            defaultValue={5}
            min={0}
            helperText="Minimum time between high priority alerts"
          />
          
          <NumberInput
            source="teams.rateLimitSettings.mediumThrottleMinutes"
            label="Medium Alert Throttle (minutes)"
            defaultValue={15}
            min={0}
            helperText="Minimum time between medium priority alerts"
          />
          
          <NumberInput
            source="teams.rateLimitSettings.lowThrottleMinutes"
            label="Low Alert Throttle (minutes)"
            defaultValue={60}
            min={0}
            helperText="Minimum time between low priority alerts"
          />
        </Box>
      </Box>
    </Box>
  );
};

// Slack Configuration Component
export const SlackConfigForm = () => {
  const record = useRecordContext();
  
  return (
    <Box>
      <Typography variant="h6" gutterBottom>
        Slack Configuration
      </Typography>
      
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <BooleanInput 
          source="slack.enabled" 
          label="Enable Slack Notifications"
          defaultValue={false}
        />
        
        <TextInput
          source="slack.webhookUrl"
          label="Slack Webhook URL"
          fullWidth
          helperText="Incoming webhook URL from your Slack workspace"
          validate={[
            (value: string) => {
              if (!value) return undefined;
              if (!value.includes('hooks.slack.com')) {
                return 'Must be a valid Slack webhook URL';
              }
              return undefined;
            }
          ]}
        />
        
        <TextInput
          source="slack.castellanUrl"
          label="Castellan URL"
          fullWidth
          helperText="Base URL for Castellan instance (for links in notifications)"
          defaultValue="http://localhost:8080"
        />
        
        <TextInput
          source="slack.defaultChannel"
          label="Default Channel"
          fullWidth
          helperText="Default Slack channel for notifications (e.g., #security-alerts)"
        />
        
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 2 }}>
          <TextInput
            source="slack.criticalChannel"
            label="Critical Alerts Channel"
            fullWidth
            helperText="Specific channel for critical alerts (optional)"
          />
          
          <TextInput
            source="slack.highChannel"
            label="High Priority Alerts Channel"
            fullWidth
            helperText="Specific channel for high priority alerts (optional)"
          />
        </Box>
        
        <ArrayInput source="slack.mentionUsersForCritical" label="Mention Users for Critical Alerts">
          <SimpleFormIterator inline>
            <TextInput 
              source="userId"
              label="User ID"
              helperText="Slack user ID (e.g., U1234567890)"
              validate={[
                (value: string) => {
                  if (value && !value.match(/^U[A-Z0-9]{10}$/)) {
                    return 'Must be a valid Slack user ID format (U followed by 10 characters)';
                  }
                  return undefined;
                }
              ]}
            />
          </SimpleFormIterator>
        </ArrayInput>
        
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 2 }}>
          <NumberInput
            source="slack.rateLimitSettings.criticalThrottleMinutes"
            label="Critical Alert Throttle (minutes)"
            defaultValue={0}
            min={0}
            helperText="Minimum time between critical alerts"
          />
          
          <NumberInput
            source="slack.rateLimitSettings.highThrottleMinutes"
            label="High Alert Throttle (minutes)"
            defaultValue={5}
            min={0}
            helperText="Minimum time between high priority alerts"
          />
          
          <NumberInput
            source="slack.rateLimitSettings.mediumThrottleMinutes"
            label="Medium Alert Throttle (minutes)"
            defaultValue={15}
            min={0}
            helperText="Minimum time between medium priority alerts"
          />
          
          <NumberInput
            source="slack.rateLimitSettings.lowThrottleMinutes"
            label="Low Alert Throttle (minutes)"
            defaultValue={60}
            min={0}
            helperText="Minimum time between low priority alerts"
          />
        </Box>
      </Box>
    </Box>
  );
};

// Test Connection Component
const TestConnectionDialog = ({ 
  open, 
  onClose, 
  channel,
  webhookUrl 
}: {
  open: boolean;
  onClose: () => void;
  channel: 'teams' | 'slack';
  webhookUrl: string;
}) => {
  const [testing, setTesting] = useState(false);
  const [result, setResult] = useState<{ success: boolean; message: string } | null>(null);
  const dataProvider = useDataProvider();
  const notify = useNotify();

  const handleTest = async () => {
    if (!webhookUrl) {
      notify('Please configure the webhook URL first', { type: 'warning' });
      return;
    }

    setTesting(true);
    setResult(null);

    try {
      const url = `${process.env.REACT_APP_CASTELLANPRO_API_URL || 'http://localhost:5000/api'}/notifications/${channel}/test`;
      const token = localStorage.getItem('auth_token');
      
      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
          ...(token ? { 'Authorization': `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ webhookUrl })
      });

      const result = await response.json();
      
      setResult({
        success: result.success || response.ok,
        message: result.message || (response.ok ? 'Test notification sent successfully!' : 'Test failed')
      });
      notify(result.success ? 'Test notification sent!' : 'Test failed', { 
        type: result.success ? 'success' : 'error' 
      });
    } catch (error) {
      console.error('Test connection error:', error);
      setResult({
        success: false,
        message: `Test failed: ${error instanceof Error ? error.message : 'Unknown error'}`
      });
      notify('Test failed', { type: 'error' });
    } finally {
      setTesting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>
        Test {channel === 'teams' ? 'Microsoft Teams' : 'Slack'} Connection
      </DialogTitle>
      <DialogContent>
        {testing ? (
          <Box display="flex" alignItems="center" justifyContent="center" py={3}>
            <CircularProgress />
            <Typography sx={{ ml: 2 }}>Sending test notification...</Typography>
          </Box>
        ) : result ? (
          <Alert 
            severity={result.success ? 'success' : 'error'}
            icon={result.success ? <CheckIcon /> : <ErrorIcon />}
          >
            {result.message}
          </Alert>
        ) : (
          <Typography>
            This will send a test notification to your configured {channel} channel.
            Make sure the webhook URL is correct before testing.
          </Typography>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button 
          onClick={handleTest} 
          variant="contained" 
          disabled={testing || !webhookUrl}
          startIcon={<TestIcon />}
        >
          Send Test
        </Button>
      </DialogActions>
    </Dialog>
  );
};

// Custom Toolbar with Test Connection buttons
const NotificationToolbar = () => {
  const record = useRecordContext();
  const [testDialogOpen, setTestDialogOpen] = useState(false);
  const [testChannel, setTestChannel] = useState<'teams' | 'slack'>('teams');

  const handleTestConnection = (channel: 'teams' | 'slack') => {
    setTestChannel(channel);
    setTestDialogOpen(true);
  };

  return (
    <Toolbar>
      <SaveButton />
      <Box sx={{ ml: 2 }}>
        <Button
          variant="outlined"
          startIcon={<TestIcon />}
          onClick={() => handleTestConnection('teams')}
          disabled={!record?.teams?.webhookUrl}
          sx={{ mr: 1 }}
        >
          Test Teams
        </Button>
        <Button
          variant="outlined"
          startIcon={<TestIcon />}
          onClick={() => handleTestConnection('slack')}
          disabled={!record?.slack?.webhookUrl}
        >
          Test Slack
        </Button>
      </Box>
      
      <TestConnectionDialog
        open={testDialogOpen}
        onClose={() => setTestDialogOpen(false)}
        channel={testChannel}
        webhookUrl={
          testChannel === 'teams' 
            ? record?.teams?.webhookUrl || ''
            : record?.slack?.webhookUrl || ''
        }
      />
    </Toolbar>
  );
};

// Notification Settings Show View
export const NotificationSettingsShow = () => {
  return (
    <Show title="Notification Configuration Details">
      <SimpleShowLayout>
        <TextField source="name" label="Configuration Name" />
        
        <Typography variant="h6" sx={{ mt: 3, mb: 1 }}>Microsoft Teams</Typography>
        <BooleanField source="teams.enabled" label="Enabled" />
        <TextField source="teams.webhookUrl" label="Webhook URL" />
        <TextField source="teams.castellanUrl" label="Castellan URL" />
        <NumberField source="teams.rateLimitSettings.criticalThrottleMinutes" label="Critical Throttle (min)" />
        <NumberField source="teams.rateLimitSettings.highThrottleMinutes" label="High Throttle (min)" />
        <NumberField source="teams.rateLimitSettings.mediumThrottleMinutes" label="Medium Throttle (min)" />
        <NumberField source="teams.rateLimitSettings.lowThrottleMinutes" label="Low Throttle (min)" />
        
        <Typography variant="h6" sx={{ mt: 3, mb: 1 }}>Slack</Typography>
        <BooleanField source="slack.enabled" label="Enabled" />
        <TextField source="slack.webhookUrl" label="Webhook URL" />
        <TextField source="slack.castellanUrl" label="Castellan URL" />
        <TextField source="slack.defaultChannel" label="Default Channel" />
        <TextField source="slack.criticalChannel" label="Critical Channel" />
        <TextField source="slack.highChannel" label="High Channel" />
        <NumberField source="slack.rateLimitSettings.criticalThrottleMinutes" label="Critical Throttle (min)" />
        <NumberField source="slack.rateLimitSettings.highThrottleMinutes" label="High Throttle (min)" />
        <NumberField source="slack.rateLimitSettings.mediumThrottleMinutes" label="Medium Throttle (min)" />
        <NumberField source="slack.rateLimitSettings.lowThrottleMinutes" label="Low Throttle (min)" />
        
        <DateField source="createdAt" showTime label="Created" />
      </SimpleShowLayout>
    </Show>
  );
};

// Main Notification Settings List
export const NotificationSettingsList = () => {
  return (
    <List
      title="Notification Settings"
      actions={
        <TopToolbar>
          <CreateButton />
          <ExportButton />
        </TopToolbar>
      }
    >
      <Datagrid>
        <TextField source="name" label="Configuration Name" />
        <BooleanField source="teams.enabled" label="Teams Enabled" />
        <BooleanField source="slack.enabled" label="Slack Enabled" />
        <TextField source="teams.webhookUrl" label="Teams Webhook" />
        <TextField source="slack.webhookUrl" label="Slack Webhook" />
        <ShowButton />
        <EditButton />
        <DeleteButton />
      </Datagrid>
    </List>
  );
};

// Create Notification Settings
export const NotificationSettingsCreate = () => {
  return (
    <Create title="Create Notification Configuration">
      <SimpleForm toolbar={<NotificationToolbar />}>
        <TextInput 
          source="name" 
          label="Configuration Name"
          required
          fullWidth
          helperText="Descriptive name for this notification configuration"
        />
        
        <Accordion defaultExpanded>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="h6">Microsoft Teams</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <TeamsConfigForm />
          </AccordionDetails>
        </Accordion>
        
        <Accordion>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="h6">Slack</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <SlackConfigForm />
          </AccordionDetails>
        </Accordion>
      </SimpleForm>
    </Create>
  );
};

// Edit Notification Settings
export const NotificationSettingsEdit = () => {
  return (
    <Edit title="Edit Notification Configuration">
      <SimpleForm toolbar={<NotificationToolbar />}>
        <TextInput 
          source="name" 
          label="Configuration Name"
          required
          fullWidth
          helperText="Descriptive name for this notification configuration"
        />
        
        <Accordion defaultExpanded>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="h6">Microsoft Teams</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <TeamsConfigForm />
          </AccordionDetails>
        </Accordion>
        
        <Accordion>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="h6">Slack</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <SlackConfigForm />
          </AccordionDetails>
        </Accordion>
      </SimpleForm>
    </Edit>
  );
};

// Quick Setup Card Component for Dashboard
export const NotificationQuickSetup = () => {
  const [expanded, setExpanded] = useState(false);
  const dataProvider = useDataProvider();
  const notify = useNotify();
  const refresh = useRefresh();

  return (
    <Card>
      <CardContent>
        <Box display="flex" alignItems="center" mb={2}>
          <NotificationsIcon sx={{ mr: 1 }} />
          <Typography variant="h6">Notification Settings</Typography>
        </Box>
        
        <Typography variant="body2" color="text.secondary" gutterBottom>
          Configure Teams and Slack notifications for security alerts
        </Typography>
        
        <Box display="flex" gap={1} mt={2}>
          <Button
            variant="outlined"
            size="small"
            onClick={() => window.location.href = '#/notification-settings/create'}
          >
            Setup Notifications
          </Button>
          <Button
            variant="text"
            size="small"
            onClick={() => setExpanded(!expanded)}
          >
            {expanded ? 'Less Info' : 'More Info'}
          </Button>
        </Box>
        
        {expanded && (
          <Box mt={2}>
            <Typography variant="body2" gutterBottom>
              <strong>Microsoft Teams:</strong> Get rich adaptive cards with security alert details
            </Typography>
            <Typography variant="body2" gutterBottom>
              <strong>Slack:</strong> Formatted messages with action buttons and user mentions
            </Typography>
            <Typography variant="body2">
              <strong>Features:</strong> Rate limiting, channel routing, test notifications
            </Typography>
          </Box>
        )}
      </CardContent>
    </Card>
  );
};