using System.Diagnostics.Eventing.Reader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Castellan.Worker.Models;

namespace Castellan.Worker.Infrastructure;

/// <summary>
/// Database implementation of IEventBookmarkStore using Entity Framework Core
/// </summary>
public class DatabaseEventBookmarkStore : IEventBookmarkStore
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<DatabaseEventBookmarkStore> _logger;

    public DatabaseEventBookmarkStore(CastellanDbContext context, ILogger<DatabaseEventBookmarkStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Load the bookmark for a specific channel from the database
    /// </summary>
    public async Task<EventBookmark?> LoadAsync(string channelName)
    {
        try
        {
            var bookmarkEntity = await _context.EventLogBookmarks
                .FirstOrDefaultAsync(b => b.ChannelName == channelName);

            if (bookmarkEntity == null)
            {
                _logger.LogDebug("No bookmark found for channel: {ChannelName}", channelName);
                return null;
            }

            // For now, we'll store the bookmark data as-is and return null
            // This is a simplified approach - in production, you'd need to implement
            // proper EventBookmark serialization or use a different bookmarking strategy
            _logger.LogDebug("Bookmark data found for channel: {ChannelName}, last updated: {LastUpdated}", 
                channelName, bookmarkEntity.UpdatedAt);
            
            return null; // Simplified implementation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading bookmark for channel: {ChannelName}", channelName);
            return null;
        }
    }

    /// <summary>
    /// Save a bookmark for a specific channel to the database
    /// </summary>
    public async Task SaveAsync(string channelName, EventBookmark bookmark)
    {
        try
        {
            // For now, we'll store a placeholder - in production, you'd need to implement
            // proper EventBookmark serialization or use a different bookmarking strategy
            var bookmarkBytes = System.Text.Encoding.UTF8.GetBytes("placeholder_bookmark_data");

            var existingBookmark = await _context.EventLogBookmarks
                .FirstOrDefaultAsync(b => b.ChannelName == channelName);

            if (existingBookmark != null)
            {
                // Update existing bookmark
                existingBookmark.BookmarkData = bookmarkBytes;
                existingBookmark.UpdatedAt = DateTime.UtcNow;
                _context.EventLogBookmarks.Update(existingBookmark);
            }
            else
            {
                // Create new bookmark
                var newBookmark = new EventLogBookmarkEntity
                {
                    ChannelName = channelName,
                    BookmarkData = bookmarkBytes,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.EventLogBookmarks.Add(newBookmark);
            }

            await _context.SaveChangesAsync();
            _logger.LogDebug("Saved bookmark for channel: {ChannelName}", channelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving bookmark for channel: {ChannelName}", channelName);
            throw;
        }
    }

    /// <summary>
    /// Delete a bookmark for a specific channel from the database
    /// </summary>
    public async Task DeleteAsync(string channelName)
    {
        try
        {
            var bookmark = await _context.EventLogBookmarks
                .FirstOrDefaultAsync(b => b.ChannelName == channelName);

            if (bookmark != null)
            {
                _context.EventLogBookmarks.Remove(bookmark);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted bookmark for channel: {ChannelName}", channelName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bookmark for channel: {ChannelName}", channelName);
            throw;
        }
    }

    /// <summary>
    /// Check if a bookmark exists for a specific channel
    /// </summary>
    public async Task<bool> ExistsAsync(string channelName)
    {
        try
        {
            return await _context.EventLogBookmarks
                .AnyAsync(b => b.ChannelName == channelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if bookmark exists for channel: {ChannelName}", channelName);
            return false;
        }
    }

    /// <summary>
    /// Get the timestamp of when the bookmark was last updated
    /// </summary>
    public async Task<DateTime?> GetLastUpdatedAsync(string channelName)
    {
        try
        {
            var bookmark = await _context.EventLogBookmarks
                .Where(b => b.ChannelName == channelName)
                .Select(b => b.UpdatedAt)
                .FirstOrDefaultAsync();

            return bookmark;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last updated timestamp for channel: {ChannelName}", channelName);
            return null;
        }
    }
}

