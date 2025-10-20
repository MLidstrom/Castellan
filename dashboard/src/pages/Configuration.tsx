import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient, useMutationState } from '@tanstack/react-query';
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
  Trash2,
  CheckCircle,
  Search,
  Loader2,
  ChevronDown,
  ChevronUp,
  Edit
} from 'lucide-react';

// Custom Teams Icon Component
function TeamsIcon({ className = "h-6 w-6" }: { className?: string }) {
  return <img src="/microsoft-teams-1.svg" alt="Microsoft Teams" className={className} />;
}

// Custom Slack Icon Component
function SlackIcon({ className = "h-6 w-6" }: { className?: string }) {
  return <img src="/slack-new-logo.svg" alt="Slack" className={className} />;
}
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { MalwareConfigComponent } from '../components/MalwareConfigComponent';
import { API_URL } from '../services/constants';
import { Api } from '../services/api';

type TabType = 'threat-intelligence' | 'notifications' | 'ip-enrichment' | 'malware' | 'mitre' | 'threat-scanner' | 'performance';

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
  id?: string;
  name?: string;
  teams: {
    enabled: boolean;
    webhookUrl: string;
    castellanUrl: string;
    rateLimitSettings: {
      criticalThrottleMinutes: number;
      highThrottleMinutes: number;
      mediumThrottleMinutes: number;
      lowThrottleMinutes: number;
    };
  };
  slack: {
    enabled: boolean;
    webhookUrl: string;
    castellanUrl: string;
    defaultChannel: string;
    criticalChannel: string;
    highChannel: string;
    rateLimitSettings: {
      criticalThrottleMinutes: number;
      highThrottleMinutes: number;
      mediumThrottleMinutes: number;
      lowThrottleMinutes: number;
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

// Notification Template Types
interface NotificationTemplate {
  id: string;
  name: string;
  platform: 'Teams' | 'Slack';
  type: 'SecurityEvent' | 'SystemAlert' | 'HealthWarning' | 'PerformanceAlert';
  templateContent: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

// Platform color mapping
const PLATFORM_COLORS: Record<string, string> = {
  'Teams': 'bg-blue-100 text-blue-800 border-blue-200 dark:bg-blue-900/30 dark:text-blue-300 dark:border-blue-800',
  'Slack': 'bg-purple-100 text-purple-800 border-purple-200 dark:bg-purple-900/30 dark:text-purple-300 dark:border-purple-800',
};

// Template type color mapping
const TYPE_COLORS: Record<string, string> = {
  'SecurityEvent': 'bg-red-100 text-red-800 border-red-200 dark:bg-red-900/30 dark:text-red-300 dark:border-red-800',
  'SystemAlert': 'bg-yellow-100 text-yellow-800 border-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-300 dark:border-yellow-800',
  'HealthWarning': 'bg-orange-100 text-orange-800 border-orange-200 dark:bg-orange-900/30 dark:text-orange-300 dark:border-orange-800',
  'PerformanceAlert': 'bg-green-100 text-green-800 border-green-200 dark:bg-green-900/30 dark:text-green-300 dark:border-green-800',
};

const tabs = [
  { id: 'threat-intelligence' as TabType, label: 'Threat Intelligence', icon: Shield },
  { id: 'notifications' as TabType, label: 'Notifications', icon: Bell },
  { id: 'ip-enrichment' as TabType, label: 'IP Enrichment', icon: Globe },
  { id: 'malware' as TabType, label: 'Malware Rules', icon: FileText },
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
        'malware': '/yara-configuration',
        'mitre': '/mitre/config',
        'threat-scanner': '/threat-scanner/config',
        'performance': '/performance/config',
      };

      const response = await fetch(`${API_URL}${endpoints[activeTab]}`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error('Failed to fetch configuration');
      const data = await response.json();

      // For notifications, the API returns a list, so we need to get the first config or create a default
      if (activeTab === 'notifications') {
        const configs = data.data || [];
        return configs.length > 0 ? configs[0] : null;
      }

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
        'malware': '/yara-configuration',
        'mitre': '/mitre/config',
        'threat-scanner': '/threat-scanner/config',
        'performance': '/performance/config',
      };

      // Special handling for notifications - use POST to create or PUT with ID to update
      if (activeTab === 'notifications') {
        const method = config.id ? 'PUT' : 'POST';
        const url = config.id
          ? `${API_URL}${endpoints[activeTab]}/${config.id}`
          : `${API_URL}${endpoints[activeTab]}`;

        const response = await fetch(url, {
          method,
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(config)
        });
        if (!response.ok) throw new Error('Failed to save configuration');
        return response.json();
      }

      const response = await fetch(`${API_URL}${endpoints[activeTab]}`, {
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
            {activeTab === 'malware' && (
              <MalwareConfigComponent
                config={configQuery.data}
                onSave={handleSave}
                isSaving={saveMutation.isPending}
              />
            )}
            {activeTab === 'mitre' && <MitreConfig onSaveSuccess={() => {
              setSaveSuccess(true);
              setTimeout(() => setSaveSuccess(false), 3000);
            }} />}
            {activeTab === 'threat-scanner' && <ThreatScannerConfig />}
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

// Notifications Configuration Component
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
    id: undefined,
    name: 'Default Notification Configuration',
    teams: {
      enabled: false,
      webhookUrl: '',
      castellanUrl: 'http://localhost:3000',
      rateLimitSettings: {
        criticalThrottleMinutes: 0,
        highThrottleMinutes: 5,
        mediumThrottleMinutes: 15,
        lowThrottleMinutes: 60
      }
    },
    slack: {
      enabled: false,
      webhookUrl: '',
      castellanUrl: 'http://localhost:3000',
      defaultChannel: '#security-alerts',
      criticalChannel: '',
      highChannel: '',
      rateLimitSettings: {
        criticalThrottleMinutes: 0,
        highThrottleMinutes: 5,
        mediumThrottleMinutes: 15,
        lowThrottleMinutes: 60
      }
    }
  };

  const [formData, setFormData] = useState<NotificationConfig>(defaultConfig);

  useEffect(() => {
    if (config && config.teams && config.slack) {
      // Merge config with defaults to ensure all fields exist
      setFormData({
        id: config.id,
        name: config.name || defaultConfig.name,
        teams: {
          ...defaultConfig.teams,
          ...config.teams,
          rateLimitSettings: {
            ...defaultConfig.teams.rateLimitSettings,
            ...config.teams.rateLimitSettings
          }
        },
        slack: {
          ...defaultConfig.slack,
          ...config.slack,
          rateLimitSettings: {
            ...defaultConfig.slack.rateLimitSettings,
            ...config.slack.rateLimitSettings
          }
        }
      });
    } else {
      // If config doesn't exist, use defaults (will create new on save)
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
          <div className="flex items-center gap-3">
            <TeamsIcon className="h-6 w-6" />
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Microsoft Teams</h2>
          </div>
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
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Castellan URL
            </label>
            <input
              type="text"
              value={formData.teams.castellanUrl}
              onChange={(e) => setFormData({
                ...formData,
                teams: { ...formData.teams, castellanUrl: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="http://localhost:3000"
            />
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
              Base URL for Castellan instance (used in notification links)
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Rate Limit Settings (minutes between alerts)
            </label>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">Critical</label>
                <input
                  type="number"
                  min="0"
                  value={formData.teams.rateLimitSettings.criticalThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    teams: {
                      ...formData.teams,
                      rateLimitSettings: {
                        ...formData.teams.rateLimitSettings,
                        criticalThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">High</label>
                <input
                  type="number"
                  min="0"
                  value={formData.teams.rateLimitSettings.highThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    teams: {
                      ...formData.teams,
                      rateLimitSettings: {
                        ...formData.teams.rateLimitSettings,
                        highThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">Medium</label>
                <input
                  type="number"
                  min="0"
                  value={formData.teams.rateLimitSettings.mediumThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    teams: {
                      ...formData.teams,
                      rateLimitSettings: {
                        ...formData.teams.rateLimitSettings,
                        mediumThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">Low</label>
                <input
                  type="number"
                  min="0"
                  value={formData.teams.rateLimitSettings.lowThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    teams: {
                      ...formData.teams,
                      rateLimitSettings: {
                        ...formData.teams.rateLimitSettings,
                        lowThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Slack */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <SlackIcon className="h-6 w-6" />
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Slack</h2>
          </div>
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
              Castellan URL
            </label>
            <input
              type="text"
              value={formData.slack.castellanUrl}
              onChange={(e) => setFormData({
                ...formData,
                slack: { ...formData.slack, castellanUrl: e.target.value }
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="http://localhost:3000"
            />
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
              Base URL for Castellan instance (used in notification links)
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Default Channel
              </label>
              <input
                type="text"
                value={formData.slack.defaultChannel}
                onChange={(e) => setFormData({
                  ...formData,
                  slack: { ...formData.slack, defaultChannel: e.target.value }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                placeholder="#security-alerts"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Critical Channel
              </label>
              <input
                type="text"
                value={formData.slack.criticalChannel}
                onChange={(e) => setFormData({
                  ...formData,
                  slack: { ...formData.slack, criticalChannel: e.target.value }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                placeholder="#critical-alerts"
              />
              <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">Optional</p>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                High Priority Channel
              </label>
              <input
                type="text"
                value={formData.slack.highChannel}
                onChange={(e) => setFormData({
                  ...formData,
                  slack: { ...formData.slack, highChannel: e.target.value }
                })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                placeholder="#high-alerts"
              />
              <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">Optional</p>
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
              Rate Limit Settings (minutes between alerts)
            </label>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">Critical</label>
                <input
                  type="number"
                  min="0"
                  value={formData.slack.rateLimitSettings.criticalThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    slack: {
                      ...formData.slack,
                      rateLimitSettings: {
                        ...formData.slack.rateLimitSettings,
                        criticalThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">High</label>
                <input
                  type="number"
                  min="0"
                  value={formData.slack.rateLimitSettings.highThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    slack: {
                      ...formData.slack,
                      rateLimitSettings: {
                        ...formData.slack.rateLimitSettings,
                        highThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">Medium</label>
                <input
                  type="number"
                  min="0"
                  value={formData.slack.rateLimitSettings.mediumThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    slack: {
                      ...formData.slack,
                      rateLimitSettings: {
                        ...formData.slack.rateLimitSettings,
                        mediumThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
              <div>
                <label className="block text-xs text-gray-600 dark:text-gray-400 mb-1">Low</label>
                <input
                  type="number"
                  min="0"
                  value={formData.slack.rateLimitSettings.lowThrottleMinutes}
                  onChange={(e) => setFormData({
                    ...formData,
                    slack: {
                      ...formData.slack,
                      rateLimitSettings: {
                        ...formData.slack.rateLimitSettings,
                        lowThrottleMinutes: parseInt(e.target.value) || 0
                      }
                    }
                  })}
                  className="w-full px-2 py-1 border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Message Templates Section */}
      <TemplateManagementSection />

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

// Template Editor Modal Component
function TemplateEditorModal({
  template,
  isOpen,
  onClose,
  onSave,
  isNew
}: {
  template: NotificationTemplate | null;
  isOpen: boolean;
  onClose: () => void;
  onSave: (template: Partial<NotificationTemplate>) => void;
  isNew: boolean;
}) {
  const [formData, setFormData] = useState<Partial<NotificationTemplate>>({
    name: '',
    platform: 'Teams',
    type: 'SecurityEvent',
    templateContent: '',
    isEnabled: true,
  });

  useEffect(() => {
    if (template && isOpen) {
      setFormData({
        name: template.name,
        platform: template.platform,
        type: template.type,
        templateContent: template.templateContent,
        isEnabled: template.isEnabled,
      });
    } else if (isNew && isOpen) {
      setFormData({
        name: '',
        platform: 'Teams',
        type: 'SecurityEvent',
        templateContent: '',
        isEnabled: true,
      });
    }
  }, [template, isOpen, isNew]);

  if (!isOpen) return null;

  const handleSave = () => {
    onSave(formData);
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-2xl w-full mx-4 max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              {isNew ? 'Create Template' : 'Edit Template'}
            </h2>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            >
              <X className="h-6 w-6" />
            </button>
          </div>
        </div>

        <div className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Template Name
            </label>
            <input
              type="text"
              value={formData.name || ''}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="e.g., Security Alert Template"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Platform
              </label>
              <select
                value={formData.platform || 'Teams'}
                onChange={(e) => setFormData({ ...formData, platform: e.target.value as any })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              >
                <option value="Teams">Teams</option>
                <option value="Slack">Slack</option>
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Type
              </label>
              <select
                value={formData.type || 'SecurityEvent'}
                onChange={(e) => setFormData({ ...formData, type: e.target.value as any })}
                className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              >
                <option value="SecurityEvent">Security Event</option>
                <option value="SystemAlert">System Alert</option>
                <option value="HealthWarning">Health Warning</option>
                <option value="PerformanceAlert">Performance Alert</option>
              </select>
            </div>
          </div>

          <div>
            <label className="flex items-center space-x-2">
              <input
                type="checkbox"
                checked={formData.isEnabled || false}
                onChange={(e) => setFormData({ ...formData, isEnabled: e.target.checked })}
                className="w-4 h-4 text-blue-600 border-gray-300 rounded"
              />
              <span className="text-sm font-medium text-gray-700 dark:text-gray-300">
                Enabled
              </span>
            </label>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Template Content
            </label>
            <textarea
              value={formData.templateContent || ''}
              onChange={(e) => setFormData({ ...formData, templateContent: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white font-mono text-sm"
              rows={12}
              placeholder="Enter template content with {{TAGS}}..."
            />
            <div className="text-xs text-gray-600 dark:text-gray-400 mt-2 space-y-1 bg-gray-50 dark:bg-gray-900/30 p-3 rounded border border-gray-200 dark:border-gray-700">
              <div className="font-semibold text-gray-700 dark:text-gray-300 mb-2">📝 Available Template Variables</div>
              <div className="grid grid-cols-2 gap-x-4 gap-y-1">
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} DATE {'}'}{'}'}</span> - Event timestamp</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} HOST {'}'}{'}'}</span> - Machine hostname</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} USER {'}'}{'}'}</span> - User account</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} EVENT_ID {'}'}{'}'}</span> - Event identifier</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} SEVERITY {'}'}{'}'}</span> - Risk severity</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} EVENT_TYPE {'}'}{'}'}</span> - Event category</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} MACHINE_NAME {'}'}{'}'}</span> - System name</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} TIMESTAMP {'}'}{'}'}</span> - Alert timestamp</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} ALERT_ID {'}'}{'}'}</span> - Alert identifier</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} DETAILS_URL {'}'}{'}'}</span> - Dashboard link</div>
              </div>
              <div className="mt-2 pt-2 border-t border-gray-300 dark:border-gray-600">
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} SUMMARY {'}'}{'}'}</span> - Detailed event description</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} MITRE_TECHNIQUES {'}'}{'}'}</span> - MITRE ATT&CK mappings</div>
                <div><span className="font-mono text-blue-600 dark:text-blue-400">{'{'}{'{'} RECOMMENDED_ACTIONS {'}'}{'}'}</span> - Response guidance</div>
              </div>
              <div className="mt-2 pt-2 border-t border-gray-300 dark:border-gray-600 font-semibold text-gray-700 dark:text-gray-300">🎨 Formatting Tags</div>
              <div className="space-y-1">
                <div><span className="font-mono text-green-600 dark:text-green-400">{'{'}{'{'} BOLD:text {'}'}{'}'}</span> - Bold formatting</div>
                <div><span className="font-mono text-green-600 dark:text-green-400">{'{'}{'{'} LINK:{'{'}{'{'} DETAILS_URL {'}'}{'}'} | Click Here {'}'}{'}'}</span> - Hyperlink with label</div>
              </div>
            </div>
          </div>

          <div className="flex justify-end space-x-3 pt-4">
            <button
              onClick={onClose}
              className="px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
            >
              Cancel
            </button>
            <button
              onClick={handleSave}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
            >
              {isNew ? 'Create Template' : 'Save Changes'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// Template Management Section Component
function TemplateManagementSection() {
  const { token } = useAuth();
  const queryClient = useQueryClient();
  const [isExpanded, setIsExpanded] = useState(false);
  const [search, setSearch] = useState('');
  const [platformFilter, setPlatformFilter] = useState('');
  const [selectedTemplate, setSelectedTemplate] = useState<NotificationTemplate | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [isNewTemplate, setIsNewTemplate] = useState(false);

  // Query for templates
  const templatesQuery = useQuery({
    queryKey: ['notification-templates'],
    queryFn: Api.getNotificationTemplates,
    enabled: !!token && isExpanded,
    staleTime: 5 * 60 * 1000,
    gcTime: 30 * 60 * 1000,
  });

  // Create mutation
  const createMutation = useMutation({
    mutationFn: (template: Partial<NotificationTemplate>) => Api.createNotificationTemplate(template),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notification-templates'] });
      setEditorOpen(false);
    },
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: ({ id, template }: { id: string; template: Partial<NotificationTemplate> }) =>
      Api.updateNotificationTemplate(id, template),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notification-templates'] });
      setEditorOpen(false);
    },
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: (id: string) => Api.deleteNotificationTemplate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notification-templates'] });
    },
  });

  // Toggle enabled mutation
  const toggleMutation = useMutation({
    mutationFn: ({ id, template }: { id: string; template: Partial<NotificationTemplate> }) =>
      Api.updateNotificationTemplate(id, template),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notification-templates'] });
    },
  });

  // Create defaults mutation
  const createDefaultsMutation = useMutation({
    mutationFn: Api.createDefaultTemplates,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notification-templates'] });
    },
  });

  const templates = (templatesQuery.data as NotificationTemplate[]) || [];

  // Filter templates
  const filteredTemplates = templates.filter(t => {
    if (platformFilter && t.platform !== platformFilter) return false;
    if (search && !t.name.toLowerCase().includes(search.toLowerCase()) &&
        !(t.templateContent || '').toLowerCase().includes(search.toLowerCase())) return false;
    return true;
  });

  const handleCreateNew = () => {
    setSelectedTemplate(null);
    setIsNewTemplate(true);
    setEditorOpen(true);
  };

  const handleEdit = (template: NotificationTemplate) => {
    setSelectedTemplate(template);
    setIsNewTemplate(false);
    setEditorOpen(true);
  };

  const handleSave = (template: Partial<NotificationTemplate>) => {
    if (isNewTemplate) {
      createMutation.mutate(template);
    } else if (selectedTemplate) {
      updateMutation.mutate({ id: selectedTemplate.id, template });
    }
  };

  const handleDelete = (template: NotificationTemplate) => {
    if (confirm(`Delete template "${template.name}"?`)) {
      deleteMutation.mutate(template.id);
    }
  };

  const handleToggle = (template: NotificationTemplate) => {
    toggleMutation.mutate({
      id: template.id,
      template: { ...template, isEnabled: !template.isEnabled }
    });
  };

  return (
    <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
      <div className="flex items-center justify-between mb-4 cursor-pointer" onClick={() => setIsExpanded(!isExpanded)}>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white flex items-center gap-2">
          <Bell className="h-5 w-5" />
          Message Templates
          <span className="text-sm font-normal text-gray-500 dark:text-gray-400">
            ({templates.length} templates)
          </span>
        </h2>
        <button type="button" className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
          {isExpanded ? <ChevronUp className="h-5 w-5" /> : <ChevronDown className="h-5 w-5" />}
        </button>
      </div>

      {isExpanded && (
        <div className="space-y-4 pt-4 border-t border-gray-200 dark:border-gray-700">
          {/* Search and Filter */}
          <div className="flex flex-col sm:flex-row gap-3">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
              <input
                type="text"
                placeholder="Search templates..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
              />
            </div>
            <select
              value={platformFilter}
              onChange={(e) => setPlatformFilter(e.target.value)}
              className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-sm"
            >
              <option value="">All Platforms</option>
              <option value="Teams">Teams</option>
              <option value="Slack">Slack</option>
            </select>
            {templates.length > 0 && (
              <button
                type="button"
                onClick={handleCreateNew}
                className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 text-sm whitespace-nowrap"
              >
                <Plus className="h-4 w-4" />
                New Template
              </button>
            )}
          </div>

          {/* Templates List */}
          {templatesQuery.isLoading && (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="h-6 w-6 animate-spin text-blue-600" />
              <span className="ml-2 text-gray-600 dark:text-gray-300">Loading templates...</span>
            </div>
          )}

          {!templatesQuery.isLoading && templates.length === 0 && (
            <div className="text-center py-8">
              <Bell className="h-12 w-12 text-gray-400 mx-auto mb-3" />
              <p className="text-gray-600 dark:text-gray-300 mb-4">No templates found</p>
              <button
                onClick={() => createDefaultsMutation.mutate()}
                disabled={createDefaultsMutation.isPending}
                className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors disabled:opacity-50"
              >
                {createDefaultsMutation.isPending ? 'Creating...' : 'Create Default Templates'}
              </button>
            </div>
          )}

          {!templatesQuery.isLoading && filteredTemplates.length > 0 && (
            <div className="space-y-3">
              {filteredTemplates.map((template) => (
                <div
                  key={template.id}
                  className="bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg p-4"
                >
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <div className="flex items-center space-x-2 mb-2">
                        <span className={`px-2 py-1 rounded-full text-xs font-medium border ${PLATFORM_COLORS[template.platform]}`}>
                          {template.platform}
                        </span>
                        <span className={`px-2 py-1 rounded-full text-xs font-medium border ${TYPE_COLORS[template.type]}`}>
                          {template.type}
                        </span>
                        {template.isEnabled ? (
                          <CheckCircle className="h-4 w-4 text-green-500" />
                        ) : (
                          <AlertCircle className="h-4 w-4 text-gray-400" />
                        )}
                      </div>
                      <h4 className="font-medium text-gray-900 dark:text-white mb-1">
                        {template.name}
                      </h4>
                      <p className="text-xs text-gray-600 dark:text-gray-400 font-mono bg-white dark:bg-gray-800 p-2 rounded">
                        {(template.templateContent || '').substring(0, 80)}...
                      </p>
                    </div>
                    <div className="flex space-x-2 ml-4">
                      <button
                        type="button"
                        onClick={() => handleEdit(template)}
                        className="p-1 text-gray-600 hover:text-blue-600 dark:text-gray-400 dark:hover:text-blue-400"
                        title="Edit"
                      >
                        <Edit className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleToggle(template)}
                        className="p-1 text-gray-600 hover:text-green-600 dark:text-gray-400 dark:hover:text-green-400"
                        title={template.isEnabled ? 'Disable' : 'Enable'}
                      >
                        <CheckCircle className={`h-4 w-4 ${template.isEnabled ? 'text-green-500' : 'text-gray-400'}`} />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(template)}
                        className="p-1 text-gray-600 hover:text-red-600 dark:text-gray-400 dark:hover:text-red-400"
                        title="Delete"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {!templatesQuery.isLoading && templates.length > 0 && filteredTemplates.length === 0 && (
            <div className="text-center py-8">
              <p className="text-gray-600 dark:text-gray-300">No templates match your filters</p>
            </div>
          )}
        </div>
      )}

      {/* Editor Modal */}
      <TemplateEditorModal
        template={selectedTemplate}
        isOpen={editorOpen}
        onClose={() => setEditorOpen(false)}
        onSave={handleSave}
        isNew={isNewTemplate}
      />
    </div>
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

// Threat Scanner Configuration Component
function ThreatScannerConfig() {
  const { token } = useAuth();
  const queryClient = useQueryClient();
  const [newDirectory, setNewDirectory] = useState('');
  const [newExtension, setNewExtension] = useState('');
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Default configuration
  const defaultConfig = {
    enabled: false,
    scheduledScanInterval: '1.00:00:00',
    defaultScanType: 0,
    quarantineThreats: false,
    quarantineDirectory: 'C:\\Castellan\\Quarantine',
    maxFileSizeMB: 100,
    maxConcurrentFiles: 10,
    notificationThreshold: 2,
    excludedDirectories: [] as string[],
    excludedExtensions: [] as string[],
    enableRealTimeProtection: false
  };

  // Local form state
  const [formData, setFormData] = useState(defaultConfig);

  // Fetch scanner config
  const configQuery = useQuery({
    queryKey: ['threat-scanner-config'],
    queryFn: async () => {
      console.log('[ThreatScanner] Fetching config from API...');
      const response = await fetch(`${API_URL}/scheduledscan/config`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (!response.ok) throw new Error('Failed to fetch scanner config');
      const data = await response.json();
      const rawConfig = data.data || data;

      console.log('[ThreatScanner] Raw config from API:', rawConfig);

      // Normalize backend flat structure to frontend flat structure
      const normalized = {
        enabled: rawConfig.enabled ?? false,
        scheduledScanInterval: rawConfig.scheduledScanInterval || '1.00:00:00',
        defaultScanType: rawConfig.defaultScanType ?? 0,
        quarantineThreats: rawConfig.quarantineThreats ?? false,
        quarantineDirectory: rawConfig.quarantineDirectory || '',
        maxFileSizeMB: rawConfig.maxFileSizeMB ?? 100,
        maxConcurrentFiles: rawConfig.maxConcurrentFiles ?? 10,
        notificationThreshold: rawConfig.notificationThreshold ?? 2,
        excludedDirectories: rawConfig.excludedDirectories || [],
        excludedExtensions: rawConfig.excludedExtensions || [],
        enableRealTimeProtection: rawConfig.enableRealTimeProtection ?? false
      };

      console.log('[ThreatScanner] Normalized config:', normalized);
      return normalized;
    },
    enabled: !!token,
  });

  // Update form data when config is loaded
  useEffect(() => {
    if (configQuery.data) {
      console.log('[ThreatScanner] useEffect updating formData with:', configQuery.data);
      setFormData(configQuery.data);
    }
  }, [configQuery.data]);

  // Fetch scanner status
  const statusQuery = useQuery({
    queryKey: ['threat-scanner-status'],
    queryFn: async () => {
      const response = await fetch(`${API_URL}/scheduledscan/status`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (!response.ok) throw new Error('Failed to fetch scanner status');
      const data = await response.json();
      return data.data || data;
    },
    enabled: !!token,
    refetchInterval: 30000, // Refresh every 30 seconds
  });

  // Save configuration mutation
  const saveMutation = useMutation({
    mutationFn: async (config: any) => {
      console.log('[ThreatScanner] Saving config:', config);

      // Transform to backend PascalCase structure
      // ThreatRiskLevel enum: Low=0, Medium=1, High=2, Critical=3
      // ThreatScanType enum: QuickScan=0, FullScan=1, DirectoryScan=2, FileScan=3
      const backendConfig = {
        Enabled: config.enabled,
        ScheduledScanInterval: config.scheduledScanInterval,
        DefaultScanType: config.defaultScanType,
        QuarantineThreats: config.quarantineThreats,
        QuarantineDirectory: config.quarantineDirectory,
        MaxFileSizeMB: config.maxFileSizeMB,
        MaxConcurrentFiles: config.maxConcurrentFiles,
        NotificationThreshold: config.notificationThreshold,
        ExcludedDirectories: config.excludedDirectories,
        ExcludedExtensions: config.excludedExtensions,
        EnableRealTimeProtection: config.enableRealTimeProtection
      };

      console.log('[ThreatScanner] Sending to backend:', backendConfig);

      const response = await fetch(`${API_URL}/scheduledscan/config`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(backendConfig)
      });

      console.log('[ThreatScanner] Response status:', response.status);

      if (!response.ok) {
        const errorText = await response.text();
        console.error('[ThreatScanner] Save failed:', errorText);
        throw new Error('Failed to save configuration');
      }

      const result = await response.json();
      console.log('[ThreatScanner] Save successful, response:', result);
      return result;
    },
    onSuccess: () => {
      console.log('[ThreatScanner] Save mutation onSuccess, invalidating queries...');
      queryClient.invalidateQueries({ queryKey: ['threat-scanner-config'] });
      setSaveSuccess(true);
      setSaveError(null);
      setTimeout(() => setSaveSuccess(false), 3000);
    },
    onError: (error: Error) => {
      console.error('[ThreatScanner] Save mutation onError:', error);
      setSaveError(error.message);
      setSaveSuccess(false);
    },
  });

  const status = statusQuery.data || {
    isEnabled: false,
    lastScanTime: null,
    nextScanTime: null,
    currentStatus: 'Idle'
  };

  // Parse scan interval from TimeSpan string (d.hh:mm:ss)
  const parseScanInterval = (interval: string | undefined) => {
    if (!interval) {
      return { days: 1, hours: 0 };
    }
    const parts = interval.split(':');
    if (parts.length === 3) {
      const [days, hours] = parts[0].split('.');
      return {
        days: parseInt(days || '0', 10),
        hours: parseInt(parts[0].includes('.') ? hours : parts[0], 10)
      };
    }
    return { days: 1, hours: 0 };
  };

  const formatScanInterval = (days: number, hours: number) => {
    return `${days}.${hours.toString().padStart(2, '0')}:00:00`;
  };

  const [intervalDays, setIntervalDays] = useState(parseScanInterval(formData.scheduledScanInterval || '1.00:00:00').days);
  const [intervalHours, setIntervalHours] = useState(parseScanInterval(formData.scheduledScanInterval || '1.00:00:00').hours);

  useEffect(() => {
    const parsed = parseScanInterval(formData.scheduledScanInterval);
    setIntervalDays(parsed.days);
    setIntervalHours(parsed.hours);
  }, [formData.scheduledScanInterval]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const updatedConfig = {
      ...formData,
      scheduledScanInterval: formatScanInterval(intervalDays, intervalHours)
    };
    saveMutation.mutate(updatedConfig);
  };

  const addDirectory = () => {
    if (newDirectory.trim()) {
      setFormData({
        ...formData,
        excludedDirectories: [...(formData.excludedDirectories || []), newDirectory.trim()]
      });
      setNewDirectory('');
    }
  };

  const removeDirectory = (dir: string) => {
    setFormData({
      ...formData,
      excludedDirectories: formData.excludedDirectories.filter((d: string) => d !== dir)
    });
  };

  const addExtension = () => {
    if (newExtension.trim()) {
      const ext = newExtension.trim().startsWith('.') ? newExtension.trim() : `.${newExtension.trim()}`;
      setFormData({
        ...formData,
        excludedExtensions: [...(formData.excludedExtensions || []), ext]
      });
      setNewExtension('');
    }
  };

  const removeExtension = (ext: string) => {
    setFormData({
      ...formData,
      excludedExtensions: formData.excludedExtensions.filter((e: string) => e !== ext)
    });
  };

  if (configQuery.isLoading || statusQuery.isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  if (configQuery.isError || statusQuery.isError) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-6 flex items-start gap-3">
        <AlertCircle className="h-5 w-5 text-red-600 mt-0.5" />
        <div>
          <h3 className="text-red-800 font-medium">Failed to load configuration</h3>
          <p className="text-red-700 text-sm mt-1">
            {(configQuery.error as Error)?.message || (statusQuery.error as Error)?.message}
          </p>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* Success Message */}
      {saveSuccess && (
        <div className="bg-green-50 border border-green-200 rounded-lg p-4 flex items-center gap-2">
          <Check className="h-5 w-5 text-green-600" />
          <span className="text-green-800 font-medium">Configuration saved successfully!</span>
        </div>
      )}

      {/* Error Message */}
      {saveError && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 flex items-start gap-3">
          <AlertCircle className="h-5 w-5 text-red-600 mt-0.5" />
          <div>
            <h3 className="text-red-800 font-medium">Failed to save configuration</h3>
            <p className="text-red-700 text-sm mt-1">{saveError}</p>
          </div>
        </div>
      )}

      {/* Status Card */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Scanner Status</h2>
          <span className={`px-3 py-1 rounded-full text-sm font-medium ${
            status.currentStatus === 'Running'
              ? 'bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-200'
              : status.isEnabled
              ? 'bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-200'
              : 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300'
          }`}>
            {status.currentStatus || 'Idle'}
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-600 dark:text-gray-400 mb-1">
              Last Scan
            </label>
            <p className="text-gray-900 dark:text-white">
              {status.lastScanTime ? new Date(status.lastScanTime).toLocaleString() : 'Never'}
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-600 dark:text-gray-400 mb-1">
              Next Scan
            </label>
            <p className="text-gray-900 dark:text-white">
              {status.nextScanTime ? new Date(status.nextScanTime).toLocaleString() : 'Not scheduled'}
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-600 dark:text-gray-400 mb-1">
              Scheduler Status
            </label>
            <p className="text-gray-900 dark:text-white">
              {status.isEnabled ? 'Enabled' : 'Disabled'}
            </p>
          </div>
        </div>
      </div>

      {/* Scheduled Scans */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Scheduled Scans</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.enabled}
              onChange={(e) => setFormData({ ...formData, enabled: e.target.checked })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Scan Interval (Days)
            </label>
            <input
              type="number"
              min="0"
              max="365"
              value={intervalDays}
              onChange={(e) => setIntervalDays(parseInt(e.target.value) || 0)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Scan Interval (Hours)
            </label>
            <input
              type="number"
              min="0"
              max="23"
              value={intervalHours}
              onChange={(e) => setIntervalHours(parseInt(e.target.value) || 0)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Default Scan Type
            </label>
            <select
              value={formData.defaultScanType}
              onChange={(e) => setFormData({ ...formData, defaultScanType: parseInt(e.target.value) })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            >
              <option value="0">Quick Scan</option>
              <option value="1">Full Scan</option>
            </select>
          </div>
        </div>
      </div>

      {/* Quarantine Settings */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Quarantine Settings</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={formData.quarantineThreats}
              onChange={(e) => setFormData({
                ...formData,
                quarantineThreats: e.target.checked
              })}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
            Quarantine Directory
          </label>
          <input
            type="text"
            value={formData.quarantineDirectory || ''}
            onChange={(e) => setFormData({
              ...formData,
              quarantineDirectory: e.target.value
            })}
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            placeholder="C:\Castellan\Quarantine"
          />
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            Suspicious files will be moved to this directory for analysis
          </p>
        </div>
      </div>

      {/* Performance Settings */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Performance Settings</h2>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Max Concurrent Files
            </label>
            <input
              type="number"
              min="1"
              max="100"
              value={formData.maxConcurrentFiles || 10}
              onChange={(e) => setFormData({
                ...formData,
                maxConcurrentFiles: parseInt(e.target.value)
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
              Number of files to scan simultaneously
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Max File Size (MB)
            </label>
            <input
              type="number"
              min="1"
              max="1000"
              value={formData.maxFileSizeMB || 100}
              onChange={(e) => setFormData({
                ...formData,
                maxFileSizeMB: parseInt(e.target.value)
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
              Skip files larger than this size
            </p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Notification Threshold
            </label>
            <input
              type="number"
              min="1"
              max="100"
              value={formData.notificationThreshold || 10}
              onChange={(e) => setFormData({
                ...formData,
                notificationThreshold: parseInt(e.target.value)
              })}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
              Notify when threats exceed this count
            </p>
          </div>
        </div>
      </div>

      {/* Exclusions */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Scan Exclusions</h2>

        {/* Excluded Directories */}
        <div className="mb-6">
          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Excluded Directories
          </label>

          <div className="flex gap-2 mb-3">
            <input
              type="text"
              value={newDirectory}
              onChange={(e) => setNewDirectory(e.target.value)}
              onKeyPress={(e) => e.key === 'Enter' && addDirectory()}
              className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder="C:\Windows\System32"
            />
            <button
              type="button"
              onClick={addDirectory}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg"
            >
              <Plus className="h-4 w-4" />
              Add
            </button>
          </div>

          <div className="space-y-2">
            {(formData.excludedDirectories || []).map((dir: string, idx: number) => (
              <div key={idx} className="flex items-center justify-between bg-gray-50 dark:bg-gray-700 px-3 py-2 rounded-lg">
                <span className="text-sm text-gray-700 dark:text-gray-300 font-mono">{dir}</span>
                <button
                  type="button"
                  onClick={() => removeDirectory(dir)}
                  className="text-red-600 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            ))}
            {(!formData.excludedDirectories || formData.excludedDirectories.length === 0) && (
              <p className="text-sm text-gray-500 dark:text-gray-400 italic">No excluded directories</p>
            )}
          </div>
        </div>

        {/* Excluded Extensions */}
        <div>
          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            Excluded File Extensions
          </label>

          <div className="flex gap-2 mb-3">
            <input
              type="text"
              value={newExtension}
              onChange={(e) => setNewExtension(e.target.value)}
              onKeyPress={(e) => e.key === 'Enter' && addExtension()}
              className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder=".tmp, .log"
            />
            <button
              type="button"
              onClick={addExtension}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg"
            >
              <Plus className="h-4 w-4" />
              Add
            </button>
          </div>

          <div className="flex flex-wrap gap-2">
            {(formData.excludedExtensions || []).map((ext: string, idx: number) => (
              <div key={idx} className="flex items-center gap-2 bg-gray-50 dark:bg-gray-700 px-3 py-1 rounded-full">
                <span className="text-sm text-gray-700 dark:text-gray-300 font-mono">{ext}</span>
                <button
                  type="button"
                  onClick={() => removeExtension(ext)}
                  className="text-red-600 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
                >
                  <X className="h-3 w-3" />
                </button>
              </div>
            ))}
            {(!formData.excludedExtensions || formData.excludedExtensions.length === 0) && (
              <p className="text-sm text-gray-500 dark:text-gray-400 italic">No excluded extensions</p>
            )}
          </div>
        </div>
      </div>

      {/* Save Button */}
      <div className="flex justify-end">
        <button
          type="submit"
          disabled={saveMutation.isPending}
          className="flex items-center gap-2 px-6 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Save className="h-4 w-4" />
          {saveMutation.isPending ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>
    </form>
  );
}

// MITRE ATT&CK Configuration Component
function MitreConfig({ onSaveSuccess }: { onSaveSuccess?: () => void }) {
  const { token } = useAuth();
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  // Fetch MITRE statistics
  const statisticsQuery = useQuery({
    queryKey: ['mitre-statistics'],
    queryFn: async () => {
      const response = await fetch(`${API_URL}/mitre/statistics`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (!response.ok) throw new Error('Failed to fetch statistics');
      const data = await response.json();
      return data;
    },
    enabled: !!token,
  });

  // Fetch MITRE auto-update config
  const configQuery = useQuery({
    queryKey: ['mitre-config'],
    queryFn: async () => {
      const response = await fetch(`${API_URL}/mitre/config`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (!response.ok) throw new Error('Failed to fetch MITRE config');
      const data = await response.json();
      return data.data || data;
    },
    enabled: !!token,
  });

  // Save configuration mutation
  const saveMutation = useMutation({
    mutationFn: async (config: any) => {
      const response = await fetch(`${API_URL}/mitre/config`, {
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
      queryClient.invalidateQueries({ queryKey: ['mitre-config'] });
      onSaveSuccess?.();
    },
  });

  const config = configQuery.data || {
    autoUpdate: {
      enabled: false,
      updateFrequencyDays: 30,
      lastUpdate: null,
      nextUpdate: null
    }
  };

  const statistics = statisticsQuery.data || {
    totalTechniques: 0,
    lastUpdated: null
  };

  const [autoUpdateEnabled, setAutoUpdateEnabled] = useState(config.autoUpdate?.enabled || false);
  const [updateFrequency, setUpdateFrequency] = useState(config.autoUpdate?.updateFrequencyDays || 30);

  useEffect(() => {
    setAutoUpdateEnabled(config.autoUpdate?.enabled || false);
    setUpdateFrequency(config.autoUpdate?.updateFrequencyDays || 30);
  }, [config]);

  const handleSave = () => {
    const updatedConfig = {
      autoUpdate: {
        enabled: autoUpdateEnabled,
        updateFrequencyDays: updateFrequency,
        lastUpdate: config.autoUpdate?.lastUpdate,
        nextUpdate: config.autoUpdate?.nextUpdate
      }
    };
    saveMutation.mutate(updatedConfig);
  };

  // Check for ongoing imports globally
  const ongoingImports = useMutationState({
    filters: { mutationKey: ['mitre-import'], status: 'pending' },
  });

  const isImporting = ongoingImports.length > 0;

  // Import mutation with persistent state
  const importMutation = useMutation({
    mutationKey: ['mitre-import'],
    mutationFn: async () => {
      const response = await fetch(`${API_URL}/mitre/import`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) throw new Error('Failed to import MITRE techniques');
      return response.json();
    },
    onSuccess: () => {
      // Refresh statistics after successful import
      queryClient.invalidateQueries({ queryKey: ['mitre-statistics'] });
      queryClient.invalidateQueries({ queryKey: ['mitre-config'] });
    },
  });

  const handleImport = () => {
    importMutation.mutate();
  };

  // Get the most recent mutation state (success or error)
  const completedImports = useMutationState({
    filters: { mutationKey: ['mitre-import'] },
    select: (mutation) => ({
      status: mutation.state.status,
      data: mutation.state.data,
      error: mutation.state.error,
    }),
  });

  const latestImport = completedImports[completedImports.length - 1];

  if (configQuery.isLoading || statisticsQuery.isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Statistics Card */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Database Statistics</h2>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div>
            <div className="text-3xl font-bold text-blue-600 dark:text-blue-400">
              {statistics.totalTechniques || 0}
            </div>
            <div className="text-sm text-gray-600 dark:text-gray-300">Total Techniques</div>
          </div>

          <div>
            <div className="text-sm text-gray-600 dark:text-gray-300">Last Updated</div>
            <div className="text-sm font-medium text-gray-900 dark:text-white">
              {statistics.lastUpdated ? new Date(statistics.lastUpdated).toLocaleString() : 'Never'}
            </div>
          </div>

          <div>
            <div className="text-sm text-gray-600 dark:text-gray-300">Next Update</div>
            <div className="text-sm font-medium text-gray-900 dark:text-white">
              {config.autoUpdate?.nextUpdate ? new Date(config.autoUpdate.nextUpdate).toLocaleString() : 'Not scheduled'}
            </div>
          </div>
        </div>

        {statistics.totalTechniques === 0 && (
          <div className="mt-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-4">
            <div className="flex items-start">
              <Info className="h-5 w-5 text-blue-600 mr-2 mt-0.5" />
              <p className="text-sm text-blue-800 dark:text-blue-300">
                The MITRE database appears to be empty. Click "Import MITRE Techniques" to download the latest techniques from the official source.
              </p>
            </div>
          </div>
        )}
      </div>

      {/* Auto Update Settings */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Auto Update Settings</h2>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={autoUpdateEnabled}
              onChange={(e) => setAutoUpdateEnabled(e.target.checked)}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-700 dark:text-gray-300">Enabled</span>
          </label>
        </div>

        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Update Frequency (days)
            </label>
            <input
              type="number"
              min="1"
              max="365"
              value={updateFrequency}
              onChange={(e) => setUpdateFrequency(parseInt(e.target.value) || 30)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
            />
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
              Automatically check for and import new MITRE ATT&CK techniques every N days
            </p>
          </div>
        </div>
      </div>

      {/* Import Section */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Manual Import</h2>

        <div className="space-y-4">
          <p className="text-sm text-gray-600 dark:text-gray-400">
            Import MITRE ATT&CK techniques from the official MITRE repository. This will download and process all current techniques and sub-techniques.
          </p>

          <button
            onClick={handleImport}
            disabled={isImporting}
            className="flex items-center gap-2 px-6 py-3 bg-green-600 text-white hover:bg-green-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors font-medium"
          >
            {isImporting ? (
              <>
                <RefreshCw className="h-5 w-5 animate-spin" />
                Importing...
              </>
            ) : (
              <>
                <Download className="h-5 w-5" />
                Import MITRE Techniques
              </>
            )}
          </button>

          {latestImport && latestImport.status === 'success' && latestImport.data ? (
            <div className="bg-green-50 border border-green-200 rounded-lg p-4">
              <div className="flex items-center">
                <Check className="h-5 w-5 text-green-600 mr-2" />
                <h3 className="text-lg font-medium text-green-800">Import Successful!</h3>
              </div>
              <div className="mt-2 text-sm text-gray-700">
                <p>• {(latestImport.data as any)?.result?.techniquesImported || 0} new techniques imported</p>
                <p>• {(latestImport.data as any)?.result?.techniquesUpdated || 0} existing techniques updated</p>
                {(latestImport.data as any)?.result?.errors?.length > 0 && (
                  <p className="text-yellow-700">
                    • {(latestImport.data as any).result.errors.length} techniques had errors
                  </p>
                )}
              </div>
            </div>
          ) : null}

          {latestImport && latestImport.status === 'error' && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <div className="flex items-center">
                <AlertCircle className="h-5 w-5 text-red-600 mr-2" />
                <h3 className="text-lg font-medium text-red-800">Import Failed</h3>
              </div>
              <p className="mt-2 text-sm text-red-700">
                {(latestImport.error as Error)?.message || 'Failed to import techniques'}
              </p>
            </div>
          )}
        </div>
      </div>

      {/* Quick Links */}
      <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Quick Links</h2>

        <div className="flex gap-3">
          <button
            onClick={() => navigate('/mitre-attack')}
            className="flex items-center gap-2 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-lg transition-colors"
          >
            <Target className="h-4 w-4" />
            Browse Techniques
          </button>

          <button
            onClick={() => window.open('https://attack.mitre.org/', '_blank')}
            className="flex items-center gap-2 px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-lg transition-colors"
          >
            <Info className="h-4 w-4" />
            MITRE ATT&CK Framework
          </button>
        </div>
      </div>

      {/* Save Button */}
      <div className="flex justify-end">
        <button
          onClick={handleSave}
          disabled={saveMutation.isPending}
          className="flex items-center gap-2 px-6 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Save className="h-4 w-4" />
          {saveMutation.isPending ? 'Saving...' : 'Save Configuration'}
        </button>
      </div>
    </div>
  );
}

