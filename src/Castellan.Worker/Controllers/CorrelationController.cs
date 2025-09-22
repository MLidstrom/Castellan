using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Controllers;

/// <summary>
/// API controller for event correlation and pattern detection
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CorrelationController : ControllerBase
{
    private readonly ILogger<CorrelationController> _logger;
    private readonly ICorrelationEngine _correlationEngine;
    private readonly ISecurityEventStore _eventStore;

    public CorrelationController(
        ILogger<CorrelationController> logger,
        ICorrelationEngine correlationEngine,
        ISecurityEventStore eventStore)
    {
        _logger = logger;
        _correlationEngine = correlationEngine;
        _eventStore = eventStore;
    }

    /// <summary>
    /// Gets all correlations within a time range
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CorrelationListResponse>> GetCorrelations(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] string? correlationType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var start = startTime ?? DateTime.UtcNow.AddDays(-7);
            var end = endTime ?? DateTime.UtcNow;

            var correlations = await _correlationEngine.GetCorrelationsAsync(start, end);

            if (!string.IsNullOrEmpty(correlationType))
            {
                correlations = correlations.Where(c => c.CorrelationType.Equals(correlationType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var pagedCorrelations = correlations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new CorrelationListResponse
            {
                Correlations = pagedCorrelations,
                TotalCount = correlations.Count(),
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving correlations");
            return StatusCode(500, new { error = "Failed to retrieve correlations" });
        }
    }

    /// <summary>
    /// Analyzes a single event for correlations
    /// </summary>
    [HttpPost("analyze-event/{eventId}")]
    public async Task<ActionResult<CorrelationResult>> AnalyzeEvent(string eventId)
    {
        try
        {
            var securityEvent = _eventStore.GetSecurityEvent(eventId);
            if (securityEvent == null)
            {
                return NotFound($"Event {eventId} not found");
            }

            var result = await _correlationEngine.AnalyzeEventAsync(securityEvent);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing event {EventId}", eventId);
            return StatusCode(500, new { error = "Failed to analyze event" });
        }
    }

    /// <summary>
    /// Analyzes a batch of events for correlations
    /// </summary>
    [HttpPost("analyze-batch")]
    public async Task<ActionResult<BatchAnalysisResponse>> AnalyzeBatch(
        [FromBody] BatchAnalysisRequest request)
    {
        try
        {
            List<SecurityEvent> events;

            if (request.EventIds?.Any() == true)
            {
                // Get specific events
                events = new List<SecurityEvent>();
                foreach (var eventId in request.EventIds)
                {
                    var evt = _eventStore.GetSecurityEvent(eventId);
                    if (evt != null)
                    {
                        events.Add(evt);
                    }
                }
            }
            else
            {
                // Get events within time window - use existing store methods
                var filterDict = new Dictionary<string, object>();
                var startTime = DateTime.UtcNow - (request.TimeWindow ?? TimeSpan.FromHours(1));
                var endTime = DateTime.UtcNow;

                filterDict["StartTime"] = startTime;
                filterDict["EndTime"] = endTime;

                events = _eventStore.GetSecurityEvents(1, request.MaxEvents ?? 1000, filterDict).ToList();
            }

            var timeWindow = request.TimeWindow ?? TimeSpan.FromHours(1);
            var correlations = await _correlationEngine.AnalyzeBatchAsync(events, timeWindow);

            return Ok(new BatchAnalysisResponse
            {
                EventsAnalyzed = events.Count,
                CorrelationsFound = correlations.Count(),
                Correlations = correlations.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing batch");
            return StatusCode(500, new { error = "Failed to analyze batch" });
        }
    }

    /// <summary>
    /// Gets correlation statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<CorrelationStatistics>> GetStatistics(
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime,
        [FromQuery] string? eventType)
    {
        try
        {
            var start = startTime ?? DateTime.UtcNow.AddDays(-7);
            var end = endTime ?? DateTime.UtcNow;

            var filterDict = new Dictionary<string, object>
            {
                ["StartTime"] = start,
                ["EndTime"] = end
            };

            if (!string.IsNullOrEmpty(eventType))
                filterDict["EventType"] = eventType;

            var statistics = await _correlationEngine.GetStatisticsAsync(start, end);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    /// <summary>
    /// Detects attack chains in recent events
    /// </summary>
    [HttpPost("attack-chains")]
    public async Task<ActionResult<AttackChainResponse>> DetectAttackChains(
        [FromBody] DetectAttackChainsRequest request)
    {
        try
        {
            var filterDict = new Dictionary<string, object>
            {
                ["StartTime"] = DateTime.UtcNow - (request.TimeWindow ?? TimeSpan.FromHours(1)),
                ["EndTime"] = DateTime.UtcNow
            };

            var events = _eventStore.GetSecurityEvents(1, request.MaxEvents ?? 1000, filterDict).ToList();

            var chains = await _correlationEngine.DetectAttackChainsAsync(
                events,
                request.TimeWindow ?? TimeSpan.FromHours(1));

            return Ok(new AttackChainResponse
            {
                EventsAnalyzed = events.Count,
                ChainsDetected = chains.Count(),
                AttackChains = chains.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting attack chains");
            return StatusCode(500, new { error = "Failed to detect attack chains" });
        }
    }

    /// <summary>
    /// Gets correlation rules
    /// </summary>
    [HttpGet("rules")]
    public async Task<ActionResult<List<CorrelationRule>>> GetRules()
    {
        try
        {
            var rules = await _correlationEngine.GetRulesAsync();
            return Ok(rules.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rules");
            return StatusCode(500, new { error = "Failed to retrieve rules" });
        }
    }
}

// Response models
public class CorrelationListResponse
{
    public List<EventCorrelation> Correlations { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class BatchAnalysisRequest
{
    public List<string>? EventIds { get; set; }
    public TimeSpan? TimeWindow { get; set; }
    public int? MaxEvents { get; set; }
}

public class BatchAnalysisResponse
{
    public int EventsAnalyzed { get; set; }
    public int CorrelationsFound { get; set; }
    public List<EventCorrelation> Correlations { get; set; } = new();
}

public class DetectAttackChainsRequest
{
    public TimeSpan? TimeWindow { get; set; }
    public int? MaxEvents { get; set; }
}

public class AttackChainResponse
{
    public int EventsAnalyzed { get; set; }
    public int ChainsDetected { get; set; }
    public List<AttackChain> AttackChains { get; set; } = new();
}