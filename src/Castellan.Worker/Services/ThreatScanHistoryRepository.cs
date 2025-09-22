using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Data;
using Castellan.Worker.Models;
using Castellan.Worker.Models.ThreatIntelligence;

namespace Castellan.Worker.Services;

public class ThreatScanHistoryRepository : IThreatScanHistoryRepository
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<ThreatScanHistoryRepository> _logger;

    public ThreatScanHistoryRepository(CastellanDbContext context, ILogger<ThreatScanHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> CreateScanAsync(ThreatScanResult scanResult)
    {
        try
        {
            var entity = MapToEntity(scanResult);
            entity.Id = scanResult.ScanId ?? Guid.NewGuid().ToString();
            entity.CreatedAt = DateTime.UtcNow;

            _context.ThreatScanHistory.Add(entity);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created threat scan history record: {ScanId}", entity.Id);
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create threat scan history record");
            throw;
        }
    }

    public async Task UpdateScanAsync(ThreatScanResult scanResult)
    {
        try
        {
            var id = scanResult.ScanId ?? throw new ArgumentException("ScanId is required for update");
            var entity = await _context.ThreatScanHistory.FindAsync(id);

            if (entity == null)
            {
                _logger.LogWarning("Threat scan history record not found for update: {ScanId}", id);
                return;
            }

            UpdateEntityFromResult(entity, scanResult);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated threat scan history record: {ScanId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update threat scan history record: {ScanId}", scanResult.ScanId);
            throw;
        }
    }

    public async Task<ThreatScanResult?> GetScanAsync(string id)
    {
        try
        {
            var entity = await _context.ThreatScanHistory.FindAsync(id);
            return entity == null ? null : MapToResult(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get threat scan history record: {ScanId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<ThreatScanResult>> GetScanHistoryAsync(int page, int pageSize)
    {
        try
        {
            var entities = await _context.ThreatScanHistory
                .OrderByDescending(x => x.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return entities.Select(MapToResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get threat scan history");
            throw;
        }
    }

    public async Task<int> GetScanCountAsync()
    {
        try
        {
            return await _context.ThreatScanHistory.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get threat scan count");
            throw;
        }
    }

    public async Task DeleteOldScansAsync(DateTime olderThan)
    {
        try
        {
            var oldScans = await _context.ThreatScanHistory
                .Where(x => x.CreatedAt < olderThan)
                .ToListAsync();

            if (oldScans.Any())
            {
                _context.ThreatScanHistory.RemoveRange(oldScans);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} old threat scan records", oldScans.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete old threat scan records");
            throw;
        }
    }

    private ThreatScanHistoryEntity MapToEntity(ThreatScanResult result)
    {
        return new ThreatScanHistoryEntity
        {
            Id = result.ScanId ?? Guid.NewGuid().ToString(),
            ScanType = result.ScanType.ToString(),
            Status = result.Status.ToString(),
            StartTime = result.StartTime,
            EndTime = result.EndTime,
            Duration = result.Duration.TotalMinutes,
            FilesScanned = result.FilesScanned,
            DirectoriesScanned = result.DirectoriesScanned,
            BytesScanned = result.BytesScanned,
            ThreatsFound = result.ThreatsFound,
            MalwareDetected = result.MalwareDetected,
            BackdoorsDetected = result.BackdoorsDetected,
            SuspiciousFiles = result.SuspiciousFiles,
            RiskLevel = result.RiskLevel.ToString(),
            Summary = result.Summary,
            ErrorMessage = result.ErrorMessage,
            ScanPath = result.ScanPath
        };
    }

    private void UpdateEntityFromResult(ThreatScanHistoryEntity entity, ThreatScanResult result)
    {
        entity.Status = result.Status.ToString();
        entity.EndTime = result.EndTime;
        entity.Duration = result.Duration.TotalMinutes;
        entity.FilesScanned = result.FilesScanned;
        entity.DirectoriesScanned = result.DirectoriesScanned;
        entity.BytesScanned = result.BytesScanned;
        entity.ThreatsFound = result.ThreatsFound;
        entity.MalwareDetected = result.MalwareDetected;
        entity.BackdoorsDetected = result.BackdoorsDetected;
        entity.SuspiciousFiles = result.SuspiciousFiles;
        entity.RiskLevel = result.RiskLevel.ToString();
        entity.Summary = result.Summary;
        entity.ErrorMessage = result.ErrorMessage;
    }

    private ThreatScanResult MapToResult(ThreatScanHistoryEntity entity)
    {
        var result = new ThreatScanResult
        {
            ScanId = entity.Id,
            ScanType = Enum.Parse<ThreatScanType>(entity.ScanType),
            Status = Enum.Parse<ThreatScanStatus>(entity.Status),
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            FilesScanned = entity.FilesScanned,
            DirectoriesScanned = entity.DirectoriesScanned,
            BytesScanned = entity.BytesScanned,
            ThreatsFound = entity.ThreatsFound,
            MalwareDetected = entity.MalwareDetected,
            BackdoorsDetected = entity.BackdoorsDetected,
            SuspiciousFiles = entity.SuspiciousFiles,
            RiskLevel = Enum.Parse<ThreatRiskLevel>(entity.RiskLevel),
            Summary = entity.Summary,
            ErrorMessage = entity.ErrorMessage,
            ScanPath = entity.ScanPath
        };

        result.Duration = TimeSpan.FromMinutes(entity.Duration);
        return result;
    }
}