import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Bell,
  Search,
  Plus,
  Edit,
  Trash2,
  CheckCircle,
  AlertCircle,
  X,
  Eye,
  Copy,
  Loader2
} from 'lucide-react';
import { Api } from '../services/api';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { LoadingSpinner } from '../components/LoadingSpinner';

// Types
interface NotificationTemplate {
  id: string;
  name: string;
  platform: 'Teams' | 'Slack';
  type: 'SecurityEvent' | 'SystemAlert' | 'HealthWarning' | 'PerformanceAlert';
  content: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

// Platform color mapping
const PLATFORM_COLORS: Record<string, string> = {
  'Teams': 'bg-blue-100 text-blue-800 border-blue-200',
  'Slack': 'bg-purple-100 text-purple-800 border-purple-200',
};

// Template type color mapping
const TYPE_COLORS: Record<string, string> = {
  'SecurityEvent': 'bg-red-100 text-red-800 border-red-200',
  'SystemAlert': 'bg-yellow-100 text-yellow-800 border-yellow-200',
  'HealthWarning': 'bg-orange-100 text-orange-800 border-orange-200',
  'PerformanceAlert': 'bg-green-100 text-green-800 border-green-200',
};

// Available template tags
const TEMPLATE_TAGS = [
  { tag: '{{DATE}}', description: 'Event date' },
  { tag: '{{TIMESTAMP}}', description: 'Event timestamp' },
  { tag: '{{HOST}}', description: 'Host machine name' },
  { tag: '{{MACHINE_NAME}}', description: 'Machine name (alias for HOST)' },
  { tag: '{{USER}}', description: 'Username' },
  { tag: '{{EVENT_ID}}', description: 'Windows Event ID' },
  { tag: '{{SEVERITY}}', description: 'Risk level (Critical, High, Medium, Low)' },
  { tag: '{{EVENT_TYPE}}', description: 'Event type description' },
  { tag: '{{SUMMARY}}', description: 'AI-generated summary' },
  { tag: '{{MITRE_TECHNIQUES}}', description: 'MITRE ATT&CK techniques' },
  { tag: '{{RECOMMENDED_ACTIONS}}', description: 'Recommended response actions' },
  { tag: '{{DETAILS_URL}}', description: 'Link to event details' },
  { tag: '{{ALERT_ID}}', description: 'Unique alert identifier' },
  { tag: '{{CONFIDENCE}}', description: 'Detection confidence percentage' },
  { tag: '{{CORRELATION_SCORE}}', description: 'Event correlation score' },
  { tag: '{{SOURCE_IP}}', description: 'Source IP address (if available)' },
  { tag: '{{LOCATION}}', description: 'Geographic location (if available)' },
  { tag: '{{EVENT_MESSAGE}}', description: 'Original event message' },
];

// Template Card Component
const TemplateCard: React.FC<{
  template: NotificationTemplate;
  onEdit: () => void;
  onDelete: () => void;
  onToggle: () => void;
  onPreview: () => void;
}> = ({ template, onEdit, onDelete, onToggle, onPreview }) => {
  return (
    <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg p-4 hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between mb-3">
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
          <h3 className="font-semibold text-gray-900 dark:text-white mb-2">
            {template.name}
          </h3>
          <p className="text-sm text-gray-600 dark:text-gray-300 line-clamp-2 font-mono bg-gray-50 dark:bg-gray-900 p-2 rounded">
            {(template.content || '').substring(0, 100)}...
          </p>
        </div>
      </div>

      <div className="flex items-center justify-between pt-3 border-t border-gray-200 dark:border-gray-700">
        <div className="text-xs text-gray-500 dark:text-gray-400">
          Updated {new Date(template.updatedAt).toLocaleDateString()}
        </div>
        <div className="flex space-x-2">
          <button
            onClick={onPreview}
            className="p-1 text-gray-600 hover:text-blue-600 dark:text-gray-400 dark:hover:text-blue-400"
            title="Preview"
          >
            <Eye className="h-4 w-4" />
          </button>
          <button
            onClick={onToggle}
            className="p-1 text-gray-600 hover:text-green-600 dark:text-gray-400 dark:hover:text-green-400"
            title={template.isEnabled ? 'Disable' : 'Enable'}
          >
            <CheckCircle className={`h-4 w-4 ${template.isEnabled ? 'text-green-500' : 'text-gray-400'}`} />
          </button>
          <button
            onClick={onEdit}
            className="p-1 text-gray-600 hover:text-blue-600 dark:text-gray-400 dark:hover:text-blue-400"
            title="Edit"
          >
            <Edit className="h-4 w-4" />
          </button>
          <button
            onClick={onDelete}
            className="p-1 text-gray-600 hover:text-red-600 dark:text-gray-400 dark:hover:text-red-400"
            title="Delete"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  );
};

// Template Editor Modal
const TemplateEditorModal: React.FC<{
  template: NotificationTemplate | null;
  isOpen: boolean;
  onClose: () => void;
  onSave: (template: Partial<NotificationTemplate>) => void;
  isNew: boolean;
}> = ({ template, isOpen, onClose, onSave, isNew }) => {
  const [formData, setFormData] = useState<Partial<NotificationTemplate>>({
    name: '',
    platform: 'Teams',
    type: 'SecurityEvent',
    content: '',
    isEnabled: true,
  });

  const [previewMode, setPreviewMode] = useState(false);

  useEffect(() => {
    if (template && isOpen) {
      setFormData({
        name: template.name,
        platform: template.platform,
        type: template.type,
        content: template.content,
        isEnabled: template.isEnabled,
      });
    } else if (isNew && isOpen) {
      setFormData({
        name: '',
        platform: 'Teams',
        type: 'SecurityEvent',
        content: '',
        isEnabled: true,
      });
    }
  }, [template, isOpen, isNew]);

  if (!isOpen) return null;

  const handleSave = () => {
    onSave(formData);
  };

  const insertTag = (tag: string) => {
    setFormData({
      ...formData,
      content: (formData.content || '') + tag
    });
  };

  // Generate preview with sample data
  const generatePreview = () => {
    let preview = formData.content || '';
    const sampleData: Record<string, string> = {
      '{{DATE}}': '2025-10-20 14:30:45',
      '{{TIMESTAMP}}': '2025-10-20 14:30:45',
      '{{HOST}}': 'DESKTOP-PROD01',
      '{{MACHINE_NAME}}': 'DESKTOP-PROD01',
      '{{USER}}': 'john.doe',
      '{{EVENT_ID}}': '4625',
      '{{SEVERITY}}': 'High',
      '{{EVENT_TYPE}}': 'Failed Login Attempt',
      '{{SUMMARY}}': 'Multiple failed login attempts detected from unusual location',
      '{{MITRE_TECHNIQUES}}': 'T1110.001 (Brute Force)',
      '{{RECOMMENDED_ACTIONS}}': 'Review event details; Check for brute force patterns; Verify user credentials',
      '{{DETAILS_URL}}': 'http://localhost:3000/security-events/123',
      '{{ALERT_ID}}': '123',
      '{{CONFIDENCE}}': '95%',
      '{{CORRELATION_SCORE}}': '0.85',
      '{{SOURCE_IP}}': '192.168.1.100',
      '{{LOCATION}}': 'Seattle, USA',
      '{{EVENT_MESSAGE}}': 'An account failed to log on. Subject: Account Name: john.doe...',
    };

    Object.entries(sampleData).forEach(([tag, value]) => {
      preview = preview.replace(new RegExp(tag.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g'), value);
    });

    return preview;
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-5xl w-full mx-4 max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <Bell className="h-6 w-6 text-blue-600" />
              <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
                {isNew ? 'Create Template' : 'Edit Template'}
              </h2>
            </div>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            >
              <X className="h-6 w-6" />
            </button>
          </div>
        </div>

        <div className="p-6">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {/* Left Column - Form */}
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                  Template Name
                </label>
                <input
                  type="text"
                  value={formData.name || ''}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
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
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
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
                    className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
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
                    className="w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
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
                  value={formData.content || ''}
                  onChange={(e) => setFormData({ ...formData, content: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500 font-mono text-sm"
                  rows={12}
                  placeholder="Enter template content with {{TAGS}}..."
                />
              </div>

              <div className="flex space-x-2">
                <button
                  onClick={() => setPreviewMode(!previewMode)}
                  className="flex-1 px-4 py-2 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors flex items-center justify-center space-x-2"
                >
                  <Eye className="h-4 w-4" />
                  <span>{previewMode ? 'Hide Preview' : 'Show Preview'}</span>
                </button>
              </div>
            </div>

            {/* Right Column - Tags Reference */}
            <div className="space-y-4">
              <div>
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">
                  Available Tags
                </h3>
                <div className="border border-gray-200 dark:border-gray-700 rounded-lg max-h-[500px] overflow-y-auto">
                  {TEMPLATE_TAGS.map((item, index) => (
                    <div
                      key={index}
                      className="p-3 border-b border-gray-200 dark:border-gray-700 last:border-b-0 hover:bg-gray-50 dark:hover:bg-gray-700 cursor-pointer"
                      onClick={() => insertTag(item.tag)}
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex-1">
                          <code className="text-sm font-mono text-blue-600 dark:text-blue-400">
                            {item.tag}
                          </code>
                          <p className="text-xs text-gray-600 dark:text-gray-400 mt-1">
                            {item.description}
                          </p>
                        </div>
                        <Copy className="h-4 w-4 text-gray-400" />
                      </div>
                    </div>
                  ))}
                </div>
                <p className="text-xs text-gray-500 dark:text-gray-400 mt-2">
                  Click a tag to insert it into the template
                </p>
              </div>
            </div>
          </div>

          {/* Preview Section */}
          {previewMode && (
            <div className="mt-6 pt-6 border-t border-gray-200 dark:border-gray-700">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">
                Preview with Sample Data
              </h3>
              <div className="bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg p-4">
                <pre className="text-sm text-gray-900 dark:text-white whitespace-pre-wrap font-mono">
                  {generatePreview()}
                </pre>
              </div>
            </div>
          )}

          {/* Actions */}
          <div className="mt-6 flex justify-end space-x-3">
            <button
              onClick={onClose}
              className="px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={handleSave}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
            >
              {isNew ? 'Create Template' : 'Save Changes'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

// Main Component
export const NotificationTemplatesPage: React.FC = () => {
  const { token, loading: authLoading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [selectedTemplate, setSelectedTemplate] = useState<NotificationTemplate | null>(null);
  const [editorOpen, setEditorOpen] = useState(false);
  const [isNewTemplate, setIsNewTemplate] = useState(false);
  const [search, setSearch] = useState('');
  const [platformFilter, setPlatformFilter] = useState('');

  useEffect(() => {
    if (!authLoading && !token) {
      navigate('/login');
    }
  }, [token, authLoading, navigate]);

  // Query for templates
  const templatesQuery = useQuery({
    queryKey: ['notification-templates'],
    queryFn: Api.getNotificationTemplates,
    enabled: !authLoading && !!token,
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

  // Create defaults mutation
  const createDefaultsMutation = useMutation({
    mutationFn: Api.createDefaultTemplates,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notification-templates'] });
    },
  });

  const templates = (templatesQuery.data as NotificationTemplate[]) || [];
  const loading = templatesQuery.isLoading;
  const error = templatesQuery.error ? (templatesQuery.error as Error).message : null;

  // Filter templates
  const filteredTemplates = templates.filter(t => {
    if (platformFilter && t.platform !== platformFilter) return false;
    if (search && !t.name.toLowerCase().includes(search.toLowerCase()) &&
        !(t.content || '').toLowerCase().includes(search.toLowerCase())) return false;
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
    updateMutation.mutate({
      id: template.id,
      template: { ...template, isEnabled: !template.isEnabled }
    });
  };

  const handlePreview = (template: NotificationTemplate) => {
    setSelectedTemplate(template);
    setIsNewTemplate(false);
    setEditorOpen(true);
  };

  if (loading) {
    return <LoadingSpinner />;
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Notification Templates</h1>
        <p className="text-gray-600 dark:text-gray-400 mt-1">
          Manage notification message templates for Teams and Slack
        </p>
      </div>

      {/* Actions Bar */}
      <div className="flex flex-col sm:flex-row gap-4 items-start sm:items-center justify-between">
        <div className="flex flex-col sm:flex-row gap-4 flex-1">
          {/* Search */}
          <div className="relative flex-1 max-w-md">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search templates..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full pl-10 pr-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white placeholder-gray-500 dark:placeholder-gray-400 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          {/* Platform Filter */}
          <select
            value={platformFilter}
            onChange={(e) => setPlatformFilter(e.target.value)}
            className="px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All Platforms</option>
            <option value="Teams">Teams</option>
            <option value="Slack">Slack</option>
          </select>
        </div>

        {/* Action Buttons */}
        <div className="flex space-x-2">
          {templates.length === 0 && (
            <button
              onClick={() => createDefaultsMutation.mutate()}
              disabled={createDefaultsMutation.isPending}
              className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center space-x-2"
            >
              {createDefaultsMutation.isPending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  <span>Creating...</span>
                </>
              ) : (
                <>
                  <CheckCircle className="h-4 w-4" />
                  <span>Create Defaults</span>
                </>
              )}
            </button>
          )}
          <button
            onClick={handleCreateNew}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors flex items-center space-x-2"
          >
            <Plus className="h-4 w-4" />
            <span>New Template</span>
          </button>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4">
          <div className="flex items-center">
            <AlertCircle className="h-5 w-5 text-red-600 mr-2" />
            <div>
              <h3 className="text-sm font-medium text-red-800">Error loading templates</h3>
              <p className="text-sm text-red-700 mt-1">{error}</p>
            </div>
          </div>
        </div>
      )}

      {/* Templates Grid */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
          <span className="ml-2 text-gray-600 dark:text-gray-300">Loading templates...</span>
        </div>
      ) : filteredTemplates.length === 0 ? (
        <div className="text-center py-12">
          <Bell className="h-12 w-12 text-gray-400 mx-auto mb-4" />
          <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">No templates found</h3>
          <p className="text-gray-600 dark:text-gray-300 mb-4">
            {templates.length === 0
              ? 'Get started by creating default templates or adding a custom template.'
              : 'Try adjusting your search or filters.'}
          </p>
          {templates.length === 0 && (
            <div className="flex justify-center space-x-3">
              <button
                onClick={() => createDefaultsMutation.mutate()}
                disabled={createDefaultsMutation.isPending}
                className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors disabled:opacity-50"
              >
                Create Default Templates
              </button>
              <button
                onClick={handleCreateNew}
                className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
              >
                Create Custom Template
              </button>
            </div>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredTemplates.map((template) => (
            <TemplateCard
              key={template.id}
              template={template}
              onEdit={() => handleEdit(template)}
              onDelete={() => handleDelete(template)}
              onToggle={() => handleToggle(template)}
              onPreview={() => handlePreview(template)}
            />
          ))}
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
};
