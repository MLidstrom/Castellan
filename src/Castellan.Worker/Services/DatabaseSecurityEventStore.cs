using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Data;

namespace Castellan.Worker.Services;

/// <summary>
/// Database-backed implementation of ISecurityEventStore using Entity Framework and SQLite.
/// This ensures events are stored in the database where the admin interface expects them.
/// </summary>
public class DatabaseSecurityEventStore : ISecurityEventStore
{
    private readonly CastellanDbContext _context;
    private readonly ILogger<DatabaseSecurityEventStore> _logger;

    public DatabaseSecurityEventStore(CastellanDbContext context, ILogger<DatabaseSecurityEventStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void AddSecurityEvent(SecurityEvent securityEvent)
    {
        try
        {
            // Convert SecurityEvent to SecurityEventEntity for database storage
            var entity = ConvertToEntity(securityEvent);

            _context.SecurityEvents.Add(entity);
            _context.SaveChanges();

            _logger.LogDebug("Added security event to database {Id}: {EventType} ({RiskLevel})",
                securityEvent.Id, securityEvent.EventType, securityEvent.RiskLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add security event {Id} to database", securityEvent.Id);
            throw;
        }
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page = 1, int pageSize = 10)
    {
        var skip = (page - 1) * pageSize;

        var entities = _context.SecurityEvents
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return entities.Select(ConvertFromEntity);
    }

    public IEnumerable<SecurityEvent> GetSecurityEvents(int page, int pageSize, Dictionary<string, object> filters)
    {
        var query = _context.SecurityEvents.AsQueryable();

        // Apply filters
        query = ApplyFilters(query, filters);

        var skip = (page - 1) * pageSize;
        var entities = query
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return entities.Select(ConvertFromEntity);
    }

    public SecurityEvent? GetSecurityEvent(string id)
    {
        var entity = _context.SecurityEvents
            .FirstOrDefault(e => e.EventId == id);

        return entity != null ? ConvertFromEntity(entity) : null;
    }

    public int GetTotalCount()
    {
        return _context.SecurityEvents.Count();
    }

    public int GetTotalCount(Dictionary<string, object> filters)
    {
        if (filters == null || filters.Count == 0)
            return GetTotalCount();

        var query = _context.SecurityEvents.AsQueryable();
        query = ApplyFilters(query, filters);

        return query.Count();
    }

    public Dictionary<string, int> GetRiskLevelCounts()
    {
        try
        {
            return _context.SecurityEvents
                .GroupBy(e => e.RiskLevel)
                .Select(g => new { RiskLevel = g.Key, Count = g.Count() })
                .AsEnumerable()
                .ToDictionary(x => x.RiskLevel.ToLower(), x => x.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get risk level counts from database");
            return new Dictionary<string, int>();
        }
    }

    public void Clear()
    {
        _context.SecurityEvents.RemoveRange(_context.SecurityEvents);
        _context.SaveChanges();
        _logger.LogInformation("Cleared all security events from database");
    }

    /// <summary>
    /// Convert SecurityEvent domain model to SecurityEventEntity for database storage
    /// </summary>
    private SecurityEventEntity ConvertToEntity(SecurityEvent securityEvent)
    {
        return new SecurityEventEntity
        {
            EventId = securityEvent.Id,
            EventType = securityEvent.EventType.ToString(),
            Severity = GetSeverityFromRiskLevel(securityEvent.RiskLevel),
            RiskLevel = securityEvent.RiskLevel,
            Source = securityEvent.OriginalEvent?.Host,
            Message = securityEvent.OriginalEvent?.Message,
            Summary = securityEvent.Summary,
            EventData = JsonSerializer.Serialize(securityEvent.OriginalEvent),
            Timestamp = securityEvent.OriginalEvent?.Time.DateTime ?? DateTime.UtcNow,
            SourceIp = ExtractSourceIp(securityEvent.OriginalEvent),
            DestinationIp = ExtractDestinationIp(securityEvent.OriginalEvent),
            MitreTechniques = securityEvent.MitreTechniques?.Length > 0
                ? JsonSerializer.Serialize(securityEvent.MitreTechniques)
                : null,
            RecommendedActions = securityEvent.RecommendedActions?.Length > 0
                ? JsonSerializer.Serialize(securityEvent.RecommendedActions)
                : null,
            Confidence = securityEvent.Confidence,
            CorrelationScore = securityEvent.CorrelationScore,
            BurstScore = securityEvent.BurstScore,
            AnomalyScore = securityEvent.AnomalyScore,
            IsDeterministic = securityEvent.IsDeterministic,
            IsCorrelationBased = securityEvent.IsCorrelationBased,
            IsEnhanced = securityEvent.IsEnhanced,
            EnrichmentData = securityEvent.EnrichmentData,
            CorrelationIds = securityEvent.CorrelationIds?.Count > 0
                ? JsonSerializer.Serialize(securityEvent.CorrelationIds)
                : null,
            CorrelationContext = securityEvent.CorrelationContext,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Convert SecurityEventEntity from database back to SecurityEvent domain model
    /// </summary>
    private SecurityEvent ConvertFromEntity(SecurityEventEntity entity)
    {
        // Deserialize original event data
        LogEvent? originalEvent = null;
        if (!string.IsNullOrEmpty(entity.EventData))
        {
            try
            {
                originalEvent = JsonSerializer.Deserialize<LogEvent>(entity.EventData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize event data for event {EventId}", entity.EventId);
            }
        }

        // If original event is null, create a minimal one
        originalEvent ??= new LogEvent(
            Time: entity.Timestamp,
            Host: entity.Source ?? "Unknown",
            Channel: "Unknown",
            EventId: 0,
            Level: entity.Severity ?? "Info",
            User: "",
            Message: entity.Message ?? "No message available",
            RawJson: "",
            UniqueId: entity.EventId
        );

        return new SecurityEvent
        {
            Id = entity.EventId,
            OriginalEvent = originalEvent,
            EventType = Enum.TryParse<SecurityEventType>(entity.EventType, out var eventType)
                ? eventType
                : SecurityEventType.Unknown,
            RiskLevel = entity.RiskLevel,
            Confidence = (int)entity.Confidence,
            Summary = entity.Summary ?? string.Empty,
            MitreTechniques = DeserializeStringArray(entity.MitreTechniques),
            RecommendedActions = DeserializeStringArray(entity.RecommendedActions),
            IsDeterministic = entity.IsDeterministic,
            IsCorrelationBased = entity.IsCorrelationBased,
            IsEnhanced = entity.IsEnhanced,
            CorrelationScore = entity.CorrelationScore,
            BurstScore = entity.BurstScore,
            AnomalyScore = entity.AnomalyScore,
            EnrichmentData = entity.EnrichmentData,
            CorrelationIds = DeserializeStringList(entity.CorrelationIds),
            CorrelationContext = entity.CorrelationContext
        };
    }

    /// <summary>
    /// Apply filters to the database query
    /// </summary>
    private IQueryable<SecurityEventEntity> ApplyFilters(IQueryable<SecurityEventEntity> query, Dictionary<string, object> filters)
    {
        foreach (var filter in filters)
        {
            switch (filter.Key.ToLowerInvariant())
            {
                case "risklevel":
                case "risk_level":
                    if (filter.Value is string riskLevel)
                        query = query.Where(e => e.RiskLevel == riskLevel);
                    break;

                case "severity":
                    if (filter.Value is string severity)
                        query = query.Where(e => e.Severity == severity);
                    break;

                case "eventtype":
                case "event_type":
                    if (filter.Value is string eventType)
                        query = query.Where(e => e.EventType == eventType);
                    break;

                case "starttime":
                case "start_time":
                case "startdate":
                    if (filter.Value is DateTime startTime)
                        query = query.Where(e => e.Timestamp >= startTime);
                    break;

                case "endtime":
                case "end_time":
                case "enddate":
                    if (filter.Value is DateTime endTime)
                        query = query.Where(e => e.Timestamp <= endTime);
                    break;

                case "sourceip":
                case "source_ip":
                    if (filter.Value is string sourceIp)
                        query = query.Where(e => e.SourceIp == sourceIp);
                    break;

                case "mitretechniques":
                case "mitre_techniques":
                    if (filter.Value is string mitreTechnique)
                        query = query.Where(e => e.MitreTechniques != null && e.MitreTechniques.Contains(mitreTechnique));
                    break;
            }
        }

        return query;
    }

    /// <summary>
    /// Map risk level to severity for database storage
    /// </summary>
    private static string GetSeverityFromRiskLevel(string riskLevel)
    {
        return riskLevel.ToLowerInvariant() switch
        {
            "critical" => "Critical",
            "high" => "High",
            "medium" => "Medium",
            "low" => "Low",
            _ => "Medium"
        };
    }

    /// <summary>
    /// Extract source IP from log event data
    /// </summary>
    private static string? ExtractSourceIp(LogEvent? logEvent)
    {
        if (logEvent == null || string.IsNullOrEmpty(logEvent.RawJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(logEvent.RawJson);
            var root = document.RootElement;

            // Try to extract IP from common fields
            if (root.TryGetProperty("SourceNetworkAddress", out var sourceIp) ||
                root.TryGetProperty("IpAddress", out sourceIp) ||
                root.TryGetProperty("ClientIP", out sourceIp))
            {
                return sourceIp.GetString();
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return null;
    }

    /// <summary>
    /// Extract destination IP from log event data
    /// </summary>
    private static string? ExtractDestinationIp(LogEvent? logEvent)
    {
        if (logEvent == null || string.IsNullOrEmpty(logEvent.RawJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(logEvent.RawJson);
            var root = document.RootElement;

            // Try to extract IP from common fields
            if (root.TryGetProperty("DestinationNetworkAddress", out var destIp) ||
                root.TryGetProperty("TargetIP", out destIp))
            {
                return destIp.GetString();
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return null;
    }

    /// <summary>
    /// Deserialize JSON string array
    /// </summary>
    private static string[] DeserializeStringArray(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Deserialize JSON string list
    /// </summary>
    private static List<string>? DeserializeStringList(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return null;
        }
    }
}