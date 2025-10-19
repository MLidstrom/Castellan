import { useEffect, useState, useRef } from 'react';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { useAuth } from '../hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { SignalRStatus } from '../components/SignalRStatus';
import { ChatAPI, ChatMessage, ChatRequest, Conversation, Citation, SuggestedAction } from '../services/chatApi';
import { ChatMessage as ChatMessageComponent } from '../components/chat/ChatMessage';
import { ThinkingIndicator } from '../components/chat/ThinkingIndicator';
import { ChatInput } from '../components/chat/ChatInput';
import { SmartSuggestions } from '../components/chat/SmartSuggestions';
import { ConversationSidebar } from '../components/chat/ConversationSidebar';
import { MessageCircle } from 'lucide-react';

export function ChatPage() {
  const { token, loading } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [selectedConversationId, setSelectedConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
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

  const handleActionClick = (action: SuggestedAction) => {
    // TODO: Implement action execution (Week 10)
    console.log('Action clicked:', action);
    alert(`Action "${action.label}" will be implemented in Week 10`);
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
            <SignalRStatus />
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
              onCitationClick={handleCitationClick}
              onActionClick={handleActionClick}
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
    </div>
  );
}
