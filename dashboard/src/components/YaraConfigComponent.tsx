import { useState, useEffect } from 'react';
import { 
  Save, 
  RefreshCw, 
  Download, 
  Plus, 
  Trash2 
} from 'lucide-react';
import { useAuth } from '../hooks/useAuth';

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

export function YaraConfigComponent({
  config,
  onSave,
  isSaving
}: {
  config: YaraConfig | undefined;
  onSave: (config: YaraConfig) => void;
  isSaving: boolean;
}) {
  const defaultConfig: YaraConfig = {
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
        'https://raw.githubusercontent.com/Yara-Rules/rules/master/malware/MALW_Zeus.yar'
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

  const [formData, setFormData] = useState<YaraConfig>(defaultConfig);
  const [importing, setImporting] = useState(false);
  const { token } = useAuth();

  useEffect(() => {
    if (config && config.autoUpdate && config.sources && config.rules && config.import) {
      setFormData({
        autoUpdate: { ...defaultConfig.autoUpdate, ...config.autoUpdate },
        sources: { ...defaultConfig.sources, ...config.sources },
        rules: { ...defaultConfig.rules, ...config.rules },
        import: { ...defaultConfig.import, ...config.import }
      });
    } else {
      setFormData(defaultConfig);
    }
  }, [config]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave(formData);
  };

  const handleImportNow = async () => {
    setImporting(true);
    try {
      const response = await fetch('/api/yara-configuration/import', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      
      if (response.ok) {
        const result = await response.json();
        const resultData = result.data || result;
        setFormData(prev => ({
          ...prev,
          import: {
            lastImportDate: new Date().toISOString(),
            totalRules: resultData.totalRules || resultData.importedCount || prev.import.totalRules,
            enabledRules: resultData.enabledRules || resultData.importedCount || prev.import.enabledRules,
            failedRules: resultData.failedRules || resultData.failedCount || 0
          }
        }));
      }
    } catch (error) {
      console.error('Error importing YARA rules:', error);
    } finally {
      setImporting(false);
    }
  };

  const addSource = () => {
    setFormData({
      ...formData,
      sources: {
        ...formData.sources,
        urls: [...formData.sources.urls, '']
      }
    });
  };

  const removeSource = (index: number) => {
    setFormData({
      ...formData,
      sources: {
        ...formData.sources,
        urls: formData.sources.urls.filter((_, i) => i !== index)
      }
    });
  };

  const updateSource = (index: number, value: string) => {
    const newUrls = [...formData.sources.urls];
    newUrls[index] = value;
    setFormData({
      ...formData,
      sources: {
        ...formData.sources,
        urls: newUrls
      }
    });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left Column */}
        <div className="space-y-6">
          {/* Auto Update Settings */}
          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <div className="flex items-center gap-2 mb-4">
              <RefreshCw className="h-5 w-5 text-blue-600" />
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Auto Update Settings</h2>
            </div>

            <div className="space-y-4">
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={formData.autoUpdate.enabled}
                  onChange={(e) => setFormData({
                    ...formData,
                    autoUpdate: { ...formData.autoUpdate, enabled: e.target.checked }
                  })}
                  className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">Enable automatic rule updates</span>
              </label>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Update Frequency (days)
                </label>
                <input
                  type="number"
                  min="1"
                  max="365"
                  value={formData.autoUpdate.updateFrequencyDays}
                  onChange={(e) => setFormData({
                    ...formData,
                    autoUpdate: { ...formData.autoUpdate, updateFrequencyDays: parseInt(e.target.value) || 7 }
                  })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                />
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  How often to check for new YARA rules (1-365 days)
                </p>
              </div>

              {formData.autoUpdate.lastUpdate && (
                <div className="bg-blue-50 border border-blue-200 dark:bg-blue-900/20 dark:border-blue-800 rounded-lg p-3">
                  <p className="text-sm text-blue-800 dark:text-blue-200">
                    <strong>Last Update:</strong> {new Date(formData.autoUpdate.lastUpdate).toLocaleString()}
                  </p>
                </div>
              )}
            </div>
          </div>

          {/* Rule Sources */}
          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <div className="flex items-center gap-2 mb-4">
              <Download className="h-5 w-5 text-blue-600" />
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Rule Sources</h2>
            </div>

            <div className="space-y-4">
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={formData.sources.enabled}
                  onChange={(e) => setFormData({
                    ...formData,
                    sources: { ...formData.sources, enabled: e.target.checked }
                  })}
                  className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">Enable rule source downloads</span>
              </label>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Max Rules Per Source
                </label>
                <input
                  type="number"
                  min="1"
                  max="1000"
                  value={formData.sources.maxRulesPerSource}
                  onChange={(e) => setFormData({
                    ...formData,
                    sources: { ...formData.sources, maxRulesPerSource: parseInt(e.target.value) || 50 }
                  })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                />
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  Maximum rules to import from each source
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Active Sources: {formData.sources.urls.length} configured
                </label>
                <div className="space-y-2 max-h-48 overflow-y-auto pr-2">
                  {formData.sources.urls.map((url, index) => (
                    <div key={index} className="flex gap-2">
                      <input
                        type="url"
                        placeholder={`Source ${index + 1}`}
                        value={url}
                        onChange={(e) => updateSource(index, e.target.value)}
                        className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white text-xs"
                      />
                      <button
                        type="button"
                        onClick={() => removeSource(index)}
                        disabled={formData.sources.urls.length <= 1}
                        className="px-3 py-2 text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>
                <button
                  type="button"
                  onClick={addSource}
                  className="mt-2 flex items-center gap-2 px-3 py-2 text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20 rounded-lg text-sm"
                >
                  <Plus className="h-4 w-4" />
                  Add Source
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Right Column */}
        <div className="space-y-6">
          {/* Import Statistics */}
          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Import Statistics</h2>

            <div className="space-y-4">
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-600 dark:text-gray-400">Total Rules:</span>
                <span className="px-3 py-1 bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200 rounded-full font-semibold">
                  {formData.import.totalRules}
                </span>
              </div>

              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-600 dark:text-gray-400">Enabled Rules:</span>
                <span className="px-3 py-1 bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200 rounded-full font-semibold">
                  {formData.import.enabledRules}
                </span>
              </div>

              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-600 dark:text-gray-400">Failed Rules:</span>
                <span className={`px-3 py-1 rounded-full font-semibold ${
                  formData.import.failedRules > 0 
                    ? 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200' 
                    : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400'
                }`}>
                  {formData.import.failedRules}
                </span>
              </div>

              {formData.import.lastImportDate && (
                <div className="pt-3 border-t border-gray-200 dark:border-gray-700">
                  <span className="text-xs text-gray-500 dark:text-gray-400">Last Import Date:</span>
                  <p className="text-sm font-medium text-gray-900 dark:text-white mt-1">
                    {new Date(formData.import.lastImportDate).toLocaleString()}
                  </p>
                </div>
              )}
            </div>

            {/* Import Now Button */}
            <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
              <button
                type="button"
                onClick={handleImportNow}
                disabled={importing}
                className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-green-600 text-white hover:bg-green-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed font-medium"
              >
                {importing ? (
                  <>
                    <RefreshCw className="h-5 w-5 animate-spin" />
                    Importing...
                  </>
                ) : (
                  <>
                    <Download className="h-5 w-5" />
                    Import YARA Rules
                  </>
                )}
              </button>
              <p className="text-xs text-gray-500 dark:text-gray-400 mt-2 text-center">
                Download and import YARA rules from all configured sources
              </p>
            </div>
          </div>

          {/* Rule Settings */}
          <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Rule Settings</h2>

            <div className="space-y-4">
              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={formData.rules.enabledByDefault}
                  onChange={(e) => setFormData({
                    ...formData,
                    rules: { ...formData.rules, enabledByDefault: e.target.checked }
                  })}
                  className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">Enable rules by default</span>
              </label>

              <label className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={formData.rules.autoValidation}
                  onChange={(e) => setFormData({
                    ...formData,
                    rules: { ...formData.rules, autoValidation: e.target.checked }
                  })}
                  className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">Auto-validate rules</span>
              </label>

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Performance Threshold (ms)
                </label>
                <input
                  type="number"
                  min="100"
                  max="10000"
                  value={formData.rules.performanceThresholdMs}
                  onChange={(e) => setFormData({
                    ...formData,
                    rules: { ...formData.rules, performanceThresholdMs: parseInt(e.target.value) || 1000 }
                  })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                />
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  Warn if rule execution exceeds this threshold
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Action Buttons */}
      <div className="flex justify-end">
        <button
          type="submit"
          disabled={isSaving}
          className="flex items-center gap-2 px-6 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Save className="h-4 w-4" />
          {isSaving ? 'Saving...' : 'Save YARA Configuration'}
        </button>
      </div>
    </form>
  );
}
