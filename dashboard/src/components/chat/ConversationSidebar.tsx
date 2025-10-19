import { useState } from 'react';
import { formatDistanceToNow } from 'date-fns';
import {
  MessageSquare,
  Plus,
  Search,
  Trash2,
  Archive,
  MoreVertical,
  Clock,
  Tag
} from 'lucide-react';
import type { Conversation } from '../../services/chatApi';

interface ConversationSidebarProps {
  conversations: Conversation[];
  selectedConversationId?: string;
  onConversationSelect: (id: string) => void;
  onNewConversation: () => void;
  onDeleteConversation: (id: string) => void;
  onArchiveConversation?: (id: string) => void;
  loading?: boolean;
}

export function ConversationSidebar({
  conversations,
  selectedConversationId,
  onConversationSelect,
  onNewConversation,
  onDeleteConversation,
  onArchiveConversation,
  loading = false
}: ConversationSidebarProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null);

  // Filter conversations by search query
  const filteredConversations = conversations.filter((conv) => {
    if (!searchQuery) return true;
    const query = searchQuery.toLowerCase();
    return (
      conv.title.toLowerCase().includes(query) ||
      conv.tags?.some((tag) => tag.toLowerCase().includes(query))
    );
  });

  const handleDelete = (id: string) => {
    onDeleteConversation(id);
    setDeleteConfirmId(null);
    setOpenMenuId(null);
  };

  const handleArchive = (id: string) => {
    onArchiveConversation?.(id);
    setOpenMenuId(null);
  };

  const toggleMenu = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setOpenMenuId(openMenuId === id ? null : id);
  };

  return (
    <div className="w-80 bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700 flex flex-col h-full">
      {/* Header */}
      <div className="p-4 border-b border-gray-200 dark:border-gray-700">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-lg font-bold text-gray-900 dark:text-gray-100 flex items-center gap-2">
            <MessageSquare className="w-5 h-5" />
            Conversations
          </h2>
          <button
            onClick={onNewConversation}
            className="p-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white transition-colors shadow-sm hover:shadow-md"
            title="New conversation"
          >
            <Plus className="w-5 h-5" />
          </button>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search conversations..."
            className="w-full pl-10 pr-4 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 text-gray-900 dark:text-gray-100 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      {/* Conversations List */}
      <div className="flex-1 overflow-y-auto">
        {loading ? (
          <div className="p-4 text-center text-gray-500 dark:text-gray-400">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto" />
            <p className="mt-2 text-sm">Loading conversations...</p>
          </div>
        ) : filteredConversations.length === 0 ? (
          <div className="p-4 text-center text-gray-500 dark:text-gray-400">
            <MessageSquare className="w-12 h-12 mx-auto mb-2 opacity-50" />
            <p className="text-sm">
              {searchQuery ? 'No conversations found' : 'No conversations yet'}
            </p>
            <p className="text-xs mt-1">
              {searchQuery ? 'Try a different search' : 'Start a new conversation'}
            </p>
          </div>
        ) : (
          <div className="divide-y divide-gray-200 dark:divide-gray-700">
            {filteredConversations.map((conversation) => (
              <div
                key={conversation.id}
                className="relative group"
                onClick={() => {
                  if (deleteConfirmId !== conversation.id) {
                    onConversationSelect(conversation.id);
                  }
                }}
              >
                {/* Delete Confirmation Overlay */}
                {deleteConfirmId === conversation.id && (
                  <div className="absolute inset-0 bg-red-50 dark:bg-red-900/20 z-10 flex items-center justify-center p-4">
                    <div className="text-center">
                      <p className="text-sm font-semibold text-red-800 dark:text-red-200 mb-3">
                        Delete this conversation?
                      </p>
                      <div className="flex gap-2 justify-center">
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            handleDelete(conversation.id);
                          }}
                          className="px-3 py-1.5 bg-red-600 hover:bg-red-700 text-white text-sm rounded-md transition-colors"
                        >
                          Delete
                        </button>
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            setDeleteConfirmId(null);
                          }}
                          className="px-3 py-1.5 bg-gray-200 dark:bg-gray-700 hover:bg-gray-300 dark:hover:bg-gray-600 text-gray-900 dark:text-gray-100 text-sm rounded-md transition-colors"
                        >
                          Cancel
                        </button>
                      </div>
                    </div>
                  </div>
                )}

                {/* Conversation Item */}
                <div
                  className={`p-4 cursor-pointer transition-colors ${
                    selectedConversationId === conversation.id
                      ? 'bg-blue-50 dark:bg-blue-900/20 border-l-4 border-blue-600'
                      : 'hover:bg-gray-50 dark:hover:bg-gray-700 border-l-4 border-transparent'
                  }`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1 min-w-0">
                      {/* Title */}
                      <h3
                        className={`text-sm font-semibold truncate ${
                          selectedConversationId === conversation.id
                            ? 'text-blue-900 dark:text-blue-100'
                            : 'text-gray-900 dark:text-gray-100'
                        }`}
                      >
                        {conversation.title}
                      </h3>

                      {/* Last Message Preview */}
                      {conversation.lastMessage && (
                        <p className="text-xs text-gray-600 dark:text-gray-400 truncate mt-1">
                          {conversation.lastMessage.content}
                        </p>
                      )}

                      {/* Metadata */}
                      <div className="flex items-center gap-2 mt-2 text-xs text-gray-500 dark:text-gray-400">
                        <div className="flex items-center gap-1">
                          <Clock className="w-3 h-3" />
                          <span>
                            {formatDistanceToNow(new Date(conversation.updatedAt), {
                              addSuffix: true
                            })}
                          </span>
                        </div>
                        {conversation.messageCount !== undefined && (
                          <span>• {conversation.messageCount} messages</span>
                        )}
                      </div>

                      {/* Tags */}
                      {conversation.tags && conversation.tags.length > 0 && (
                        <div className="flex items-center gap-1 mt-2">
                          <Tag className="w-3 h-3 text-gray-400" />
                          <div className="flex gap-1 flex-wrap">
                            {conversation.tags.slice(0, 2).map((tag, idx) => (
                              <span
                                key={idx}
                                className="px-1.5 py-0.5 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded text-xs"
                              >
                                {tag}
                              </span>
                            ))}
                            {conversation.tags.length > 2 && (
                              <span className="text-xs text-gray-500">
                                +{conversation.tags.length - 2}
                              </span>
                            )}
                          </div>
                        </div>
                      )}
                    </div>

                    {/* Action Menu */}
                    <div className="relative">
                      <button
                        onClick={(e) => toggleMenu(conversation.id, e)}
                        className="p-1 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors opacity-0 group-hover:opacity-100"
                      >
                        <MoreVertical className="w-4 h-4 text-gray-600 dark:text-gray-400" />
                      </button>

                      {/* Dropdown Menu */}
                      {openMenuId === conversation.id && (
                        <>
                          {/* Backdrop */}
                          <div
                            className="fixed inset-0 z-20"
                            onClick={(e) => {
                              e.stopPropagation();
                              setOpenMenuId(null);
                            }}
                          />

                          {/* Menu */}
                          <div className="absolute right-0 top-6 z-30 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg py-1 min-w-[160px]">
                            {onArchiveConversation && !conversation.isArchived && (
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  handleArchive(conversation.id);
                                }}
                                className="w-full px-4 py-2 text-left text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2"
                              >
                                <Archive className="w-4 h-4" />
                                Archive
                              </button>
                            )}
                            <button
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteConfirmId(conversation.id);
                                setOpenMenuId(null);
                              }}
                              className="w-full px-4 py-2 text-left text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 flex items-center gap-2"
                            >
                              <Trash2 className="w-4 h-4" />
                              Delete
                            </button>
                          </div>
                        </>
                      )}
                    </div>
                  </div>

                  {/* New Badge */}
                  {conversation.isNew && (
                    <div className="absolute top-2 right-2">
                      <span className="px-2 py-0.5 bg-blue-600 text-white text-xs font-semibold rounded-full">
                        New
                      </span>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Footer */}
      <div className="p-3 border-t border-gray-200 dark:border-gray-700 text-xs text-gray-500 dark:text-gray-400 text-center">
        {filteredConversations.length} conversation{filteredConversations.length !== 1 ? 's' : ''}
      </div>
    </div>
  );
}
