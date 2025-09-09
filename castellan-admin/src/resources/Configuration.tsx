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
  Paper
} from '@mui/material';
import { 
  Settings as SettingsIcon,
  Security as SecurityIcon,
  Save as SaveIcon,
  Visibility as VisibilityIcon,
  VisibilityOff as VisibilityOffIcon,
  CheckCircle as CheckIcon,
  Error as ErrorIcon,
  Warning as WarningIcon
} from '@mui/icons-material';

// Configuration header component
const ConfigurationHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
    <SettingsIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      System Configuration
    </Typography>
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

// Configuration List Component (for sidebar navigation)
export const ConfigurationList = () => {
  const redirect = useRedirect();

  // For configuration, we only have one item, so redirect to show view
  React.useEffect(() => {
    redirect('/configuration/threat-intelligence/show');
  }, [redirect]);

  // Fallback - show the configuration directly if redirect doesn't work
  return (
    <List title="System Configuration">
      <Box sx={{ p: 2 }}>
        <ConfigurationHeader />
        <ThreatIntelligenceConfig record={null} />
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
