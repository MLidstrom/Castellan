using System.ComponentModel.DataAnnotations;

namespace Castellan.Worker.Models;

public class CloudSecurityEvent
{
    [Key]
    public int Id { get; set; }
    
    public string EventId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // AzureAD, Exchange, SharePoint, etc.
    public string EventType { get; set; } = string.Empty; // SignIn, RiskyUser, etc.
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string ClientAppUsed { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RawData { get; set; } = string.Empty; // JSON of full event
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Microsoft365Config
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool EnableAzureAD { get; set; } = true;
    public bool EnableExchange { get; set; } = false;
    public bool EnableSharePoint { get; set; } = false;
    public bool EnableTeams { get; set; } = false;
    public int PollIntervalMinutes { get; set; } = 5;
}

