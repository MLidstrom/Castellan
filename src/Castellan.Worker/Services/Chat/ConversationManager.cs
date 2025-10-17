using System.Text.Json;
using Castellan.Worker.Models.Chat;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Microsoft.EntityFrameworkCore;

namespace Castellan.Worker.Services.Chat;

/// <summary>
/// Manages chat conversations and message history with database persistence.
/// Provides CRUD operations and lifecycle management for conversations.
/// </summary>
public class ConversationManager : IConversationManager
{
    private readonly IDbContextFactory<CastellanDbContext> _dbContextFactory;
    private readonly ILogger<ConversationManager> _logger;

    public ConversationManager(
        IDbContextFactory<CastellanDbContext> dbContextFactory,
        ILogger<ConversationManager> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<Conversation> CreateConversationAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var conversation = new Conversation
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Title = "New Conversation",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Created conversation {ConversationId} for user {UserId}", conversation.Id, userId);

            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversation for user {UserId}", userId);
            throw;
        }
    }

    public async Task<Conversation?> GetConversationAsync(string conversationId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var conversation = await db.Conversations
                .Include(c => c.Messages.OrderBy(m => m.Timestamp))
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

            if (conversation == null)
            {
                _logger.LogWarning("Conversation {ConversationId} not found", conversationId);
            }

            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public async Task<List<Conversation>> GetConversationsAsync(
        string userId,
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var query = db.Conversations
                .Where(c => c.UserId == userId);

            if (!includeArchived)
            {
                query = query.Where(c => !c.IsArchived);
            }

            var conversations = await query
                .Include(c => c.Messages.OrderBy(m => m.Timestamp))
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync(ct);

            _logger.LogDebug("Retrieved {Count} conversations for user {UserId}", conversations.Count, userId);

            return conversations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversations for user {UserId}", userId);
            throw;
        }
    }

    public async Task AddMessageAsync(string conversationId, ChatMessage message, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var conversation = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            message.ConversationId = conversationId;
            db.ChatMessages.Add(message);

            // Update conversation timestamp
            conversation.UpdatedAt = DateTime.UtcNow;

            // Auto-generate title from first user message
            if (conversation.Title == "New Conversation" && message.Role == MessageRole.User)
            {
                conversation.Title = GenerateConversationTitle(message.Content);
            }

            await db.SaveChangesAsync(ct);

            _logger.LogDebug(
                "Added {Role} message to conversation {ConversationId}",
                message.Role,
                conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add message to conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public async Task UpdateConversationAsync(
        string conversationId,
        string? title = null,
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var conversation = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            if (title != null)
            {
                conversation.Title = title;
            }

            if (tags != null)
            {
                conversation.Tags = tags;
            }

            conversation.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Updated conversation {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public async Task ArchiveConversationAsync(string conversationId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var conversation = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            conversation.IsArchived = true;
            conversation.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Archived conversation {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public async Task DeleteConversationAsync(string conversationId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // No need to load messages - cascade delete will handle them
            var conversation = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            db.Conversations.Remove(conversation);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted conversation {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public async Task RecordFeedbackAsync(
        string conversationId,
        int rating,
        string? comment = null,
        CancellationToken ct = default)
    {
        try
        {
            if (rating < 1 || rating > 5)
            {
                throw new ArgumentException("Rating must be between 1 and 5", nameof(rating));
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var conversation = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found");
            }

            conversation.Rating = rating;
            conversation.FeedbackComment = comment;
            conversation.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Recorded feedback for conversation {ConversationId}: {Rating}/5",
                conversationId,
                rating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record feedback for conversation {ConversationId}", conversationId);
            throw;
        }
    }

    /// <summary>
    /// Adds multiple messages to a conversation in a single transaction.
    /// </summary>
    /// <param name="conversationId">Conversation ID</param>
    /// <param name="messages">Messages to add</param>
    /// <param name="ct">Cancellation token</param>
    public async Task AddMessagesAsync(string conversationId, IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            using var transaction = await db.Database.BeginTransactionAsync(ct);

            try
            {
                var conversation = await db.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

                if (conversation == null)
                {
                    throw new InvalidOperationException($"Conversation {conversationId} not found");
                }

                var messageList = messages.ToList();
                foreach (var message in messageList)
                {
                    message.ConversationId = conversationId;
                    db.ChatMessages.Add(message);

                    // Auto-generate title from first user message
                    if (conversation.Title == "New Conversation" && message.Role == MessageRole.User)
                    {
                        conversation.Title = GenerateConversationTitle(message.Content);
                    }
                }

                // Update conversation timestamp
                conversation.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogDebug(
                    "Added {Count} messages to conversation {ConversationId}",
                    messageList.Count,
                    conversationId);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add messages to conversation {ConversationId}", conversationId);
            throw;
        }
    }

    private string GenerateConversationTitle(string firstMessage)
    {
        // Take first 50 characters of message as title
        var title = firstMessage.Length > 50
            ? firstMessage.Substring(0, 47) + "..."
            : firstMessage;

        return title;
    }
}
