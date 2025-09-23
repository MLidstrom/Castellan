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

