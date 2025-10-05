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
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { getResourceCacheConfig, queryKeys } from '../config/reactQueryConfig';
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
  Chat as SlackIcon,
  Shield as YaraIcon,
  Security as MitreIcon,
  BugReport as ThreatScannerIcon,
  Download as DownloadIcon,
  Update as UpdateIcon,
  Delete as DeleteIcon,
  Add as AddIcon
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
      <Tab label="IP Enrichment" />
      <Tab label="YARA Rules" />
      <Tab label="MITRE Techniques" />
      <Tab label="Threat Scanner" />
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

type IPEnrichmentConfigType = {
  enabled: boolean;
  provider: 'MaxMind' | 'IPInfo' | 'Disabled';
  maxMind: {
    licenseKey: string;
    accountId: string;
    autoUpdate: boolean;
    updateFrequencyDays: number;
    lastUpdate: string | null;
    databasePaths: {
      city: string;
      asn: string;
      country: string;
    };
  };
  ipInfo: {
    apiKey: string;
  };
  enrichment: {
    cacheMinutes: number;
    maxCacheEntries: number;
    enrichPrivateIPs: boolean;
    timeoutMs: number;
  };
  highRiskCountries: string[];
  highRiskASNs: number[];
};

type YaraConfigType = {
  autoUpdate: {
    enabled: boolean;
    updateFrequencyDays: number;
    lastUpdate: string | null;
    nextUpdate: string | null;
  };
  sources: {
    enabled: boolean;
    urls: string[];
    maxRulesPerSource: number;
  };
  rules: {
    enabledByDefault: boolean;
    autoValidation: boolean;
    performanceThresholdMs: number;
  };
  import: {
    lastImportDate: string | null;
    totalRules: number;
    enabledRules: number;
    failedRules: number;
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

const DEFAULT_IP_ENRICHMENT_CONFIG: IPEnrichmentConfigType = {
  enabled: true,
  provider: 'MaxMind',
  maxMind: {
    licenseKey: '',
    accountId: '',
    autoUpdate: false,
    updateFrequencyDays: 30,
    lastUpdate: null,
    databasePaths: {
      city: 'data/GeoLite2-City.mmdb',
      asn: 'data/GeoLite2-ASN.mmdb',
      country: 'data/GeoLite2-Country.mmdb'
    }
  },
  ipInfo: {
    apiKey: ''
  },
  enrichment: {
    cacheMinutes: 60,
    maxCacheEntries: 10000,
    enrichPrivateIPs: false,
    timeoutMs: 5000
  },
  highRiskCountries: ['CN', 'RU', 'KP', 'IR', 'SY', 'BY'],
  highRiskASNs: []
};

const DEFAULT_YARA_CONFIG: YaraConfigType = {
  autoUpdate: {
    enabled: false,
    updateFrequencyDays: 7,
    lastUpdate: null,
    nextUpdate: null
  },
  sources: {
    enabled: true,
    urls: [
      'https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/APT_APT1.yar',
      'https://raw.githubusercontent.com/Neo23x0/signature-base/master/yara/apt_cobalt_strike.yar',
      'https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_Zeus.yar',
      'https://raw.githubusercontent.com/Neo23x0/signature-base/master/yara/general_clamav_signature_set.yar',
      'https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_Ransomware.yar',
      'https://raw.githubusercontent.com/YARAHQ/yara-rules/main/malware/TrickBot.yar'
    ],
    maxRulesPerSource: 50
  },
  rules: {
    enabledByDefault: true,
    autoValidation: true,
    performanceThresholdMs: 1000
  },
  import: {
    lastImportDate: null,
    totalRules: 0,
    enabledRules: 0,
    failedRules: 0
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

  // Load configuration on mount
  useEffect(() => {
    const loadConfig = async () => {
      try {
        const { data } = await dataProvider.getOne('configuration', {
          id: 'threat-intelligence'
        });
        console.log('[ThreatIntelligenceConfig] Loaded config from backend:', data);
        if (data) {
          setConfig((prevConfig: ConfigType) => ({ ...DEFAULT_CONFIG, ...data }));
        }
      } catch (error) {
        console.error('[ThreatIntelligenceConfig] Failed to load config:', error);
        // Keep using defaults
      }
    };

    loadConfig();
  }, [dataProvider]);

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
      const result = await dataProvider.update('configuration', {
        id: 'threat-intelligence',
        data: config,
        previousData: {}
      });
      notify('Configuration saved successfully', { type: 'success' });
      // Don't refresh - the dataProvider.update already returns the updated data
      // and React Admin will use that to update the UI
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

  // Special status for MalwareBazaar (doesn't require API key)
  const getMalwareBazaarStatus = (service: any) => {
    if (!service.enabled) return { color: 'default', icon: <ErrorIcon />, text: 'Disabled' };
    return { color: 'success', icon: <CheckIcon />, text: 'Configured' };
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
                  icon={getMalwareBazaarStatus(config.malwareBazaar).icon}
                  label={getMalwareBazaarStatus(config.malwareBazaar).text}
                  color={getMalwareBazaarStatus(config.malwareBazaar).color as any}
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
      // Don't refresh - the dataProvider.update already returns the updated data
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

// IP Enrichment Configuration Component
const IPEnrichmentConfig = ({ record }: { record?: any }) => {
  const [config, setConfig] = useState<IPEnrichmentConfigType>(DEFAULT_IP_ENRICHMENT_CONFIG);
  const [showLicenseKey, setShowLicenseKey] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const notify = useNotify();
  const dataProvider = useDataProvider();
  const refresh = useRefresh();

  // Load configuration on mount
  useEffect(() => {
    loadConfiguration();
  }, []);

  const loadConfiguration = async () => {
    try {
      const { data } = await dataProvider.getOne('settings', {
        id: 'ip-enrichment'
      });
      if (data) {
        setConfig(data);
      }
    } catch (error) {
      console.log('No existing IP enrichment configuration found, using defaults');
    }
  };

  const handleSave = async () => {
    try {
      await dataProvider.update('configuration', {
        id: 'ip-enrichment',
        data: config,
        previousData: config
      });
      notify('IP enrichment configuration saved successfully', { type: 'success' });
      // Don't refresh - the dataProvider.update already returns the updated data
    } catch (error: any) {
      notify(`Failed to save configuration: ${error.message}`, { type: 'error' });
    }
  };

  const handleProviderChange = (provider: 'MaxMind' | 'IPInfo' | 'Disabled') => {
    setConfig({ ...config, provider });
  };

  const handleDownloadDatabases = async () => {
    if (!config.maxMind.licenseKey) {
      notify('Please enter your MaxMind license key first', { type: 'warning' });
      return;
    }

    try {
      // Make direct API call to the correct MaxMind download endpoint
      const token = localStorage.getItem('auth_token');
      const response = await fetch('http://localhost:5000/api/settings/ip-enrichment/download-databases', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({
          licenseKey: config.maxMind.licenseKey,
          accountId: config.maxMind.accountId
        })
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP ${response.status}`);
      }

      const result = await response.json();
      notify(result.data.message || 'MaxMind databases download started', { type: 'info' });

      // Update last update time
      setConfig({
        ...config,
        maxMind: {
          ...config.maxMind,
          lastUpdate: new Date().toISOString()
        }
      });
    } catch (error: any) {
      notify(`Failed to download databases: ${error.message}`, { type: 'error' });
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" gutterBottom sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
        <SecurityIcon sx={{ mr: 1 }} />
        IP Enrichment Configuration
      </Typography>

      <Grid container spacing={3}>
        {/* Provider Selection */}
        <Grid item xs={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>Provider Settings</Typography>

            <FormControlLabel
              control={
                <Switch
                  checked={config.enabled}
                  onChange={(e) => setConfig({ ...config, enabled: e.target.checked })}
                  color="primary"
                />
              }
              label="Enable IP Enrichment"
              sx={{ mb: 2 }}
            />

            <Box sx={{ mb: 2 }}>
              <Typography variant="body2" color="textSecondary" gutterBottom>
                Select IP enrichment provider:
              </Typography>
              <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
                <Button
                  variant={config.provider === 'MaxMind' ? 'contained' : 'outlined'}
                  onClick={() => handleProviderChange('MaxMind')}
                  size="small"
                >
                  MaxMind GeoLite2
                </Button>
                <Button
                  variant={config.provider === 'IPInfo' ? 'contained' : 'outlined'}
                  onClick={() => handleProviderChange('IPInfo')}
                  size="small"
                >
                  IPInfo
                </Button>
                <Button
                  variant={config.provider === 'Disabled' ? 'contained' : 'outlined'}
                  onClick={() => handleProviderChange('Disabled')}
                  size="small"
                  color="error"
                >
                  Disabled
                </Button>
              </Box>
            </Box>
          </Paper>
        </Grid>

        {/* MaxMind Configuration */}
        {config.provider === 'MaxMind' && (
          <Grid item xs={12}>
            <Paper sx={{ p: 2 }}>
              <Typography variant="h6" gutterBottom>MaxMind Configuration</Typography>

              <Alert severity="info" sx={{ mb: 2 }}>
                To use MaxMind GeoLite2 databases, you need a free license key from{' '}
                <a href="https://www.maxmind.com/en/geolite2/signup" target="_blank" rel="noopener noreferrer">
                  maxmind.com
                </a>
              </Alert>

              <Grid container spacing={2}>
                <Grid item xs={12} md={6}>
                  <MuiTextField
                    fullWidth
                    label="License Key"
                    type={showLicenseKey ? 'text' : 'password'}
                    value={config.maxMind.licenseKey}
                    onChange={(e) => setConfig({
                      ...config,
                      maxMind: { ...config.maxMind, licenseKey: e.target.value }
                    })}
                    InputProps={{
                      endAdornment: (
                        <IconButton onClick={() => setShowLicenseKey(!showLicenseKey)} size="small">
                          {showLicenseKey ? <VisibilityOffIcon /> : <VisibilityIcon />}
                        </IconButton>
                      )
                    }}
                  />
                </Grid>
                <Grid item xs={12} md={6}>
                  <MuiTextField
                    fullWidth
                    label="Account ID (optional)"
                    value={config.maxMind.accountId}
                    onChange={(e) => setConfig({
                      ...config,
                      maxMind: { ...config.maxMind, accountId: e.target.value }
                    })}
                  />
                </Grid>
              </Grid>

              <Box sx={{ mt: 2 }}>
                <FormControlLabel
                  control={
                    <Switch
                      checked={config.maxMind.autoUpdate}
                      onChange={(e) => setConfig({
                        ...config,
                        maxMind: { ...config.maxMind, autoUpdate: e.target.checked }
                      })}
                      color="primary"
                    />
                  }
                  label="Enable Automatic Updates"
                />
              </Box>

              {config.maxMind.autoUpdate && (
                <Box sx={{ mt: 2 }}>
                  <MuiTextField
                    label="Update Frequency (days)"
                    type="number"
                    value={config.maxMind.updateFrequencyDays}
                    onChange={(e) => setConfig({
                      ...config,
                      maxMind: {
                        ...config.maxMind,
                        updateFrequencyDays: parseInt(e.target.value) || 30
                      }
                    })}
                    InputProps={{ inputProps: { min: 1, max: 90 } }}
                    helperText="Recommended: 30 days"
                    sx={{ width: 200 }}
                  />
                </Box>
              )}

              <Box sx={{ mt: 2, display: 'flex', alignItems: 'center', gap: 2 }}>
                <Button
                  variant="outlined"
                  onClick={handleDownloadDatabases}
                  disabled={!config.maxMind.licenseKey}
                >
                  Download Databases Now
                </Button>
                {config.maxMind.lastUpdate && (
                  <Typography variant="body2" color="textSecondary">
                    Last updated: {new Date(config.maxMind.lastUpdate).toLocaleDateString()}
                  </Typography>
                )}
              </Box>

              <Box sx={{ mt: 3 }}>
                <Typography variant="body2" color="textSecondary" gutterBottom>
                  Database Locations:
                </Typography>
                <Typography variant="body2" sx={{ fontFamily: 'monospace', ml: 2 }}>
                  City: {config.maxMind.databasePaths.city}<br />
                  ASN: {config.maxMind.databasePaths.asn}<br />
                  Country: {config.maxMind.databasePaths.country}
                </Typography>
              </Box>
            </Paper>
          </Grid>
        )}

        {/* IPInfo Configuration */}
        {config.provider === 'IPInfo' && (
          <Grid item xs={12}>
            <Paper sx={{ p: 2 }}>
              <Typography variant="h6" gutterBottom>IPInfo Configuration</Typography>

              <Alert severity="info" sx={{ mb: 2 }}>
                IPInfo provides IP geolocation via API. Get your API key from{' '}
                <a href="https://ipinfo.io/signup" target="_blank" rel="noopener noreferrer">
                  ipinfo.io
                </a>
              </Alert>

              <MuiTextField
                fullWidth
                label="API Key"
                type={showApiKey ? 'text' : 'password'}
                value={config.ipInfo.apiKey}
                onChange={(e) => setConfig({
                  ...config,
                  ipInfo: { ...config.ipInfo, apiKey: e.target.value }
                })}
                InputProps={{
                  endAdornment: (
                    <IconButton onClick={() => setShowApiKey(!showApiKey)} size="small">
                      {showApiKey ? <VisibilityOffIcon /> : <VisibilityIcon />}
                    </IconButton>
                  )
                }}
                sx={{ maxWidth: 500 }}
              />
            </Paper>
          </Grid>
        )}

        {/* Enrichment Settings */}
        <Grid item xs={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>Enrichment Settings</Typography>

            <Grid container spacing={2}>
              <Grid item xs={12} md={3}>
                <MuiTextField
                  fullWidth
                  label="Cache Duration (minutes)"
                  type="number"
                  value={config.enrichment.cacheMinutes}
                  onChange={(e) => setConfig({
                    ...config,
                    enrichment: {
                      ...config.enrichment,
                      cacheMinutes: parseInt(e.target.value) || 60
                    }
                  })}
                  InputProps={{ inputProps: { min: 0, max: 1440 } }}
                />
              </Grid>
              <Grid item xs={12} md={3}>
                <MuiTextField
                  fullWidth
                  label="Max Cache Entries"
                  type="number"
                  value={config.enrichment.maxCacheEntries}
                  onChange={(e) => setConfig({
                    ...config,
                    enrichment: {
                      ...config.enrichment,
                      maxCacheEntries: parseInt(e.target.value) || 10000
                    }
                  })}
                  InputProps={{ inputProps: { min: 100, max: 100000 } }}
                />
              </Grid>
              <Grid item xs={12} md={3}>
                <MuiTextField
                  fullWidth
                  label="Timeout (ms)"
                  type="number"
                  value={config.enrichment.timeoutMs}
                  onChange={(e) => setConfig({
                    ...config,
                    enrichment: {
                      ...config.enrichment,
                      timeoutMs: parseInt(e.target.value) || 5000
                    }
                  })}
                  InputProps={{ inputProps: { min: 100, max: 30000 } }}
                />
              </Grid>
              <Grid item xs={12} md={3}>
                <FormControlLabel
                  control={
                    <Switch
                      checked={config.enrichment.enrichPrivateIPs}
                      onChange={(e) => setConfig({
                        ...config,
                        enrichment: {
                          ...config.enrichment,
                          enrichPrivateIPs: e.target.checked
                        }
                      })}
                      color="primary"
                    />
                  }
                  label="Enrich Private IPs"
                />
              </Grid>
            </Grid>
          </Paper>
        </Grid>

        {/* High Risk Configuration */}
        <Grid item xs={12}>
          <Paper sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>High Risk Indicators</Typography>

            <Typography variant="body2" color="textSecondary" gutterBottom>
              High Risk Countries (comma-separated country codes):
            </Typography>
            <MuiTextField
              fullWidth
              value={config.highRiskCountries.join(', ')}
              onChange={(e) => setConfig({
                ...config,
                highRiskCountries: e.target.value.split(',').map(s => s.trim()).filter(Boolean)
              })}
              placeholder="CN, RU, KP, IR, SY"
              helperText="Example: CN (China), RU (Russia), KP (North Korea)"
              sx={{ mb: 2 }}
            />

            <Typography variant="body2" color="textSecondary" gutterBottom>
              High Risk ASNs (comma-separated AS numbers):
            </Typography>
            <MuiTextField
              fullWidth
              value={config.highRiskASNs.join(', ')}
              onChange={(e) => setConfig({
                ...config,
                highRiskASNs: e.target.value.split(',').map(s => parseInt(s.trim()) || 0).filter(Boolean)
              })}
              placeholder="12345, 67890"
              helperText="Enter AS numbers known for malicious activity"
            />
          </Paper>
        </Grid>
      </Grid>

      {/* Save Button */}
      <Box sx={{ mt: 3, display: 'flex', justifyContent: 'flex-end' }}>
        <Button
          variant="contained"
          color="primary"
          size="large"
          startIcon={<SaveIcon />}
          onClick={handleSave}
        >
          Save IP Enrichment Configuration
        </Button>
      </Box>
    </Box>
  );
};

// YARA Configuration Component
const YaraConfig = ({ record }: { record?: any }) => {
  const [config, setConfig] = useState<YaraConfigType>(() => {
    // Initialize with record data if available, otherwise use defaults
    if (record && record.id === 'yara-rules') {
      return { ...DEFAULT_YARA_CONFIG, ...record };
    }
    return DEFAULT_YARA_CONFIG;
  });
  const [loading, setLoading] = useState(false);
  const notify = useNotify();
  const dataProvider = useDataProvider();
  const refresh = useRefresh();

  // Load configuration on mount
  useEffect(() => {
    loadConfiguration();
  }, []);

  const loadConfiguration = async () => {
    try {
      setLoading(true);
      const response = await fetch('http://localhost:5000/api/yara-configuration', {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`
        }
      });

      if (response.ok) {
        const result = await response.json();
        setConfig({ ...DEFAULT_YARA_CONFIG, ...result });
      } else {
        console.log('No existing YARA configuration found, using defaults');
        setConfig(DEFAULT_YARA_CONFIG);
      }
    } catch (error) {
      console.log('No existing YARA configuration found, using defaults');
      setConfig(DEFAULT_YARA_CONFIG);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      setLoading(true);
      const response = await fetch('http://localhost:5000/api/yara-configuration', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`
        },
        body: JSON.stringify(config)
      });

      if (response.ok) {
        notify('YARA configuration saved successfully', { type: 'success' });
        refresh();
      } else {
        const errorData = await response.json();
        notify(`Error saving YARA configuration: ${errorData.message}`, { type: 'error' });
      }
    } catch (error) {
      notify('Error saving YARA configuration', { type: 'error' });
      console.error('Error saving YARA configuration:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleImportNow = async () => {
    try {
      setLoading(true);
      notify('Starting YARA rules import...', { type: 'info' });

      // Get the auth token from localStorage
      const authToken = localStorage.getItem('auth_token');

      // Call the import API endpoint
      const response = await fetch('http://localhost:5000/api/yara-configuration/import', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${authToken}`
        }
      });

      if (response.ok) {
        const result = await response.json();
        notify(result.message, { type: 'success' });

        // Update config with new import stats
        setConfig(prev => ({
          ...prev,
          import: {
            lastImportDate: new Date().toISOString(),
            totalRules: result.totalRules || prev.import.totalRules,
            enabledRules: result.enabledRules || prev.import.enabledRules,
            failedRules: result.failedRules || 0
          },
          autoUpdate: {
            ...prev.autoUpdate,
            lastUpdate: new Date().toISOString()
          }
        }));
      } else {
        throw new Error('Import failed');
      }
    } catch (error) {
      notify('Error importing YARA rules', { type: 'error' });
      console.error('Error importing YARA rules:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
        <YaraIcon sx={{ mr: 2, color: 'primary.main' }} />
        <Typography variant="h5" component="h2">
          YARA Rules Configuration
        </Typography>
      </Box>

      <Typography variant="body2" color="text.secondary" sx={{ mb: 4 }}>
        Configure automatic updates and import settings for YARA malware detection rules.
      </Typography>

      <Grid container spacing={4}>
        {/* Auto Update Settings */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3, border: '1px solid #e0e0e0' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <UpdateIcon sx={{ mr: 1, color: 'primary.main' }} />
              <Typography variant="h6">Auto Update Settings</Typography>
            </Box>

            <FormControlLabel
              control={
                <Switch
                  checked={config.autoUpdate.enabled}
                  onChange={(e) => setConfig({
                    ...config,
                    autoUpdate: { ...config.autoUpdate, enabled: e.target.checked }
                  })}
                />
              }
              label="Enable automatic rule updates"
              sx={{ mb: 2 }}
            />

            <MuiTextField
              fullWidth
              type="number"
              label="Update Frequency (days)"
              value={config.autoUpdate.updateFrequencyDays}
              onChange={(e) => setConfig({
                ...config,
                autoUpdate: { ...config.autoUpdate, updateFrequencyDays: parseInt(e.target.value) || 7 }
              })}
              inputProps={{ min: 1, max: 365 }}
              helperText="How often to check for new YARA rules (1-365 days)"
              sx={{ mb: 2 }}
            />

            {config.autoUpdate.lastUpdate && (
              <Alert severity="info" sx={{ mb: 2 }}>
                <Typography variant="body2">
                  <strong>Last Update:</strong> {new Date(config.autoUpdate.lastUpdate).toLocaleString()}
                </Typography>
              </Alert>
            )}
          </Paper>
        </Grid>

        {/* Rule Sources */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3, border: '1px solid #e0e0e0' }}>
            <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
              <DownloadIcon sx={{ mr: 1, color: 'primary.main' }} />
              <Typography variant="h6">Rule Sources</Typography>
            </Box>

            <FormControlLabel
              control={
                <Switch
                  checked={config.sources.enabled}
                  onChange={(e) => setConfig({
                    ...config,
                    sources: { ...config.sources, enabled: e.target.checked }
                  })}
                />
              }
              label="Enable rule source downloads"
              sx={{ mb: 2 }}
            />

            <MuiTextField
              fullWidth
              type="number"
              label="Max Rules Per Source"
              value={config.sources.maxRulesPerSource}
              onChange={(e) => setConfig({
                ...config,
                sources: { ...config.sources, maxRulesPerSource: parseInt(e.target.value) || 50 }
              })}
              inputProps={{ min: 1, max: 1000 }}
              helperText="Maximum rules to import from each source"
              sx={{ mb: 2 }}
            />

            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              <strong>Active Sources:</strong> {config.sources.urls.length} configured
            </Typography>

            {/* Source URLs List */}
            <Box sx={{ mb: 2 }}>
              {config.sources.urls.map((url, index) => (
                <Box key={index} sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                  <MuiTextField
                    fullWidth
                    size="small"
                    label={`Source ${index + 1}`}
                    value={url}
                    onChange={(e) => {
                      const newUrls = [...config.sources.urls];
                      newUrls[index] = e.target.value;
                      setConfig({
                        ...config,
                        sources: { ...config.sources, urls: newUrls }
                      });
                    }}
                    sx={{ mr: 1 }}
                  />
                  <IconButton
                    color="error"
                    onClick={() => {
                      const newUrls = config.sources.urls.filter((_, i) => i !== index);
                      setConfig({
                        ...config,
                        sources: { ...config.sources, urls: newUrls }
                      });
                    }}
                    disabled={config.sources.urls.length <= 1}
                  >
                    <DeleteIcon />
                  </IconButton>
                </Box>
              ))}
            </Box>

            {/* Add New Source Button */}
            <Button
              variant="outlined"
              size="small"
              startIcon={<AddIcon />}
              onClick={() => {
                setConfig({
                  ...config,
                  sources: { ...config.sources, urls: [...config.sources.urls, ''] }
                });
              }}
              sx={{ mb: 1 }}
            >
              Add Source
            </Button>
          </Paper>
        </Grid>

        {/* Import Statistics */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3, border: '1px solid #e8f5e8', bgcolor: '#f9f9f9' }}>
            <Typography variant="h6" sx={{ mb: 2 }}>Import Statistics</Typography>

            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="body2">Total Rules:</Typography>
              <Chip label={config.import.totalRules} color="primary" size="small" />
            </Box>

            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
              <Typography variant="body2">Enabled Rules:</Typography>
              <Chip label={config.import.enabledRules} color="success" size="small" />
            </Box>

            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
              <Typography variant="body2">Failed Rules:</Typography>
              <Chip
                label={config.import.failedRules}
                color={config.import.failedRules > 0 ? "error" : "default"}
                size="small"
              />
            </Box>

            {config.import.lastImportDate && (
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                <strong>Last Import:</strong> {new Date(config.import.lastImportDate).toLocaleString()}
              </Typography>
            )}

            <Button
              variant="outlined"
              fullWidth
              startIcon={<DownloadIcon />}
              onClick={handleImportNow}
              disabled={loading}
              sx={{ mb: 2 }}
            >
              Import Rules Now
            </Button>
          </Paper>
        </Grid>

        {/* Rule Settings */}
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3, border: '1px solid #e0e0e0' }}>
            <Typography variant="h6" sx={{ mb: 2 }}>Rule Settings</Typography>

            <FormControlLabel
              control={
                <Switch
                  checked={config.rules.enabledByDefault}
                  onChange={(e) => setConfig({
                    ...config,
                    rules: { ...config.rules, enabledByDefault: e.target.checked }
                  })}
                />
              }
              label="Enable imported rules by default"
              sx={{ mb: 2 }}
            />

            <FormControlLabel
              control={
                <Switch
                  checked={config.rules.autoValidation}
                  onChange={(e) => setConfig({
                    ...config,
                    rules: { ...config.rules, autoValidation: e.target.checked }
                  })}
                />
              }
              label="Auto-validate rules on import"
              sx={{ mb: 2 }}
            />

            <MuiTextField
              fullWidth
              type="number"
              label="Performance Threshold (ms)"
              value={config.rules.performanceThresholdMs}
              onChange={(e) => setConfig({
                ...config,
                rules: { ...config.rules, performanceThresholdMs: parseInt(e.target.value) || 1000 }
              })}
              inputProps={{ min: 100, max: 10000 }}
              helperText="Disable rules that take longer than this to execute"
              sx={{ mb: 2 }}
            />
          </Paper>
        </Grid>
      </Grid>

      <Box sx={{ mt: 4, display: 'flex', justifyContent: 'flex-end' }}>
        <Button
          variant="contained"
          disabled={loading}
          startIcon={<SaveIcon />}
          onClick={handleSave}
        >
          Save YARA Configuration
        </Button>
      </Box>
    </Box>
  );
};

// MITRE ATT&CK Configuration Component
const MitreConfig = ({ record }: { record?: any }) => {
  const dataProvider = useDataProvider();
  const notify = useNotify();
  const [mitreStats, setMitreStats] = useState<any>(null);
  const [importing, setImporting] = useState(false);

  useEffect(() => {
    const fetchMitreStats = async () => {
      try {
        // Direct fetch to avoid dataProvider's ID appending behavior
        const response = await fetch('http://localhost:5000/api/mitre/count', {
          headers: {
            'Authorization': `Bearer ${localStorage.getItem('auth_token')}`
          }
        });
        if (response.ok) {
          const data = await response.json();
          setMitreStats(data);
        } else {
          throw new Error('Failed to fetch MITRE stats');
        }
      } catch (error) {
        console.warn('Could not fetch MITRE stats:', error);
        setMitreStats({ count: 0, shouldImport: true });
      }
    };

    fetchMitreStats();
  }, [dataProvider]);

  const handleImport = async () => {
    setImporting(true);
    try {
      const response = await dataProvider.create('mitre/import', { data: {} });
      notify(`MITRE import completed: ${response.data.message}`, { type: 'success' });

      // Refresh stats - direct fetch to avoid dataProvider's ID appending behavior
      const countResponse = await fetch('http://localhost:5000/api/mitre/count', {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`
        }
      });
      if (countResponse.ok) {
        const data = await countResponse.json();
        setMitreStats(data);
      }
    } catch (error: any) {
      notify(`MITRE import failed: ${error.message}`, { type: 'error' });
    } finally {
      setImporting(false);
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
        <MitreIcon sx={{ mr: 1, color: 'primary.main' }} />
        <Typography variant="h5" component="h2">
          MITRE ATT&CK Techniques
        </Typography>
      </Box>

      <Grid container spacing={3}>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center' }}>
              <MitreIcon sx={{ mr: 1 }} />
              Database Status
            </Typography>

            {mitreStats && (
              <Box sx={{ mb: 3 }}>
                <Typography variant="body1" sx={{ mb: 1 }}>
                  <strong>Techniques in Database:</strong> {mitreStats.count?.toLocaleString() || 0}
                </Typography>

                {mitreStats.count > 0 ? (
                  <Chip
                    icon={<CheckIcon />}
                    label="Database Ready"
                    color="success"
                    sx={{ mb: 2 }}
                  />
                ) : (
                  <Chip
                    icon={<WarningIcon />}
                    label="Database Empty - Import Required"
                    color="warning"
                    sx={{ mb: 2 }}
                  />
                )}

                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                  Last checked: {new Date().toLocaleString()}
                </Typography>
              </Box>
            )}

            <Button
              variant="contained"
              disabled={importing}
              startIcon={importing ? <UpdateIcon sx={{ animation: 'spin 1s linear infinite' }} /> : <DownloadIcon />}
              onClick={handleImport}
              sx={{ mr: 2 }}
            >
              {importing ? 'Importing...' : 'Import MITRE Techniques'}
            </Button>

            <Alert severity="info" sx={{ mt: 2 }}>
              This will download and import the latest MITRE ATT&CK techniques from the official source.
              The process may take several minutes to complete.
            </Alert>
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              About MITRE ATT&CK
            </Typography>

            <Typography variant="body2" sx={{ mb: 2 }}>
              The MITRE ATT&CK framework is a comprehensive knowledge base of adversary tactics,
              techniques, and procedures (TTPs) based on real-world observations.
            </Typography>

            <Typography variant="body2" sx={{ mb: 2 }}>
              <strong>Features:</strong>
            </Typography>
            <Box component="ul" sx={{ pl: 2 }}>
              <li>800+ technique definitions</li>
              <li>Tactic categorization</li>
              <li>Platform-specific mappings</li>
              <li>Mitigation strategies</li>
              <li>Detection data sources</li>
            </Box>

            <Alert severity="info" sx={{ mt: 2 }}>
              <strong>Note:</strong> MITRE techniques are used throughout Castellan for
              threat classification and security event correlation.
            </Alert>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
};

// Threat Scanner Configuration Component
const ThreatScannerConfig = ({ record }: { record: any }) => {
  const [config, setConfig] = useState({
    enabled: false,
    scheduledScanInterval: { days: 1, hours: 0, minutes: 0 },
    defaultScanType: 0, // 0=QuickScan, 1=FullScan
    excludedDirectories: [] as string[],
    excludedExtensions: [] as string[],
    maxFileSizeMB: 100,
    maxConcurrentFiles: 4,
    quarantineThreats: false,
    quarantineDirectory: 'C:\\Quarantine',
    enableRealTimeProtection: false,
    notificationThreshold: 1 // 1=Medium
  });
  const [status, setStatus] = useState<any>(null);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [newExcludedDir, setNewExcludedDir] = useState('');
  const [newExcludedExt, setNewExcludedExt] = useState('');
  const notify = useNotify();
  const dataProvider = useDataProvider();

  // Load configuration on component mount
  useEffect(() => {
    loadConfiguration();
    loadStatus();
  }, []);

  const loadConfiguration = async () => {
    try {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('http://localhost:5000/api/scheduledscan/config', {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      });
      if (response.ok) {
        const data = await response.json();

        // Convert TimeSpan to our format - handle both ticks object and string format
        let days = 1, hours = 0, minutes = 0;

        if (data.scheduledScanInterval) {
          if (typeof data.scheduledScanInterval === 'object' && data.scheduledScanInterval.ticks) {
            // TimeSpan as object with ticks
            const totalHours = Math.floor(data.scheduledScanInterval.ticks / 36000000000);
            days = Math.floor(totalHours / 24);
            hours = totalHours % 24;
          } else if (typeof data.scheduledScanInterval === 'string') {
            // TimeSpan as string "d.hh:mm:ss" or "hh:mm:ss"
            const parts = data.scheduledScanInterval.split(':');
            if (parts.length >= 2) {
              const firstPart = parts[0];
              if (firstPart.includes('.')) {
                // Format: "d.hh:mm:ss"
                const [d, h] = firstPart.split('.');
                days = parseInt(d) || 0;
                hours = parseInt(h) || 0;
              } else {
                // Format: "hh:mm:ss"
                hours = parseInt(firstPart) || 0;
                days = Math.floor(hours / 24);
                hours = hours % 24;
              }
              minutes = parseInt(parts[1]) || 0;
            }
          }
        }

        setConfig({
          enabled: data.enabled,
          scheduledScanInterval: { days, hours, minutes },
          defaultScanType: data.defaultScanType,
          excludedDirectories: data.excludedDirectories || [],
          excludedExtensions: data.excludedExtensions || [],
          maxFileSizeMB: data.maxFileSizeMB,
          maxConcurrentFiles: data.maxConcurrentFiles,
          quarantineThreats: data.quarantineThreats,
          quarantineDirectory: data.quarantineDirectory,
          enableRealTimeProtection: data.enableRealTimeProtection,
          notificationThreshold: data.notificationThreshold
        });
      }
    } catch (error) {
      console.error('Failed to load configuration:', error);
      notify('Failed to load threat scanner configuration', { type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  const loadStatus = async () => {
    try {
      const token = localStorage.getItem('auth_token');
      const response = await fetch('http://localhost:5000/api/scheduledscan/status', {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      });
      if (response.ok) {
        const data = await response.json();
        setStatus(data);
      }
    } catch (error) {
      console.error('Failed to load status:', error);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      // Convert our format to TimeSpan - .NET expects format "d.hh:mm:ss" or TimeSpan object
      // Ensure we have valid numbers, default to 0 if undefined/NaN
      const days = Number(config.scheduledScanInterval?.days) || 0;
      const hours = Number(config.scheduledScanInterval?.hours) || 0;
      const minutes = Number(config.scheduledScanInterval?.minutes) || 0;

      // Validate that we have at least some time interval
      if (days === 0 && hours === 0 && minutes === 0) {
        notify('Scan interval must be greater than zero', { type: 'error' });
        setSaving(false);
        return;
      }

      // Format as "d.hh:mm:ss" (days.hours:minutes:seconds)
      const timeSpan = days > 0
        ? `${days}.${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:00`
        : `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:00`;

      const payload = {
        ...config,
        scheduledScanInterval: timeSpan
      };

      const token = localStorage.getItem('auth_token');
      const response = await fetch('http://localhost:5000/api/scheduledscan/config', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify(payload)
      });

      if (response.ok) {
        notify('Threat scanner configuration saved successfully', { type: 'success' });
        await loadStatus(); // Reload status after save
      } else {
        const errorData = await response.json();
        const errorMessage = errorData?.error || errorData?.message || 'Unknown error';
        notify(`Failed to save: ${errorMessage}`, { type: 'error' });
      }
    } catch (error: any) {
      const errorMessage = error?.message || 'Failed to save threat scanner configuration';
      notify(errorMessage, { type: 'error' });
      console.error('Threat scanner save error:', error);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <Box sx={{ p: 3, textAlign: 'center' }}>
        <Typography>Loading threat scanner configuration...</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 3 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
        <ThreatScannerIcon sx={{ mr: 1, color: 'primary.main' }} />
        <Typography variant="h5" component="h2">
          Threat Scanner Configuration
        </Typography>
      </Box>

      {status && (
        <Box sx={{ mb: 3 }}>
          <Alert severity={status.isEnabled ? "success" : "warning"}>
            Scheduled scanning is {status.isEnabled ? "enabled" : "disabled"}
            {status.lastScanTime && ` • Last scan: ${new Date(status.lastScanTime).toLocaleString()}`}
            {status.nextScanTime && ` • Next scan: ${new Date(status.nextScanTime).toLocaleString()}`}
            {status.isScanInProgress && " • Scan currently in progress"}
          </Alert>
        </Box>
      )}

      <Grid container spacing={3}>
        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Scheduled Scans
            </Typography>

            <FormControlLabel
              control={
                <Switch
                  checked={config.enabled}
                  onChange={(e) => setConfig({
                    ...config,
                    enabled: e.target.checked
                  })}
                />
              }
              label="Enable Scheduled Scans"
              sx={{ mb: 2 }}
            />

            {config.enabled && (
              <Box>
                <Typography variant="body2" gutterBottom>
                  Scan Interval
                </Typography>
                <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
                  <MuiTextField
                    label="Days"
                    type="number"
                    value={config.scheduledScanInterval.days}
                    onChange={(e) => setConfig({
                      ...config,
                      scheduledScanInterval: {
                        ...config.scheduledScanInterval,
                        days: parseInt(e.target.value) || 0
                      }
                    })}
                    size="small"
                    inputProps={{ min: 0, max: 30 }}
                  />
                  <MuiTextField
                    label="Hours"
                    type="number"
                    value={config.scheduledScanInterval.hours}
                    onChange={(e) => setConfig({
                      ...config,
                      scheduledScanInterval: {
                        ...config.scheduledScanInterval,
                        hours: parseInt(e.target.value) || 0
                      }
                    })}
                    size="small"
                    inputProps={{ min: 0, max: 23 }}
                  />
                </Box>

                <Typography variant="body2" gutterBottom>
                  Default Scan Type
                </Typography>
                <select
                  value={config.defaultScanType}
                  onChange={(e) => setConfig({
                    ...config,
                    defaultScanType: parseInt(e.target.value)
                  })}
                  style={{ width: '100%', padding: '8px', marginBottom: '16px' }}
                >
                  <option value={0}>Quick Scan</option>
                  <option value={1}>Full Scan</option>
                </select>
              </Box>
            )}
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Quarantine Settings
            </Typography>

            <FormControlLabel
              control={
                <Switch
                  checked={config.quarantineThreats}
                  onChange={(e) => setConfig({
                    ...config,
                    quarantineThreats: e.target.checked
                  })}
                />
              }
              label="Enable Threat Quarantine"
              sx={{ mb: 2 }}
            />

            {config.quarantineThreats && (
              <MuiTextField
                label="Quarantine Directory"
                value={config.quarantineDirectory}
                onChange={(e) => setConfig({
                  ...config,
                  quarantineDirectory: e.target.value
                })}
                fullWidth
                helperText="Directory where threats will be quarantined"
              />
            )}
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Performance Settings
            </Typography>

            <Typography variant="body2" gutterBottom>
              Max Concurrent Files: {config.maxConcurrentFiles}
            </Typography>
            <input
              type="range"
              min="1"
              max="16"
              value={config.maxConcurrentFiles}
              onChange={(e) => setConfig({
                ...config,
                maxConcurrentFiles: parseInt(e.target.value)
              })}
              style={{ width: '100%', marginBottom: '16px' }}
            />

            <MuiTextField
              label="Max File Size (MB)"
              type="number"
              value={config.maxFileSizeMB}
              onChange={(e) => setConfig({
                ...config,
                maxFileSizeMB: parseInt(e.target.value) || 100
              })}
              fullWidth
              helperText="Maximum file size to scan (larger files will be skipped)"
              sx={{ mb: 2 }}
            />

            <Typography variant="body2" gutterBottom>
              Notification Threshold
            </Typography>
            <select
              value={config.notificationThreshold}
              onChange={(e) => setConfig({
                ...config,
                notificationThreshold: parseInt(e.target.value)
              })}
              style={{ width: '100%', padding: '8px' }}
            >
              <option value={0}>Low</option>
              <option value={1}>Medium</option>
              <option value={2}>High</option>
            </select>
          </Paper>
        </Grid>

        <Grid item xs={12} md={6}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Exclusions
            </Typography>

            <Typography variant="body2" gutterBottom>
              Excluded Directories
            </Typography>
            <Box sx={{ mb: 2 }}>
              {config.excludedDirectories.map((dir, index) => (
                <Chip
                  key={index}
                  label={dir}
                  onDelete={() => {
                    const newDirs = [...config.excludedDirectories];
                    newDirs.splice(index, 1);
                    setConfig({ ...config, excludedDirectories: newDirs });
                  }}
                  sx={{ m: 0.5 }}
                />
              ))}
            </Box>
            <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
              <MuiTextField
                label="Add directory to exclude"
                value={newExcludedDir}
                onChange={(e) => setNewExcludedDir(e.target.value)}
                size="small"
                fullWidth
              />
              <IconButton
                onClick={() => {
                  if (newExcludedDir.trim()) {
                    setConfig({
                      ...config,
                      excludedDirectories: [...config.excludedDirectories, newExcludedDir.trim()]
                    });
                    setNewExcludedDir('');
                  }
                }}
              >
                <AddIcon />
              </IconButton>
            </Box>

            <Typography variant="body2" gutterBottom>
              Excluded Extensions
            </Typography>
            <Box sx={{ mb: 2 }}>
              {config.excludedExtensions.map((ext, index) => (
                <Chip
                  key={index}
                  label={ext}
                  onDelete={() => {
                    const newExts = [...config.excludedExtensions];
                    newExts.splice(index, 1);
                    setConfig({ ...config, excludedExtensions: newExts });
                  }}
                  sx={{ m: 0.5 }}
                />
              ))}
            </Box>
            <Box sx={{ display: 'flex', gap: 1 }}>
              <MuiTextField
                label="Add extension to exclude (e.g., .txt)"
                value={newExcludedExt}
                onChange={(e) => setNewExcludedExt(e.target.value)}
                size="small"
                fullWidth
              />
              <IconButton
                onClick={() => {
                  if (newExcludedExt.trim()) {
                    setConfig({
                      ...config,
                      excludedExtensions: [...config.excludedExtensions, newExcludedExt.trim()]
                    });
                    setNewExcludedExt('');
                  }
                }}
              >
                <AddIcon />
              </IconButton>
            </Box>
          </Paper>
        </Grid>

        <Grid item xs={12}>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              About Threat Scanner
            </Typography>
            <Typography variant="body2" sx={{ mb: 2 }}>
              The Threat Scanner provides automated malware detection and system protection capabilities.
            </Typography>
            <Typography variant="body2" sx={{ mb: 2 }}>
              <strong>Features:</strong>
            </Typography>
            <Box component="ul" sx={{ pl: 2 }}>
              <li>Scheduled security scans with configurable intervals</li>
              <li>Threat quarantine and isolation</li>
              <li>Performance-optimized scanning with concurrent file processing</li>
              <li>Integration with threat intelligence feeds (VirusTotal, MalwareBazaar, OTX)</li>
              <li>Database persistence for scan history</li>
            </Box>

            <Button
              variant="contained"
              onClick={handleSave}
              disabled={saving}
              startIcon={saving ? <UpdateIcon sx={{ animation: 'spin 1s linear infinite' }} /> : <SaveIcon />}
              sx={{ mt: 2 }}
            >
              {saving ? 'Saving...' : 'Save Configuration'}
            </Button>
          </Paper>
        </Grid>
      </Grid>
    </Box>
  );
};

// Configuration List Component (for sidebar navigation)
export const ConfigurationList = () => {
  const [config, setConfig] = useState<ConfigType>(DEFAULT_CONFIG);
  const [notificationConfig, setNotificationConfig] = useState<NotificationConfigType>(DEFAULT_NOTIFICATION_CONFIG);
  const [ipEnrichmentConfig, setIpEnrichmentConfig] = useState<IPEnrichmentConfigType>(DEFAULT_IP_ENRICHMENT_CONFIG);
  const [yaraConfig, setYaraConfig] = useState<YaraConfigType>(DEFAULT_YARA_CONFIG);
  const [activeTab, setActiveTab] = useState(0);
  const dataProvider = useDataProvider();

  // Get cache config for configuration resource
  const cacheConfig = getResourceCacheConfig('configuration');

  // React Query for threat intelligence config - CACHED with instant snapshots!
  const { data: threatIntelData, isLoading: threatIntelLoading } = useQuery({
    queryKey: queryKeys.one('configuration', { id: 'threat-intelligence' }),
    queryFn: async () => {
      try {
        const result = await dataProvider.getOne('configuration', { id: 'threat-intelligence' });
        return result.data;
      } catch (error) {
        console.log('No existing threat intelligence configuration found, using defaults');
        return DEFAULT_CONFIG;
      }
    },
    placeholderData: keepPreviousData,
    ...cacheConfig, // 5min fresh, 30min memory
  });

  // React Query for notification config - CACHED with instant snapshots!
  const { data: notificationData, isLoading: notificationLoading } = useQuery({
    queryKey: queryKeys.one('configuration', { id: 'notifications' }),
    queryFn: async () => {
      try {
        const result = await dataProvider.getOne('configuration', { id: 'notifications' });
        return result.data;
      } catch (error) {
        console.log('No existing notification configuration found, using defaults');
        return DEFAULT_NOTIFICATION_CONFIG;
      }
    },
    placeholderData: keepPreviousData,
    ...cacheConfig,
  });

  // React Query for IP enrichment config - CACHED with instant snapshots!
  const { data: ipEnrichmentData, isLoading: ipEnrichmentLoading } = useQuery({
    queryKey: queryKeys.one('configuration', { id: 'ip-enrichment' }),
    queryFn: async () => {
      try {
        const result = await dataProvider.getOne('configuration', { id: 'ip-enrichment' });
        return result.data;
      } catch (error) {
        console.log('No existing IP enrichment configuration found, using defaults');
        return DEFAULT_IP_ENRICHMENT_CONFIG;
      }
    },
    placeholderData: keepPreviousData,
    ...cacheConfig,
  });

  // React Query for YARA config - CACHED with instant snapshots!
  const { data: yaraData, isLoading: yaraLoading } = useQuery({
    queryKey: queryKeys.one('configuration', { id: 'yara-rules' }),
    queryFn: async () => {
      try {
        const response = await fetch('http://localhost:5000/api/yara-configuration', {
          headers: {
            'Authorization': `Bearer ${localStorage.getItem('auth_token')}`
          }
        });

        if (response.ok) {
          const result = await response.json();
          return { ...DEFAULT_YARA_CONFIG, ...result };
        }
        return DEFAULT_YARA_CONFIG;
      } catch (error) {
        console.log('No existing YARA configuration found, using defaults');
        return DEFAULT_YARA_CONFIG;
      }
    },
    placeholderData: keepPreviousData,
    ...cacheConfig,
  });

  // Apply query data to state when loaded
  useEffect(() => {
    if (threatIntelData) {
      setConfig({ ...DEFAULT_CONFIG, ...threatIntelData });
    }
  }, [threatIntelData]);

  useEffect(() => {
    if (notificationData) {
      setNotificationConfig({ ...DEFAULT_NOTIFICATION_CONFIG, ...notificationData });
    }
  }, [notificationData]);

  useEffect(() => {
    if (ipEnrichmentData) {
      setIpEnrichmentConfig({ ...DEFAULT_IP_ENRICHMENT_CONFIG, ...ipEnrichmentData });
    }
  }, [ipEnrichmentData]);

  useEffect(() => {
    if (yaraData) {
      setYaraConfig(yaraData);
    }
  }, [yaraData]);

  // Combine loading states
  const loading = threatIntelLoading || notificationLoading || ipEnrichmentLoading || yaraLoading;

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
        return <IPEnrichmentConfig record={{ id: 'ip-enrichment', ...ipEnrichmentConfig }} />;
      case 3:
        return <YaraConfig record={{ id: 'yara-rules', ...yaraConfig }} />;
      case 4:
        return <MitreConfig record={{ id: 'mitre-techniques' }} />;
      case 5:
        return <ThreatScannerConfig record={{ id: 'threat-scanner' }} />;
      case 6:
        return (
          <Box sx={{ p: 3, textAlign: 'center' }}>
            <Typography variant="h6" color="text.secondary">
              Performance settings coming soon...
            </Typography>
          </Box>
        );
      case 7:
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
