import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Search,
  Filter,
  Upload,
  Download,
  CheckCircle,
  XCircle,
  AlertCircle,
  Loader2,
  ChevronRight,
  X,
  Power,
  PowerOff,
  RefreshCw,
  Trash2,
  Edit,
  Eye,
  FileText,
  BarChart3,
  Shield
} from 'lucide-react';
import { Api } from '../services/api';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';

// Types
interface YaraRule {
  id: number;
  name: string;
  category: string;
  threatLevel: string;
  author?: string;
  description?: string;
  ruleContent: string;
  isValid: boolean;
  isEnabled: boolean;
  validationError?: string;
  priority?: number;
  tags?: string[];
  hitCount?: number;
  lastMatch?: string;
  createdAt: string;
  updatedAt: string;
}

interface YaraStatistics {
  totalRules: number;
  enabledRules: number;
  disabledRules: number;
  validRules: number;
  invalidRules: number;
  rulesByCategory?: Record<string, number>;
  rulesByThreatLevel?: Record<string, number>;
  topPerformingRules?: any[];
  slowestRules?: any[];
}

// Constants
const YARA_CATEGORIES = [
  { id: 'Malware', name: 'Malware' },
  { id: 'Ransomware', name: 'Ransomware' },
  { id: 'Trojan', name: 'Trojan' },
  { id: 'Backdoor', name: 'Backdoor' },
  { id: 'Suspicious', name: 'Suspicious' },
  { id: 'PUA', name: 'Potentially Unwanted Application' },
  { id: 'Exploit', name: 'Exploit' },
  { id: 'Custom', name: 'Custom' },
];

const THREAT_LEVELS = [
  { id: 'Low', name: 'Low' },
  { id: 'Medium', name: 'Medium' },
  { id: 'High', name: 'High' },
  { id: 'Critical', name: 'Critical' },
];

// Helper functions
const getThreatLevelColor = (level: string) => {
  switch (level?.toLowerCase()) {
    case 'critical': return 'bg-red-100 text-red-800 border-red-200';
    case 'high': return 'bg-orange-100 text-orange-800 border-orange-200';
    case 'medium': return 'bg-yellow-100 text-yellow-800 border-yellow-200';
    case 'low': return 'bg-green-100 text-green-800 border-green-200';
    default: return 'bg-gray-100 text-gray-800 border-gray-200';
  }
};

// API functions
const fetchYaraRules = async (params: {
  page?: number;
  perPage?: number;
  search?: string;
  category?: string;
  threatLevel?: string;
  isValid?: boolean;
  isEnabled?: boolean;
}) => {
  return Api.getYaraRules(params);
};

const fetchYaraStatistics = async () => {
  return Api.getYaraStatistics();
};

const toggleRuleEnabled = async (id: number, enabled: boolean) => {
  return Api.toggleYaraRule(id, enabled);
};

const deleteRule = async (id: number) => {
  return Api.deleteYaraRule(id);
};

const importRules = async (data: {
  ruleContent: string;
  category: string;
  author: string;
  skipDuplicates: boolean;
  enableByDefault: boolean;
}) => {
  return Api.importYaraRules(data);
};

// Import Dialog Component
const ImportDialog: React.FC<{
  isOpen: boolean;
  onClose: () => void;
  importMutation: any;
}> = ({ isOpen, onClose, importMutation }) => {
  const [ruleContent, setRuleContent] = useState('');
  const [category, setCategory] = useState('Custom');
  const [author, setAuthor] = useState('Imported');
  const [skipDuplicates, setSkipDuplicates] = useState(true);
  const [enableByDefault, setEnableByDefault] = useState(false);
  const [result, setResult] = useState<any>(null);

  const handleFileUpload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e) => {
        setRuleContent(e.target?.result as string);
      };
      reader.readAsText(file);
    }
  };

  const handleImport = async () => {
    if (!ruleContent.trim()) {
      alert('Please enter or upload rule content');
      return;
    }

    setResult(null);
    try {
      const response = await importMutation.mutateAsync({
        ruleContent,
        category,
        author,
        skipDuplicates,
        enableByDefault
      });
      setResult(response.data || response);
    } catch (error: any) {
      setResult({
        success: false,
        message: error.message,
        details: error.toString()
      });
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
            Import YARA Rules
          </h2>
        </div>
        
        <div className="p-6 space-y-4">
          {importMutation.isPending ? (
            <div className="text-center py-8">
              <Loader2 className="h-8 w-8 animate-spin mx-auto mb-4 text-blue-600" />
              <p className="text-gray-600 dark:text-gray-300">Importing YARA rules...</p>
            </div>
          ) : result ? (
            <div className="space-y-4">
              {result.success !== false ? (
                <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                  <div className="flex items-center">
                    <CheckCircle className="h-5 w-5 text-green-600 mr-2" />
                    <h3 className="text-lg font-medium text-green-800">Import Successful!</h3>
                  </div>
                  <div className="mt-2 text-sm text-green-700">
                    <p>• {result.importedCount || 0} rules imported</p>
                    <p>• {result.skippedCount || 0} rules skipped</p>
                    <p>• {result.failedCount || 0} rules failed</p>
                  </div>
                </div>
              ) : (
                <div className="bg-red-50 border border-red-200 rounded-lg p-4">
                  <div className="flex items-center">
                    <AlertCircle className="h-5 w-5 text-red-600 mr-2" />
                    <h3 className="text-lg font-medium text-red-800">Import Failed</h3>
                  </div>
                  <p className="mt-2 text-sm text-red-700">{result.message}</p>
                </div>
              )}
            </div>
          ) : (
            <>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                    Default Category
                  </label>
                  <select
                    value={category}
                    onChange={(e) => setCategory(e.target.value)}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  >
                    {YARA_CATEGORIES.map(cat => (
                      <option key={cat.id} value={cat.id}>{cat.name}</option>
                    ))}
                  </select>
                </div>
                
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                    Default Author
                  </label>
                  <input
                    type="text"
                    value={author}
                    onChange={(e) => setAuthor(e.target.value)}
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                  />
                </div>
              </div>
              
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <label className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    checked={skipDuplicates}
                    onChange={(e) => setSkipDuplicates(e.target.checked)}
                    className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                  />
                  <span className="text-sm text-gray-700 dark:text-gray-300">Skip Duplicates</span>
                </label>
                
                <label className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    checked={enableByDefault}
                    onChange={(e) => setEnableByDefault(e.target.checked)}
                    className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                  />
                  <span className="text-sm text-gray-700 dark:text-gray-300">Enable Rules by Default</span>
                </label>
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Upload YARA File
                </label>
                <input
                  type="file"
                  accept=".yar,.yara,.txt"
                  onChange={handleFileUpload}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
                />
              </div>
              
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  YARA Rules Content
                </label>
                <textarea
                  value={ruleContent}
                  onChange={(e) => setRuleContent(e.target.value)}
                  rows={10}
                  placeholder="Paste YARA rules here or upload a file..."
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white font-mono text-sm"
                />
              </div>
            </>
          )}
        </div>
        
        <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-end space-x-3">
          <button
            onClick={onClose}
            disabled={importMutation.isPending}
            className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg disabled:opacity-50"
          >
            {result ? 'Close' : 'Cancel'}
          </button>
          {!result && (
            <button
              onClick={handleImport}
              disabled={importMutation.isPending}
              className="px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 flex items-center"
            >
              <Upload className="h-4 w-4 mr-2" />
              Import
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

// Rule Detail Modal
const RuleDetailModal: React.FC<{
  rule: YaraRule | null;
  isOpen: boolean;
  onClose: () => void;
  onToggle: (id: number, enabled: boolean) => void;
  onDelete: (id: number) => void;
}> = ({ rule, isOpen, onClose, onToggle, onDelete }) => {
  if (!isOpen || !rule) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-xl font-semibold text-gray-900 dark:text-white">{rule.name}</h2>
              <div className="flex items-center space-x-2 mt-2">
                <span className={`px-2 py-1 rounded-full text-xs font-medium border ${getThreatLevelColor(rule.threatLevel)}`}>
                  {rule.threatLevel}
                </span>
                <span className="px-2 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 text-xs rounded">
                  {rule.category}
                </span>
                {rule.isValid ? (
                  <span className="flex items-center text-xs text-green-600">
                    <CheckCircle className="h-3 w-3 mr-1" />
                    Valid
                  </span>
                ) : (
                  <span className="flex items-center text-xs text-red-600">
                    <XCircle className="h-3 w-3 mr-1" />
                    Invalid
                  </span>
                )}
                {rule.isEnabled ? (
                  <span className="flex items-center text-xs text-blue-600">
                    <Power className="h-3 w-3 mr-1" />
                    Enabled
                  </span>
                ) : (
                  <span className="flex items-center text-xs text-gray-500">
                    <PowerOff className="h-3 w-3 mr-1" />
                    Disabled
                  </span>
                )}
              </div>
            </div>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            >
              <X className="h-6 w-6" />
            </button>
          </div>
        </div>
        
        <div className="p-6 space-y-6">
          {/* Metadata */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Author</label>
              <p className="mt-1 text-gray-900 dark:text-white">{rule.author || 'Unknown'}</p>
            </div>
            
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Priority</label>
              <p className="mt-1 text-gray-900 dark:text-white">{rule.priority || 0}</p>
            </div>
            
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Hit Count</label>
              <p className="mt-1 text-gray-900 dark:text-white">{rule.hitCount || 0}</p>
            </div>
            
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Last Match</label>
              <p className="mt-1 text-gray-900 dark:text-white">
                {rule.lastMatch ? new Date(rule.lastMatch).toLocaleString() : 'Never'}
              </p>
            </div>
          </div>
          
          {/* Tags */}
          {rule.tags && rule.tags.length > 0 && (
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Tags</label>
              <div className="mt-2 flex flex-wrap gap-2">
                {rule.tags.map((tag, index) => (
                  <span
                    key={index}
                    className="px-2 py-1 bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 text-sm rounded border border-blue-200 dark:border-blue-800"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </div>
          )}
          
          {/* Description */}
          {rule.description && (
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Description</label>
              <p className="mt-1 text-gray-700 dark:text-gray-300">{rule.description}</p>
            </div>
          )}
          
          {/* Rule Content */}
          <div>
            <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Rule Content</label>
            <pre className="mt-2 p-4 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg overflow-x-auto text-sm font-mono text-gray-900 dark:text-gray-100">
              {rule.ruleContent}
            </pre>
          </div>
          
          {/* Validation Error */}
          {!rule.isValid && rule.validationError && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4">
              <div className="flex items-start">
                <AlertCircle className="h-5 w-5 text-red-600 mr-2 mt-0.5" />
                <div>
                  <h4 className="text-sm font-medium text-red-800">Validation Error</h4>
                  <p className="text-sm text-red-700 mt-1">{rule.validationError}</p>
                </div>
              </div>
            </div>
          )}
          
          {/* Timestamps */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-4 border-t border-gray-200 dark:border-gray-700">
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Created</label>
              <p className="mt-1 text-sm text-gray-900 dark:text-white">
                {new Date(rule.createdAt).toLocaleString()}
              </p>
            </div>
            
            <div>
              <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Updated</label>
              <p className="mt-1 text-sm text-gray-900 dark:text-white">
                {new Date(rule.updatedAt).toLocaleString()}
              </p>
            </div>
          </div>
        </div>
        
        <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-between">
          <div className="flex space-x-2">
            <button
              onClick={() => onToggle(rule.id, !rule.isEnabled)}
              className={`px-4 py-2 rounded-lg flex items-center ${
                rule.isEnabled 
                  ? 'bg-gray-100 text-gray-700 hover:bg-gray-200' 
                  : 'bg-blue-600 text-white hover:bg-blue-700'
              }`}
            >
              {rule.isEnabled ? (
                <><PowerOff className="h-4 w-4 mr-2" />Disable</>
              ) : (
                <><Power className="h-4 w-4 mr-2" />Enable</>
              )}
            </button>
            
            <button
              onClick={() => {
                if (confirm('Are you sure you want to delete this rule?')) {
                  onDelete(rule.id);
                  onClose();
                }
              }}
              className="px-4 py-2 bg-red-600 text-white hover:bg-red-700 rounded-lg flex items-center"
            >
              <Trash2 className="h-4 w-4 mr-2" />
              Delete
            </button>
          </div>
          
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};

// Rule Card Component
const RuleCard: React.FC<{
  rule: YaraRule;
  onClick: () => void;
  onToggle: (id: number, enabled: boolean) => void;
}> = ({ rule, onClick, onToggle }) => {
  return (
    <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg p-4 hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between mb-3">
        <div className="flex-1 cursor-pointer" onClick={onClick}>
          <div className="flex items-center space-x-2 mb-2">
            <h3 className="font-semibold text-gray-900 dark:text-white">{rule.name}</h3>
            {rule.isValid ? (
              <CheckCircle className="h-4 w-4 text-green-500" />
            ) : (
              <XCircle className="h-4 w-4 text-red-500" />
            )}
          </div>
          
          <div className="flex items-center space-x-2 mb-2">
            <span className={`px-2 py-1 rounded-full text-xs font-medium border ${getThreatLevelColor(rule.threatLevel)}`}>
              {rule.threatLevel}
            </span>
            <span className="px-2 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 text-xs rounded">
              {rule.category}
            </span>
          </div>
          
          {rule.description && (
            <p className="text-sm text-gray-600 dark:text-gray-300 line-clamp-2 mb-2">
              {rule.description}
            </p>
          )}
          
          <div className="flex items-center justify-between text-xs text-gray-500 dark:text-gray-400">
            <span>Hits: {rule.hitCount || 0}</span>
            <span>Updated: {new Date(rule.updatedAt).toLocaleDateString()}</span>
          </div>
        </div>
        
        <button
          onClick={(e) => {
            e.stopPropagation();
            onToggle(rule.id, !rule.isEnabled);
          }}
          className={`ml-4 p-2 rounded-lg ${
            rule.isEnabled 
              ? 'bg-blue-50 text-blue-600 hover:bg-blue-100' 
              : 'bg-gray-100 text-gray-500 hover:bg-gray-200'
          }`}
        >
          {rule.isEnabled ? <Power className="h-4 w-4" /> : <PowerOff className="h-4 w-4" />}
        </button>
      </div>
    </div>
  );
};

// Main Component
export const YaraRulesPage: React.FC = () => {
  const { token, loading: authLoading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [importDialogOpen, setImportDialogOpen] = useState(false);
  const [selectedRule, setSelectedRule] = useState<YaraRule | null>(null);
  const [detailModalOpen, setDetailModalOpen] = useState(false);

  // Filters
  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [threatLevelFilter, setThreatLevelFilter] = useState('');
  const [validFilter, setValidFilter] = useState<boolean | undefined>(undefined);
  const [enabledFilter, setEnabledFilter] = useState<boolean | undefined>(undefined);
  const [currentPage, setCurrentPage] = useState(1);
  const perPage = 25;

  useEffect(() => {
    if (!authLoading && !token) {
      navigate('/login');
    }
  }, [token, authLoading, navigate]);

  // Reset to page 1 when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [search, categoryFilter, threatLevelFilter, validFilter, enabledFilter]);

  // Query for rules with React Query caching
  const rulesQuery = useQuery({
    queryKey: ['yara-rules', currentPage, search, categoryFilter, threatLevelFilter, validFilter, enabledFilter],
    queryFn: async () => {
      const response = await fetchYaraRules({
        page: currentPage,
        perPage: perPage,
        search: search || undefined,
        category: categoryFilter || undefined,
        threatLevel: threatLevelFilter || undefined,
        isValid: validFilter,
        isEnabled: enabledFilter
      });

      const rulesData = response.data || response.rules || [];
      const total = response.total || response.totalCount || rulesData.length;
      // Use the actual page size from the backend, or the number of rules returned
      const actualPageSize = response.pageSize || response.perPage || rulesData.length || perPage;

      return {
        rules: rulesData,
        totalCount: total,
        totalPages: Math.ceil(total / actualPageSize),
        pageSize: actualPageSize
      };
    },
    enabled: !authLoading && !!token,
    staleTime: 5 * 60 * 1000, // 5 minutes
    gcTime: 30 * 60 * 1000, // 30 minutes
  });

  // Query for statistics with React Query caching
  const statisticsQuery = useQuery({
    queryKey: ['yara-statistics'],
    queryFn: async () => {
      const response = await fetchYaraStatistics();
      console.log('[YaraRules] Statistics response:', response);

      // Handle the response format: { data: { TotalRules, EnabledRules, ... } }
      const statsData = response.data || response;

      // Map backend property names (PascalCase) to frontend (camelCase)
      const mappedStats = {
        totalRules: statsData.totalRules || statsData.TotalRules || 0,
        enabledRules: statsData.enabledRules || statsData.EnabledRules || 0,
        disabledRules: statsData.disabledRules || statsData.DisabledRules || 0,
        validRules: statsData.validRules || statsData.ValidRules || 0,
        invalidRules: statsData.invalidRules || statsData.InvalidRules || 0,
        rulesByCategory: statsData.rulesByCategory || statsData.RulesByCategory || {},
        rulesByThreatLevel: statsData.rulesByThreatLevel || statsData.RulesByThreatLevel || {},
        topPerformingRules: statsData.topPerformingRules || statsData.TopPerformingRules || [],
        slowestRules: statsData.slowestRules || statsData.SlowestRules || []
      };

      console.log('[YaraRules] Mapped statistics:', mappedStats);
      return mappedStats;
    },
    enabled: !authLoading && !!token,
    staleTime: 10 * 60 * 1000, // 10 minutes
    gcTime: 30 * 60 * 1000, // 30 minutes
  });

  // Toggle mutation
  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: number; enabled: boolean }) => toggleRuleEnabled(id, enabled),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['yara-rules'] });
      queryClient.invalidateQueries({ queryKey: ['yara-statistics'] });
    },
    onError: (error: any) => {
      alert(`Failed to toggle rule: ${error.message}`);
    },
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: deleteRule,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['yara-rules'] });
      queryClient.invalidateQueries({ queryKey: ['yara-statistics'] });
    },
    onError: (error: any) => {
      alert(`Failed to delete rule: ${error.message}`);
    },
  });

  // Import mutation
  const importMutation = useMutation({
    mutationFn: importRules,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['yara-rules'] });
      queryClient.invalidateQueries({ queryKey: ['yara-statistics'] });
    },
  });

  const handleToggle = (id: number, enabled: boolean) => {
    toggleMutation.mutate({ id, enabled });
  };

  const handleDelete = (id: number) => {
    deleteMutation.mutate(id);
  };

  const rules = rulesQuery.data?.rules || [];
  const totalCount = rulesQuery.data?.totalCount || 0;
  const totalPages = rulesQuery.data?.totalPages || 1;
  const actualPageSize = rulesQuery.data?.pageSize || perPage;
  const statistics = statisticsQuery.data || null;
  const loading = rulesQuery.isLoading;
  const error = rulesQuery.error ? (rulesQuery.error as Error).message : null;

  const handleRuleClick = (rule: YaraRule) => {
    setSelectedRule(rule);
    setDetailModalOpen(true);
  };

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900 dark:text-white">YARA Rules</h1>
        <p className="text-gray-600 dark:text-gray-400 mt-1">Malware Detection Signatures</p>
      </div>

      {/* Statistics Card */}
      {statistics && (
        <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg p-6">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Statistics</h2>
          
          <div className="grid grid-cols-2 md:grid-cols-5 gap-6">
            <div>
              <div className="text-2xl font-bold text-blue-600 dark:text-blue-400">
                {statistics.totalRules || 0}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">Total Rules</div>
            </div>
            
            <div>
              <div className="text-2xl font-bold text-green-600 dark:text-green-400">
                {statistics.enabledRules || 0}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">Enabled</div>
            </div>
            
            <div>
              <div className="text-2xl font-bold text-red-600 dark:text-red-400">
                {statistics.disabledRules || 0}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">Disabled</div>
            </div>
            
            <div>
              <div className="text-2xl font-bold text-purple-600 dark:text-purple-400">
                {statistics.validRules || 0}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">Valid</div>
            </div>
            
            <div>
              <div className="text-2xl font-bold text-orange-600 dark:text-orange-400">
                {statistics.invalidRules || 0}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">Invalid</div>
            </div>
          </div>
        </div>
      )}

      {/* Filters and Actions */}
      <div className="flex flex-col lg:flex-row gap-4 items-start lg:items-center justify-between">
        <div className="flex flex-col sm:flex-row gap-4 flex-1">
          {/* Search */}
          <div className="relative flex-1 max-w-md">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search rules..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white placeholder-gray-500 dark:placeholder-gray-400 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>
          
          {/* Filters */}
          <div className="flex gap-2">
            <select
              value={categoryFilter}
              onChange={(e) => setCategoryFilter(e.target.value)}
              className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
            >
              <option value="">All Categories</option>
              {YARA_CATEGORIES.map(cat => (
                <option key={cat.id} value={cat.id}>{cat.name}</option>
              ))}
            </select>
            
            <select
              value={threatLevelFilter}
              onChange={(e) => setThreatLevelFilter(e.target.value)}
              className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
            >
              <option value="">All Threat Levels</option>
              {THREAT_LEVELS.map(level => (
                <option key={level.id} value={level.id}>{level.name}</option>
              ))}
            </select>
            
            <select
              value={enabledFilter === undefined ? '' : enabledFilter ? 'true' : 'false'}
              onChange={(e) => setEnabledFilter(e.target.value === '' ? undefined : e.target.value === 'true')}
              className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
            >
              <option value="">All Status</option>
              <option value="true">Enabled</option>
              <option value="false">Disabled</option>
            </select>
          </div>
        </div>
        
        {/* Action Buttons */}
        <div className="flex gap-2">
          <button
            onClick={() => setImportDialogOpen(true)}
            className="px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg flex items-center space-x-2 transition-colors"
          >
            <Upload className="h-4 w-4" />
            <span>Import</span>
          </button>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4">
          <div className="flex items-center">
            <AlertCircle className="h-5 w-5 text-red-600 mr-2" />
            <div>
              <h3 className="text-sm font-medium text-red-800">Error loading rules</h3>
              <p className="text-sm text-red-700 mt-1">{error}</p>
            </div>
          </div>
        </div>
      )}

      {/* Rules Grid */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
          <span className="ml-2 text-gray-600 dark:text-gray-300">Loading rules...</span>
        </div>
      ) : rules.length === 0 ? (
        <div className="text-center py-12">
          <Shield className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">No rules found</h3>
          <p className="text-gray-600 dark:text-gray-300 mb-4">
            {search || categoryFilter || threatLevelFilter 
              ? 'Try adjusting your search or filters'
              : 'No YARA rules are available. Consider importing them.'
            }
          </p>
          {!search && !categoryFilter && !threatLevelFilter && (
            <button
              onClick={() => setImportDialogOpen(true)}
              className="px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg flex items-center space-x-2 mx-auto"
            >
              <Upload className="h-4 w-4" />
              <span>Import Rules</span>
            </button>
          )}
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {rules.map((rule) => (
              <RuleCard
                key={rule.id}
                rule={rule}
                onClick={() => handleRuleClick(rule)}
                onToggle={handleToggle}
              />
            ))}
          </div>
          
          {/* Pagination */}
          {totalCount > 0 && (
            <div className="flex items-center justify-between">
              <div className="text-sm text-gray-600 dark:text-gray-300">
                Showing {rules.length > 0 ? ((currentPage - 1) * actualPageSize) + 1 : 0} to {Math.min(((currentPage - 1) * actualPageSize) + rules.length, totalCount)} of {totalCount} rules
              </div>

              {totalPages > 1 && (
                <div className="flex space-x-2">
                  <button
                    onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
                    disabled={currentPage === 1}
                    className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    Previous
                  </button>

                  <span className="px-3 py-1 text-gray-700 dark:text-gray-300">
                    Page {currentPage} of {totalPages}
                  </span>

                  <button
                    onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
                    disabled={currentPage === totalPages}
                    className="px-3 py-1 border border-gray-300 dark:border-gray-600 rounded text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    Next
                  </button>
                </div>
              )}
            </div>
          )}
        </>
      )}

      {/* Modals */}
      <ImportDialog
        isOpen={importDialogOpen}
        onClose={() => setImportDialogOpen(false)}
        importMutation={importMutation}
      />
      
      <RuleDetailModal
        rule={selectedRule}
        isOpen={detailModalOpen}
        onClose={() => setDetailModalOpen(false)}
        onToggle={handleToggle}
        onDelete={handleDelete}
      />
    </div>
  );
};

