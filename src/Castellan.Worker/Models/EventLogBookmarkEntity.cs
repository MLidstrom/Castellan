namespace Castellan.Worker.Models;

/// <summary>
/// Entity model for storing EventLog bookmarks in the database
/// </summary>
public class EventLogBookmarkEntity
{
    public int Id { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public byte[] BookmarkData { get; set; } = Array.Empty<byte>();
    public DateTime UpdatedAt { get; set; }
}
