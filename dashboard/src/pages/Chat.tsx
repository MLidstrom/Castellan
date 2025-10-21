import { useEffect, useState, useRef } from 'react';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { SignalRStatus } from '../components/SignalRStatus';
import { ChatAPI, ChatMessage, ChatRequest, Conversation, Citation, SuggestedAction } from '../services/chatApi';
import { ActionsAPI, ActionExecution, ActionType, SuggestActionRequest } from '../services/actionsApi';
import { ChatMessage as ChatMessageComponent } from '../components/chat/ChatMessage';
import { ThinkingIndicator } from '../components/chat/ThinkingIndicator';
import { ChatInput } from '../components/chat/ChatInput';
import { SmartSuggestions } from '../components/chat/SmartSuggestions';
import { ConversationSidebar } from '../components/chat/ConversationSidebar';
import { ActionHistory } from '../components/chat/ActionHistory';
import { MessageCircle, History } from 'lucide-react';

export function ChatPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [selectedConversationId, setSelectedConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [actionExecutions, setActionExecutions] = useState<Map<string, ActionExecution>>(new Map());
  const [showActionHistory, setShowActionHistory] = useState(false);
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

  const handleDeleteConversation = (id: string) => {
    deleteConversationMutation.mutate(id);
  };

  const handleArchiveConversation = (id: string) => {
    ChatAPI.archiveConversation(id).then(() => {
      queryClient.invalidateQueries({ queryKey: ['chat-conversations'] });
    });
  };

  const handleSendMessage = async (message: string) => {
    if (!message.trim()) return;

    // Add user message to UI immediately
    const userMessage: ChatMessage = {
      id: `msg-${Date.now()}`,
      conversationId: selectedConversationId || 'temp',
      role: 'user',
      content: message,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, userMessage]);

    // Send to backend
    sendMessageMutation.mutate({
      message,
      conversationId: selectedConversationId || undefined,
    });
  };

  const handleCitationClick = (citation: Citation) => {
    if (citation.url) {
      navigate(citation.url);
    }
  };

  // Map action type strings to ActionType enum
  const mapActionType = (actionType: string): ActionType => {
    switch (actionType.toLowerCase()) {
      case 'block_ip':
      case 'blockip':
        return ActionType.BlockIP;
      case 'isolate_host':
      case 'isolatehost':
        return ActionType.IsolateHost;
      case 'quarantine_file':
      case 'quarantinefile':
        return ActionType.QuarantineFile;
      case 'add_to_watchlist':
      case 'addtowatchlist':
        return ActionType.AddToWatchlist;
      case 'create_ticket':
      case 'createticket':
        return ActionType.CreateTicket;
      default:
        throw new Error(`Unknown action type: ${actionType}`);
    }
  };

  // Map action parameters to correct property names for backend
  const mapActionParameters = (actionType: string, parameters: any): any => {
    const lowerType = actionType.toLowerCase();
    
    switch (lowerType) {
      case 'block_ip':
      case 'blockip':
        return {
          IpAddress: parameters.ipAddress || parameters.IpAddress,
          Reason: parameters.reason || parameters.Reason,
          DurationHours: parameters.durationHours || parameters.DurationHours || 0
        };
      
      case 'quarantine_file':
      case 'quarantinefile':
        return {
          FilePath: parameters.filePath || parameters.FilePath,
          Reason: parameters.reason || parameters.Reason,
          FileHash: parameters.fileHash || parameters.FileHash || null,
          EventId: parameters.eventId || parameters.EventId || null,
          YaraRuleName: parameters.yaraRuleName || parameters.YaraRuleName || null
        };
      
      case 'isolate_host':
      case 'isolatehost':
        return {
          Hostname: parameters.hostname || parameters.Hostname,
          Reason: parameters.reason || parameters.Reason,
          DisableAllAdapters: parameters.disableAllAdapters !== undefined ? parameters.disableAllAdapters : parameters.DisableAllAdapters !== undefined ? parameters.DisableAllAdapters : true,
          EventId: parameters.eventId || parameters.EventId || null
        };
      
      case 'add_to_watchlist':
      case 'addtowatchlist':
        return {
          EntityType: parameters.entityType || parameters.EntityType || parameters.targetType || 'IpAddress',
          EntityValue: parameters.entityValue || parameters.EntityValue || parameters.targetValue,
          Severity: parameters.severity || parameters.Severity || 'Medium',
          Reason: parameters.reason || parameters.Reason,
          DurationHours: parameters.durationHours || parameters.DurationHours || 0,
          EventId: parameters.eventId || parameters.EventId || null
        };
      
      case 'create_ticket':
      case 'createticket':
        return {
          Title: parameters.title || parameters.Title,
          Description: parameters.description || parameters.Description,
          Priority: parameters.priority || parameters.Priority || parameters.severity || 'Medium',
          Category: parameters.category || parameters.Category || 'Security Incident',
          AssignedTo: parameters.assignedTo || parameters.AssignedTo || null,
          RelatedEventIds: parameters.relatedEventIds || parameters.RelatedEventIds || [],
          TicketSystem: parameters.ticketSystem || parameters.TicketSystem || null
        };
      
      default:
        // Return parameters as-is for unknown types
        return parameters;
    }
  };

  const handleActionClick = async (action: SuggestedAction, messageId: string) => {
    if (!selectedConversationId) {
      alert('No conversation selected');
      return;
    }

    try {
      // Suggest the action (create pending execution)
      // Map parameters to correct property names for backend
      const mappedActionData = mapActionParameters(action.type, action.parameters || {});
      
      const request: SuggestActionRequest = {
        conversationId: selectedConversationId,
        chatMessageId: messageId,
        type: mapActionType(action.type),
        actionData: mappedActionData,
      };
      const execution = await ActionsAPI.suggestAction(request);

      // Track the execution
      setActionExecutions(prev => {
        const newMap = new Map(prev);
        const key = `${messageId}-${action.type}`;
        newMap.set(key, execution);
        return newMap;
      });

    } catch (error: any) {
      console.error('Failed to suggest action:', error);
      alert(`Failed to suggest action: ${error.message || 'Unknown error'}`);
    }
  };

  const handleExecuteAction = async (action: SuggestedAction, messageId: string) => {
    const key = `${messageId}-${action.type}`;

    // If action already has an executionId (persisted from chat response), use it directly
    // Note: Check for > 0 because 0 is the default/unset value
    if (action.executionId && action.executionId > 0) {
      await executeActionById(action.executionId, key);
      return;
    }

    // Otherwise, check if we've already created an execution in state
    const execution = actionExecutions.get(key);

    if (!execution) {
      // Create the action first, then execute
      await handleActionClick(action, messageId);
      // Wait a bit for the suggestion to complete, then execute
      setTimeout(async () => {
        const updatedExecution = actionExecutions.get(key);
        if (updatedExecution) {
          await executeActionById(updatedExecution.id, key);
        }
      }, 500);
      return;
    }

    await executeActionById(execution.id, key);
  };

  const executeActionById = async (executionId: number, key: string) => {
    try {
      const updatedExecution = await ActionsAPI.executeAction(executionId);

      // Update the execution state
      setActionExecutions(prev => {
        const newMap = new Map(prev);
        newMap.set(key, updatedExecution);
        return newMap;
      });

      console.log('Action executed:', updatedExecution);
    } catch (error: any) {
      console.error('Failed to execute action:', error);
      alert(`Failed to execute action: ${error.message || 'Unknown error'}`);
    }
  };

  const handleExecuteActionById = async (executionId: number) => {
    try {
      await ActionsAPI.executeAction(executionId);
      
      // Refresh action history
      if (selectedConversationId) {
        // The ActionHistory component will automatically refetch due to React Query
      }
    } catch (error: any) {
      console.error('Failed to execute action:', error);
      alert(`Failed to execute action: ${error.message || 'Unknown error'}`);
    }
  };

  const handleRollbackAction = async (executionId: number, key: string, reason?: string) => {
    try {
      const updatedExecution = await ActionsAPI.rollbackAction(executionId, { reason });

      // Update the execution state
      setActionExecutions(prev => {
        const newMap = new Map(prev);
        newMap.set(key, updatedExecution);
        return newMap;
      });

      console.log('Action rolled back:', updatedExecution);
    } catch (error: any) {
      console.error('Failed to rollback action:', error);
      alert(`Failed to rollback action: ${error.message || 'Unknown error'}`);
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
      <ConversationSidebar
        conversations={conversations}
        selectedConversationId={selectedConversationId || undefined}
        onConversationSelect={setSelectedConversationId}
        onNewConversation={() => createConversationMutation.mutate()}
        onDeleteConversation={handleDeleteConversation}
        onArchiveConversation={handleArchiveConversation}
        loading={conversationsQuery.isLoading}
      />

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
            <div className="flex items-center gap-4">
              {selectedConversationId && (
                <button
                  onClick={() => setShowActionHistory(!showActionHistory)}
                  className={`flex items-center gap-2 px-4 py-2 rounded-lg transition-colors ${
                    showActionHistory
                      ? 'bg-blue-600 text-white hover:bg-blue-700'
                      : 'bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600'
                  }`}
                  title="View action history"
                >
                  <History className="w-4 h-4" />
                  <span className="text-sm font-medium">Actions</span>
                </button>
              )}
              <SignalRStatus />
            </div>
          </div>
        </div>

        {/* Smart Suggestions (shown when no conversation selected) */}
        {!selectedConversationId && messages.length === 0 && (
          <SmartSuggestions
            onSuggestionClick={handleSendMessage}
            context="default"
          />
        )}

        {/* Messages Area */}
        <div className="flex-1 overflow-y-auto p-8">
          {!selectedConversationId && messages.length === 0 && (
            <div className="text-center py-16">
              <MessageCircle className="h-16 w-16 mx-auto text-gray-400 dark:text-gray-600 mb-4" />
              <h3 className="text-lg font-medium text-gray-900 dark:text-white mb-2">
                Start a Conversation
              </h3>
              <p className="text-gray-600 dark:text-gray-400">
                Select a suggestion above or type your security question below
              </p>
            </div>
          )}

          {messages.map((message) => (
            <ChatMessageComponent
              key={message.id}
              message={message}
              conversationId={selectedConversationId || undefined}
              actionExecutions={actionExecutions}
              onActionExecute={handleExecuteAction}
              onActionRollback={handleRollbackAction}
              onCitationClick={handleCitationClick}
            />
          ))}

          {sendMessageMutation.isPending && (
            <ThinkingIndicator
              message="CastellanAI is analyzing your security query..."
              showProgress={true}
              estimatedTimeMs={3000}
            />
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input Area */}
        <ChatInput
          onSendMessage={handleSendMessage}
          disabled={sendMessageMutation.isPending}
          loading={sendMessageMutation.isPending}
          placeholder="Ask CastellanAI about security events, threats, compliance, or investigations..."
          maxLength={2000}
        />
      </div>

      {/* Action History Panel */}
            {showActionHistory && selectedConversationId && (
              <ActionHistory
                conversationId={selectedConversationId}
                onClose={() => setShowActionHistory(false)}
                onExecute={handleExecuteActionById}
                onRollback={handleRollbackAction}
              />
            )}
    </div>
  );
}
