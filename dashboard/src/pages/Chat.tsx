import { useEffect, useState, useRef } from 'react';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { SignalRStatus } from '../components/SignalRStatus';
import { LoadingSpinner } from '../components/LoadingSpinner';
import { ChatAPI, ChatMessage, ChatRequest, Conversation } from '../services/chatApi';
import { MessageCircle, Plus, Send, Trash2 } from 'lucide-react';
import ReactMarkdown from 'react-markdown';

export function ChatPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [selectedConversationId, setSelectedConversationId] = useState<string | null>(null);
  const [inputMessage, setInputMessage] = useState('');
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [hoveredConversationId, setHoveredConversationId] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!loading && !token) {
      navigate('/login');
    }
  }, [token, loading, navigate]);

  // Fetch conversations list
  const conversationsQuery = useQuery({
    queryKey: ['chat-conversations'],
    queryFn: () => ChatAPI.getConversations(),
    enabled: !loading && !!token,
    retry: 2,
    retryDelay: 1000,
    staleTime: 30000, // 30 seconds
  });

  // Fetch messages for selected conversation
  const conversationQuery = useQuery({
    queryKey: ['chat-conversation', selectedConversationId],
    queryFn: () => ChatAPI.getConversation(selectedConversationId!),
    enabled: !loading && !!token && !!selectedConversationId,
    staleTime: 0, // Always refetch when conversation is selected
    gcTime: 0, // Don't cache conversation data
  });

  // Update messages when conversation data loads
  useEffect(() => {
    if (conversationQuery.data?.messages) {
      setMessages(conversationQuery.data.messages);
    } else if (!selectedConversationId) {
      setMessages([]);
    }
  }, [conversationQuery.data, selectedConversationId]);

  // Auto-scroll to bottom when new messages arrive
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // Send message mutation
  const sendMessageMutation = useMutation({
    mutationFn: (request: ChatRequest) => ChatAPI.sendMessage(request),
    onSuccess: (response) => {
      // Add assistant response to messages
      // The backend returns the full ChatMessage object in response.message
      const assistantMessage: ChatMessage = {
        ...response.message,
        id: response.message.id || `msg-${Date.now()}`,
      };
      setMessages((prev) => [...prev, assistantMessage]);

      // Update conversation ID if new conversation
      if (!selectedConversationId) {
        setSelectedConversationId(response.conversationId);
      }

      // Refresh conversations list and current conversation
      queryClient.invalidateQueries({ queryKey: ['chat-conversations'] });
      queryClient.invalidateQueries({ queryKey: ['chat-conversation', selectedConversationId || response.conversationId] });
    },
    onError: (error: any) => {
      console.error('Failed to send message:', error);
      // Show error to user
      alert(`Failed to send message: ${error.message || 'Unknown error'}`);
      // Remove the optimistic user message on error
      setMessages((prev) => prev.slice(0, -1));
    },
  });

  // Create new conversation
  const createConversationMutation = useMutation({
    mutationFn: () => ChatAPI.createConversation(),
    onSuccess: (conversation) => {
      setSelectedConversationId(conversation.id);
      setMessages([]);
      queryClient.invalidateQueries({ queryKey: ['chat-conversations'] });
    },
  });

  // Delete conversation
  const deleteConversationMutation = useMutation({
    mutationFn: (id: string) => ChatAPI.deleteConversation(id),
    onMutate: async (deletedId) => {
      // Cancel any outgoing refetches
      await queryClient.cancelQueries({ queryKey: ['chat-conversations'] });

      // Snapshot the previous value
      const previousConversations = queryClient.getQueryData(['chat-conversations']);

      // Optimistically update - remove from cache immediately
      queryClient.setQueryData(['chat-conversations'], (old: Conversation[] | undefined) => {
        return old ? old.filter(c => c.id !== deletedId) : [];
      });

      // If deleted conversation was selected, clear selection
      if (selectedConversationId === deletedId) {
        setSelectedConversationId(null);
        setMessages([]);
      }

      // Return context with previous data for rollback
      return { previousConversations };
    },
    onError: (error: any, _deletedId, context) => {
      // Rollback to previous state on error
      if (context?.previousConversations) {
        queryClient.setQueryData(['chat-conversations'], context.previousConversations);
      }
      console.error('Failed to delete conversation:', error);
      alert(`Failed to delete conversation: ${error.message || 'Unknown error'}`);
    },
    onSettled: () => {
      // Refetch to ensure server state is in sync (but only after optimistic update is done)
      queryClient.invalidateQueries({ queryKey: ['chat-conversations'] });
    },
  });

  const handleDeleteConversation = (e: React.MouseEvent, id: string) => {
    e.stopPropagation(); // Prevent conversation selection when clicking delete
    if (confirm('Are you sure you want to delete this conversation?')) {
      deleteConversationMutation.mutate(id);
    }
  };

  const handleSendMessage = async () => {
    if (!inputMessage.trim()) return;

    // Add user message to UI immediately
    const userMessage: ChatMessage = {
      id: `msg-${Date.now()}`,
      conversationId: selectedConversationId || 'temp',
      role: 'user',
      content: inputMessage,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, userMessage]);

    const messageToSend = inputMessage;
    setInputMessage('');

    // Send to backend
    sendMessageMutation.mutate({
      message: messageToSend,
      conversationId: selectedConversationId || undefined,
    });
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && e.ctrlKey) {
      handleSendMessage();
    }
  };

  const conversations = conversationsQuery.data || [];
  const selectedConversation = conversations.find((c) => c.id === selectedConversationId);

  // Don't block the entire UI for conversations loading - show input field anyway
  // if (conversationsQuery.isLoading) {
  //   return <LoadingSpinner />;
  // }

  return (
    <div className="h-screen bg-gray-50 dark:bg-gray-900 flex overflow-hidden">
      {/* Sidebar - Conversations List */}
      <div className="w-80 bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700 flex flex-col h-full">
        <div className="p-4 border-b border-gray-200 dark:border-gray-700">
          <button
            onClick={() => createConversationMutation.mutate()}
            disabled={createConversationMutation.isPending}
            className="w-full flex items-center justify-center space-x-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50"
          >
            <Plus className="h-5 w-5" />
            <span className="font-medium">New Conversation</span>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto">
          {conversationsQuery.isLoading ? (
            <div className="p-4 text-center text-gray-500 dark:text-gray-400">
              <LoadingSpinner />
              <div className="text-sm mt-2">Loading conversations...</div>
            </div>
          ) : conversations.length === 0 ? (
            <div className="p-8 text-center">
              <MessageCircle className="h-12 w-12 mx-auto text-gray-400 dark:text-gray-600 mb-3" />
              <p className="text-sm text-gray-600 dark:text-gray-400">No conversations yet</p>
              <p className="text-xs text-gray-500 dark:text-gray-500 mt-1">Start a new conversation to begin</p>
            </div>
          ) : (
            <div className="divide-y divide-gray-200 dark:divide-gray-700">
              {conversations.map((conversation) => (
                <div
                  key={conversation.id}
                  className="relative group"
                  onMouseEnter={() => setHoveredConversationId(conversation.id)}
                  onMouseLeave={() => setHoveredConversationId(null)}
                >
                  <div
                    className={`w-full p-4 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors ${
                      selectedConversationId === conversation.id
                        ? 'bg-blue-50 dark:bg-blue-900/20 border-l-4 border-blue-600'
                        : ''
                    }`}
                  >
                    <div className="flex items-start justify-between">
                      <div
                        className="flex-1 min-w-0"
                        onClick={() => setSelectedConversationId(conversation.id)}
                      >
                        <div className="font-medium text-gray-900 dark:text-white truncate">
                          {conversation.title}
                        </div>
                        <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                          {new Date(conversation.updatedAt).toLocaleDateString()}
                        </div>
                      </div>
                      {hoveredConversationId === conversation.id && (
                        <button
                          onClick={(e) => handleDeleteConversation(e, conversation.id)}
                          className="ml-2 p-1.5 text-gray-400 hover:text-red-600 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 rounded transition-colors flex-shrink-0"
                          title="Delete conversation"
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Main Chat Area */}
      <div className="flex-1 flex flex-col h-full">
        {/* Header */}
        <div className="flex-shrink-0 bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
          <div className="px-8 py-6 flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-900 dark:text-white">
                {selectedConversation?.title || 'CastellanAI Chat'}
              </h1>
              <p className="text-gray-600 dark:text-gray-400 mt-1">
                Ask questions about your security events
              </p>
            </div>
            <SignalRStatus />
          </div>
        </div>

        {/* Messages Area */}
        <div className="flex-1 overflow-y-auto p-8 space-y-6">
          {!selectedConversationId && messages.length === 0 && (
            <div className="text-center py-16">
              <MessageCircle className="h-16 w-16 mx-auto text-gray-400 dark:text-gray-600 mb-4" />
              <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">
                Start a Conversation
              </h3>
              <p className="text-gray-600 dark:text-gray-400 mb-6">
                Ask me about security events, threats, or compliance
              </p>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 max-w-2xl mx-auto">
                {[
                  'Show critical events from last hour',
                  'Summarize today\'s threats',
                  'Check compliance status',
                  'Find failed login attempts',
                ].map((suggestion) => (
                  <button
                    key={suggestion}
                    onClick={() => setInputMessage(suggestion)}
                    className="px-4 py-3 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors text-left"
                  >
                    {suggestion}
                  </button>
                ))}
              </div>
            </div>
          )}

          {messages.map((message) => (
            <div
              key={message.id}
              className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}
            >
              <div
                className={`max-w-3xl rounded-lg p-4 ${
                  message.role === 'user'
                    ? 'bg-blue-600 text-white'
                    : 'bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 text-gray-900 dark:text-white'
                }`}
              >
                <div className={`prose prose-sm max-w-none ${
                  message.role === 'user'
                    ? 'prose-invert'
                    : 'dark:prose-invert'
                }`}>
                  <ReactMarkdown
                    components={{
                      ul: (props) => <ul className="list-disc pl-6 my-2 space-y-1" {...props} />,
                      ol: (props) => <ol className="list-decimal pl-6 my-2 space-y-1" {...props} />,
                      li: (props) => <li className="ml-2" {...props} />,
                      p: (props) => <p className="my-2" {...props} />,
                      h1: (props) => <h1 className="text-xl font-bold mt-4 mb-2" {...props} />,
                      h2: (props) => <h2 className="text-lg font-bold mt-3 mb-2" {...props} />,
                      h3: (props) => <h3 className="text-base font-bold mt-2 mb-1" {...props} />,
                      strong: (props) => <strong className="font-bold" {...props} />,
                      em: (props) => <em className="italic" {...props} />,
                      code: (props: any) => {
                        const { inline, className, children, ...rest } = props;
                        return inline ? (
                          <code className="bg-gray-100 dark:bg-gray-700 px-1 py-0.5 rounded text-sm" {...rest}>
                            {children}
                          </code>
                        ) : (
                          <code className="block bg-gray-100 dark:bg-gray-700 p-2 rounded my-2 text-sm" {...rest}>
                            {children}
                          </code>
                        );
                      }
                    }}
                  >
                    {message.content}
                  </ReactMarkdown>
                </div>
                {message.citations && message.citations.length > 0 && (
                  <div className="mt-3 pt-3 border-t border-gray-200 dark:border-gray-700">
                    <div className="text-xs font-medium mb-2">References:</div>
                    {message.citations.map((citation, idx) => (
                      <div key={idx} className="text-xs mb-1">
                        {citation.url ? (
                          <a
                            href={citation.url}
                            className="text-blue-600 dark:text-blue-400 hover:underline cursor-pointer"
                          >
                            {citation.displayText}
                          </a>
                        ) : (
                          <span className="text-gray-700 dark:text-gray-300">{citation.displayText}</span>
                        )}
                        <span className="text-gray-500 dark:text-gray-400 ml-2">
                          ({Math.round(citation.relevance * 100)}% relevant)
                        </span>
                      </div>
                    ))}
                  </div>
                )}
                {message.suggestedActions && message.suggestedActions.length > 0 && (
                  <div className="mt-3 pt-3 border-t border-gray-200 dark:border-gray-700">
                    <div className="text-xs font-medium mb-2">Suggested Actions:</div>
                    <div className="flex flex-wrap gap-2">
                      {message.suggestedActions.map((action, idx) => (
                        <button
                          key={idx}
                          className="px-3 py-1 bg-blue-100 dark:bg-blue-900/20 text-blue-700 dark:text-blue-300 rounded text-xs font-medium hover:bg-blue-200 dark:hover:bg-blue-900/40 transition-colors"
                        >
                          {action.label}
                        </button>
                      ))}
                    </div>
                  </div>
                )}
                <div className="text-xs opacity-70 mt-2">
                  {new Date(message.timestamp).toLocaleTimeString()}
                </div>
              </div>
            </div>
          ))}

          {sendMessageMutation.isPending && (
            <div className="flex justify-start">
              <div className="max-w-3xl rounded-lg p-4 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700">
                <div className="flex items-center space-x-2">
                  <div className="flex space-x-1">
                    <div className="h-2 w-2 bg-blue-600 rounded-full animate-bounce" style={{ animationDelay: '0ms' }}></div>
                    <div className="h-2 w-2 bg-blue-600 rounded-full animate-bounce" style={{ animationDelay: '150ms' }}></div>
                    <div className="h-2 w-2 bg-blue-600 rounded-full animate-bounce" style={{ animationDelay: '300ms' }}></div>
                  </div>
                  <span className="text-sm text-gray-600 dark:text-gray-400">CastellanAI is thinking...</span>
                </div>
              </div>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input Area */}
        <div className="flex-shrink-0 border-t border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-4">
          <div className="flex items-end space-x-3">
            <textarea
              value={inputMessage}
              onChange={(e) => setInputMessage(e.target.value)}
              onKeyDown={handleKeyPress}
              placeholder="Ask about security events, threats, or compliance... (Ctrl+Enter to send)"
              className="flex-1 resize-none rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 px-4 py-3 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 min-h-[60px] max-h-[200px]"
              rows={2}
            />
            <button
              onClick={handleSendMessage}
              disabled={!inputMessage.trim() || sendMessageMutation.isPending}
              className="flex items-center space-x-2 px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Send className="h-5 w-5" />
              <span className="font-medium">Send</span>
            </button>
          </div>
          <div className="text-xs text-gray-500 dark:text-gray-400 mt-2">
            Tip: Press Ctrl+Enter to send your message
          </div>
        </div>
      </div>
    </div>
  );
}
