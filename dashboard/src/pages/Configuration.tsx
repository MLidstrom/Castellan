import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Settings,
  Shield,
  Bell,
  Globe,
  FileText,
  Target,
  Save,
  Eye,
  EyeOff,
  Check,
  X,
  AlertCircle,
  Activity,
  Info,
  RefreshCw,
  Download,
  Plus,
  Trash2
} from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { YaraConfigComponent } from '../components/YaraConfigComponent';

type TabType = 'threat-intelligence' | 'notifications' | 'ip-enrichment' | 'yara' | 'mitre' | 'threat-scanner' | 'performance';

interface ThreatIntelConfig {
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
}

interface NotificationConfig {
  teams: {
    enabled: boolean;
    webhookUrl: string;
    notificationTypes: {
      criticalEvents: boolean;
      highRiskEvents: boolean;
      mediumRiskEvents: boolean;
      correlationAlerts: boolean;
      yaraMatches: boolean;
    };
  };
  slack: {
    enabled: boolean;
    webhookUrl: string;
    channel: string;
    notificationTypes: {
      criticalEvents: boolean;
      highRiskEvents: boolean;
      mediumRiskEvents: boolean;
      correlationAlerts: boolean;
      yaraMatches: boolean;
    };
  };
}

interface IPEnrichmentConfig {
  enabled: boolean;
  provider: 'MaxMind' | 'IPInfo' | 'Disabled';
  maxMind: {
    licenseKey: string;
    accountId: string;
    autoUpdate: boolean;
    updateFrequencyDays: number;
  };
  ipInfo: {
    apiKey: string;
  };
}

interface YaraConfig {
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
}

const tabs = [
  { id: 'threat-intelligence' as TabType, label: 'Threat Intelligence', icon: Shield },
  { id: 'notifications' as TabType, label: 'Notifications', icon: Bell },
  { id: 'ip-enrichment' as TabType, label: 'IP Enrichment', icon: Globe },
  { id: 'yara' as TabType, label: 'YARA Rules', icon: FileText },
  { id: 'mitre' as TabType, label: 'MITRE Techniques', icon: Target },
  { id: 'threat-scanner' as TabType, label: 'Threat Scanner', icon: Activity },
  { id: 'performance' as TabType, label: 'Performance', icon: Settings, disabled: true },
];

export function ConfigurationPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<TabType>('threat-intelligence');
  const [showApiKeys, setShowApiKeys] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);

  useEffect(() => {
    if (!loading && !token) {
      navigate('/login');
    }
  }, [token, loading, navigate]);

  // Fetch configurations based on active tab
  // Skip fetching for tabs that don't have backend config (mitre, threat-scanner, performance)
  const shouldFetchConfig = activeTab !== 'mitre' && activeTab !== 'threat-scanner' && activeTab !== 'performance';
  
  const configQuery = useQuery({
    queryKey: ['configuration', activeTab],
    queryFn: async () => {
      const endpoints: Record<TabType, string> = {
        'threat-intelligence': '/settings/threat-intelligence',
        'notifications': '/notifications/config',
        'ip-enrichment': '/settings/ip-enrichment',
        'yara': '/yara-configuration',
        'mitre': '/mitre/config',
        'threat-scanner': '/threat-scanner/config',
        'performance': '/performance/config',
      };

      const response = await fetch(`/api${endpoints[activeTab]}`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error('Failed to fetch configuration');
      const data = await response.json();
      return data.data || data;
    },
    enabled: !loading && !!token && shouldFetchConfig,
  });

  // Save configuration mutation
  const saveMutation = useMutation({
    mutationFn: async (config: any) => {
      const endpoints: Record<TabType, string> = {
        'threat-intelligence': '/settings/threat-intelligence',
        'notifications': '/notifications/config',
        'ip-enrichment': '/settings/ip-enrichment',
        'yara': '/yara-configuration',
        'mitre': '/mitre/config',
        'threat-scanner': '/threat-scanner/config',
        'performance': '/performance/config',
      };

      const response = await fetch(`/api${endpoints[activeTab]}`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(config)
      });
      if (!response.ok) throw new Error('Failed to save configuration');
      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configuration', activeTab] });
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    },
  });

  const handleSave = (config: any) => {
    saveMutation.mutate(config);
  };

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Header */}
      <div className="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div className="px-8 py-6">
          <div className="flex items-center gap-3 mb-4">
            <Settings className="h-8 w-8 text-blue-600 dark:text-blue-400" />
            <div>
              <h1 className="text-3xl font-bold text-gray-900 dark:text-white">System Configuration</h1>
              <p className="text-gray-600 dark:text-gray-400 mt-1">Configure integrations and platform settings</p>
            </div>
          </div>

          {/* Tabs */}
          <div className="flex gap-2 overflow-x-auto pb-2">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => !tab.disabled && setActiveTab(tab.id)}
                disabled={tab.disabled}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg whitespace-nowrap transition-colors ${
                  activeTab === tab.id
                    ? 'bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-200'
                    : tab.disabled
                    ? 'text-gray-400 dark:text-gray-600 cursor-not-allowed'
                    : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700'
                }`}
              >
                <tab.icon className="h-4 w-4" />
                {tab.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="p-8 max-w-6xl mx-auto">
        {/* Success Message */}
        {saveSuccess && (
          <div className="mb-6 bg-green-50 border border-green-200 rounded-lg p-4 flex items-center gap-2">
            <Check className="h-5 w-5 text-green-600" />
            <span className="text-green-800 font-medium">Configuration saved successfully!</span>
          </div>
        )}

        {/* Content based on active tab */}
        {shouldFetchConfig && configQuery.isLoading ? (
          <div className="flex items-center justify-center py-12">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
          </div>
        ) : shouldFetchConfig && configQuery.isError ? (
          <div className="bg-red-50 border border-red-200 rounded-lg p-6 flex items-start gap-3">
            <AlertCircle className="h-5 w-5 text-red-600 mt-0.5" />
            <div>
              <h3 className="text-red-800 font-medium">Failed to load configuration</h3>
              <p className="text-red-700 text-sm mt-1">{(configQuery.error as Error).message}</p>
            </div>
          </div>
        ) : (
          <>
            {activeTab === 'threat-intelligence' && (
              <ThreatIntelligenceConfig
                config={configQuery.data}
                onSave={handleSave}
                showApiKeys={showApiKeys}
                onToggleApiKeys={() => setShowApiKeys(!showApiKeys)}
                isSaving={saveMutation.isPending}
              />
            )}
            {activeTab === 'notifications' && (
              <NotificationsConfig
                config={configQuery.data}
                onSave={handleSave}
                showApiKeys={showApiKeys}
                onToggleApiKeys={() => setShowApiKeys(!showApiKeys)}
                isSaving={saveMutation.isPending}
              />
            )}
            {activeTab === 'ip-enrichment' && (
              <IPEnrichmentConfig
                config={configQuery.data}
                onSave={handleSave}
                showApiKeys={showApiKeys}
                onToggleApiKeys={() => setShowApiKeys(!showApiKeys)}
                isSaving={saveMutation.isPending}
              />
            )}
            {activeTab === 'yara' && (
              <YaraConfigComponent
                config={configQuery.data}
                onSave={handleSave}
                isSaving={saveMutation.isPending}
              />
            )}
            {activeTab === 'mitre' && (
              <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
                <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">MITRE ATT&CK Configuration</h2>
                <p className="text-gray-600 dark:text-gray-400 mb-4">
                  MITRE ATT&CK techniques are managed through the Threat Intelligence page.
                </p>
                <button
                  onClick={() => navigate('/mitre-attack')}
                  className="px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg"
                >
                  Go to MITRE ATT&CK
                </button>
              </div>
            )}
            {activeTab === 'threat-scanner' && (
              <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
                <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Threat Scanner Configuration</h2>
                <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
                  <div className="flex items-start">
                    <Info className="h-5 w-5 text-blue-600 mr-2 mt-0.5" />
                    <p className="text-sm text-blue-800">
                      Threat scanner settings are configured through the API endpoints and managed via the backend configuration.
                    </p>
                  </div>
                </div>
                <p className="text-gray-600 dark:text-gray-400 text-sm">
                  This section is under development. Threat scanning features are currently managed through the YARA Rules and Threat Intelligence integrations.
                </p>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

// Threat Intelligence Configuration Component
function ThreatIntelligenceConfig({
  config,
  onSave,
  showApiKeys,
  onToggleApiKeys,
  isSaving
}: {
  config: ThreatIntelConfig | undefined;
  onSave: (config: ThreatIntelConfig) => void;
  showApiKeys: boolean;
  onToggleApiKeys: () => void;
  isSaving: boolean;
}) {
  // Default values
  const defaultConfig: ThreatIntelConfig = {
    virusTotal: {
      enabled: false,
      apiKey: '',
      rateLimitPerMinute: 4,
      cacheEnabled: true,
      cacheTtlMinutes: 60
    },
    malwareBazaar: {
      enabled: false,
      rateLimitPerMinute: 10,
      cacheEnabled: true,
      cacheTtlMinutes: 60
    },
    alienVaultOtx: {
      enabled: false,
      apiKey: '',
      rateLimitPerMinute: 10,
      cacheEnabled: true,
      cacheTtlMinutes: 60
    }
  };

  const [formData, setFormData] = useState<ThreatIntelConfig>(defaultConfig);

  useEffect(() => {
    if (config && config.virusTotal && config.malwareBazaar && config.alienVaultOtx) {
      // Merge config with defaults to ensure all fields exist
      setFormData({
        virusTotal: { ...defaultConfig.virusTotal, ...config.virusTotal },
        malwareBazaar: { ...defaultConfig.malwareBazaar, ...config.malwareBazaar },
        alienVaultOtx: { ...defaultConfig.alienVaultOtx, ...config.alienVaultOtx }
      });
    } else {
      // If config doesn't have proper structure, use defaults
      setFormData(defaultConfig);
    }
  }, [config]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* VirusTotal */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">VirusTotal</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.virusTotal.enabled}
              onChange={(e) => setFormData({
                ...formData,
                virusTotal: { ...formData.virusTotal, enabled: e.target.checked }
              })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              API Key
            </label>
            <input
              type={showApiKeys ? 'text' : 'password'}
              value={formData.virusTotal.apiKey}
              onChange={(e) => setFormData({
                ...formData,
                virusTotal: { ...formData.virusTotal, apiKey: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="Enter VirusTotal API key"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Rate Limit (per minute)
            </label>
            <input
              type="number"
              value={formData.virusTotal.rateLimitPerMinute}
              onChange={(e) => setFormData({
                ...formData,
                virusTotal: { ...formData.virusTotal, rateLimitPerMinute: parseInt(e.target.value) }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>

          <div className="flex items-center gap-4">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={formData.virusTotal.cacheEnabled}
                onChange={(e) => setFormData({
                  ...formData,
                  virusTotal: { ...formData.virusTotal, cacheEnabled: e.target.checked }
                })}
                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              <span className="text-sm text-gray-700 dark:text-gray-300">Cache Enabled</span>
            </label>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Cache TTL (minutes)
            </label>
            <input
              type="number"
              value={formData.virusTotal.cacheTtlMinutes}
              onChange={(e) => setFormData({
                ...formData,
                virusTotal: { ...formData.virusTotal, cacheTtlMinutes: parseInt(e.target.value) }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
        </div>
      </div>

      {/* MalwareBazaar */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">MalwareBazaar</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.malwareBazaar.enabled}
              onChange={(e) => setFormData({
                ...formData,
                malwareBazaar: { ...formData.malwareBazaar, enabled: e.target.checked }
              })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Rate Limit (per minute)
            </label>
            <input
              type="number"
              value={formData.malwareBazaar.rateLimitPerMinute}
              onChange={(e) => setFormData({
                ...formData,
                malwareBazaar: { ...formData.malwareBazaar, rateLimitPerMinute: parseInt(e.target.value) }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>

          <div className="flex items-center gap-4">
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={formData.malwareBazaar.cacheEnabled}
                onChange={(e) => setFormData({
                  ...formData,
                  malwareBazaar: { ...formData.malwareBazaar, cacheEnabled: e.target.checked }
                })}
                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              <span className="text-sm text-gray-700 dark:text-gray-300">Cache Enabled</span>
            </label>
          </div>
        </div>
      </div>

      {/* AlienVault OTX */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">AlienVault OTX</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.alienVaultOtx.enabled}
              onChange={(e) => setFormData({
                ...formData,
                alienVaultOtx: { ...formData.alienVaultOtx, enabled: e.target.checked }
              })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              API Key
            </label>
            <input
              type={showApiKeys ? 'text' : 'password'}
              value={formData.alienVaultOtx.apiKey}
              onChange={(e) => setFormData({
                ...formData,
                alienVaultOtx: { ...formData.alienVaultOtx, apiKey: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="Enter AlienVault OTX API key"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Rate Limit (per minute)
            </label>
            <input
              type="number"
              value={formData.alienVaultOtx.rateLimitPerMinute}
              onChange={(e) => setFormData({
                ...formData,
                alienVaultOtx: { ...formData.alienVaultOtx, rateLimitPerMinute: parseInt(e.target.value) }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={onToggleApiKeys}
          className="flex items-center gap-2 px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg"
        >
          {showApiKeys ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          {showApiKeys ? 'Hide' : 'Show'} API Keys
        </button>

        <button
          type="submit"
          disabled={isSaving}
          className="flex items-center gap-2 px-6 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Save className="h-4 w-4" />
          {isSaving ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>
    </form>
  );
}

// Notifications Configuration Component (simplified for brevity)
function NotificationsConfig({
  config,
  onSave,
  showApiKeys,
  onToggleApiKeys,
  isSaving
}: {
  config: NotificationConfig | undefined;
  onSave: (config: NotificationConfig) => void;
  showApiKeys: boolean;
  onToggleApiKeys: () => void;
  isSaving: boolean;
}) {
  const defaultConfig: NotificationConfig = {
    teams: {
      enabled: false,
      webhookUrl: '',
      notificationTypes: {
        criticalEvents: true,
        highRiskEvents: true,
        mediumRiskEvents: false,
        correlationAlerts: true,
        yaraMatches: true
      }
    },
    slack: {
      enabled: false,
      webhookUrl: '',
      channel: '#security-alerts',
      notificationTypes: {
        criticalEvents: true,
        highRiskEvents: true,
        mediumRiskEvents: false,
        correlationAlerts: true,
        yaraMatches: true
      }
    }
  };

  const [formData, setFormData] = useState<NotificationConfig>(defaultConfig);

  useEffect(() => {
    if (config && config.teams && config.slack) {
      // Merge config with defaults to ensure all fields exist
      setFormData({
        teams: {
          ...defaultConfig.teams,
          ...config.teams,
          notificationTypes: {
            ...defaultConfig.teams.notificationTypes,
            ...config.teams.notificationTypes
          }
        },
        slack: {
          ...defaultConfig.slack,
          ...config.slack,
          notificationTypes: {
            ...defaultConfig.slack.notificationTypes,
            ...config.slack.notificationTypes
          }
        }
      });
    } else {
      // If config doesn't have proper structure, use defaults
      setFormData(defaultConfig);
    }
  }, [config]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* Teams */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Microsoft Teams</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.teams.enabled}
              onChange={(e) => setFormData({
                ...formData,
                teams: { ...formData.teams, enabled: e.target.checked }
              })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Webhook URL
            </label>
            <input
              type={showApiKeys ? 'text' : 'password'}
              value={formData.teams.webhookUrl}
              onChange={(e) => setFormData({
                ...formData,
                teams: { ...formData.teams, webhookUrl: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="https://outlook.office.com/webhook/..."
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Notification Types
            </label>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
              {Object.entries(formData.teams.notificationTypes).map(([key, value]) => (
                <label key={key} className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={value}
                    onChange={(e) => setFormData({
                      ...formData,
                      teams: {
                        ...formData.teams,
                        notificationTypes: {
                          ...formData.teams.notificationTypes,
                          [key]: e.target.checked
                        }
                      }
                    })}
                    className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                  />
                  <span className="text-sm text-gray-700 dark:text-gray-300">
                    {key.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase())}
                  </span>
                </label>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Slack */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Slack</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.slack.enabled}
              onChange={(e) => setFormData({
                ...formData,
                slack: { ...formData.slack, enabled: e.target.checked }
              })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Webhook URL
            </label>
            <input
              type={showApiKeys ? 'text' : 'password'}
              value={formData.slack.webhookUrl}
              onChange={(e) => setFormData({
                ...formData,
                slack: { ...formData.slack, webhookUrl: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="https://hooks.slack.com/services/..."
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Channel
            </label>
            <input
              type="text"
              value={formData.slack.channel}
              onChange={(e) => setFormData({
                ...formData,
                slack: { ...formData.slack, channel: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="#security-alerts"
            />
          </div>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={onToggleApiKeys}
          className="flex items-center gap-2 px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg"
        >
          {showApiKeys ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          {showApiKeys ? 'Hide' : 'Show'} Webhook URLs
        </button>

        <button
          type="submit"
          disabled={isSaving}
          className="flex items-center gap-2 px-6 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Save className="h-4 w-4" />
          {isSaving ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>
    </form>
  );
}

// IP Enrichment Configuration Component (simplified for brevity)
function IPEnrichmentConfig({
  config,
  onSave,
  showApiKeys,
  onToggleApiKeys,
  isSaving
}: {
  config: IPEnrichmentConfig | undefined;
  onSave: (config: IPEnrichmentConfig) => void;
  showApiKeys: boolean;
  onToggleApiKeys: () => void;
  isSaving: boolean;
}) {
  const defaultConfig: IPEnrichmentConfig = {
    enabled: false,
    provider: 'Disabled',
    maxMind: {
      licenseKey: '',
      accountId: '',
      autoUpdate: false,
      updateFrequencyDays: 7
    },
    ipInfo: {
      apiKey: ''
    }
  };

  const [formData, setFormData] = useState<IPEnrichmentConfig>(defaultConfig);

  useEffect(() => {
    if (config && typeof config.enabled !== 'undefined' && config.maxMind && config.ipInfo) {
      // Merge config with defaults to ensure all fields exist
      setFormData({
        ...defaultConfig,
        ...config,
        maxMind: { ...defaultConfig.maxMind, ...config.maxMind },
        ipInfo: { ...defaultConfig.ipInfo, ...config.ipInfo }
      });
    } else {
      // If config doesn't have proper structure, use defaults
      setFormData(defaultConfig);
    }
  }, [config]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* General Settings */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">General Settings</h2>
        
        <div className="space-y-4">
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.enabled}
              onChange={(e) => setFormData({ ...formData, enabled: e.target.checked })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enable IP Enrichment</span>
          </label>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Provider
            </label>
            <select
              value={formData.provider}
              onChange={(e) => setFormData({ ...formData, provider: e.target.value as any })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="Disabled">Disabled</option>
              <option value="MaxMind">MaxMind GeoIP2</option>
              <option value="IPInfo">IPInfo</option>
            </select>
          </div>
        </div>
      </div>

      {/* MaxMind Settings */}
      {formData.provider === 'MaxMind' && (
        <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">MaxMind Configuration</h2>
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                License Key
              </label>
              <input
                type={showApiKeys ? 'text' : 'password'}
                value={formData.maxMind.licenseKey}
                onChange={(e) => setFormData({
                  ...formData,
                  maxMind: { ...formData.maxMind, licenseKey: e.target.value }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                placeholder="Enter MaxMind license key"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Account ID
              </label>
              <input
                type="text"
                value={formData.maxMind.accountId}
                onChange={(e) => setFormData({
                  ...formData,
                  maxMind: { ...formData.maxMind, accountId: e.target.value }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                placeholder="Enter account ID"
              />
            </div>

            <div className="flex items-center gap-4">
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={formData.maxMind.autoUpdate}
                  onChange={(e) => setFormData({
                    ...formData,
                    maxMind: { ...formData.maxMind, autoUpdate: e.target.checked }
                  })}
                  className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">Auto Update</span>
              </label>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Update Frequency (days)
              </label>
              <input
                type="number"
                value={formData.maxMind.updateFrequencyDays}
                onChange={(e) => setFormData({
                  ...formData,
                  maxMind: { ...formData.maxMind, updateFrequencyDays: parseInt(e.target.value) }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              />
            </div>
          </div>
        </div>
      )}

      {/* IPInfo Settings */}
      {formData.provider === 'IPInfo' && (
        <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">IPInfo Configuration</h2>
          
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              API Key
            </label>
            <input
              type={showApiKeys ? 'text' : 'password'}
              value={formData.ipInfo.apiKey}
              onChange={(e) => setFormData({
                ...formData,
                ipInfo: { ...formData.ipInfo, apiKey: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="Enter IPInfo API key"
            />
          </div>
        </div>
      )}

      {/* Action Buttons */}
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={onToggleApiKeys}
          className="flex items-center gap-2 px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg"
        >
          {showApiKeys ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          {showApiKeys ? 'Hide' : 'Show'} API Keys
        </button>

        <button
          type="submit"
          disabled={isSaving}
          className="flex items-center gap-2 px-6 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Save className="h-4 w-4" />
          {isSaving ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>
    </form>
  );
}

