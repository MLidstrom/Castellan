import React, { useState, useEffect } from 'react';
import {
  Show,
  SimpleShowLayout,
  List,
  useNotify,
  useRefresh,
  useDataProvider,
  useRedirect,
  useShowContext,
} from 'react-admin';
import {
  Typography,
  Box,
  Switch,
  FormControlLabel,
  TextField as MuiTextField,
  Button,
  Alert,
  Chip,
  IconButton,
  Grid,
  Paper,
  Tabs,
  Tab
} from '@mui/material';
import {
  Settings as SettingsIcon,
  Security as SecurityIcon,
  Save as SaveIcon,
  Visibility as VisibilityIcon,
  VisibilityOff as VisibilityOffIcon,
  CheckCircle as CheckIcon,
  Error as ErrorIcon,
  Warning as WarningIcon,
  Notifications as NotificationIcon,
  Groups as TeamsIcon,
  Chat as SlackIcon
} from '@mui/icons-material';

// Configuration header component
const ConfigurationHeader = ({
  activeTab = 0,
  onTabChange
}: {
  activeTab?: number;
  onTabChange?: (newValue: number) => void;
}) => (
  <Box sx={{ mb: 3 }}>
    <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
      <SettingsIcon sx={{ mr: 1, color: 'primary.main' }} />
      <Typography variant="h4" component="h1">
        System Configuration
      </Typography>
    </Box>
    <Tabs
      value={activeTab}
      onChange={(_, newValue) => onTabChange?.(newValue)}
      sx={{ borderBottom: 1, borderColor: 'divider' }}
    >
      <Tab label="Threat Intelligence" />
      <Tab label="Notifications" />
      <Tab label="Performance" disabled />
      <Tab label="Security" disabled />
    </Tabs>
  </Box>
);

type ConfigType = {
  virusTotal: {
    enabled: boolean;
    apiKey: string;
    rateLimitPerMinute: number;
    cacheEnabled: boolean;
    cacheTtlMinutes: number;
  };
  malwareBazaar: {
    enabled: boolean;
    rateLimitPerMinute: number;
    cacheEnabled: boolean;
    cacheTtlMinutes: number;
  };
  alienVaultOtx: {
    enabled: boolean;
    apiKey: string;
    rateLimitPerMinute: number;
    cacheEnabled: boolean;
    cacheTtlMinutes: number;
  };
};

type NotificationConfigType = {
  teams: {
    enabled: boolean;
    webhookUrl: string;
    notificationTypes: {
      criticalEvents: boolean;
      highRiskEvents: boolean;
      yaraMatches: boolean;
      systemAlerts: boolean;
    };
    rateLimitPerHour: number;
  };
  slack: {
    enabled: boolean;
    webhookUrl: string;
    channel: string;
    notificationTypes: {
      criticalEvents: boolean;
      highRiskEvents: boolean;
      yaraMatches: boolean;
      systemAlerts: boolean;
    };
    rateLimitPerHour: number;
  };
};

// Default configuration constant
const DEFAULT_CONFIG: ConfigType = {
  virusTotal: {
    enabled: true,
    apiKey: '',
    rateLimitPerMinute: 4,
    cacheEnabled: true,
    cacheTtlMinutes: 60
  },
  malwareBazaar: {
    enabled: true,
    rateLimitPerMinute: 10,
    cacheEnabled: true,
    cacheTtlMinutes: 30
  },
  alienVaultOtx: {
    enabled: false,
    apiKey: '',
    rateLimitPerMinute: 10,
    cacheEnabled: true,
    cacheTtlMinutes: 60
  }
};

const DEFAULT_NOTIFICATION_CONFIG: NotificationConfigType = {
  teams: {
    enabled: false,
    webhookUrl: '',
    notificationTypes: {
      criticalEvents: true,
      highRiskEvents: true,
      yaraMatches: true,
      systemAlerts: true
    },
    rateLimitPerHour: 60
  },
  slack: {
    enabled: false,
    webhookUrl: '',
    channel: '#security',
    notificationTypes: {
      criticalEvents: true,
      highRiskEvents: true,
      yaraMatches: true,
      systemAlerts: false
    },
    rateLimitPerHour: 60
  }
};

// Threat Intelligence Configuration Component
const ThreatIntelligenceConfig = ({ record }: { record?: any }) => {
  const [config, setConfig] = useState<ConfigType>(() => {
    // Initialize with record data if available, otherwise use defaults
    if (record && record.id === 'threat-intelligence') {
      return { ...DEFAULT_CONFIG, ...record };
    }
    return DEFAULT_CONFIG;
  });
  const [saving, setSaving] = useState(false);
  const [showApiKeys, setShowApiKeys] = useState({
    virusTotal: false,
    alienVaultOtx: false
  });
  
  const dataProvider = useDataProvider();
  const notify = useNotify();
  const refresh = useRefresh();

  // Update config when record changes
  useEffect(() => {
    console.log('[ThreatIntelligenceConfig] Record changed:', record);
    if (record && record.id === 'threat-intelligence') {
      setConfig((prevConfig: ConfigType) => ({ ...DEFAULT_CONFIG, ...prevConfig, ...record }));
    }
  }, [record]);

  const handleSave = async () => {
    setSaving(true);
    try {
      await dataProvider.update('configuration', {
        id: 'threat-intelligence',
        data: config,
        previousData: {}
      });
      notify('Configuration saved successfully', { type: 'success' });
      refresh();
    } catch (error) {
      notify('Failed to save configuration', { type: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const handleConfigChange = (service: string, field: string, value: any) => {
    setConfig((prev: ConfigType) => ({
      ...prev,
      [service]: {
        ...prev[service as keyof typeof prev],
        [field]: value
      }
    }));
  };

  const toggleApiKeyVisibility = (service: 'virusTotal' | 'alienVaultOtx') => {
    setShowApiKeys(prev => ({
      ...prev,
      [service]: !prev[service]
    }));
  };

  const getServiceStatus = (service: any) => {
    if (!service.enabled) return { color: 'default', icon: <ErrorIcon />, text: 'Disabled' };
    if (service.apiKey && service.apiKey.length > 0) return { color: 'success', icon: <CheckIcon />, text: 'Configured' };
    if (service.enabled && !service.apiKey) return { color: 'warning', icon: <WarningIcon />, text: 'API Key Required' };
    return { color: 'info', icon: <CheckIcon />, text: 'Enabled' };
  };

  return (
    <Box>
      <Grid container spacing={3}>
        {/* VirusTotal Configuration */}
        <Grid item xs={12} md={4}>
          <Paper elevation={2} sx={{ p: 3, height: '100%' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
              <Typography variant="h6">VirusTotal</Typography>
              <Box sx={{ ml: 'auto' }}>
                <Chip
                  icon={getServiceStatus(config.virusTotal).icon}
                  label={getServiceStatus(config.virusTotal).text}
                  color={getServiceStatus(config.virusTotal).color as any}
                  size="small"
                />
              </Box>
            </Box>
            
            <FormControlLabel
              control={
                <Switch
                  checked={config.virusTotal.enabled}
                  onChange={(e) => handleConfigChange('virusTotal', 'enabled', e.target.checked)}
                />
              }
              label="Enable VirusTotal Integration"
              sx={{ mb: 2 }}
            />

            {config.virusTotal.enabled && (
              <Box>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                  <MuiTextField
                    fullWidth
                    label="API Key"
                    type={showApiKeys.virusTotal ? 'text' : 'password'}
                    value={config.virusTotal.apiKey}
                    onChange={(e) => handleConfigChange('virusTotal', 'apiKey', e.target.value)}
                    size="small"
                    helperText="Get free API key from virustotal.com"
                  />
                  <IconButton
                    onClick={() => toggleApiKeyVisibility('virusTotal')}
                    size="small"
                    sx={{ ml: 1 }}
                  >
                    {showApiKeys.virusTotal ? <VisibilityOffIcon /> : <VisibilityIcon />}
                  </IconButton>
                </Box>

                <MuiTextField
                  fullWidth
                  label="Rate Limit (requests/minute)"
                  type="number"
                  value={config.virusTotal.rateLimitPerMinute}
                  onChange={(e) => handleConfigChange('virusTotal', 'rateLimitPerMinute', parseInt(e.target.value))}
                  size="small"
                  sx={{ mb: 2 }}
                  inputProps={{ min: 1, max: 1000 }}
                />

                <FormControlLabel
                  control={
                    <Switch
                      checked={config.virusTotal.cacheEnabled}
                      onChange={(e) => handleConfigChange('virusTotal', 'cacheEnabled', e.target.checked)}
                    />
                  }
                  label="Enable Caching"
                  sx={{ mb: 1 }}
                />

                {config.virusTotal.cacheEnabled && (
                  <MuiTextField
                    fullWidth
                    label="Cache TTL (minutes)"
                    type="number"
                    value={config.virusTotal.cacheTtlMinutes}
                    onChange={(e) => handleConfigChange('virusTotal', 'cacheTtlMinutes', parseInt(e.target.value))}
                    size="small"
                    inputProps={{ min: 1, max: 1440 }}
                  />
                )}
              </Box>
            )}
          </Paper>
        </Grid>

        {/* MalwareBazaar Configuration */}
        <Grid item xs={12} md={4}>
          <Paper elevation={2} sx={{ p: 3, height: '100%' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <SecurityIcon sx={{ mr: 1, color: 'secondary.main' }} />
              <Typography variant="h6">MalwareBazaar</Typography>
              <Box sx={{ ml: 'auto' }}>
                <Chip
                  icon={getServiceStatus(config.malwareBazaar).icon}
                  label={getServiceStatus(config.malwareBazaar).text}
                  color={getServiceStatus(config.malwareBazaar).color as any}
                  size="small"
                />
              </Box>
            </Box>

            <FormControlLabel
              control={
                <Switch
                  checked={config.malwareBazaar.enabled}
                  onChange={(e) => handleConfigChange('malwareBazaar', 'enabled', e.target.checked)}
                />
              }
              label="Enable MalwareBazaar Integration"
              sx={{ mb: 2 }}
            />

            {config.malwareBazaar.enabled && (
              <Box>
                <Alert severity="info" sx={{ mb: 2 }}>
                  MalwareBazaar doesn't require an API key - free public access
                </Alert>

                <MuiTextField
                  fullWidth
                  label="Rate Limit (requests/minute)"
                  type="number"
                  value={config.malwareBazaar.rateLimitPerMinute}
                  onChange={(e) => handleConfigChange('malwareBazaar', 'rateLimitPerMinute', parseInt(e.target.value))}
                  size="small"
                  sx={{ mb: 2 }}
                  inputProps={{ min: 1, max: 60 }}
                />

                <FormControlLabel
                  control={
                    <Switch
                      checked={config.malwareBazaar.cacheEnabled}
                      onChange={(e) => handleConfigChange('malwareBazaar', 'cacheEnabled', e.target.checked)}
                    />
                  }
                  label="Enable Caching"
                  sx={{ mb: 1 }}
                />

                {config.malwareBazaar.cacheEnabled && (
                  <MuiTextField
                    fullWidth
                    label="Cache TTL (minutes)"
                    type="number"
                    value={config.malwareBazaar.cacheTtlMinutes}
                    onChange={(e) => handleConfigChange('malwareBazaar', 'cacheTtlMinutes', parseInt(e.target.value))}
                    size="small"
                    inputProps={{ min: 1, max: 1440 }}
                  />
                )}
              </Box>
            )}
          </Paper>
        </Grid>

        {/* AlienVault OTX Configuration */}
        <Grid item xs={12} md={4}>
          <Paper elevation={2} sx={{ p: 3, height: '100%' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <SecurityIcon sx={{ mr: 1, color: 'info.main' }} />
              <Typography variant="h6">AlienVault OTX</Typography>
              <Box sx={{ ml: 'auto' }}>
                <Chip
                  icon={getServiceStatus(config.alienVaultOtx).icon}
                  label={getServiceStatus(config.alienVaultOtx).text}
                  color={getServiceStatus(config.alienVaultOtx).color as any}
                  size="small"
                />
              </Box>
            </Box>

            <FormControlLabel
              control={
                <Switch
                  checked={config.alienVaultOtx.enabled}
                  onChange={(e) => handleConfigChange('alienVaultOtx', 'enabled', e.target.checked)}
                />
              }
              label="Enable AlienVault OTX Integration"
              sx={{ mb: 2 }}
            />

            {config.alienVaultOtx.enabled && (
              <Box>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                  <MuiTextField
                    fullWidth
                    label="API Key"
                    type={showApiKeys.alienVaultOtx ? 'text' : 'password'}
                    value={config.alienVaultOtx.apiKey}
                    onChange={(e) => handleConfigChange('alienVaultOtx', 'apiKey', e.target.value)}
                    size="small"
                    helperText="Get free API key from otx.alienvault.com"
                  />
                  <IconButton
                    onClick={() => toggleApiKeyVisibility('alienVaultOtx')}
                    size="small"
                    sx={{ ml: 1 }}
                  >
                    {showApiKeys.alienVaultOtx ? <VisibilityOffIcon /> : <VisibilityIcon />}
                  </IconButton>
                </Box>

                <MuiTextField
                  fullWidth
                  label="Rate Limit (requests/minute)"
                  type="number"
                  value={config.alienVaultOtx.rateLimitPerMinute}
                  onChange={(e) => handleConfigChange('alienVaultOtx', 'rateLimitPerMinute', parseInt(e.target.value))}
                  size="small"
                  sx={{ mb: 2 }}
                  inputProps={{ min: 1, max: 1000 }}
                />

                <FormControlLabel
                  control={
                    <Switch
                      checked={config.alienVaultOtx.cacheEnabled}
                      onChange={(e) => handleConfigChange('alienVaultOtx', 'cacheEnabled', e.target.checked)}
                    />
                  }
                  label="Enable Caching"
                  sx={{ mb: 1 }}
                />

                {config.alienVaultOtx.cacheEnabled && (
                  <MuiTextField
                    fullWidth
                    label="Cache TTL (minutes)"
                    type="number"
                    value={config.alienVaultOtx.cacheTtlMinutes}
                    onChange={(e) => handleConfigChange('alienVaultOtx', 'cacheTtlMinutes', parseInt(e.target.value))}
                    size="small"
                    inputProps={{ min: 1, max: 1440 }}
                  />
                )}
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>

      {/* Save Button */}
      <Box sx={{ mt: 3, textAlign: 'right' }}>
        <Button
          variant="contained"
          startIcon={<SaveIcon />}
          onClick={handleSave}
          disabled={saving}
          size="large"
        >
          {saving ? 'Saving...' : 'Save Configuration'}
        </Button>
      </Box>
    </Box>
  );
};

// Notifications Configuration Component
const NotificationsConfig = ({ record }: { record?: any }) => {
  const [config, setConfig] = useState<NotificationConfigType>(() => {
    if (record && record.id === 'notifications') {
      return { ...DEFAULT_NOTIFICATION_CONFIG, ...record };
    }
    return DEFAULT_NOTIFICATION_CONFIG;
  });
  const [saving, setSaving] = useState(false);
  const [showWebhookUrls, setShowWebhookUrls] = useState({
    teams: false,
    slack: false
  });

  const dataProvider = useDataProvider();
  const notify = useNotify();
  const refresh = useRefresh();

  useEffect(() => {
    console.log('[NotificationsConfig] Record changed:', record);
    if (record && record.id === 'notifications') {
      setConfig((prevConfig: NotificationConfigType) => ({ ...DEFAULT_NOTIFICATION_CONFIG, ...prevConfig, ...record }));
    }
  }, [record]);

  const handleSave = async () => {
    setSaving(true);
    try {
      await dataProvider.update('configuration', {
        id: 'notifications',
        data: config,
        previousData: {}
      });
      notify('Notification configuration saved successfully', { type: 'success' });
      refresh();
    } catch (error) {
      notify('Failed to save notification configuration', { type: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const handleConfigChange = (service: 'teams' | 'slack', field: string, value: any) => {
    setConfig((prev: NotificationConfigType) => ({
      ...prev,
      [service]: {
        ...prev[service],
        [field]: value
      }
    }));
  };

  const handleNotificationTypeChange = (service: 'teams' | 'slack', type: string, value: boolean) => {
    setConfig((prev: NotificationConfigType) => ({
      ...prev,
      [service]: {
        ...prev[service],
        notificationTypes: {
          ...prev[service].notificationTypes,
          [type]: value
        }
      }
    }));
  };

  const toggleWebhookVisibility = (service: 'teams' | 'slack') => {
    setShowWebhookUrls(prev => ({
      ...prev,
      [service]: !prev[service]
    }));
  };

  const getServiceStatus = (service: any) => {
    if (!service.enabled) return { color: 'default', icon: <ErrorIcon />, text: 'Disabled' };
    if (service.webhookUrl && service.webhookUrl.length > 0) return { color: 'success', icon: <CheckIcon />, text: 'Configured' };
    if (service.enabled && !service.webhookUrl) return { color: 'warning', icon: <WarningIcon />, text: 'Webhook Required' };
    return { color: 'info', icon: <CheckIcon />, text: 'Enabled' };
  };

  return (
    <Box>
      <Grid container spacing={3}>
        {/* Microsoft Teams Configuration */}
        <Grid item xs={12} md={6}>
          <Paper elevation={2} sx={{ p: 3, height: '100%' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <TeamsIcon sx={{ mr: 1, color: 'primary.main' }} />
              <Typography variant="h6">Microsoft Teams</Typography>
              <Box sx={{ ml: 'auto' }}>
                <Chip
                  icon={getServiceStatus(config.teams).icon}
                  label={getServiceStatus(config.teams).text}
                  color={getServiceStatus(config.teams).color as any}
                  size="small"
                />
              </Box>
            </Box>

            <FormControlLabel
              control={
                <Switch
                  checked={config.teams.enabled}
                  onChange={(e) => handleConfigChange('teams', 'enabled', e.target.checked)}
                />
              }
              label="Enable Teams Notifications"
              sx={{ mb: 2 }}
            />

            {config.teams.enabled && (
              <Box>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                  <MuiTextField
                    fullWidth
                    label="Webhook URL"
                    type={showWebhookUrls.teams ? 'text' : 'password'}
                    value={config.teams.webhookUrl}
                    onChange={(e) => handleConfigChange('teams', 'webhookUrl', e.target.value)}
                    size="small"
                    helperText="Paste your Teams incoming webhook URL here"
                    placeholder="https://outlook.office.com/webhook/..."
                  />
                  <IconButton
                    onClick={() => toggleWebhookVisibility('teams')}
                    size="small"
                    sx={{ ml: 1 }}
                  >
                    {showWebhookUrls.teams ? <VisibilityOffIcon /> : <VisibilityIcon />}
                  </IconButton>
                </Box>

                <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                  Notification Types:
                </Typography>
                <Box sx={{ pl: 2, mb: 2 }}>
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.teams.notificationTypes.criticalEvents}
                        onChange={(e) => handleNotificationTypeChange('teams', 'criticalEvents', e.target.checked)}
                      />
                    }
                    label="Critical Events"
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.teams.notificationTypes.highRiskEvents}
                        onChange={(e) => handleNotificationTypeChange('teams', 'highRiskEvents', e.target.checked)}
                      />
                    }
                    label="High Risk Events"
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.teams.notificationTypes.yaraMatches}
                        onChange={(e) => handleNotificationTypeChange('teams', 'yaraMatches', e.target.checked)}
                      />
                    }
                    label="YARA Matches"
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.teams.notificationTypes.systemAlerts}
                        onChange={(e) => handleNotificationTypeChange('teams', 'systemAlerts', e.target.checked)}
                      />
                    }
                    label="System Alerts"
                  />
                </Box>

                <MuiTextField
                  fullWidth
                  label="Rate Limit (notifications/hour)"
                  type="number"
                  value={config.teams.rateLimitPerHour}
                  onChange={(e) => handleConfigChange('teams', 'rateLimitPerHour', parseInt(e.target.value))}
                  size="small"
                  inputProps={{ min: 1, max: 100 }}
                  helperText="Maximum notifications per hour"
                />
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Slack Configuration */}
        <Grid item xs={12} md={6}>
          <Paper elevation={2} sx={{ p: 3, height: '100%' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <SlackIcon sx={{ mr: 1, color: 'secondary.main' }} />
              <Typography variant="h6">Slack</Typography>
              <Box sx={{ ml: 'auto' }}>
                <Chip
                  icon={getServiceStatus(config.slack).icon}
                  label={getServiceStatus(config.slack).text}
                  color={getServiceStatus(config.slack).color as any}
                  size="small"
                />
              </Box>
            </Box>

            <FormControlLabel
              control={
                <Switch
                  checked={config.slack.enabled}
                  onChange={(e) => handleConfigChange('slack', 'enabled', e.target.checked)}
                />
              }
              label="Enable Slack Notifications"
              sx={{ mb: 2 }}
            />

            {config.slack.enabled && (
              <Box>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                  <MuiTextField
                    fullWidth
                    label="Webhook URL"
                    type={showWebhookUrls.slack ? 'text' : 'password'}
                    value={config.slack.webhookUrl}
                    onChange={(e) => handleConfigChange('slack', 'webhookUrl', e.target.value)}
                    size="small"
                    helperText="Paste your Slack incoming webhook URL here"
                    placeholder="https://hooks.slack.com/services/..."
                  />
                  <IconButton
                    onClick={() => toggleWebhookVisibility('slack')}
                    size="small"
                    sx={{ ml: 1 }}
                  >
                    {showWebhookUrls.slack ? <VisibilityOffIcon /> : <VisibilityIcon />}
                  </IconButton>
                </Box>

                <MuiTextField
                  fullWidth
                  label="Channel"
                  value={config.slack.channel}
                  onChange={(e) => handleConfigChange('slack', 'channel', e.target.value)}
                  size="small"
                  sx={{ mb: 2 }}
                  helperText="Slack channel name (e.g., #security)"
                  placeholder="#security"
                />

                <Typography variant="body2" sx={{ mb: 1, fontWeight: 500 }}>
                  Notification Types:
                </Typography>
                <Box sx={{ pl: 2, mb: 2 }}>
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.slack.notificationTypes.criticalEvents}
                        onChange={(e) => handleNotificationTypeChange('slack', 'criticalEvents', e.target.checked)}
                      />
                    }
                    label="Critical Events"
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.slack.notificationTypes.highRiskEvents}
                        onChange={(e) => handleNotificationTypeChange('slack', 'highRiskEvents', e.target.checked)}
                      />
                    }
                    label="High Risk Events"
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.slack.notificationTypes.yaraMatches}
                        onChange={(e) => handleNotificationTypeChange('slack', 'yaraMatches', e.target.checked)}
                      />
                    }
                    label="YARA Matches"
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        size="small"
                        checked={config.slack.notificationTypes.systemAlerts}
                        onChange={(e) => handleNotificationTypeChange('slack', 'systemAlerts', e.target.checked)}
                      />
                    }
                    label="System Alerts"
                  />
                </Box>

                <MuiTextField
                  fullWidth
                  label="Rate Limit (notifications/hour)"
                  type="number"
                  value={config.slack.rateLimitPerHour}
                  onChange={(e) => handleConfigChange('slack', 'rateLimitPerHour', parseInt(e.target.value))}
                  size="small"
                  inputProps={{ min: 1, max: 100 }}
                  helperText="Maximum notifications per hour"
                />
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>

      {/* Save Button */}
      <Box sx={{ mt: 3, textAlign: 'right' }}>
        <Button
          variant="contained"
          startIcon={<SaveIcon />}
          onClick={handleSave}
          disabled={saving}
          size="large"
        >
          {saving ? 'Saving...' : 'Save Notification Settings'}
        </Button>
      </Box>
    </Box>
  );
};

// Configuration List Component (for sidebar navigation)
export const ConfigurationList = () => {
  const [config, setConfig] = useState<ConfigType>(DEFAULT_CONFIG);
  const [notificationConfig, setNotificationConfig] = useState<NotificationConfigType>(DEFAULT_NOTIFICATION_CONFIG);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState(0);
  const dataProvider = useDataProvider();

  // Load configuration data
  useEffect(() => {
    const loadConfig = async () => {
      try {
        // Load threat intelligence config
        const threatIntelResult = await dataProvider.getOne('configuration', { id: 'threat-intelligence' });
        setConfig({ ...DEFAULT_CONFIG, ...threatIntelResult.data });
      } catch (error) {
        console.log('No existing threat intelligence configuration found, using defaults');
      }

      try {
        // Load notification config
        const notificationResult = await dataProvider.getOne('configuration', { id: 'notifications' });
        setNotificationConfig({ ...DEFAULT_NOTIFICATION_CONFIG, ...notificationResult.data });
      } catch (error) {
        console.log('No existing notification configuration found, using defaults');
      } finally {
        setLoading(false);
      }
    };

    loadConfig();
  }, [dataProvider]);

  const handleTabChange = (newValue: number) => {
    setActiveTab(newValue);
  };

  if (loading) {
    return (
      <List title="System Configuration">
        <Box sx={{ p: 2, textAlign: 'center' }}>
          <Typography>Loading configuration...</Typography>
        </Box>
      </List>
    );
  }

  const renderTabContent = () => {
    switch (activeTab) {
      case 0:
        return <ThreatIntelligenceConfig record={{ id: 'threat-intelligence', ...config }} />;
      case 1:
        return <NotificationsConfig record={{ id: 'notifications', ...notificationConfig }} />;
      case 2:
        return (
          <Box sx={{ p: 3, textAlign: 'center' }}>
            <Typography variant="h6" color="text.secondary">
              Performance settings coming soon...
            </Typography>
          </Box>
        );
      case 3:
        return (
          <Box sx={{ p: 3, textAlign: 'center' }}>
            <Typography variant="h6" color="text.secondary">
              Security settings coming soon...
            </Typography>
          </Box>
        );
      default:
        return <ThreatIntelligenceConfig record={{ id: 'threat-intelligence', ...config }} />;
    }
  };

  return (
    <List title="System Configuration">
      <Box sx={{ p: 2 }}>
        <ConfigurationHeader activeTab={activeTab} onTabChange={handleTabChange} />
        {renderTabContent()}
      </Box>
    </List>
  );
};

// Configuration Show Component Content
const ConfigurationShowContent = () => {
  const { record } = useShowContext();
  
  return (
    <Box>
      <ConfigurationHeader />
      <ThreatIntelligenceConfig record={record} />
    </Box>
  );
};

// Configuration Show Component (for react-admin routing)
export const ConfigurationShow = () => (
  <Show title="System Configuration" id="threat-intelligence">
    <SimpleShowLayout>
      <ConfigurationShowContent />
    </SimpleShowLayout>
  </Show>
);
