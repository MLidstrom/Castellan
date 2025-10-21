import { API_URL } from './constants';

/**
 * Action execution status enum matching backend ActionStatus
 */
export enum ActionStatus {
  Pending = 'Pending',
  Executed = 'Executed',
  RolledBack = 'RolledBack',
  Failed = 'Failed',
  Expired = 'Expired',
}

/**
 * Action type enum matching backend ActionType
 */
export enum ActionType {
  BlockIP = 'BlockIP',
  IsolateHost = 'IsolateHost',
  QuarantineFile = 'QuarantineFile',
  AddToWatchlist = 'AddToWatchlist',
  CreateTicket = 'CreateTicket',
}

/**
 * Action execution record from backend
 */
export interface ActionExecution {
  id: number;
  conversationId: string;
  chatMessageId: string;
  type: ActionType;
  actionData: string; // JSON string
  status: ActionStatus;
  suggestedAt: string;
  executedAt?: string;
  rolledBackAt?: string;
  executedBy?: string;
  rolledBackBy?: string;
  beforeState?: string;
  afterState?: string;
  rollbackReason?: string;
  executionLog: string; // JSON array of log entries
}

/**
 * Request to suggest a new action
 */
export interface SuggestActionRequest {
  conversationId: string;
  chatMessageId: string;
  type: ActionType;
  actionData: any;
}

/**
 * Request to rollback an action
 */
export interface RollbackActionRequest {
  reason?: string;
}

/**
 * Response for can-rollback check
 */
export interface CanRollbackResponse {
  canRollback: boolean;
  reason: string;
}

/**
 * Action statistics from backend
 */
export interface ActionStatistics {
  totalActions: number;
  pendingActions: number;
  executedActions: number;
  rolledBackActions: number;
  failedActions: number;
  actionsByType: Record<string, number>;
}

/**
 * API service for action execution and rollback operations
 */
export const ActionsAPI = {
  /**
   * Suggests a new action for user review (creates pending ActionExecution)
   */
  async suggestAction(request: SuggestActionRequest): Promise<ActionExecution> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/suggest`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Failed to suggest action: ${response.statusText} - ${errorText}`);
    }

    return response.json();
  },

  /**
   * Executes a pending action
   */
  async executeAction(actionId: number): Promise<ActionExecution> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/${actionId}/execute`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Failed to execute action: ${response.statusText} - ${errorText}`);
    }

    return response.json();
  },

  /**
   * Rolls back an executed action (undo)
   */
  async rollbackAction(actionId: number, request: RollbackActionRequest = {}): Promise<ActionExecution> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/${actionId}/rollback`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Failed to rollback action: ${response.statusText} - ${errorText}`);
    }

    return response.json();
  },

  /**
   * Gets all pending actions for a conversation
   */
  async getPendingActions(conversationId: string): Promise<ActionExecution[]> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/pending?conversationId=${encodeURIComponent(conversationId)}`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to get pending actions: ${response.statusText}`);
    }

    const data = await response.json();
    return Array.isArray(data) ? data : [];
  },

  /**
   * Gets action history for a conversation
   */
  async getActionHistory(conversationId: string): Promise<ActionExecution[]> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/history?conversationId=${encodeURIComponent(conversationId)}`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to get action history: ${response.statusText}`);
    }

    const data = await response.json();
    return Array.isArray(data) ? data : [];
  },

  /**
   * Gets a specific action by ID
   */
  async getAction(actionId: number): Promise<ActionExecution> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/${actionId}`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to get action: ${response.statusText}`);
    }

    return response.json();
  },

  /**
   * Checks if an action can be rolled back
   */
  async canRollback(actionId: number): Promise<CanRollbackResponse> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/${actionId}/can-rollback`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to check rollback status: ${response.statusText}`);
    }

    return response.json();
  },

  /**
   * Gets action execution statistics
   */
  async getStatistics(): Promise<ActionStatistics> {
    const token = localStorage.getItem('auth_token');
    const response = await fetch(`${API_URL}/actions/statistics`, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      throw new Error(`Failed to get action statistics: ${response.statusText}`);
    }

    return response.json();
  },
};
