import { API_URL } from './constants';

export interface ChatMessage {
  id: string;
  conversationId: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  intent?: string;
  citations?: Citation[];
  suggestedActions?: SuggestedAction[];
  visualizations?: Visualization[];
}

export interface Citation {
  type: string;
  sourceId: string;
  displayText: string;
  url?: string;
  relevance: number;
}

export interface SuggestedAction {
  type: string;
  label: string;
  description: string;
  parameters: Record<string, any>;
  icon?: string;
  confidence: number;
  executionId?: number; // ID of persisted ActionExecution record
}

export interface Visualization {
  type: string;
  title: string;
  data: any;
  config: Record<string, any>;
}

export interface Conversation {
  id: string;
  userId: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  isArchived: boolean;
  tags?: string[];
  rating?: number;
  feedbackComment?: string;
  messages?: ChatMessage[];
  messageCount?: number;
  lastMessage?: ChatMessage;
  isNew?: boolean;
}

export interface ChatRequest {
  message: string;
  conversationId?: string;
  contextOptions?: {
    timeRange?: {
      start: string;
      end: string;
    };
    maxSimilarEvents?: number;
    maxRecentCriticalEvents?: number;
    includeCorrelationPatterns?: boolean;
    includeSystemMetrics?: boolean;
    minSimilarityScore?: number;
  };
  includeVisualizations?: boolean;
  includeSuggestedActions?: boolean;
  maxCitations?: number;
}

export interface ChatResponse {
  message: ChatMessage;
  conversationId: string;
  conversationTitle: string;
  intent?: {
    type: string;
    confidence: number;
  };
  context?: any;
  suggestedFollowUps: string[];
  isComplete: boolean;
  error?: string;
  metrics?: {
    totalMs: number;
    intentClassificationMs: number;
    contextRetrievalMs: number;
    llmGenerationMs: number;
    eventsRetrieved: number;
    tokensUsed: number;
    modelUsed: string;
  };
  success: boolean;
}

export const ChatAPI = {
  async sendMessage(request: ChatRequest): Promise<ChatResponse> {
    const token = localStorage.getItem('auth_token');

    // Create abort controller for timeout
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 120000); // 2 minute timeout

    try {
      const response = await fetch(`${API_URL}/chat/message`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify(request),
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to send message: ${response.statusText} - ${errorText}`);
      }

      const data = await response.json();
      return data;
    } catch (error: any) {
      clearTimeout(timeoutId);
      if (error.name === 'AbortError') {
        throw new Error('Request timeout - the AI is taking too long to respond. Please try again.');
      }
      throw error;
    }
  },

  async getConversations(): Promise<Conversation[]> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/chat/conversations`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to get conversations: ${response.statusText}`);
    }

    const data = await response.json();
    return Array.isArray(data) ? data : data.data || [];
  },

  async getConversation(id: string): Promise<Conversation> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/chat/conversations/${id}`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to get conversation: ${response.statusText}`);
    }

    const conversation = await response.json();
    // Backend returns Conversation object with Messages array
    return conversation;
  },

  async createConversation(): Promise<Conversation> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/chat/conversations`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify({ title: 'New Conversation' }),
    });

    if (!response.ok) {
      throw new Error(`Failed to create conversation: ${response.statusText}`);
    }

    return response.json();
  },

  async deleteConversation(id: string): Promise<void> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/chat/conversations/${id}`, {
      method: 'DELETE',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to delete conversation: ${response.statusText}`);
    }
  },

  async archiveConversation(id: string): Promise<void> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/chat/conversations/${id}/archive`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to archive conversation: ${response.statusText}`);
    }
  },

  async getSuggestedFollowups(conversationId: string): Promise<string[]> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/chat/conversations/${conversationId}/suggested-followups`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to get suggested followups: ${response.statusText}`);
    }

    const data = await response.json();
    return Array.isArray(data) ? data : data.suggestions || [];
  },

  async submitFeedback(conversationId: string, rating: number, comment?: string): Promise<void> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/chat/conversations/${conversationId}/feedback`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify({ rating, comment }),
    });

    if (!response.ok) {
      throw new Error(`Failed to submit feedback: ${response.statusText}`);
    }
  },
};
