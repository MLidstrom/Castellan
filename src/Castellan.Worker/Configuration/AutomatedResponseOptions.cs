namespace Castellan.Worker.Configuration;

public class AutomatedResponseOptions
{
    public const string SectionName = "AutomatedResponse";

    /// <summary>
    /// Whether automated responses are enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Minimum risk level required to trigger automated responses
    /// Valid values: low, medium, high, critical
    /// </summary>
    public string RiskLevelThreshold { get; set; } = "high";

    /// <summary>
    /// Whether to require manual confirmation before executing responses
    /// </summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>
    /// Configuration for specific response actions
    /// </summary>
    public ResponseActions Actions { get; set; } = new();

    /// <summary>
    /// Whether to log all response actions for audit purposes
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Maximum number of response actions per hour to prevent abuse
    /// </summary>
    public int MaxActionsPerHour { get; set; } = 10;

    /// <summary>
    /// Whether to send notifications when automated responses are executed
    /// </summary>
    public bool NotifyOnResponse { get; set; } = true;
}

public class ResponseActions
{
    /// <summary>
    /// Whether to block IP addresses for authentication failures
    /// </summary>
    public bool BlockIPAddresses { get; set; } = true;

    /// <summary>
    /// Whether to lock user accounts for suspicious activity
    /// </summary>
    public bool LockUserAccounts { get; set; } = true;

    /// <summary>
    /// Whether to revoke privileges for privilege escalation
    /// </summary>
    public bool RevokePrivileges { get; set; } = true;

    /// <summary>
    /// Whether to kill suspicious PowerShell processes
    /// </summary>
    public bool KillSuspiciousProcesses { get; set; } = true;

    /// <summary>
    /// Whether to restrict PowerShell execution
    /// </summary>
    public bool RestrictPowerShellExecution { get; set; } = true;

    /// <summary>
    /// Whether to disable accounts for unauthorized management
    /// </summary>
    public bool DisableAccounts { get; set; } = true;

    /// <summary>
    /// Whether to create incident tickets
    /// </summary>
    public bool CreateIncidentTickets { get; set; } = true;
}
