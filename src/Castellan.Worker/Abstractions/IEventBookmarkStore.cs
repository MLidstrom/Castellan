using System.Diagnostics.Eventing.Reader;

namespace Castellan.Worker.Abstractions;

/// <summary>
/// Interface for persisting and retrieving EventLog bookmarks to enable resuming from the last processed event
/// </summary>
public interface IEventBookmarkStore
{
    /// <summary>
    /// Load the bookmark for a specific channel
    /// </summary>
    /// <param name="channelName">The name of the event log channel</param>
    /// <returns>The bookmark for the channel, or null if no bookmark exists</returns>
    Task<EventBookmark?> LoadAsync(string channelName);

    /// <summary>
    /// Save a bookmark for a specific channel
    /// </summary>
    /// <param name="channelName">The name of the event log channel</param>
    /// <param name="bookmark">The bookmark to save</param>
    /// <returns>A task representing the save operation</returns>
    Task SaveAsync(string channelName, EventBookmark bookmark);

    /// <summary>
    /// Delete a bookmark for a specific channel
    /// </summary>
    /// <param name="channelName">The name of the event log channel</param>
    /// <returns>A task representing the delete operation</returns>
    Task DeleteAsync(string channelName);

    /// <summary>
    /// Check if a bookmark exists for a specific channel
    /// </summary>
    /// <param name="channelName">The name of the event log channel</param>
    /// <returns>True if a bookmark exists, false otherwise</returns>
    Task<bool> ExistsAsync(string channelName);

    /// <summary>
    /// Get the timestamp of when the bookmark was last updated
    /// </summary>
    /// <param name="channelName">The name of the event log channel</param>
    /// <returns>The last updated timestamp, or null if no bookmark exists</returns>
    Task<DateTime?> GetLastUpdatedAsync(string channelName);
}
