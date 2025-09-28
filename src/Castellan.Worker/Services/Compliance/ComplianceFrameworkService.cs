using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Compliance;

namespace Castellan.Worker.Services.Compliance;

/// <summary>
/// Service for managing compliance frameworks with proper visibility separation
/// Ensures that Application-scope frameworks (CIS Controls, Windows baselines) are hidden from users
/// while Organization-scope frameworks (HIPAA, SOX, PCI-DSS, ISO 27001) remain visible
/// </summary>
public interface IComplianceFrameworkService
{
    /// <summary>
    /// Get all available frameworks for user-facing operations
    /// Only returns Organization-scope frameworks (IsUserVisible = true)
    /// </summary>
    Task<List<string>> GetAvailableFrameworksAsync();

    /// <summary>
    /// Get all frameworks including Application-scope for internal operations
    /// Returns all frameworks regardless of visibility
    /// </summary>
    Task<List<string>> GetAllFrameworksAsync();

    /// <summary>
    /// Check if a framework is user-visible
    /// </summary>
    Task<bool> IsFrameworkUserVisibleAsync(string framework);

    /// <summary>
    /// Get framework scope (Application vs Organization)
    /// </summary>
    Task<ComplianceScope?> GetFrameworkScopeAsync(string framework);

    /// <summary>
    /// Get controls for a specific framework with scope filtering
    /// </summary>
    Task<List<ComplianceControl>> GetFrameworkControlsAsync(string framework, bool userVisibleOnly = true);

    /// <summary>
    /// Get organization-scope frameworks only
    /// </summary>
    Task<List<string>> GetOrganizationFrameworksAsync();

    /// <summary>
    /// Get application-scope frameworks only
    /// </summary>
    Task<List<string>> GetApplicationFrameworksAsync();
}

public class ComplianceFrameworkService : IComplianceFrameworkService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<ComplianceFrameworkService> _logger;

    public ComplianceFrameworkService(
        CastellanDbContext context,
        ILogger<ComplianceFrameworkService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<string>> GetAvailableFrameworksAsync()
    {
        try
        {
            // Only return Organization-scope frameworks that are user-visible
            var frameworks = await _context.ComplianceControls
                .Where(c => c.IsUserVisible && c.Scope == ComplianceScope.Organization && c.IsActive)
                .Select(c => c.Framework)
                .Distinct()
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} user-visible frameworks: {Frameworks}",
                frameworks.Count, string.Join(", ", frameworks));

            return frameworks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available frameworks");
            return new List<string>();
        }
    }

    public async Task<List<string>> GetAllFrameworksAsync()
    {
        try
        {
            // Return all frameworks regardless of visibility or scope
            var frameworks = await _context.ComplianceControls
                .Where(c => c.IsActive)
                .Select(c => c.Framework)
                .Distinct()
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} total frameworks: {Frameworks}",
                frameworks.Count, string.Join(", ", frameworks));

            return frameworks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all frameworks");
            return new List<string>();
        }
    }

    public async Task<bool> IsFrameworkUserVisibleAsync(string framework)
    {
        try
        {
            var isVisible = await _context.ComplianceControls
                .Where(c => c.Framework == framework && c.IsActive)
                .AnyAsync(c => c.IsUserVisible && c.Scope == ComplianceScope.Organization);

            _logger.LogDebug("Framework {Framework} user visibility: {IsVisible}", framework, isVisible);

            return isVisible;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking framework visibility for {Framework}", framework);
            return false;
        }
    }

    public async Task<ComplianceScope?> GetFrameworkScopeAsync(string framework)
    {
        try
        {
            var scope = await _context.ComplianceControls
                .Where(c => c.Framework == framework && c.IsActive)
                .Select(c => c.Scope)
                .FirstOrDefaultAsync();

            _logger.LogDebug("Framework {Framework} scope: {Scope}", framework, scope);

            return scope;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving framework scope for {Framework}", framework);
            return null;
        }
    }

    public async Task<List<ComplianceControl>> GetFrameworkControlsAsync(string framework, bool userVisibleOnly = true)
    {
        try
        {
            var query = _context.ComplianceControls
                .Where(c => c.Framework == framework && c.IsActive);

            if (userVisibleOnly)
            {
                query = query.Where(c => c.IsUserVisible && c.Scope == ComplianceScope.Organization);
            }

            var controls = await query.ToListAsync();

            _logger.LogDebug("Retrieved {Count} controls for framework {Framework} (userVisibleOnly: {UserVisibleOnly})",
                controls.Count, framework, userVisibleOnly);

            return controls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving controls for framework {Framework}", framework);
            return new List<ComplianceControl>();
        }
    }

    public async Task<List<string>> GetOrganizationFrameworksAsync()
    {
        try
        {
            var frameworks = await _context.ComplianceControls
                .Where(c => c.Scope == ComplianceScope.Organization && c.IsActive)
                .Select(c => c.Framework)
                .Distinct()
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} organization-scope frameworks: {Frameworks}",
                frameworks.Count, string.Join(", ", frameworks));

            return frameworks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organization frameworks");
            return new List<string>();
        }
    }

    public async Task<List<string>> GetApplicationFrameworksAsync()
    {
        try
        {
            var frameworks = await _context.ComplianceControls
                .Where(c => c.Scope == ComplianceScope.Application && c.IsActive)
                .Select(c => c.Framework)
                .Distinct()
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} application-scope frameworks: {Frameworks}",
                frameworks.Count, string.Join(", ", frameworks));

            return frameworks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving application frameworks");
            return new List<string>();
        }
    }
}