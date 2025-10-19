import { useState, useRef, useEffect, KeyboardEvent, ChangeEvent } from 'react';
import { Send, Loader2, AlertCircle } from 'lucide-react';

interface ChatInputProps {
  onSendMessage: (message: string) => void;
  disabled?: boolean;
  loading?: boolean;
  placeholder?: string;
  maxLength?: number;
  suggestions?: string[];
  onSuggestionClick?: (suggestion: string) => void;
}

export function ChatInput({
  onSendMessage,
  disabled = false,
  loading = false,
  placeholder = 'Ask CastellanAI about security events, threats, or investigations...',
  maxLength = 2000,
  suggestions = [],
  onSuggestionClick
}: ChatInputProps) {
  const [message, setMessage] = useState('');
  const [showSuggestions, setShowSuggestions] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const inputContainerRef = useRef<HTMLDivElement>(null);

  // Auto-resize textarea as user types
  useEffect(() => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    // Reset height to auto to get the correct scrollHeight
    textarea.style.height = 'auto';

    // Set height based on content, with min and max constraints
    const newHeight = Math.min(Math.max(textarea.scrollHeight, 60), 200);
    textarea.style.height = `${newHeight}px`;
  }, [message]);

  // Focus on mount
  useEffect(() => {
    textareaRef.current?.focus();
  }, []);

  const handleSend = () => {
    const trimmedMessage = message.trim();
    if (!trimmedMessage || disabled || loading) return;

    onSendMessage(trimmedMessage);
    setMessage('');
    setShowSuggestions(false);

    // Reset textarea height
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
    }
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    // Send on Ctrl+Enter or Cmd+Enter
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      e.preventDefault();
      handleSend();
    }
    // Allow Enter for new line without Ctrl/Cmd
  };

  const handleChange = (e: ChangeEvent<HTMLTextAreaElement>) => {
    const newValue = e.target.value;
    if (newValue.length <= maxLength) {
      setMessage(newValue);

      // Show suggestions when user starts typing
      if (newValue.length > 0 && suggestions.length > 0) {
        setShowSuggestions(true);
      } else {
        setShowSuggestions(false);
      }
    }
  };

  const handleSuggestionSelect = (suggestion: string) => {
    setMessage(suggestion);
    setShowSuggestions(false);
    onSuggestionClick?.(suggestion);
    textareaRef.current?.focus();
  };

  const characterCount = message.length;
  const isNearLimit = characterCount > maxLength * 0.8;
  const isAtLimit = characterCount >= maxLength;

  return (
    <div className="relative" ref={inputContainerRef}>
      {/* Suggestions Dropdown */}
      {showSuggestions && suggestions.length > 0 && (
        <div className="absolute bottom-full left-0 right-0 mb-2 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg max-h-48 overflow-y-auto z-10">
          <div className="p-2">
            <div className="text-xs font-semibold text-gray-600 dark:text-gray-400 px-2 py-1">
              Suggestions
            </div>
            {suggestions.map((suggestion, idx) => (
              <button
                key={idx}
                onClick={() => handleSuggestionSelect(suggestion)}
                className="w-full text-left px-3 py-2 rounded-md hover:bg-gray-100 dark:hover:bg-gray-700 text-sm text-gray-900 dark:text-gray-100 transition-colors"
              >
                {suggestion}
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Input Container */}
      <div className="flex items-end gap-2 p-4 bg-white dark:bg-gray-800 border-t border-gray-200 dark:border-gray-700">
        <div className="flex-1 relative">
          {/* Textarea */}
          <textarea
            ref={textareaRef}
            value={message}
            onChange={handleChange}
            onKeyDown={handleKeyDown}
            placeholder={placeholder}
            disabled={disabled || loading}
            className={`w-full px-4 py-3 pr-16 rounded-lg border resize-none focus:outline-none focus:ring-2 focus:ring-blue-500 dark:focus:ring-blue-400 transition-all ${
              disabled || loading
                ? 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400 cursor-not-allowed'
                : 'bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100 border-gray-300 dark:border-gray-600'
            } ${
              isAtLimit
                ? 'border-red-500 dark:border-red-400'
                : ''
            }`}
            rows={1}
            style={{
              minHeight: '60px',
              maxHeight: '200px'
            }}
          />

          {/* Character Count */}
          <div
            className={`absolute bottom-2 right-2 text-xs ${
              isAtLimit
                ? 'text-red-600 dark:text-red-400 font-semibold'
                : isNearLimit
                ? 'text-orange-600 dark:text-orange-400'
                : 'text-gray-400 dark:text-gray-500'
            }`}
          >
            {characterCount} / {maxLength}
          </div>
        </div>

        {/* Send Button */}
        <button
          onClick={handleSend}
          disabled={!message.trim() || disabled || loading || isAtLimit}
          className={`flex items-center justify-center w-12 h-12 rounded-lg transition-all ${
            !message.trim() || disabled || loading || isAtLimit
              ? 'bg-gray-200 dark:bg-gray-700 text-gray-400 dark:text-gray-500 cursor-not-allowed'
              : 'bg-blue-600 hover:bg-blue-700 text-white shadow-md hover:shadow-lg transform hover:scale-105'
          }`}
          title="Send message (Ctrl+Enter)"
        >
          {loading ? (
            <Loader2 className="w-5 h-5 animate-spin" />
          ) : (
            <Send className="w-5 h-5" />
          )}
        </button>
      </div>

      {/* Helper Text */}
      <div className="px-4 pb-2 flex items-center justify-between text-xs text-gray-500 dark:text-gray-400">
        <div className="flex items-center gap-1">
          <span>Press</span>
          <kbd className="px-1.5 py-0.5 bg-gray-100 dark:bg-gray-700 rounded border border-gray-300 dark:border-gray-600 font-mono">
            Ctrl
          </kbd>
          <span>+</span>
          <kbd className="px-1.5 py-0.5 bg-gray-100 dark:bg-gray-700 rounded border border-gray-300 dark:border-gray-600 font-mono">
            Enter
          </kbd>
          <span>to send</span>
        </div>

        {isAtLimit && (
          <div className="flex items-center gap-1 text-red-600 dark:text-red-400">
            <AlertCircle className="w-3 h-3" />
            <span>Character limit reached</span>
          </div>
        )}
      </div>
    </div>
  );
}
