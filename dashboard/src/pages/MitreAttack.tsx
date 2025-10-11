import React, { useState, useEffect } from 'react';
import { 
  Shield, 
  Search, 
  Filter, 
  Download, 
  BarChart3, 
  Target, 
  CheckCircle, 
  AlertCircle, 
  Info,
  ChevronDown,
  ChevronRight,
  Loader2,
  ExternalLink,
  X
} from 'lucide-react';
import { Api } from '../services/api';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';

// Types
interface MitreTechnique {
  id: number;
  techniqueId: string;
  name: string;
  description: string;
  tactic: string;
  platform: string;
  dataSources?: string[];
  mitigations?: string[];
  examples?: string[];
  createdAt: string;
  associatedApplications?: Array<{
    ApplicationName: string;
    Confidence: number;
  }>;
}

interface MitreStatistics {
  totalTechniques: number;
  lastUpdated: string;
  techniquesByTactic: Record<string, number>;
  shouldImport: boolean;
}

// Tactic color mapping
const TACTIC_COLORS: Record<string, string> = {
  'reconnaissance': 'bg-blue-100 text-blue-800 border-blue-200',
  'resource-development': 'bg-purple-100 text-purple-800 border-purple-200',
  'initial-access': 'bg-yellow-100 text-yellow-800 border-yellow-200',
  'execution': 'bg-red-100 text-red-800 border-red-200',
  'persistence': 'bg-red-100 text-red-800 border-red-200',
  'privilege-escalation': 'bg-red-100 text-red-800 border-red-200',
  'defense-evasion': 'bg-yellow-100 text-yellow-800 border-yellow-200',
  'credential-access': 'bg-red-100 text-red-800 border-red-200',
  'discovery': 'bg-blue-100 text-blue-800 border-blue-200',
  'lateral-movement': 'bg-yellow-100 text-yellow-800 border-yellow-200',
  'collection': 'bg-blue-100 text-blue-800 border-blue-200',
  'command-and-control': 'bg-yellow-100 text-yellow-800 border-yellow-200',
  'exfiltration': 'bg-red-100 text-red-800 border-red-200',
  'impact': 'bg-red-100 text-red-800 border-red-200',
};

const getTacticColor = (tactic: string) => {
  const tacticKey = tactic?.toLowerCase().replace(/\s+/g, '-') || '';
  return TACTIC_COLORS[tacticKey] || 'bg-gray-100 text-gray-800 border-gray-200';
};

// API functions
const fetchMitreTechniques = async (params: {
  page?: number;
  perPage?: number;
  search?: string;
  tactic?: string;
  platform?: string;
  sort?: string;
  order?: string;
}) => {
  return Api.getMitreTechniques(params);
};

const fetchMitreStatistics = async () => {
  return Api.getMitreStatistics();
};

const importMitreTechniques = async () => {
  return Api.importMitreTechniques();
};

// Import Dialog Component
const ImportDialog: React.FC<{
  isOpen: boolean;
  onClose: () => void;
  onImport: () => Promise<void>;
}> = ({ isOpen, onClose, onImport }) => {
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState<any>(null);

  const handleImport = async () => {
    setImporting(true);
    setResult(null);
    try {
      const response = await importMitreTechniques();
      setResult(response);
    } catch (error: any) {
      setResult({
        success: false,
        message: error.message,
        details: error.toString()
      });
    } finally {
      setImporting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-2xl w-full mx-4 max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
            Import MITRE ATT&CK Techniques
          </h2>
        </div>
        
        <div className="p-6">
          {importing ? (
            <div className="text-center py-8">
              <Loader2 className="h-8 w-8 animate-spin mx-auto mb-4 text-blue-600" />
              <p className="text-gray-600 dark:text-gray-300">
                Importing MITRE ATT&CK techniques from official source...
              </p>
              <div className="w-full bg-gray-200 rounded-full h-2 mt-4">
                <div className="bg-blue-600 h-2 rounded-full animate-pulse" style={{ width: '60%' }}></div>
              </div>
              <p className="text-sm text-gray-500 mt-2">
                This may take a few minutes. Please don't close this dialog.
              </p>
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
                    <p>• {result.result?.techniquesImported || 0} new techniques imported</p>
                    <p>• {result.result?.techniquesUpdated || 0} existing techniques updated</p>
                    {result.result?.errors?.length > 0 && (
                      <p className="text-yellow-700">
                        • {result.result.errors.length} techniques had errors
                      </p>
                    )}
                  </div>
                </div>
              ) : (
                <div className="bg-red-50 border border-red-200 rounded-lg p-4">
                  <div className="flex items-center">
                    <AlertCircle className="h-5 w-5 text-red-600 mr-2" />
                    <h3 className="text-lg font-medium text-red-800">Import Failed</h3>
                  </div>
                  <p className="mt-2 text-sm text-red-700">{result.message}</p>
                  {result.details && (
                    <div className="mt-2 p-2 bg-red-100 rounded text-xs font-mono text-red-800">
                      Details: {result.details}
                    </div>
                  )}
                </div>
              )}
            </div>
          ) : (
            <div className="space-y-4">
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <div className="flex items-start">
                  <Info className="h-5 w-5 text-blue-600 mr-2 mt-0.5" />
                  <p className="text-sm text-blue-800">
                    This will download and import the latest MITRE ATT&CK techniques from the official MITRE repository.
                  </p>
                </div>
              </div>
              
              <div className="border border-gray-200 rounded-lg">
                <button className="w-full p-4 text-left flex items-center justify-between hover:bg-gray-50 dark:hover:bg-gray-700">
                  <span className="font-medium text-gray-900 dark:text-white">What will be imported?</span>
                  <ChevronDown className="h-4 w-4 text-gray-500" />
                </button>
                <div className="px-4 pb-4 text-sm text-gray-600 dark:text-gray-300 space-y-1">
                  <p>• All current MITRE ATT&CK techniques and sub-techniques</p>
                  <p>• Technique descriptions, tactics, and platforms</p>
                  <p>• Data sources, mitigations, and examples</p>
                  <p>• Updates to existing techniques in the database</p>
                </div>
              </div>
              
              <p className="text-sm text-gray-500">
                <strong>Note:</strong> This operation may take several minutes to complete and requires an active internet connection.
              </p>
            </div>
          )}
        </div>
        
        <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-end space-x-3">
          <button
            onClick={onClose}
            disabled={importing}
            className="px-4 py-2 text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg disabled:opacity-50"
          >
            {result ? 'Close' : 'Cancel'}
          </button>
          {!result && (
            <button
              onClick={handleImport}
              disabled={importing}
              className="px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg disabled:opacity-50 flex items-center"
            >
              <Download className="h-4 w-4 mr-2" />
              Start Import
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

// Technique Card Component
const TechniqueCard: React.FC<{ technique: MitreTechnique; onClick: () => void }> = ({ technique, onClick }) => {
  const platforms = technique.platform?.split(',').map(p => p.trim()) || [];
  
  return (
    <div 
      onClick={onClick}
      className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg p-4 hover:shadow-md transition-shadow cursor-pointer"
    >
      <div className="flex items-start justify-between mb-3">
        <div className="flex-1">
          <div className="flex items-center space-x-2 mb-2">
            <span className="text-sm font-mono text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/20 px-2 py-1 rounded">
              {technique.techniqueId}
            </span>
            <span className={`px-2 py-1 rounded-full text-xs font-medium border ${getTacticColor(technique.tactic)}`}>
              {technique.tactic}
            </span>
          </div>
          <h3 className="font-semibold text-gray-900 dark:text-white mb-2 line-clamp-2">
            {technique.name}
          </h3>
          <p className="text-sm text-gray-600 dark:text-gray-300 line-clamp-2">
            {technique.description}
          </p>
        </div>
      </div>
      
      <div className="flex items-center justify-between">
        <div className="flex flex-wrap gap-1">
          {platforms.slice(0, 3).map((platform, index) => (
            <span
              key={index}
              className="px-2 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 text-xs rounded"
            >
              {platform}
            </span>
          ))}
          {platforms.length > 3 && (
            <span className="px-2 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 text-xs rounded">
              +{platforms.length - 3} more
            </span>
          )}
        </div>
        <ChevronRight className="h-4 w-4 text-gray-400" />
      </div>
    </div>
  );
};

// Technique Detail Modal
const TechniqueDetailModal: React.FC<{
  technique: MitreTechnique | null;
  isOpen: boolean;
  onClose: () => void;
}> = ({ technique, isOpen, onClose }) => {
  if (!isOpen || !technique) return null;

  const platforms = technique.platform?.split(',').map(p => p.trim()) || [];
  const dataSources = technique.dataSources || [];
  const mitigations = technique.mitigations || [];
  const examples = technique.examples || [];

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <Shield className="h-6 w-6 text-blue-600" />
              <div>
                <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
                  {technique.techniqueId}
                </h2>
                <p className="text-gray-600 dark:text-gray-300">{technique.name}</p>
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
          {/* Overview */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-4">
              <div>
                <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Tactic</label>
                <div className="mt-1">
                  <span className={`px-3 py-1 rounded-full text-sm font-medium border ${getTacticColor(technique.tactic)}`}>
                    {technique.tactic}
                  </span>
                </div>
              </div>
              
              <div>
                <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Platforms</label>
                <div className="mt-1 flex flex-wrap gap-2">
                  {platforms.map((platform, index) => (
                    <span
                      key={index}
                      className="px-2 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 text-sm rounded"
                    >
                      {platform}
                    </span>
                  ))}
                </div>
              </div>
            </div>
            
            <div className="space-y-4">
              <div>
                <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Added to Database</label>
                <p className="mt-1 text-sm text-gray-900 dark:text-white">
                  {new Date(technique.createdAt).toLocaleString()}
                </p>
              </div>
            </div>
          </div>
          
          {/* Description */}
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Description</h3>
            <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
              <p className="text-gray-700 dark:text-gray-300 leading-relaxed whitespace-pre-wrap">
                {technique.description || 'No description available'}
              </p>
            </div>
          </div>
          
          {/* Technical Details */}
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Technical Details</h3>
            <div className="border-t border-gray-200 dark:border-gray-700 pt-4 space-y-6">
              {/* Data Sources */}
              <div>
                <h4 className="text-sm font-medium text-blue-600 dark:text-blue-400 mb-2">Data Sources</h4>
                {dataSources.length > 0 ? (
                  <div className="flex flex-wrap gap-2">
                    {dataSources.map((source, index) => (
                      <span
                        key={index}
                        className="px-2 py-1 bg-blue-50 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 text-sm rounded border border-blue-200 dark:border-blue-800"
                      >
                        {source}
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    {technique.dataSources || 'No data sources specified'}
                  </p>
                )}
              </div>
              
              {/* Mitigations */}
              <div>
                <h4 className="text-sm font-medium text-green-600 dark:text-green-400 mb-2">Mitigations</h4>
                {mitigations.length > 0 ? (
                  <div className="flex flex-wrap gap-2">
                    {mitigations.map((mitigation, index) => (
                      <span
                        key={index}
                        className="px-2 py-1 bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300 text-sm rounded border border-green-200 dark:border-green-800"
                      >
                        {mitigation}
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    {technique.mitigations || 'No mitigations specified'}
                  </p>
                )}
              </div>
              
              {/* Examples */}
              <div>
                <h4 className="text-sm font-medium text-purple-600 dark:text-purple-400 mb-2">Examples</h4>
                {examples.length > 0 ? (
                  <div className="flex flex-wrap gap-2">
                    {examples.map((example, index) => (
                      <span
                        key={index}
                        className="px-2 py-1 bg-purple-50 dark:bg-purple-900/20 text-purple-700 dark:text-purple-300 text-sm rounded border border-purple-200 dark:border-purple-800"
                      >
                        {example}
                      </span>
                    ))}
                  </div>
                ) : (
                  <p className="text-sm text-gray-500 dark:text-gray-400">
                    {technique.examples || 'No examples specified'}
                  </p>
                )}
              </div>
            </div>
          </div>
          
          {/* Associated Applications */}
          {technique.associatedApplications && technique.associatedApplications.length > 0 && (
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Associated Applications</h3>
              <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                <div className="flex flex-wrap gap-2">
                  {technique.associatedApplications.map((app, index) => (
                    <span
                      key={index}
                      className="px-3 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 text-sm rounded border"
                    >
                      {app.ApplicationName} ({app.Confidence}%)
                    </span>
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

// Main Component
export const MitreAttackPage: React.FC = () => {
  const { token, loading: authLoading } = useAuth();
  const navigate = useNavigate();
  const [techniques, setTechniques] = useState<MitreTechnique[]>([]);
  const [statistics, setStatistics] = useState<MitreStatistics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [importDialogOpen, setImportDialogOpen] = useState(false);
  const [selectedTechnique, setSelectedTechnique] = useState<MitreTechnique | null>(null);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  
  // Filters
  const [search, setSearch] = useState('');
  const [tacticFilter, setTacticFilter] = useState('');
  const [platformFilter, setPlatformFilter] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);

  useEffect(() => {
    if (!authLoading && !token) {
      navigate('/login');
    }
  }, [token, authLoading, navigate]);

  const loadTechniques = async () => {
    try {
      setLoading(true);
      setError(null);
      
      console.log('[MitreAttack] Fetching techniques with params:', {
        page: currentPage,
        perPage: 25,
        search: search || undefined,
        tactic: tacticFilter || undefined,
        platform: platformFilter || undefined,
      });
      
      const response = await fetchMitreTechniques({
        page: currentPage,
        perPage: 25,
        search: search || undefined,
        tactic: tacticFilter || undefined,
        platform: platformFilter || undefined,
        sort: 'techniqueId',
        order: 'ASC'
      });
      
      console.log('[MitreAttack] Raw API response:', response);
      
      // Handle the response format: { techniques: [...], totalCount: 823 }
      const techniquesData = response.techniques || response.data || [];
      const total = response.totalCount || response.total || techniquesData.length;
      
      console.log('[MitreAttack] Parsed techniques:', techniquesData.length, 'Total:', total);
      
      // Ensure each technique has an id field (use techniqueId as id)
      const techniquesWithId = techniquesData.map((t: any) => ({
        ...t,
        id: t.id || t.techniqueId
      }));
      
      setTechniques(techniquesWithId);
      setTotalCount(total);
      setTotalPages(Math.ceil(total / 25));
    } catch (error: any) {
      console.error('[MitreAttack] Failed to load techniques:', error);
      setError(error.message || 'Failed to load techniques');
    } finally {
      setLoading(false);
    }
  };

  const loadStatistics = async () => {
    try {
      const stats = await fetchMitreStatistics();
      setStatistics(stats);
    } catch (error) {
      console.error('Failed to load statistics:', error);
    }
  };

  useEffect(() => {
    loadTechniques();
    loadStatistics();
  }, [currentPage, search, tacticFilter, platformFilter]);

  const handleImport = async () => {
    await loadTechniques();
    await loadStatistics();
  };

  const handleTechniqueClick = (technique: MitreTechnique) => {
    setSelectedTechnique(technique);
    setDetailModalOpen(true);
  };

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900 dark:text-white">MITRE ATT&CK Techniques</h1>
        <p className="text-gray-600 dark:text-gray-400 mt-1">Threat Intelligence Database</p>
      </div>

      {/* Statistics Card */}
      {statistics && (
        <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Database Statistics</h2>
            <div className="flex items-center space-x-2">
              <CheckCircle className="h-4 w-4 text-green-500" />
              <span className="text-sm text-gray-500">Cached</span>
            </div>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div>
              <div className="text-3xl font-bold text-blue-600 dark:text-blue-400">
                {statistics.totalTechniques}
              </div>
              <div className="text-sm text-gray-600 dark:text-gray-300">Total Techniques</div>
            </div>
            
            <div>
              <div className="text-sm text-gray-600 dark:text-gray-300">Last Updated</div>
              <div className="text-sm font-medium text-gray-900 dark:text-white">
                {statistics.lastUpdated ? new Date(statistics.lastUpdated).toLocaleString() : 'Unknown'}
              </div>
            </div>
            
            <div>
              <div className="text-sm text-gray-600 dark:text-gray-300">⚡ Instant load from cache</div>
            </div>
          </div>
          
          {statistics.shouldImport && (
            <div className="mt-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-4">
              <div className="flex items-start">
                <Info className="h-5 w-5 text-blue-600 mr-2 mt-0.5" />
                <p className="text-sm text-blue-800 dark:text-blue-300">
                  The MITRE database appears to be empty or outdated. Consider importing the latest techniques.
                </p>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Actions Bar */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
        <div className="flex flex-col sm:flex-row gap-4 flex-1">
          {/* Search */}
          <div className="relative flex-1 max-w-md">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search techniques..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white placeholder-gray-500 dark:placeholder-gray-400 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>
          
          {/* Filters */}
          <div className="flex gap-2">
            <select
              value={tacticFilter}
              onChange={(e) => setTacticFilter(e.target.value)}
              className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
            >
              <option value="">All Tactics</option>
              <option value="reconnaissance">Reconnaissance</option>
              <option value="resource-development">Resource Development</option>
              <option value="initial-access">Initial Access</option>
              <option value="execution">Execution</option>
              <option value="persistence">Persistence</option>
              <option value="privilege-escalation">Privilege Escalation</option>
              <option value="defense-evasion">Defense Evasion</option>
              <option value="credential-access">Credential Access</option>
              <option value="discovery">Discovery</option>
              <option value="lateral-movement">Lateral Movement</option>
              <option value="collection">Collection</option>
              <option value="command-and-control">Command and Control</option>
              <option value="exfiltration">Exfiltration</option>
              <option value="impact">Impact</option>
            </select>
            
            <select
              value={platformFilter}
              onChange={(e) => setPlatformFilter(e.target.value)}
              className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
            >
              <option value="">All Platforms</option>
              <option value="Windows">Windows</option>
              <option value="Linux">Linux</option>
              <option value="macOS">macOS</option>
              <option value="Network">Network</option>
              <option value="Cloud">Cloud</option>
              <option value="Mobile">Mobile</option>
            </select>
          </div>
        </div>
        
        {/* Action Buttons */}
        <div className="flex gap-2">
          <button
            onClick={() => setImportDialogOpen(true)}
            className="px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg flex items-center space-x-2 transition-colors"
          >
            <Download className="h-4 w-4" />
            <span>Import</span>
          </button>
          
          <button
            onClick={() => window.open('/api/mitre/statistics', '_blank')}
            className="px-4 py-2 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 rounded-lg flex items-center space-x-2 transition-colors"
          >
            <BarChart3 className="h-4 w-4" />
            <span>Statistics</span>
          </button>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4">
          <div className="flex items-center">
            <AlertCircle className="h-5 w-5 text-red-600 mr-2" />
            <div>
              <h3 className="text-sm font-medium text-red-800">Error loading techniques</h3>
              <p className="text-sm text-red-700 mt-1">{error}</p>
              <p className="text-xs text-red-600 mt-2">
                Check the browser console for more details. Make sure the backend API is running on port 5000.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Techniques Grid */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
          <span className="ml-2 text-gray-600 dark:text-gray-300">Loading techniques...</span>
        </div>
      ) : techniques.length === 0 ? (
        <div className="text-center py-12">
          <Target className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">No techniques found</h3>
          <p className="text-gray-600 dark:text-gray-300 mb-4">
            {search || tacticFilter || platformFilter 
              ? 'Try adjusting your search or filters'
              : 'No MITRE techniques are available. Consider importing them.'
            }
          </p>
          {!search && !tacticFilter && !platformFilter && (
            <button
              onClick={() => setImportDialogOpen(true)}
              className="px-4 py-2 bg-blue-600 text-white hover:bg-blue-700 rounded-lg flex items-center space-x-2 mx-auto"
            >
              <Download className="h-4 w-4" />
              <span>Import Techniques</span>
            </button>
          )}
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {techniques.map((technique) => (
              <TechniqueCard
                key={technique.id}
                technique={technique}
                onClick={() => handleTechniqueClick(technique)}
              />
            ))}
          </div>
          
          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between">
              <div className="text-sm text-gray-600 dark:text-gray-300">
                Showing {((currentPage - 1) * 25) + 1} to {Math.min(currentPage * 25, totalCount)} of {totalCount} techniques
              </div>
              
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
            </div>
          )}
        </>
      )}

      {/* Modals */}
      <ImportDialog
        isOpen={importDialogOpen}
        onClose={() => setImportDialogOpen(false)}
        onImport={handleImport}
      />
      
      <TechniqueDetailModal
        technique={selectedTechnique}
        isOpen={detailModalOpen}
        onClose={() => setDetailModalOpen(false)}
      />
    </div>
  );
};
