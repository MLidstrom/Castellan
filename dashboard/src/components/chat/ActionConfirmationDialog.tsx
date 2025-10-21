import { useState } from 'react';
import { X, AlertTriangle, Info, Loader } from 'lucide-react';
import type { SuggestedAction } from '../../services/chatApi';
import { ActionExecution } from '../../services/actionsApi';

interface ActionConfirmationDialogProps {
  action: SuggestedAction;
  execution?: ActionExecution;
  isExecuting: boolean;
  mode: 'execute' | 'rollback';
  onConfirm: (reason?: string) => void | Promise<void>;
  onCancel: () => void;
}

export function ActionConfirmationDialog({
  action,
  execution,
  isExecuting,
  mode,
  onConfirm,
  onCancel,
}: ActionConfirmationDialogProps) {
  const [rollbackReason, setRollbackReason] = useState('');

  const handleConfirm = () => {
    if (mode === 'rollback') {
      onConfirm(rollbackReason || undefined);
    } else {
      onConfirm();
    }
  };

  const getConfirmationMessage = () => {
    if (mode === 'rollback') {
      return {
        title: 'Confirm Rollback',
        message: 'Are you sure you want to undo this action? This will attempt to reverse the changes made.',
        actionLabel: 'Rollback',
        actionClass: 'bg-orange-600 hover:bg-orange-700',
      };
    }

    const severity = action.parameters?.severity?.toLowerCase();
    const isCritical = severity === 'critical' || severity === 'high';

    return {
      title: 'Confirm Action Execution',
      message: isCritical
        ? 'This is a critical action that will make immediate changes to your system. Please review carefully before proceeding.'
        : 'This action will make changes to your system. You can undo it later if needed.',
      actionLabel: 'Execute',
      actionClass: isCritical
        ? 'bg-red-600 hover:bg-red-700'
        : 'bg-blue-600 hover:bg-blue-700',
    };
  };

  const confirmation = getConfirmationMessage();
  const severity = action.parameters?.severity?.toLowerCase();
  const isCritical = severity === 'critical' || severity === 'high';

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-lg w-full border border-gray-200 dark:border-gray-700">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-gray-200 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100 flex items-center gap-2">
            {isCritical && mode === 'execute' && (
              <AlertTriangle className="w-5 h-5 text-red-600 dark:text-red-400" />
            )}
            {mode === 'rollback' && (
              <Info className="w-5 h-5 text-orange-600 dark:text-orange-400" />
            )}
            {confirmation.title}
          </h3>
          <button
            onClick={onCancel}
            disabled={isExecuting}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 disabled:opacity-50"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-4 space-y-4">
          {/* Warning Message */}
          <div className={`p-3 rounded-lg ${isCritical && mode === 'execute' ? 'bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800' : 'bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800'}`}>
            <p className="text-sm text-gray-700 dark:text-gray-300">
              {confirmation.message}
            </p>
          </div>

          {/* Action Details */}
          <div className="space-y-2">
            <div className="text-sm">
              <span className="font-semibold text-gray-700 dark:text-gray-300">Action:</span>
              <span className="ml-2 text-gray-900 dark:text-gray-100">{action.label}</span>
            </div>
            <div className="text-sm">
              <span className="font-semibold text-gray-700 dark:text-gray-300">Description:</span>
              <span className="ml-2 text-gray-600 dark:text-gray-400">{action.description}</span>
            </div>
            {action.parameters?.severity && (
              <div className="text-sm">
                <span className="font-semibold text-gray-700 dark:text-gray-300">Severity:</span>
                <span className={`ml-2 px-2 py-0.5 rounded text-xs font-medium ${
                  severity === 'critical' ? 'bg-red-100 dark:bg-red-900 text-red-800 dark:text-red-200' :
                  severity === 'high' ? 'bg-orange-100 dark:bg-orange-900 text-orange-800 dark:text-orange-200' :
                  severity === 'medium' ? 'bg-yellow-100 dark:bg-yellow-900 text-yellow-800 dark:text-yellow-200' :
                  'bg-green-100 dark:bg-green-900 text-green-800 dark:text-green-200'
                }`}>
                  {action.parameters.severity}
                </span>
              </div>
            )}
            {action.confidence !== undefined && (
              <div className="text-sm">
                <span className="font-semibold text-gray-700 dark:text-gray-300">Confidence:</span>
                <span className="ml-2 text-gray-600 dark:text-gray-400">
                  {Math.round(action.confidence * 100)}%
                </span>
              </div>
            )}
          </div>

          {/* Parameters Display */}
          {action.parameters && Object.keys(action.parameters).length > 0 && (
            <div className="space-y-2">
              <div className="text-sm font-semibold text-gray-700 dark:text-gray-300">Parameters:</div>
              <div className="bg-gray-50 dark:bg-gray-900 rounded p-3 space-y-1">
                {Object.entries(action.parameters).map(([key, value]) => (
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

          {/* Rollback Reason Input */}
          {mode === 'rollback' && (
            <div className="space-y-2">
              <label className="text-sm font-semibold text-gray-700 dark:text-gray-300">
                Reason for Rollback (Optional):
              </label>
              <textarea
                value={rollbackReason}
                onChange={(e) => setRollbackReason(e.target.value)}
                disabled={isExecuting}
                placeholder="Enter a reason for rolling back this action..."
                rows={3}
                className="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100 placeholder-gray-400 dark:placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
              />
            </div>
          )}

          {/* Execution Info for Rollback */}
          {mode === 'rollback' && execution && execution.executedAt && (
            <div className="text-xs text-gray-500 dark:text-gray-400 space-y-0.5 border-t border-gray-200 dark:border-gray-700 pt-3">
              <div>
                <span className="font-medium">Originally executed by:</span> {execution.executedBy || 'Unknown'}
              </div>
              <div>
                <span className="font-medium">Executed at:</span>{' '}
                {new Date(execution.executedAt).toLocaleString()}
              </div>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="flex justify-end gap-3 p-4 border-t border-gray-200 dark:border-gray-700">
          <button
            onClick={onCancel}
            disabled={isExecuting}
            className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleConfirm}
            disabled={isExecuting}
            className={`px-4 py-2 text-sm font-medium text-white rounded-md disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2 ${confirmation.actionClass}`}
          >
            {isExecuting ? (
              <>
                <Loader className="w-4 h-4 animate-spin" />
                <span>Processing...</span>
              </>
            ) : (
              <span>{confirmation.actionLabel}</span>
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
