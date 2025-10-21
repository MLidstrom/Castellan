import { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import { format } from 'date-fns';
import { Copy, CheckCircle, Shield, ExternalLink } from 'lucide-react';
import type { ChatMessage as ChatMessageType, Citation, SuggestedAction } from '../../services/chatApi';
import type { ActionExecution } from '../../services/actionsApi';
import { ActionButton } from './ActionButton';

interface ChatMessageProps {
  message: ChatMessageType;
  conversationId?: string;
  actionExecutions?: Map<string, ActionExecution>;
  onActionExecute?: (action: SuggestedAction, messageId: string) => Promise<void>;
  onActionRollback?: (executionId: number, key: string, reason?: string) => Promise<void>;
  onCitationClick?: (citation: Citation) => void;
}

export function ChatMessage({
  message,
  conversationId,
  actionExecutions,
  onActionExecute,
  onActionRollback,
  onCitationClick
}: ChatMessageProps) {
  const [copied, setCopied] = useState(false);
  const isUser = message.role === 'user';

  const handleCopy = async () => {
    await navigator.clipboard.writeText(message.content);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} mb-4`}>
      <div className={`max-w-[80%] ${isUser ? 'order-2' : 'order-1'}`}>
        {/* Message Bubble */}
        <div
          className={`rounded-lg p-4 ${
            isUser
              ? 'bg-blue-600 text-white'
              : 'bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 border border-gray-200 dark:border-gray-700'
          }`}
        >
          {/* Message Header (for assistant messages) */}
          {!isUser && (
            <div className="flex items-center justify-between mb-2 pb-2 border-b border-gray-200 dark:border-gray-700">
              <div className="flex items-center gap-2">
                <Shield className="w-4 h-4 text-blue-600 dark:text-blue-400" />
                <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">CastellanAI</span>
                {message.intent && (
                  <span className="text-xs px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-900 text-blue-800 dark:text-blue-200">
                    {message.intent}
                  </span>
                )}
              </div>
              <button
                onClick={handleCopy}
                className="text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 transition-colors"
                title="Copy message"
              >
                {copied ? (
                  <CheckCircle className="w-4 h-4 text-green-600" />
                ) : (
                  <Copy className="w-4 h-4" />
                )}
              </button>
            </div>
          )}

          {/* Message Content with Markdown */}
          <div className={`prose ${isUser ? 'prose-invert' : 'dark:prose-invert'} prose-sm max-w-none`}>
            <ReactMarkdown
              components={{
                // Custom rendering for links to prevent default link styling
                a: ({ node, ...props }) => (
                  <a
                    {...props}
                    className="text-blue-600 dark:text-blue-400 hover:underline"
                    target="_blank"
                    rel="noopener noreferrer"
                  />
                ),
                // Custom code block styling
                code: ({ node, ...props }: any) => {
                  const inline = !props.className;
                  if (inline) {
                    return (
                      <code
                        {...props}
                        className="px-1.5 py-0.5 rounded bg-gray-100 dark:bg-gray-700 text-gray-800 dark:text-gray-200 font-mono text-sm"
                      />
                    );
                  }
                  return (
                    <code
                      {...props}
                      className="block px-4 py-3 rounded bg-gray-100 dark:bg-gray-900 text-gray-800 dark:text-gray-200 font-mono text-sm overflow-x-auto"
                    />
                  );
                },
              }}
            >
              {message.content}
            </ReactMarkdown>
          </div>

          {/* Copy button for user messages */}
          {isUser && (
            <div className="flex justify-end mt-2 pt-2 border-t border-blue-500">
              <button
                onClick={handleCopy}
                className="text-blue-100 hover:text-white transition-colors text-sm flex items-center gap-1"
                title="Copy message"
              >
                {copied ? (
                  <>
                    <CheckCircle className="w-3 h-3" />
                    <span>Copied</span>
                  </>
                ) : (
                  <>
                    <Copy className="w-3 h-3" />
                    <span>Copy</span>
                  </>
                )}
              </button>
            </div>
          )}
        </div>

        {/* Citations */}
        {message.citations && message.citations.length > 0 && (
          <div className="mt-2 space-y-1">
            <div className="text-xs font-semibold text-gray-600 dark:text-gray-400 mb-1">Sources:</div>
            {message.citations.map((citation, idx) => (
              <button
                key={idx}
                onClick={() => onCitationClick?.(citation)}
                className="w-full text-left p-2 rounded-lg bg-gray-50 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-600 transition-all group"
              >
                <div className="flex items-start gap-2">
                  <ExternalLink className="w-3 h-3 mt-0.5 text-gray-400 group-hover:text-blue-600 dark:group-hover:text-blue-400" />
                  <div className="flex-1 min-w-0">
                    <div className="text-xs font-medium text-gray-900 dark:text-gray-100 truncate">
                      {citation.displayText || citation.sourceId}
                    </div>
                    {citation.url && (
                      <div className="text-xs text-gray-500 dark:text-gray-400 truncate">
                        {citation.url}
                      </div>
                    )}
                  </div>
                  <span className="text-xs text-gray-400">
                    {Math.round(citation.relevance * 100)}%
                  </span>
                </div>
              </button>
            ))}
          </div>
        )}

        {/* Suggested Actions */}
        {message.suggestedActions && message.suggestedActions.length > 0 && conversationId && (
          <div className="mt-3 space-y-2">
            <div className="text-xs font-semibold text-gray-600 dark:text-gray-400 mb-1">Suggested Actions:</div>
            {message.suggestedActions.map((action, idx) => {
              const key = `${message.id}-${action.type}`;
              const execution = actionExecutions?.get(key);

              return (
                <ActionButton
                  key={idx}
                  action={action}
                  execution={execution}
                  conversationId={conversationId}
                  messageId={message.id}
                  onExecute={async (act) => {
                    if (onActionExecute) {
                      await onActionExecute(act, message.id);
                    }
                  }}
                  onRollback={execution && onActionRollback ? async (executionId, reason) => {
                    await onActionRollback(executionId, key, reason);
                  } : undefined}
                />
              );
            })}
          </div>
        )}

        {/* Visualizations */}
        {message.visualizations && message.visualizations.length > 0 && (
          <div className="mt-3 space-y-3">
            {message.visualizations.map((viz, idx) => (
              <div
                key={idx}
                className="p-4 rounded-lg bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700"
              >
                <h4 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-2">
                  {viz.title}
                </h4>
                <div className="text-xs text-gray-600 dark:text-gray-400">
                  Visualization: {viz.type}
                </div>
                {/* Visualization rendering would go here - can be extended based on viz.type */}
                <pre className="mt-2 text-xs bg-gray-50 dark:bg-gray-900 p-2 rounded overflow-x-auto">
                  {JSON.stringify(viz.data, null, 2)}
                </pre>
              </div>
            ))}
          </div>
        )}

        {/* Timestamp */}
        <div className={`text-xs text-gray-500 dark:text-gray-400 mt-1 ${isUser ? 'text-right' : 'text-left'}`}>
          {format(new Date(message.timestamp), 'MMM d, yyyy h:mm a')}
        </div>
      </div>
    </div>
  );
}
