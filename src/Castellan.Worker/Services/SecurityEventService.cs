using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using System.Text.Json;

namespace Castellan.Worker.Services;

public class SecurityEventService
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<SecurityEventService> _logger;

    public SecurityEventService(CastellanDbContext context, ILogger<SecurityEventService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SecurityEventEntity> CreateSecurityEventAsync(SecurityEventEntity securityEvent)
    {
        try
        {
            securityEvent.CreatedAt = DateTime.UtcNow;
            _context.SecurityEvents.Add(securityEvent);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created security event: {EventId} - {EventType} - {Severity}", 
                securityEvent.EventId, securityEvent.EventType, securityEvent.Severity);
            
            return securityEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating security event: {EventId}", securityEvent.EventId);
            throw;
        }
    }

    public async Task<SecurityEventEntity?> GetSecurityEventByIdAsync(string eventId)
    {
        return await _context.SecurityEvents
            .Include(se => se.Application)
            .FirstOrDefaultAsync(se => se.EventId == eventId);
    }

    public async Task<List<SecurityEventEntity>> GetSecurityEventsByApplicationAsync(int applicationId, int limit = 100)
    {
        return await _context.SecurityEvents
            .Where(se => se.ApplicationId == applicationId)
            .Include(se => se.Application)
            .OrderByDescending(se => se.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<SecurityEventEntity>> GetSecurityEventsBySeverityAsync(string severity, int limit = 100)
    {
        return await _context.SecurityEvents
            .Where(se => se.Severity == severity)
            .Include(se => se.Application)
            .OrderByDescending(se => se.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<SecurityEventEntity>> GetSecurityEventsByTimeRangeAsync(DateTime startTime, DateTime endTime, int limit = 1000)
    {
        return await _context.SecurityEvents
            .Where(se => se.Timestamp >= startTime && se.Timestamp <= endTime)
            .Include(se => se.Application)
            .OrderByDescending(se => se.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<SecurityEventEntity>> GetSecurityEventsByMitreTechniqueAsync(string techniqueId, int limit = 100)
    {
        return await _context.SecurityEvents
            .Where(se => se.MitreTechniques != null && se.MitreTechniques.Contains(techniqueId))
            .Include(se => se.Application)
            .OrderByDescending(se => se.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetEventCountsBySeverityAsync(DateTime? startTime = null, DateTime? endTime = null)
    {
        var query = _context.SecurityEvents.AsQueryable();
        
        if (startTime.HasValue)
            query = query.Where(se => se.Timestamp >= startTime.Value);
        
        if (endTime.HasValue)
            query = query.Where(se => se.Timestamp <= endTime.Value);

        return await query
            .GroupBy(se => se.Severity)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetEventCountsByTypeAsync(DateTime? startTime = null, DateTime? endTime = null)
    {
        var query = _context.SecurityEvents.AsQueryable();
        
        if (startTime.HasValue)
            query = query.Where(se => se.Timestamp >= startTime.Value);
        
        if (endTime.HasValue)
            query = query.Where(se => se.Timestamp <= endTime.Value);

        return await query
            .GroupBy(se => se.EventType)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<List<SecurityEventEntity>> SearchSecurityEventsAsync(string searchTerm, int limit = 100)
    {
        return await _context.SecurityEvents
            .Where(se => se.Message!.Contains(searchTerm) || 
                        se.EventType.Contains(searchTerm) ||
                        se.Source!.Contains(searchTerm) ||
                        se.EventId.Contains(searchTerm))
            .Include(se => se.Application)
            .OrderByDescending(se => se.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> BulkCreateSecurityEventsAsync(List<SecurityEventEntity> securityEvents)
    {
        try
        {
            foreach (var evt in securityEvents)
            {
                evt.CreatedAt = DateTime.UtcNow;
            }
            
            _context.SecurityEvents.AddRange(securityEvents);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Bulk created {Count} security events", securityEvents.Count);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk creating security events");
            throw;
        }
    }

    public async Task<List<SecurityEventEntity>> GetRecentSecurityEventsAsync(int hours = 24, int limit = 100)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-hours);
        
        return await _context.SecurityEvents
            .Where(se => se.Timestamp >= cutoffTime)
            .Include(se => se.Application)
            .OrderByDescending(se => se.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<SecurityEventEntity>> GetHighRiskEventsAsync(int limit = 50)
    {
        return await _context.SecurityEvents
            .Where(se => se.Severity == "High" || se.Severity == "Critical")
            .Include(se => se.Application)
            .OrderByDescending(se => se.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetTotalEventCountAsync(DateTime? startTime = null, DateTime? endTime = null)
    {
        var query = _context.SecurityEvents.AsQueryable();
        
        if (startTime.HasValue)
            query = query.Where(se => se.Timestamp >= startTime.Value);
        
        if (endTime.HasValue)
            query = query.Where(se => se.Timestamp <= endTime.Value);

        return await query.CountAsync();
    }
}
