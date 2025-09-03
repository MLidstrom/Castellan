namespace Castellan.Worker.Models;

public class NotificationConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public TeamsConfiguration Teams { get; set; } = new();
    public SlackConfiguration Slack { get; set; } = new();
}

public class TeamsConfiguration
{
    public bool Enabled { get; set; } = false;
    public string? WebhookUrl { get; set; }
    public string CastellanUrl { get; set; } = "http://localhost:8080";
    public RateLimitSettings RateLimitSettings { get; set; } = new();
}

public class SlackConfiguration
{
    public bool Enabled { get; set; } = false;
    public string? WebhookUrl { get; set; }
    public string CastellanUrl { get; set; } = "http://localhost:8080";
    public string? DefaultChannel { get; set; }
    public string? CriticalChannel { get; set; }
    public string? HighChannel { get; set; }
    public List<string> MentionUsersForCritical { get; set; } = new();
    public Dictionary<string, string> ChannelMappings { get; set; } = new();
    public RateLimitSettings RateLimitSettings { get; set; } = new();
}

public class RateLimitSettings
{
    public int CriticalThrottleMinutes { get; set; } = 0;
    public int HighThrottleMinutes { get; set; } = 5;
    public int MediumThrottleMinutes { get; set; } = 15;
    public int LowThrottleMinutes { get; set; } = 60;
}