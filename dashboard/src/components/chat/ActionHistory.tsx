import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { History, X, RefreshCw } from 'lucide-react';
import { ActionsAPI } from '../../services/actionsApi';
import { ActionHistoryItem } from './ActionHistoryItem';

interface ActionHistoryProps {
  conversationId: string;
  onClose: () => void;
  onExecute?: (executionId: number) => Promise<void>;
  onRollback?: (executionId: number, key: string, reason?: string) => Promise<void>;
}

type FilterTab = 'all' | 'pending' | 'executed' | 'rolledback' | 'failed';

export function ActionHistory({ conversationId, onClose, onExecute, onRollback }: ActionHistoryProps) {
  const [activeFilter, setActiveFilter] = useState<FilterTab>('all');

  // Fetch action history
  const { data: actions = [], isLoading, refetch } = useQuery({
    queryKey: ['action-history', conversationId],
    queryFn: () => ActionsAPI.getActionHistory(conversationId),
    enabled: !!conversationId,
    refetchInterval: 5000, // Auto-refresh every 5 seconds
  });

  // Filter actions based on active filter
  const filteredActions = actions.filter((action) => {
    const status = action.status?.toLowerCase();
    switch (activeFilter) {
      case 'pending':
        return status === 'pending';
      case 'executed':
        return status === 'executed';
      case 'rolledback':
        return status === 'rolledback';
      case 'failed':
        return status === 'failed' || status === 'expired';
      default:
        return true;
    }
  });

  // Count actions by status
  const counts = {
    all: actions.length,
    pending: actions.filter((a) => a.status?.toLowerCase() === 'pending').length,
    executed: actions.filter((a) => a.status?.toLowerCase() === 'executed').length,
    rolledback: actions.filter((a) => a.status?.toLowerCase() === 'rolledback').length,
    failed: actions.filter((a) => {
      const status = a.status?.toLowerCase();
      return status === 'failed' || status === 'expired';
    }).length,
  };

  const handleRollback = async (executionId: number, reason?: string) => {
    if (!onRollback) return;

    // Find the action to get the key
    const action = actions.find((a) => a.id === executionId);
    if (!action) return;

    const key = `${action.chatMessageId}-${action.type}`;
    await onRollback(executionId, key, reason);

    // Refresh the list
    refetch();
  };

  const filterTabs: { key: FilterTab; label: string }[] = [
    { key: 'all', label: 'All' },
    { key: 'pending', label: 'Pending' },
    { key: 'executed', label: 'Executed' },
    { key: 'rolledback', label: 'Rolled Back' },
    { key: 'failed', label: 'Failed' },
  ];

  return (
    <div className="fixed inset-y-0 right-0 w-96 bg-white dark:bg-gray-800 border-l border-gray-200 dark:border-gray-700 shadow-xl flex flex-col z-50">
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-gray-200 dark:border-gray-700">
        <div className="flex items-center gap-2">
          <History className="w-5 h-5 text-blue-600 dark:text-blue-400" />
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Action History</h2>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => refetch()}
            className="p-1 rounded hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-600 dark:text-gray-400 transition-colors"
            title="Refresh"
          >
            <RefreshCw className="w-4 h-4" />
          </button>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-gray-100 dark:hover:bg-gray-700 text-gray-600 dark:text-gray-400 transition-colors"
            title="Close"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Filter Tabs */}
      <div className="flex border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900">
        {filterTabs.map((tab) => (
          <button
            key={tab.key}
            onClick={() => setActiveFilter(tab.key)}
            className={`flex-1 px-3 py-2 text-xs font-medium transition-colors relative ${
              activeFilter === tab.key
                ? 'text-blue-600 dark:text-blue-400 bg-white dark:bg-gray-800'
                : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200'
            }`}
          >
            <span>{tab.label}</span>
            {counts[tab.key] > 0 && (
              <span
                className={`ml-1.5 px-1.5 py-0.5 rounded-full text-[10px] ${
                  activeFilter === tab.key
                    ? 'bg-blue-100 dark:bg-blue-900 text-blue-700 dark:text-blue-300'
                    : 'bg-gray-200 dark:bg-gray-700 text-gray-600 dark:text-gray-400'
                }`}
              >
                {counts[tab.key]}
              </span>
            )}
            {activeFilter === tab.key && (
              <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-blue-600 dark:bg-blue-400" />
            )}
          </button>
        ))}
      </div>

      {/* Action List */}
      <div className="flex-1 overflow-y-auto p-4">
        {isLoading ? (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 dark:border-blue-400"></div>
          </div>
        ) : filteredActions.length === 0 ? (
          <div className="text-center py-8 text-gray-500 dark:text-gray-400">
            <History className="w-12 h-12 mx-auto mb-2 opacity-50" />
            <p className="text-sm">
              {activeFilter === 'all'
                ? 'No actions yet'
                : `No ${activeFilter} actions`}
            </p>
          </div>
        ) : (
          <div className="space-y-3">
            {filteredActions.map((action) => (
              <ActionHistoryItem
                key={action.id}
                action={action}
                onExecute={onExecute}
                onRollback={onRollback ? handleRollback : undefined}
              />
            ))}
          </div>
        )}
      </div>

      {/* Footer Stats */}
      <div className="border-t border-gray-200 dark:border-gray-700 p-3 bg-gray-50 dark:bg-gray-900">
        <div className="text-xs text-gray-600 dark:text-gray-400 space-y-1">
          <div className="flex justify-between">
            <span>Total Actions:</span>
            <span className="font-medium">{counts.all}</span>
          </div>
          <div className="flex justify-between">
            <span>Executed:</span>
            <span className="font-medium text-green-600 dark:text-green-400">{counts.executed}</span>
          </div>
          <div className="flex justify-between">
            <span>Pending:</span>
            <span className="font-medium text-blue-600 dark:text-blue-400">{counts.pending}</span>
          </div>
          {counts.rolledback > 0 && (
            <div className="flex justify-between">
              <span>Rolled Back:</span>
              <span className="font-medium text-gray-600 dark:text-gray-400">{counts.rolledback}</span>
            </div>
          )}
          {counts.failed > 0 && (
            <div className="flex justify-between">
              <span>Failed:</span>
              <span className="font-medium text-red-600 dark:text-red-400">{counts.failed}</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
