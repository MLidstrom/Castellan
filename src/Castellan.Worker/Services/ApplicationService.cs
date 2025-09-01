using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using ApplicationModel = Castellan.Worker.Models.Application;

namespace Castellan.Worker.Services;

public class ApplicationService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(CastellanDbContext context, ILogger<ApplicationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ApplicationModel> CreateApplicationAsync(ApplicationModel application)
    {
        try
        {
            application.CreatedAt = DateTime.UtcNow;
            application.UpdatedAt = DateTime.UtcNow;
            
            _context.Applications.Add(application);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created application: {ApplicationName} with ID: {ApplicationId}", 
                application.Name, application.Id);
            
            return application;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating application: {ApplicationName}", application.Name);
            throw;
        }
    }

    public async Task<ApplicationModel?> GetApplicationByIdAsync(int id)
    {
        return await _context.Applications
            .Include(a => a.MitreAssociations)
                .ThenInclude(ma => ma.MitreTechnique)
            .Include(a => a.SecurityEvents)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<ApplicationModel?> GetApplicationByNameAsync(string name)
    {
        return await _context.Applications
            .Include(a => a.MitreAssociations)
                .ThenInclude(ma => ma.MitreTechnique)
            .FirstOrDefaultAsync(a => a.Name == name && a.IsActive);
    }

    public async Task<List<ApplicationModel>> GetAllApplicationsAsync(bool includeInactive = false)
    {
        var query = _context.Applications.AsQueryable();
        
        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        return await query
            .Include(a => a.MitreAssociations)
                .ThenInclude(ma => ma.MitreTechnique)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<ApplicationModel> UpdateApplicationAsync(ApplicationModel application)
    {
        try
        {
            application.UpdatedAt = DateTime.UtcNow;
            _context.Applications.Update(application);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated application: {ApplicationName} with ID: {ApplicationId}", 
                application.Name, application.Id);
            
            return application;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating application: {ApplicationId}", application.Id);
            throw;
        }
    }

    public async Task<bool> DeleteApplicationAsync(int id)
    {
        try
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null)
            {
                return false;
            }

            application.IsActive = false;
            application.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deactivated application: {ApplicationName} with ID: {ApplicationId}", 
                application.Name, application.Id);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting application: {ApplicationId}", id);
            throw;
        }
    }

    public async Task<ApplicationMitreAssociation> AssociateMitreTechniqueAsync(int applicationId, string techniqueId, double confidence = 1.0, string? notes = null)
    {
        try
        {
            var association = new ApplicationMitreAssociation
            {
                ApplicationId = applicationId,
                TechniqueId = techniqueId,
                Confidence = confidence,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.ApplicationMitreAssociations.Add(association);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Associated application {ApplicationId} with MITRE technique {TechniqueId}", 
                applicationId, techniqueId);
            
            return association;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error associating application {ApplicationId} with MITRE technique {TechniqueId}", 
                applicationId, techniqueId);
            throw;
        }
    }

    public async Task<List<ApplicationModel>> GetApplicationsByRiskScoreAsync(int minRiskScore)
    {
        return await _context.Applications
            .Where(a => a.IsActive && a.RiskScore >= minRiskScore)
            .Include(a => a.MitreAssociations)
                .ThenInclude(ma => ma.MitreTechnique)
            .OrderByDescending(a => a.RiskScore)
            .ToListAsync();
    }
}
