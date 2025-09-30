using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using System.Text.Json;

namespace Castellan.Worker.Services;

public class MitreService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<MitreService> _logger;

    public MitreService(CastellanDbContext context, ILogger<MitreService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MitreTechnique> CreateTechniqueAsync(MitreTechnique technique)
    {
        try
        {
            technique.CreatedAt = DateTime.UtcNow;
            _context.MitreTechniques.Add(technique);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created MITRE technique: {TechniqueId} - {Name}", 
                technique.TechniqueId, technique.Name);
            
            return technique;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MITRE technique: {TechniqueId}", technique.TechniqueId);
            throw;
        }
    }

    public async Task<MitreTechnique?> GetTechniqueByIdAsync(string techniqueId)
    {
        return await _context.MitreTechniques
            .Include(t => t.ApplicationAssociations)
                .ThenInclude(aa => aa.Application)
            .FirstOrDefaultAsync(t => t.TechniqueId == techniqueId);
    }

    public async Task<List<MitreTechnique>> GetTechniquesByTacticAsync(string tactic)
    {
        return await _context.MitreTechniques
            .Where(t => t.Tactic == tactic)
            .OrderBy(t => t.TechniqueId)
            .ToListAsync();
    }

    public async Task<List<MitreTechnique>> GetAllTechniquesAsync()
    {
        return await _context.MitreTechniques
            .OrderBy(t => t.TechniqueId)
            .ToListAsync();
    }

    public async Task<List<string>> GetAllTacticsAsync()
    {
        return await _context.MitreTechniques
            .Where(t => !string.IsNullOrEmpty(t.Tactic))
            .Select(t => t.Tactic!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    public async Task<MitreTechnique> UpdateTechniqueAsync(MitreTechnique technique)
    {
        try
        {
            _context.MitreTechniques.Update(technique);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated MITRE technique: {TechniqueId}", technique.TechniqueId);
            
            return technique;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating MITRE technique: {TechniqueId}", technique.TechniqueId);
            throw;
        }
    }

    public async Task<List<MitreTechnique>> SearchTechniquesAsync(string searchTerm)
    {
        return await _context.MitreTechniques
            .Where(t => t.Name.Contains(searchTerm) || 
                       t.Description!.Contains(searchTerm) ||
                       t.TechniqueId.Contains(searchTerm))
            .OrderBy(t => t.TechniqueId)
            .ToListAsync();
    }

    public async Task<bool> BulkImportTechniquesAsync(List<MitreTechnique> techniques)
    {
        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            foreach (var technique in techniques)
            {
                var existing = await _context.MitreTechniques
                    .FirstOrDefaultAsync(t => t.TechniqueId == technique.TechniqueId);
                
                if (existing == null)
                {
                    technique.CreatedAt = DateTime.UtcNow;
                    _context.MitreTechniques.Add(technique);
                }
                else
                {
                    // Update existing technique
                    existing.Name = technique.Name;
                    existing.Description = technique.Description;
                    existing.Tactic = technique.Tactic;
                    existing.Platform = technique.Platform;
                    existing.DataSources = technique.DataSources;
                    existing.Mitigations = technique.Mitigations;
                    existing.Examples = technique.Examples;
                }
            }
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Bulk imported {Count} MITRE techniques", techniques.Count);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk importing MITRE techniques");
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetTechniqueCountsByTacticAsync()
    {
        return await _context.MitreTechniques
            .Where(t => !string.IsNullOrEmpty(t.Tactic))
            .GroupBy(t => t.Tactic)
            .ToDictionaryAsync(g => g.Key!, g => g.Count());
    }

    public async Task<List<MitreTechnique>> GetTechniquesForApplicationAsync(int applicationId)
    {
        return await _context.ApplicationMitreAssociations
            .Where(ama => ama.ApplicationId == applicationId)
            .Include(ama => ama.MitreTechnique)
            .Select(ama => ama.MitreTechnique)
            .OrderBy(t => t.TechniqueId)
            .ToListAsync();
    }

    /// <summary>
    /// Gets paginated MITRE techniques with filtering and sorting applied at database level
    /// </summary>
    public async Task<(List<MitreTechnique> Techniques, int TotalCount)> GetTechniquesPagedAsync(
        int page = 1,
        int pageSize = 50,
        string? sort = null,
        bool descending = false,
        string? tactic = null,
        string? search = null)
    {
        var query = _context.MitreTechniques.AsQueryable();

        // Apply filters at database level
        if (!string.IsNullOrEmpty(tactic))
        {
            query = query.Where(t => t.Tactic == tactic);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Name.Contains(search) ||
                                   t.Description!.Contains(search) ||
                                   t.TechniqueId.Contains(search));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting at database level
        query = (sort?.ToLowerInvariant()) switch
        {
            "techniqueid" => descending ? query.OrderByDescending(t => t.TechniqueId)
                                        : query.OrderBy(t => t.TechniqueId),
            "name" => descending ? query.OrderByDescending(t => t.Name)
                                 : query.OrderBy(t => t.Name),
            "tactic" => descending ? query.OrderByDescending(t => t.Tactic)
                                   : query.OrderBy(t => t.Tactic),
            "platform" => descending ? query.OrderByDescending(t => t.Platform)
                                     : query.OrderBy(t => t.Platform),
            "createdat" => descending ? query.OrderByDescending(t => t.CreatedAt)
                                      : query.OrderBy(t => t.CreatedAt),
            _ => query.OrderBy(t => t.TechniqueId)
        };

        // Apply pagination at database level
        var techniques = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (techniques, totalCount);
    }
}
