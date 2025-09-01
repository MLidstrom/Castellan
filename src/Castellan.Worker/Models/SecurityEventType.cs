namespace Castellan.Worker.Models;

public enum SecurityEventType
{
    // Authentication Events
    AuthenticationSuccess,
    AuthenticationFailure,
    
    // Authorization & Privilege Events
    PrivilegeEscalation,
    
    // Process & System Events
    ProcessCreation,
    ServiceInstallation,
    ScheduledTask,
    SystemStartup,
    SystemShutdown,
    
    // Account Management
    AccountManagement,
    
    // Security Policy Changes
    SecurityPolicyChange,
    
    // Network Events
    NetworkConnection,
    
    // PowerShell & Script Execution
    PowerShellExecution,
    
    // General categories
    Unknown
}
