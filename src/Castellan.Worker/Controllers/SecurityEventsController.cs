using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Diagnostics;
using Castellan.Worker.Models;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/security-events")]
[Authorize]
public class SecurityEventsController : ControllerBase
{
    private readonly ILogger<SecurityEventsController> _logger;
    private readonly IVectorStore _vectorStore;
    private readonly ISecurityEventStore _securityEventStore;
    private readonly IAdvancedSearchService _advancedSearchService;
    private readonly ISearchHistoryService _searchHistoryService;

    public SecurityEventsController(
        ILogger<SecurityEventsController> logger, 
        IVectorStore vectorStore, 
        ISecurityEventStore securityEventStore,
        IAdvancedSearchService advancedSearchService,
        ISearchHistoryService searchHistoryService)
    {
        _logger = logger;
        _vectorStore = vectorStore;
        _securityEventStore = securityEventStore;
        _advancedSearchService = advancedSearchService;
        _searchHistoryService = searchHistoryService;
    }

    [HttpGet]
    public Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int? perPage = null,
        [FromQuery] int? limit = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        [FromQuery] string? filter = null,
        
        // Existing single-value filters (maintain compatibility)
        [FromQuery] string? riskLevel = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? machine = null,
        [FromQuery] string? user = null,
        [FromQuery] string? source = null,
        
        // New advanced search parameters for v0.4.0
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? eventTypes = null,           // CSV: "Failed Login,Malware"
        [FromQuery] string? riskLevels = null,           // CSV: "high,critical"
        [FromQuery] string? search = null,               // Full-text search
        [FromQuery] float? minConfidence = null,
        [FromQuery] float? maxConfidence = null,
        [FromQuery] float? minCorrelationScore = null,
        [FromQuery] float? maxCorrelationScore = null,
        [FromQuery] string? status = null,
        [FromQuery] string? mitreTechnique = null)
    {
        try
        {
            // Use limit parameter if perPage is not provided (for react-admin compatibility)
            int pageSize = perPage ?? limit ?? 10;
            
            _logger.LogInformation("Getting security events list - page: {Page}, pageSize: {PageSize}, riskLevel: {RiskLevel}, eventType: {EventType}, search: {Search}", 
                page, pageSize, riskLevel, eventType, search);

            // Build filter criteria - v0.4.0 Advanced Search Enhancement
            var filterCriteria = new Dictionary<string, object>();
            
            // Backward compatibility: Single-value filters (existing)
            if (!string.IsNullOrEmpty(riskLevel)) filterCriteria["riskLevel"] = riskLevel;
            if (!string.IsNullOrEmpty(eventType)) filterCriteria["eventType"] = eventType;
            if (!string.IsNullOrEmpty(machine)) filterCriteria["machine"] = machine;
            if (!string.IsNullOrEmpty(user)) filterCriteria["user"] = user;
            if (!string.IsNullOrEmpty(source)) filterCriteria["source"] = source;
            
            // Advanced filters (new in v0.4.0)
            if (startDate.HasValue) filterCriteria["startDate"] = startDate.Value;
            if (endDate.HasValue) filterCriteria["endDate"] = endDate.Value;
            
            // Multi-select filters (comma-separated values)
            if (!string.IsNullOrEmpty(eventTypes)) 
            {
                var eventTypeList = eventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (eventTypeList.Any()) filterCriteria["eventTypes"] = eventTypeList;
            }
            
            if (!string.IsNullOrEmpty(riskLevels))
            {
                var riskLevelList = riskLevels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                if (riskLevelList.Any()) filterCriteria["riskLevels"] = riskLevelList;
            }
            
            // Full-text search - v0.5.0 Enhancement with FTS5
            if (!string.IsNullOrEmpty(search)) 
            {
                filterCriteria["search"] = search;
                filterCriteria["useFullTextSearch"] = true;  // Enable FTS5 search
            }
            
            // Numeric range filters
            if (minConfidence.HasValue) filterCriteria["minConfidence"] = minConfidence.Value;
            if (maxConfidence.HasValue) filterCriteria["maxConfidence"] = maxConfidence.Value;
            if (minCorrelationScore.HasValue) filterCriteria["minCorrelationScore"] = minCorrelationScore.Value;
            if (maxCorrelationScore.HasValue) filterCriteria["maxCorrelationScore"] = maxCorrelationScore.Value;
            
            // Other filters
            if (!string.IsNullOrEmpty(status)) filterCriteria["status"] = status;
            if (!string.IsNullOrEmpty(mitreTechnique)) filterCriteria["mitreTechnique"] = mitreTechnique;

            // Get filtered security events from the store
            var securityEvents = _securityEventStore.GetSecurityEvents(page, pageSize, filterCriteria);
            var securityEventDtos = securityEvents.Select(ConvertToDto).ToList();
            var totalCount = _securityEventStore.GetTotalCount(filterCriteria);

            var response = new
            {
                data = securityEventDtos,
                total = totalCount,
                page,
                perPage = pageSize
            };

            return Task.FromResult<IActionResult>(Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security events list");
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }

    [HttpGet("{id}")]
    public Task<IActionResult> GetOne(string id)
    {
        try
        {
            _logger.LogInformation("Getting security event: {Id}", id);

            var securityEvent = _securityEventStore.GetSecurityEvent(id);

            if (securityEvent == null)
            {
                return Task.FromResult<IActionResult>(NotFound(new { message = "Security event not found" }));
            }

            return Task.FromResult<IActionResult>(Ok(new { data = ConvertToDto(securityEvent) }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security event: {Id}", id);
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }

    /// <summary>
    /// Advanced search endpoint with enhanced filtering and full-text search capabilities
    /// v0.5.0 Feature with Search History Recording
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> AdvancedSearch([FromBody] AdvancedSearchRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Advanced search request: {@Request}", request);

            var searchResult = await _advancedSearchService.SearchAsync(request);
            
            stopwatch.Stop();
            
            // Record search in history (v0.5.0 feature)
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                try
                {
                    await _searchHistoryService.AddSearchToHistoryAsync(
                        userId, request, searchResult.TotalCount, (int)stopwatch.ElapsedMilliseconds);
                }
                catch (Exception historyEx)
                {
                    // Log but don't fail the search if history recording fails
                    _logger.LogWarning(historyEx, "Failed to record search in history for user: {UserId}", userId);
                }
            }
            
            var response = new
            {
                data = searchResult.Results.Select(ConvertToDto).ToList(),
                total = searchResult.TotalCount,
                page = request.Page,
                perPage = request.PageSize,
                searchMetadata = new
                {
                    queryTime = searchResult.QueryTimeMs,
                    useFullTextSearch = !string.IsNullOrEmpty(request.FullTextQuery),
                    appliedFilters = searchResult.AppliedFilters
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error performing advanced search");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPut("{id}")]
    public Task<IActionResult> Update(string id, [FromBody] SecurityEventUpdateRequest request)
    {
        try
        {
            _logger.LogInformation("Updating security event: {Id}", id);

            // In production, this would update the actual security event
            var mockEvents = GenerateMockSecurityEvents();
            var securityEvent = mockEvents.FirstOrDefault(e => e.Id == id);

            if (securityEvent == null)
            {
                return Task.FromResult<IActionResult>(NotFound(new { message = "Security event not found" }));
            }

            // Update fields from request
            securityEvent.Status = request.Status ?? securityEvent.Status;
            securityEvent.AssignedTo = request.AssignedTo ?? securityEvent.AssignedTo;
            securityEvent.Notes = request.Notes ?? securityEvent.Notes;

            return Task.FromResult<IActionResult>(Ok(new { data = securityEvent }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating security event: {Id}", id);
            return Task.FromResult<IActionResult>(StatusCode(500, new { message = "Internal server error" }));
        }
    }

    private SecurityEventDto ConvertToDto(SecurityEvent securityEvent)
    {
        var originalEvent = securityEvent.OriginalEvent;
        
        return new SecurityEventDto
        {
            Id = securityEvent.Id,
            EventType = securityEvent.EventType.ToString(),
            Timestamp = originalEvent.Time.DateTime,
            Source = originalEvent.Channel,
            EventId = originalEvent.EventId,
            Level = securityEvent.RiskLevel switch
            {
                "critical" => "Critical",
                "high" => "High", 
                "medium" => "Medium",
                "low" => "Low",
                _ => "Unknown"
            },
            RiskLevel = securityEvent.RiskLevel,
            Machine = originalEvent.Host,
            User = originalEvent.User,
            Message = securityEvent.Summary,
            MitreAttack = securityEvent.MitreTechniques,
            CorrelationScore = (float)securityEvent.CorrelationScore,
            BurstScore = (float)securityEvent.BurstScore,
            AnomalyScore = (float)securityEvent.AnomalyScore,
            Confidence = (float)securityEvent.Confidence,
            Status = "Open",
            AssignedTo = null,
            Notes = null,
            IPAddresses = ExtractIPAddresses(originalEvent.Message),
            EnrichedIPs = ParseEnrichedIPs(securityEvent.EnrichmentData)
        };
    }

    private string[] ExtractIPAddresses(string description)
    {
        // Simple IP extraction - in production this would be more sophisticated
        var ipPattern = @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b";
        var matches = System.Text.RegularExpressions.Regex.Matches(description, ipPattern);
        return matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).Distinct().ToArray();
    }

    private IPEnrichmentDto[] ParseEnrichedIPs(string? enrichmentData)
    {
        if (string.IsNullOrEmpty(enrichmentData))
            return Array.Empty<IPEnrichmentDto>();

        try
        {
            // Parse JSON enrichment data
            var jsonDoc = System.Text.Json.JsonDocument.Parse(enrichmentData);
            var root = jsonDoc.RootElement;

            // Handle single object or array
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var results = new List<IPEnrichmentDto>();
                foreach (var item in root.EnumerateArray())
                {
                    results.Add(ParseSingleEnrichment(item));
                }
                return results.ToArray();
            }
            else if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                return new[] { ParseSingleEnrichment(root) };
            }

            return Array.Empty<IPEnrichmentDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse enrichment data: {EnrichmentData}", enrichmentData);
            return Array.Empty<IPEnrichmentDto>();
        }
    }

    private IPEnrichmentDto ParseSingleEnrichment(System.Text.Json.JsonElement element)
    {
        return new IPEnrichmentDto
        {
            IP = GetJsonString(element, "ipAddress") ?? GetJsonString(element, "IP") ?? "Unknown",
            Country = GetJsonString(element, "country") ?? GetJsonString(element, "Country") ?? "Unknown",
            City = GetJsonString(element, "city") ?? GetJsonString(element, "City") ?? "Unknown",
            ASN = GetJsonString(element, "asn") ?? GetJsonString(element, "ASN") ?? "Unknown",
            IsHighRisk = GetJsonBool(element, "isHighRisk") ?? GetJsonBool(element, "IsHighRisk") ?? false
        };
    }

    private string? GetJsonString(System.Text.Json.JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private bool? GetJsonBool(System.Text.Json.JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == System.Text.Json.JsonValueKind.True) return true;
            if (prop.ValueKind == System.Text.Json.JsonValueKind.False) return false;
        }
        return null;
    }

    private List<SecurityEventDto> GenerateMockSecurityEvents()
    {
        return new List<SecurityEventDto>
        {
            new SecurityEventDto
            {
                Id = "1",
                EventType = "Failed Login",
                Timestamp = DateTime.UtcNow.AddMinutes(-15),
                Source = "Security",
                EventId = 4625,
                Level = "High",
                RiskLevel = "high",
                Machine = Environment.MachineName,
                User = "admin",
                Message = "An account failed to log on",
                MitreAttack = new[] { "T1110.001 - Password Guessing" },
                CorrelationScore = 0.85f,
                BurstScore = 0.7f,
                AnomalyScore = 0.9f,
                Confidence = 0.88f,
                Status = "Open",
                AssignedTo = null,
                Notes = null,
                IPAddresses = new[] { "192.168.1.100", "10.0.0.50" },
                EnrichedIPs = new[]
                {
                    new IPEnrichmentDto
                    {
                        IP = "192.168.1.100",
                        Country = "US",
                        City = "New York",
                        ASN = "AS15169 Google LLC",
                        IsHighRisk = false
                    }
                }
            },
            new SecurityEventDto
            {
                Id = "2",
                EventType = "Privilege Escalation",
                Timestamp = DateTime.UtcNow.AddMinutes(-30),
                Source = "Security",
                EventId = 4672,
                Level = "Critical",
                RiskLevel = "critical",
                Machine = Environment.MachineName,
                User = "system",
                Message = "Special privileges assigned to new logon",
                MitreAttack = new[] { "T1548 - Abuse Elevation Control Mechanism" },
                CorrelationScore = 0.95f,
                BurstScore = 0.8f,
                AnomalyScore = 0.92f,
                Confidence = 0.94f,
                Status = "In Progress",
                AssignedTo = "Security Team",
                Notes = "Investigating unusual privilege escalation",
                IPAddresses = new[] { "192.168.1.50" },
                EnrichedIPs = new[]
                {
                    new IPEnrichmentDto
                    {
                        IP = "192.168.1.50",
                        Country = "US",
                        City = "Seattle",
                        ASN = "AS8075 Microsoft Corporation",
                        IsHighRisk = false
                    }
                }
            }
        };
    }
}

// DTOs
public class SecurityEventDto
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string Level { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string[] MitreAttack { get; set; } = Array.Empty<string>();
    public float CorrelationScore { get; set; }
    public float BurstScore { get; set; }
    public float AnomalyScore { get; set; }
    public float Confidence { get; set; }
    public string Status { get; set; } = "Open";
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
    public string[] IPAddresses { get; set; } = Array.Empty<string>();
    public IPEnrichmentDto[] EnrichedIPs { get; set; } = Array.Empty<IPEnrichmentDto>();
}

public class SecurityEventUpdateRequest
{
    public string? Status { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
}

public class IPEnrichmentDto
{
    public string IP { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ASN { get; set; } = string.Empty;
    public bool IsHighRisk { get; set; }
}