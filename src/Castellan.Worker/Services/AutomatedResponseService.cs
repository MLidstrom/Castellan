using System.Diagnostics;
using System.Text.Json;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Configuration;
using Castellan.Worker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Services;

public interface IAutomatedResponseService
{
    Task ExecuteResponseAsync(SecurityEvent securityEvent);
    Task<bool> IsResponseEnabledAsync();
}

public class AutomatedResponseService : IAutomatedResponseService
{
    private readonly ILogger<AutomatedResponseService> _logger;
    private readonly AutomatedResponseOptions _options;
    private readonly IIPEnrichmentService? _ipEnrichmentService;

    public AutomatedResponseService(
        ILogger<AutomatedResponseService> logger,
        IOptions<AutomatedResponseOptions> options,
        IIPEnrichmentService? ipEnrichmentService = null)
    {
        _logger = logger;
        _options = options.Value;
        _ipEnrichmentService = ipEnrichmentService;
        
        _logger.LogInformation("🤖 AutomatedResponseService initialized with settings:");
        _logger.LogInformation("   - Enabled: {Enabled}", _options.Enabled);
        _logger.LogInformation("   - RiskLevelThreshold: {RiskLevelThreshold}", _options.RiskLevelThreshold);
        _logger.LogInformation("   - RequireConfirmation: {RequireConfirmation}", _options.RequireConfirmation);
    }

    public Task<bool> IsResponseEnabledAsync()
    {
        return Task.FromResult(_options.Enabled);
    }

    public async Task ExecuteResponseAsync(SecurityEvent securityEvent)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("🤖 Automated response is disabled in configuration");
            return;
        }

        if (!ShouldExecuteResponse(securityEvent))
        {
            _logger.LogDebug("🤖 Skipping automated response for event {EventId} with risk level {RiskLevel}", 
                securityEvent.OriginalEvent.EventId, securityEvent.RiskLevel);
            return;
        }

        _logger.LogInformation("🤖 Executing automated response for {EventType} with risk level {RiskLevel}", 
            securityEvent.EventType, securityEvent.RiskLevel);

        try
        {
            switch (securityEvent.EventType)
            {
                case SecurityEventType.AuthenticationFailure:
                    await HandleAuthenticationFailureAsync(securityEvent);
                    break;

                case SecurityEventType.PrivilegeEscalation:
                    await HandlePrivilegeEscalationAsync(securityEvent);
                    break;

                case SecurityEventType.PowerShellExecution:
                    await HandlePowerShellExecutionAsync(securityEvent);
                    break;

                case SecurityEventType.AccountManagement:
                    await HandleAccountManagementAsync(securityEvent);
                    break;

                case SecurityEventType.SecurityPolicyChange:
                    await HandleSecurityPolicyChangeAsync(securityEvent);
                    break;

                // AntiForensics handling removed - not in SecurityEventType enum

                default:
                    _logger.LogDebug("🤖 No automated response defined for event type {EventType}", securityEvent.EventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Failed to execute automated response for event {EventId}", 
                securityEvent.OriginalEvent.EventId);
        }
    }

    private bool ShouldExecuteResponse(SecurityEvent securityEvent)
    {
        var riskLevels = new[] { "low", "medium", "high", "critical" };
        var eventRiskIndex = Array.IndexOf(riskLevels, securityEvent.RiskLevel.ToLowerInvariant());
        var thresholdIndex = Array.IndexOf(riskLevels, _options.RiskLevelThreshold.ToLowerInvariant());

        return eventRiskIndex >= thresholdIndex;
    }

    private async Task HandleAuthenticationFailureAsync(SecurityEvent securityEvent)
    {
        _logger.LogInformation("🤖 Handling authentication failure for event {EventId}", securityEvent.OriginalEvent.EventId);

        if (securityEvent.RiskLevel == "critical")
        {
            // Extract IP address from enrichment data
            var ipAddress = ExtractIPAddress(securityEvent);
            if (!string.IsNullOrEmpty(ipAddress))
            {
                await BlockIPAddressAsync(ipAddress, securityEvent);
            }

            // Extract username and lock account
            var username = ExtractUsername(securityEvent);
            if (!string.IsNullOrEmpty(username))
            {
                await LockUserAccountAsync(username, securityEvent);
            }
        }
        else if (securityEvent.RiskLevel == "high")
        {
            // For high-risk failures, just log and monitor
            _logger.LogWarning("🤖 High-risk authentication failure detected - monitoring for escalation");
        }
    }

    private async Task HandlePrivilegeEscalationAsync(SecurityEvent securityEvent)
    {
        _logger.LogInformation("🤖 Handling privilege escalation for event {EventId}", securityEvent.OriginalEvent.EventId);

        var username = ExtractUsername(securityEvent);
        if (!string.IsNullOrEmpty(username))
        {
            await RevokePrivilegesAsync(username, securityEvent);
        }

        await CreateIncidentTicketAsync(securityEvent, "Privilege Escalation Detected");
    }

    private async Task HandlePowerShellExecutionAsync(SecurityEvent securityEvent)
    {
        _logger.LogInformation("🤖 Handling PowerShell execution for event {EventId}", securityEvent.OriginalEvent.EventId);

        if (securityEvent.RiskLevel == "high" || securityEvent.RiskLevel == "critical")
        {
            await KillSuspiciousProcessesAsync(securityEvent);
            await RestrictPowerShellExecutionAsync(securityEvent);
        }
    }

    private async Task HandleAccountManagementAsync(SecurityEvent securityEvent)
    {
        _logger.LogInformation("🤖 Handling account management for event {EventId}", securityEvent.OriginalEvent.EventId);

        if (securityEvent.RiskLevel == "high" || securityEvent.RiskLevel == "critical")
        {
            var username = ExtractUsername(securityEvent);
            if (!string.IsNullOrEmpty(username))
            {
                await DisableAccountAsync(username, securityEvent);
            }

            await CreateIncidentTicketAsync(securityEvent, "Unauthorized Account Management Detected");
        }
    }

    private async Task HandleSecurityPolicyChangeAsync(SecurityEvent securityEvent)
    {
        _logger.LogInformation("🤖 Handling security policy change for event {EventId}", securityEvent.OriginalEvent.EventId);

        await CreateIncidentTicketAsync(securityEvent, "Security Policy Change Detected");
        
        // Log the change for audit purposes
        _logger.LogWarning("🤖 Security policy change detected - review required");
    }

    private async Task HandleAntiForensicsAsync(SecurityEvent securityEvent)
    {
        _logger.LogInformation("🤖 Handling anti-forensics activity for event {EventId}", securityEvent.OriginalEvent.EventId);

        // Anti-forensics is always critical
        await CreateIncidentTicketAsync(securityEvent, "Anti-Forensics Activity Detected");
        
        // Take immediate action
        var username = ExtractUsername(securityEvent);
        if (!string.IsNullOrEmpty(username))
        {
            await LockUserAccountAsync(username, securityEvent);
        }
    }

    private async Task BlockIPAddressAsync(string ipAddress, SecurityEvent securityEvent)
    {
        try
        {
            _logger.LogInformation("🤖 Blocking IP address {IPAddress}", ipAddress);

            // Create Windows Firewall rule to block the IP
            var ruleName = $"Castellan-Block-{ipAddress}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            var command = $"netsh advfirewall firewall add rule name=\"{ruleName}\" dir=in action=block remoteip={ipAddress}";

            var result = await ExecuteCommandAsync(command);
            
            if (result.Success)
            {
                _logger.LogInformation("🤖 Successfully blocked IP address {IPAddress}", ipAddress);
            }
            else
            {
                _logger.LogError("🤖 Failed to block IP address {IPAddress}: {Error}", ipAddress, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Exception while blocking IP address {IPAddress}", ipAddress);
        }
    }

    private async Task LockUserAccountAsync(string username, SecurityEvent securityEvent)
    {
        try
        {
            _logger.LogInformation("🤖 Locking user account {Username}", username);

            var command = $"net user \"{username}\" /active:no";
            var result = await ExecuteCommandAsync(command);
            
            if (result.Success)
            {
                _logger.LogInformation("🤖 Successfully locked user account {Username}", username);
            }
            else
            {
                _logger.LogError("🤖 Failed to lock user account {Username}: {Error}", username, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Exception while locking user account {Username}", username);
        }
    }

    private async Task RevokePrivilegesAsync(string username, SecurityEvent securityEvent)
    {
        try
        {
            _logger.LogInformation("🤖 Revoking privileges for user {Username}", username);

            // Remove from administrators group
            var command = $"net localgroup administrators \"{username}\" /delete";
            var result = await ExecuteCommandAsync(command);
            
            if (result.Success)
            {
                _logger.LogInformation("🤖 Successfully revoked privileges for user {Username}", username);
            }
            else
            {
                _logger.LogError("🤖 Failed to revoke privileges for user {Username}: {Error}", username, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Exception while revoking privileges for user {Username}", username);
        }
    }

    private async Task KillSuspiciousProcessesAsync(SecurityEvent securityEvent)
    {
        try
        {
            _logger.LogInformation("🤖 Killing suspicious PowerShell processes");

            var command = "taskkill /f /im powershell.exe";
            var result = await ExecuteCommandAsync(command);
            
            if (result.Success)
            {
                _logger.LogInformation("🤖 Successfully killed suspicious PowerShell processes");
            }
            else
            {
                _logger.LogError("🤖 Failed to kill suspicious PowerShell processes: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Exception while killing suspicious processes");
        }
    }

    private async Task RestrictPowerShellExecutionAsync(SecurityEvent securityEvent)
    {
        try
        {
            _logger.LogInformation("🤖 Restricting PowerShell execution");

            var command = "Set-ExecutionPolicy -ExecutionPolicy Restricted -Force";
            var result = await ExecuteCommandAsync(command);
            
            if (result.Success)
            {
                _logger.LogInformation("🤖 Successfully restricted PowerShell execution");
            }
            else
            {
                _logger.LogError("🤖 Failed to restrict PowerShell execution: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Exception while restricting PowerShell execution");
        }
    }

    private async Task DisableAccountAsync(string username, SecurityEvent securityEvent)
    {
        try
        {
            _logger.LogInformation("🤖 Disabling account {Username}", username);

            var command = $"net user \"{username}\" /active:no";
            var result = await ExecuteCommandAsync(command);
            
            if (result.Success)
            {
                _logger.LogInformation("🤖 Successfully disabled account {Username}", username);
            }
            else
            {
                _logger.LogError("🤖 Failed to disable account {Username}: {Error}", username, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Exception while disabling account {Username}", username);
        }
    }

    private Task CreateIncidentTicketAsync(SecurityEvent securityEvent, string title)
    {
        try
        {
            _logger.LogInformation("🤖 Creating incident ticket for {EventType}", securityEvent.EventType);

            // This is a placeholder for incident ticket creation
            // In a real implementation, you would integrate with your ticketing system
            // (ServiceNow, Jira, Azure DevOps, etc.)
            
            var incidentData = new
            {
                Title = title,
                Description = securityEvent.Summary,
                RiskLevel = securityEvent.RiskLevel,
                Confidence = securityEvent.Confidence,
                EventId = securityEvent.OriginalEvent.EventId,
                EventType = securityEvent.EventType.ToString(),
                Timestamp = securityEvent.OriginalEvent.Time,
                User = securityEvent.OriginalEvent.User,
                SourceIP = ExtractIPAddress(securityEvent),
                MITRETechniques = securityEvent.MitreTechniques,
                RecommendedActions = securityEvent.RecommendedActions
            };

            _logger.LogInformation("🤖 Incident ticket data prepared: {@IncidentData}", incidentData);
            
            // TODO: Implement actual ticketing system integration
            // await _ticketingService.CreateIncidentAsync(incidentData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🤖 Exception while creating incident ticket");
        }
        
        return Task.CompletedTask;
    }

    private async Task<(bool Success, string? Error)> ExecuteCommandAsync(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return (true, null);
            }
            else
            {
                return (false, error);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private string? ExtractIPAddress(SecurityEvent securityEvent)
    {
        if (string.IsNullOrEmpty(securityEvent.EnrichmentData))
            return null;

        try
        {
            var enrichment = JsonSerializer.Deserialize<Dictionary<string, object>>(securityEvent.EnrichmentData);
            if (enrichment != null && enrichment.ContainsKey("ip"))
            {
                return enrichment["ip"].ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract IP address from enrichment data");
        }

        return null;
    }

    private string? ExtractUsername(SecurityEvent securityEvent)
    {
        return securityEvent.OriginalEvent.User;
    }
}
