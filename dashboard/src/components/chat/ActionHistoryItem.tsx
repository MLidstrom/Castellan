import { useState } from 'react';
import {
  ChevronDown,
  ChevronUp,
  Ban,
  PauseCircle,
  FileX,
  Eye,
  Ticket,
  CheckCircle,
  XCircle,
  Clock,
  Undo,
  AlertCircle,
  Play,
  Loader,
} from 'lucide-react';
import type { ActionExecution } from '../../services/actionsApi';

interface ActionHistoryItemProps {
  action: ActionExecution;
  onExecute?: (executionId: number) => Promise<void>;
  onRollback?: (executionId: number, reason?: string) => Promise<void>;
}

export function ActionHistoryItem({ action, onExecute, onRollback }: ActionHistoryItemProps) {
  const [expanded, setExpanded] = useState(false);
  const [rollbackReason, setRollbackReason] = useState('');
  const [showRollbackInput, setShowRollbackInput] = useState(false);
  const [isExecuting, setIsExecuting] = useState(false);

  // Map action type to icon
  const getIcon = () => {
    switch (action.type) {
      case 'BlockIP':
        return Ban;
      case 'IsolateHost':
        return PauseCircle;
      case 'QuarantineFile':
        return FileX;
      case 'AddToWatchlist':
        return Eye;
      case 'CreateTicket':
        return Ticket;
      default:
        return AlertCircle;
    }
  };

  // Get status styling
  const getStatusDisplay = () => {
    const status = action.status?.toLowerCase();
    switch (status) {
      case 'executed':
        return {
          icon: CheckCircle,
          color: 'text-green-600 dark:text-green-400',
          bgColor: 'bg-green-100 dark:bg-green-900',
          borderColor: 'border-green-300 dark:border-green-700',
          label: 'Executed',
        };
      case 'rolledback':
        return {
          icon: Undo,
          color: 'text-gray-600 dark:text-gray-400',
          bgColor: 'bg-gray-100 dark:bg-gray-900',
          borderColor: 'border-gray-300 dark:border-gray-700',
          label: 'Rolled Back',
        };
      case 'failed':
        return {
          icon: XCircle,
          color: 'text-red-600 dark:text-red-400',
          bgColor: 'bg-red-100 dark:bg-red-900',
          borderColor: 'border-red-300 dark:border-red-700',
          label: 'Failed',
        };
      case 'expired':
        return {
          icon: Clock,
          color: 'text-gray-600 dark:text-gray-400',
          bgColor: 'bg-gray-100 dark:bg-gray-900',
          borderColor: 'border-gray-300 dark:border-gray-700',
          label: 'Expired',
        };
      default:
        return {
          icon: Clock,
          color: 'text-blue-600 dark:text-blue-400',
          bgColor: 'bg-blue-100 dark:bg-blue-900',
          borderColor: 'border-blue-300 dark:border-blue-700',
          label: 'Pending',
        };
    }
  };

  const handleExecute = async () => {
    if (!onExecute) return;

    setIsExecuting(true);
    try {
      await onExecute(action.id);
    } catch (error) {
      console.error('Failed to execute action:', error);
    } finally {
      setIsExecuting(false);
    }
  };

  const handleRollback = async () => {
    if (!onRollback) return;

    await onRollback(action.id, rollbackReason || undefined);
    setShowRollbackInput(false);
    setRollbackReason('');
  };

  const Icon = getIcon();
  const statusDisplay = getStatusDisplay();
  const StatusIcon = statusDisplay.icon;

  const canExecute = (action.status === 'Pending' || action.status === 'pending') && onExecute;
  const canRollback = (action.status === 'Executed' || action.status === 'executed') && onRollback;


  // Parse action data
  let actionData: any = {};
  try {
    actionData = typeof action.actionData === 'string' ? JSON.parse(action.actionData) : action.actionData;
  } catch {
    actionData = {};
  }

  // Parse execution log
  let executionLog: any[] = [];
  try {
    executionLog = typeof action.executionLog === 'string' ? JSON.parse(action.executionLog) : action.executionLog;
  } catch {
    executionLog = [];
  }

  return (
    <div className="border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-800">
      {/* Header - Always visible */}
      <div className="p-3 flex items-start gap-3">
        {/* Icon */}
        <div className="flex-shrink-0 mt-1">
          <Icon className="w-5 h-5 text-gray-700 dark:text-gray-300" />
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2 mb-1">
            {/* Action Type Label */}
            <div className="text-sm font-semibold text-gray-900 dark:text-gray-100">
              {action.type.replace(/([A-Z])/g, ' $1').trim()}
            </div>

            {/* Status Badge */}
            <div className={`flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${statusDisplay.bgColor} ${statusDisplay.color}`}>
              <StatusIcon className="w-3 h-3" />
              <span>{statusDisplay.label}</span>
            </div>
          </div>

          {/* Timestamp */}
          <div className="text-xs text-gray-500 dark:text-gray-400">
            Suggested: {new Date(action.suggestedAt).toLocaleString()}
          </div>

          {action.executedAt && (
            <div className="text-xs text-gray-500 dark:text-gray-400">
              Executed: {new Date(action.executedAt).toLocaleString()}
              {action.executedBy && ` by ${action.executedBy}`}
            </div>
          )}

          {action.rolledBackAt && (
            <div className="text-xs text-gray-500 dark:text-gray-400">
              Rolled Back: {new Date(action.rolledBackAt).toLocaleString()}
              {action.rolledBackBy && ` by ${action.rolledBackBy}`}
            </div>
          )}

          {/* Quick Actions */}
          <div className="flex gap-2 mt-2">
            {canExecute && (
              <button
                onClick={handleExecute}
                disabled={isExecuting}
                className="px-2 py-1 text-xs font-medium rounded bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-1"
              >
                {isExecuting ? (
                  <>
                    <Loader className="w-3 h-3 animate-spin" />
                    <span>Executing...</span>
                  </>
                ) : (
                  <>
                    <Play className="w-3 h-3" />
                    <span>Execute</span>
                  </>
                )}
              </button>
            )}

            {canRollback && !showRollbackInput && (
              <button
                onClick={() => setShowRollbackInput(true)}
                className="px-2 py-1 text-xs font-medium rounded bg-orange-600 text-white hover:bg-orange-700 transition-colors flex items-center gap-1"
              >
                <Undo className="w-3 h-3" />
                <span>Undo</span>
              </button>
            )}

            <button
              onClick={() => setExpanded(!expanded)}
              className="px-2 py-1 text-xs font-medium rounded bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors flex items-center gap-1"
            >
              {expanded ? (
                <>
                  <ChevronUp className="w-3 h-3" />
                  <span>Less</span>
                </>
              ) : (
                <>
                  <ChevronDown className="w-3 h-3" />
                  <span>Details</span>
                </>
              )}
            </button>
          </div>

          {/* Rollback Input */}
          {showRollbackInput && (
            <div className="mt-3 p-3 bg-orange-50 dark:bg-orange-900/20 border border-orange-200 dark:border-orange-800 rounded">
              <label className="text-xs font-medium text-gray-700 dark:text-gray-300 block mb-1">
                Reason for rollback (optional):
              </label>
              <textarea
                value={rollbackReason}
                onChange={(e) => setRollbackReason(e.target.value)}
                placeholder="Enter reason..."
                rows={2}
                className="w-full px-2 py-1 text-xs border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-orange-500"
              />
              <div className="flex gap-2 mt-2">
                <button
                  onClick={handleRollback}
                  className="px-2 py-1 text-xs font-medium rounded bg-orange-600 text-white hover:bg-orange-700 transition-colors"
                >
                  Confirm Rollback
                </button>
                <button
                  onClick={() => {
                    setShowRollbackInput(false);
                    setRollbackReason('');
                  }}
                  className="px-2 py-1 text-xs font-medium rounded bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-300 dark:hover:bg-gray-600 transition-colors"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Expanded Details */}
      {expanded && (
        <div className="border-t border-gray-200 dark:border-gray-700 p-3 space-y-3">
          {/* Action Data */}
          {Object.keys(actionData).length > 0 && (
            <div>
              <div className="text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                Parameters:
              </div>
              <div className="bg-gray-50 dark:bg-gray-900 rounded p-2 space-y-1">
                {Object.entries(actionData).map(([key, value]) => (
                  <div key={key} className="text-xs flex justify-between">
                    <span className="font-medium text-gray-600 dark:text-gray-400">{key}:</span>
                    <span className="text-gray-900 dark:text-gray-100 font-mono">
                      {typeof value === 'object' ? JSON.stringify(value) : String(value)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Before/After State */}
          {(action.beforeState || action.afterState) && (
            <div className="grid grid-cols-2 gap-2">
              {action.beforeState && (
                <div>
                  <div className="text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                    Before State:
                  </div>
                  <div className="bg-gray-50 dark:bg-gray-900 rounded p-2 text-xs font-mono text-gray-900 dark:text-gray-100 overflow-x-auto">
                    {action.beforeState}
                  </div>
                </div>
              )}
              {action.afterState && (
                <div>
                  <div className="text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                    After State:
                  </div>
                  <div className="bg-gray-50 dark:bg-gray-900 rounded p-2 text-xs font-mono text-gray-900 dark:text-gray-100 overflow-x-auto">
                    {action.afterState}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Execution Log */}
          {executionLog.length > 0 && (
            <div>
              <div className="text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                Execution Log:
              </div>
              <div className="bg-gray-50 dark:bg-gray-900 rounded p-2 space-y-1 max-h-40 overflow-y-auto">
                {executionLog.map((log, idx) => (
                  <div key={idx} className="text-xs text-gray-700 dark:text-gray-300">
                    {typeof log === 'string' ? log : JSON.stringify(log)}
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Rollback Reason */}
          {action.rollbackReason && (
            <div>
              <div className="text-xs font-semibold text-gray-700 dark:text-gray-300 mb-1">
                Rollback Reason:
              </div>
              <div className="bg-orange-50 dark:bg-orange-900/20 rounded p-2 text-xs text-gray-700 dark:text-gray-300 italic">
                {action.rollbackReason}
              </div>
            </div>
          )}

          {/* IDs */}
          <div className="text-xs text-gray-500 dark:text-gray-400 space-y-0.5">
            <div>Action ID: {action.id}</div>
            <div>Conversation ID: {action.conversationId}</div>
            <div>Message ID: {action.chatMessageId}</div>
          </div>
        </div>
      )}
    </div>
  );
}
