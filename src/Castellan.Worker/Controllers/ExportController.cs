using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Castellan.Worker.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly ILogger<ExportController> _logger;
    private readonly IExportService _exportService;
    private readonly ISecurityEventStore _securityEventStore;

    public ExportController(
        ILogger<ExportController> logger,
        IExportService exportService,
        ISecurityEventStore securityEventStore)
    {
        _logger = logger;
        _exportService = exportService;
        _securityEventStore = securityEventStore;
    }

    /// <summary>
    /// Get available export formats
    /// </summary>
    [HttpGet("formats")]
    public async Task<IActionResult> GetFormats()
    {
        try
        {
            var formats = await _exportService.GetSupportedFormatsAsync();
            return Ok(new { data = formats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export formats");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Export security events
    /// </summary>
    [HttpPost("security-events")]
    public async Task<IActionResult> ExportSecurityEvents([FromBody] ExportRequest request)
    {
        try
        {
            _logger.LogInformation("Exporting security events to {Format} format", request.Format);

            // Apply filters and get events
            var filterCriteria = BuildFilterCriteria(request);
            var events = _securityEventStore.GetSecurityEvents(1, int.MaxValue, filterCriteria);
            var eventsList = events.ToList();

            if (!eventsList.Any())
            {
                return BadRequest(new { message = "No security events found matching the specified criteria" });
            }

            byte[] exportData;
            string fileName;
            string mimeType;

            switch (request.Format.ToLower())
            {
                case "csv":
                    exportData = await _exportService.ExportToCsvAsync(eventsList, request.IncludeRawData);
                    fileName = $"security-events-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                    mimeType = "text/csv";
                    break;

                case "json":
                    exportData = await _exportService.ExportToJsonAsync(eventsList, request.IncludeRawData);
                    fileName = $"security-events-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
                    mimeType = "application/json";
                    break;

                case "pdf":
                    exportData = await _exportService.ExportToPdfAsync(eventsList, request.IncludeSummary, request.IncludeRecommendations);
                    fileName = $"security-events-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
                    mimeType = "application/pdf";
                    break;

                default:
                    return BadRequest(new { message = $"Unsupported export format: {request.Format}" });
            }

            _logger.LogInformation("Successfully exported {Count} security events to {Format} format", 
                eventsList.Count, request.Format);

            return File(exportData, mimeType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting security events");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Export filtered security events with GET request (for direct download links)
    /// </summary>
    [HttpGet("security-events/{format}")]
    public async Task<IActionResult> ExportSecurityEventsGet(
        string format,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? riskLevels = null,
        [FromQuery] string? eventTypes = null,
        [FromQuery] bool includeRawData = false,
        [FromQuery] bool includeSummary = true,
        [FromQuery] bool includeRecommendations = true)
    {
        try
        {
            var request = new ExportRequest
            {
                Format = format,
                StartDate = startDate,
                EndDate = endDate,
                RiskLevels = !string.IsNullOrEmpty(riskLevels) 
                    ? riskLevels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToArray() 
                    : Array.Empty<string>(),
                EventTypes = !string.IsNullOrEmpty(eventTypes) 
                    ? eventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim()).ToArray() 
                    : Array.Empty<string>(),
                IncludeRawData = includeRawData,
                IncludeSummary = includeSummary,
                IncludeRecommendations = includeRecommendations
            };

            return await ExportSecurityEvents(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting security events via GET");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get export statistics
    /// </summary>
    [HttpGet("stats")]
    public Task<IActionResult> GetExportStats(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? riskLevels = null,
        [FromQuery] string? eventTypes = null)
    {
        try
        {
            var filterCriteria = new Dictionary<string, object>();

            if (startDate.HasValue) filterCriteria["startDate"] = startDate.Value;
            if (endDate.HasValue) filterCriteria["endDate"] = endDate.Value;

            if (!string.IsNullOrEmpty(riskLevels))
            {
                var riskLevelList = riskLevels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (riskLevelList.Any()) filterCriteria["riskLevels"] = riskLevelList;
            }

            if (!string.IsNullOrEmpty(eventTypes))
            {
                var eventTypeList = eventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (eventTypeList.Any()) filterCriteria["eventTypes"] = eventTypeList;
            }

            var totalCount = _securityEventStore.GetTotalCount(filterCriteria);
            var events = _securityEventStore.GetSecurityEvents(1, totalCount, filterCriteria);
            var eventsList = events.ToList();

            var stats = new
            {
                totalEvents = totalCount,
                riskLevelDistribution = eventsList.GroupBy(e => e.RiskLevel)
                    .ToDictionary(g => g.Key, g => g.Count()),
                eventTypeDistribution = eventsList.GroupBy(e => e.EventType.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                dateRange = eventsList.Any() ? new
                {
                    earliest = eventsList.Min(e => e.OriginalEvent.Time),
                    latest = eventsList.Max(e => e.OriginalEvent.Time)
                } : null,
                averageConfidence = eventsList.Any() 
                    ? Math.Round(eventsList.Average(e => e.Confidence), 2) 
                    : 0,
                enhancedEvents = eventsList.Count(e => e.IsEnhanced),
                deterministicEvents = eventsList.Count(e => e.IsDeterministic),
                correlationBasedEvents = eventsList.Count(e => e.IsCorrelationBased)
            };

            return Task.FromResult<IActionResult>(Ok(new { data = stats }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export statistics");
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }

    private Dictionary<string, object> BuildFilterCriteria(ExportRequest request)
    {
        var filterCriteria = new Dictionary<string, object>();

        if (request.StartDate.HasValue) 
            filterCriteria["startDate"] = request.StartDate.Value;
        
        if (request.EndDate.HasValue) 
            filterCriteria["endDate"] = request.EndDate.Value;

        if (request.RiskLevels.Any()) 
            filterCriteria["riskLevels"] = request.RiskLevels;

        if (request.EventTypes.Any()) 
            filterCriteria["eventTypes"] = request.EventTypes;

        // Apply additional filters from the Filters dictionary
        foreach (var filter in request.Filters)
        {
            filterCriteria[filter.Key] = filter.Value;
        }

        return filterCriteria;
    }
}
