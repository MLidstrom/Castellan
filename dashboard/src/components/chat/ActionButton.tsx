import { useState } from 'react';
import {
  AlertCircle,
  Ban,
  PauseCircle,
  FileX,
  Eye,
  Ticket,
  CheckCircle,
  XCircle,
  Clock,
  Undo,
  Loader,
  X,
  Info,
} from 'lucide-react';
import type { SuggestedAction } from '../../services/chatApi';
import { ActionExecution, ActionStatus, ActionType } from '../../services/actionsApi';
import { ActionConfirmationDialog } from './ActionConfirmationDialog';

interface ActionButtonProps {
  action: SuggestedAction;
  execution?: ActionExecution;
  conversationId: string;
  messageId: string;
  onExecute: (action: SuggestedAction) => Promise<void>;
  onRollback?: (executionId: number, reason?: string) => Promise<void>;
}

export function ActionButton({
  action,
  execution,
  onExecute,
  onRollback,
}: ActionButtonProps) {
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [showDetailsModal, setShowDetailsModal] = useState(false);
  const [isExecuting, setIsExecuting] = useState(false);

  // Map action type to icon
  const getIcon = () => {
    switch (action.type) {
      case 'block_ip':
      case ActionType.BlockIP:
        return Ban;
      case 'isolate_host':
      case ActionType.IsolateHost:
        return PauseCircle;
      case 'quarantine_file':
      case ActionType.QuarantineFile:
        return FileX;
      case 'add_to_watchlist':
      case ActionType.AddToWatchlist:
        return Eye;
      case 'create_ticket':
      case ActionType.CreateTicket:
        return Ticket;
      default:
        return AlertCircle;
    }
  };

  // Get status icon and colors
  const getStatusDisplay = () => {
    if (!execution) {
      return {
        icon: Clock,
        color: 'text-blue-600 dark:text-blue-400',
        bgColor: 'bg-blue-100 dark:bg-blue-900',
        borderColor: 'border-blue-300 dark:border-blue-700',
        label: 'Pending',
      };
    }

    switch (execution.status) {
      case ActionStatus.Executed:
        return {
          icon: CheckCircle,
          color: 'text-green-600 dark:text-green-400',
          bgColor: 'bg-green-100 dark:bg-green-900',
          borderColor: 'border-green-300 dark:border-green-700',
          label: 'Executed',
        };
      case ActionStatus.RolledBack:
        return {
          icon: Undo,
          color: 'text-gray-600 dark:text-gray-400',
          bgColor: 'bg-gray-100 dark:bg-gray-900',
          borderColor: 'border-gray-300 dark:border-gray-700',
          label: 'Rolled Back',
        };
      case ActionStatus.Failed:
        return {
          icon: XCircle,
          color: 'text-red-600 dark:text-red-400',
          bgColor: 'bg-red-100 dark:bg-red-900',
          borderColor: 'border-red-300 dark:border-red-700',
          label: 'Failed',
        };
      case ActionStatus.Expired:
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

  const getSeverityColor = (severity: string) => {
    switch (severity?.toLowerCase()) {
      case 'critical':
        return 'bg-red-50 dark:bg-red-900/20 border-red-300 dark:border-red-700';
      case 'high':
        return 'bg-orange-50 dark:bg-orange-900/20 border-orange-300 dark:border-orange-700';
      case 'medium':
        return 'bg-yellow-50 dark:bg-yellow-900/20 border-yellow-300 dark:border-yellow-700';
      case 'low':
        return 'bg-green-50 dark:bg-green-900/20 border-green-300 dark:border-green-700';
      default:
        return 'bg-gray-50 dark:bg-gray-800 border-gray-300 dark:border-gray-700';
    }
  };

  const handleExecute = async () => {
    setIsExecuting(true);
    try {
      await onExecute(action);
      setShowConfirmDialog(false);
    } catch (error) {
      console.error('Failed to execute action:', error);
    } finally {
      setIsExecuting(false);
    }
  };

  const handleRollback = async (reason?: string) => {
    if (!execution || !onRollback) return;

    setIsExecuting(true);
    try {
      await onRollback(execution.id, reason);
      setShowConfirmDialog(false);
    } catch (error) {
      console.error('Failed to rollback action:', error);
    } finally {
      setIsExecuting(false);
    }
  };

  const Icon = getIcon();
  const statusDisplay = getStatusDisplay();
  const StatusIcon = statusDisplay.icon;
  const severityColor = getSeverityColor(action.parameters?.severity);

  const canExecute = !execution || execution.status === ActionStatus.Pending;
  const canRollback = execution?.status === ActionStatus.Executed && onRollback;

  return (
    <>
      <div className={`w-full p-3 rounded-lg border transition-all ${severityColor}`}>
        <div className="flex items-start gap-3">
          {/* Action Icon */}
          <div className="flex-shrink-0">
            <Icon className="w-5 h-5 text-gray-700 dark:text-gray-300" />
          </div>

          {/* Action Details */}
          <div className="flex-1 min-w-0">
            <div className="flex items-start justify-between gap-2">
              <div className="flex-1">
                <div className="text-sm font-semibold text-gray-900 dark:text-gray-100">
                  {action.label}
                </div>
                <div className="text-xs text-gray-600 dark:text-gray-400 mt-0.5">
                  {action.description}
                </div>
              </div>

              {/* Status Badge */}
              <div className={`flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${statusDisplay.bgColor} ${statusDisplay.color}`}>
                <StatusIcon className="w-3 h-3" />
                <span>{statusDisplay.label}</span>
              </div>
            </div>

            {/* Confidence Score */}
            {action.confidence !== undefined && (
              <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                Confidence: {Math.round(action.confidence * 100)}%
              </div>
            )}

            {/* Action Buttons */}
            <div className="flex gap-2 mt-3">
              {canExecute && (
                <button
                  onClick={() => setShowConfirmDialog(true)}
                  disabled={isExecuting}
                  className="px-3 py-1.5 text-xs font-medium rounded-md bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-1"
                >
                  {isExecuting ? (
                    <>
                      <Loader className="w-3 h-3 animate-spin" />
                      <span>Executing...</span>
                    </>
                  ) : (
                    <>
                      <CheckCircle className="w-3 h-3" />
                      <span>Execute</span>
                    </>
                  )}
                </button>
              )}

              {canRollback && (
                <button
                  onClick={() => setShowConfirmDialog(true)}
                  disabled={isExecuting}
                  className="px-3 py-1.5 text-xs font-medium rounded-md bg-gray-600 text-white hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-1"
                >
                  {isExecuting ? (
                    <>
                      <Loader className="w-3 h-3 animate-spin" />
                      <span>Rolling back...</span>
                    </>
                  ) : (
                    <>
                      <Undo className="w-3 h-3" />
                      <span>Undo</span>
                    </>
                  )}
                </button>
              )}

              {execution && execution.status !== ActionStatus.Pending && (
                <button
                  onClick={() => setShowDetailsModal(true)}
                  className="px-3 py-1.5 text-xs font-medium rounded-md bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors flex items-center gap-1"
                >
                  <Info className="w-3 h-3" />
                  <span>View Details</span>
                </button>
              )}
            </div>

            {/* Execution Info */}
            {execution && execution.executedAt && (
              <div className="text-xs text-gray-500 dark:text-gray-400 mt-2 space-y-0.5">
                <div>Executed by: {execution.executedBy || 'Unknown'}</div>
                <div>
                  Executed at: {new Date(execution.executedAt).toLocaleString()}
                </div>
                {execution.rolledBackAt && (
                  <>
                    <div>Rolled back by: {execution.rolledBackBy || 'Unknown'}</div>
                    <div>
                      Rolled back at: {new Date(execution.rolledBackAt).toLocaleString()}
                    </div>
                    {execution.rollbackReason && (
                      <div className="italic">Reason: {execution.rollbackReason}</div>
                    )}
                  </>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Confirmation Dialog */}
      {showConfirmDialog && (
        <ActionConfirmationDialog
          action={action}
          execution={execution}
          isExecuting={isExecuting}
          mode={canRollback ? 'rollback' : 'execute'}
          onConfirm={canRollback ? handleRollback : handleExecute}
          onCancel={() => setShowConfirmDialog(false)}
        />
      )}

      {/* Details Modal */}
      {showDetailsModal && execution && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
            <div className="p-6">
              {/* Header */}
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100 flex items-center gap-2">
                  <Icon className="w-5 h-5" />
                  Action Details
                </h3>
                <button
                  onClick={() => setShowDetailsModal(false)}
                  className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Action Information */}
              <div className="space-y-4">
                {/* Basic Info */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Action Type</label>
                    <div className="text-sm text-gray-900 dark:text-gray-100 mt-1">{action.label}</div>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Status</label>
                    <div className="flex items-center gap-2 mt-1">
                      <StatusIcon className={`w-4 h-4 ${statusDisplay.color}`} />
                      <span className={`text-sm font-medium ${statusDisplay.color}`}>{statusDisplay.label}</span>
                    </div>
                  </div>
                </div>

                {/* Description */}
                <div>
                  <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Description</label>
                  <div className="text-sm text-gray-900 dark:text-gray-100 mt-1">{action.description}</div>
                </div>

                {/* Execution Details */}
                <div className="border-t pt-4">
                  <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">Execution Details</h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Execution ID</label>
                      <div className="text-sm text-gray-900 dark:text-gray-100 mt-1 font-mono">{execution.id}</div>
                    </div>
                    <div>
                      <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Suggested At</label>
                      <div className="text-sm text-gray-900 dark:text-gray-100 mt-1">
                        {new Date(execution.suggestedAt).toLocaleString()}
                      </div>
                    </div>
                    {execution.executedAt && (
                      <>
                        <div>
                          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Executed At</label>
                          <div className="text-sm text-gray-900 dark:text-gray-100 mt-1">
                            {new Date(execution.executedAt).toLocaleString()}
                          </div>
                        </div>
                        <div>
                          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Executed By</label>
                          <div className="text-sm text-gray-900 dark:text-gray-100 mt-1">
                            {execution.executedBy || 'Unknown'}
                          </div>
                        </div>
                      </>
                    )}
                    {execution.rolledBackAt && (
                      <>
                        <div>
                          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Rolled Back At</label>
                          <div className="text-sm text-gray-900 dark:text-gray-100 mt-1">
                            {new Date(execution.rolledBackAt).toLocaleString()}
                          </div>
                        </div>
                        <div>
                          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Rolled Back By</label>
                          <div className="text-sm text-gray-900 dark:text-gray-100 mt-1">
                            {execution.rolledBackBy || 'Unknown'}
                          </div>
                        </div>
                        {execution.rollbackReason && (
                          <div className="md:col-span-2">
                            <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Rollback Reason</label>
                            <div className="text-sm text-gray-900 dark:text-gray-100 mt-1 italic">
                              {execution.rollbackReason}
                            </div>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </div>

                {/* Action Parameters */}
                <div className="border-t pt-4">
                  <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">Action Parameters</h4>
                  <div className="bg-gray-50 dark:bg-gray-700 rounded-lg p-3">
                    <pre className="text-xs text-gray-900 dark:text-gray-100 whitespace-pre-wrap">
                      {JSON.stringify(JSON.parse(execution.actionData), null, 2)}
                    </pre>
                  </div>
                </div>

                {/* Execution Log */}
                {execution.executionLog && (
                  <div className="border-t pt-4">
                    <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">Execution Log</h4>
                    <div className="bg-gray-50 dark:bg-gray-700 rounded-lg p-3">
                      <pre className="text-xs text-gray-900 dark:text-gray-100 whitespace-pre-wrap">
                        {JSON.stringify(JSON.parse(execution.executionLog), null, 2)}
                      </pre>
                    </div>
                  </div>
                )}

                {/* State Information */}
                {(execution.beforeState || execution.afterState) && (
                  <div className="border-t pt-4">
                    <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-3">State Information</h4>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      {execution.beforeState && (
                        <div>
                          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">Before State</label>
                          <div className="bg-gray-50 dark:bg-gray-700 rounded-lg p-3 mt-1">
                            <pre className="text-xs text-gray-900 dark:text-gray-100 whitespace-pre-wrap">
                              {JSON.stringify(JSON.parse(execution.beforeState), null, 2)}
                            </pre>
                          </div>
                        </div>
                      )}
                      {execution.afterState && (
                        <div>
                          <label className="text-sm font-medium text-gray-700 dark:text-gray-300">After State</label>
                          <div className="bg-gray-50 dark:bg-gray-700 rounded-lg p-3 mt-1">
                            <pre className="text-xs text-gray-900 dark:text-gray-100 whitespace-pre-wrap">
                              {JSON.stringify(JSON.parse(execution.afterState), null, 2)}
                            </pre>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                )}
              </div>

              {/* Footer */}
              <div className="flex justify-end mt-6 pt-4 border-t">
                <button
                  onClick={() => setShowDetailsModal(false)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
