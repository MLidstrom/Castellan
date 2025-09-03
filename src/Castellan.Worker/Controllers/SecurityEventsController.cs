using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public SecurityEventsController(ILogger<SecurityEventsController> logger, IVectorStore vectorStore, ISecurityEventStore securityEventStore)
    {
        _logger = logger;
        _vectorStore = vectorStore;
        _securityEventStore = securityEventStore;
    }

    [HttpGet]
    public Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int? perPage = null,
        [FromQuery] int? limit = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? riskLevel = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? machine = null,
        [FromQuery] string? user = null,
        [FromQuery] string? source = null)
    {
        try
        {
            // Use limit parameter if perPage is not provided (for react-admin compatibility)
            int pageSize = perPage ?? limit ?? 10;
            
            _logger.LogInformation("Getting security events list - page: {Page}, pageSize: {PageSize}, riskLevel: {RiskLevel}, eventType: {EventType}", 
                page, pageSize, riskLevel, eventType);

            // Build filter criteria
            var filterCriteria = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(riskLevel)) filterCriteria["riskLevel"] = riskLevel;
            if (!string.IsNullOrEmpty(eventType)) filterCriteria["eventType"] = eventType;
            if (!string.IsNullOrEmpty(machine)) filterCriteria["machine"] = machine;
            if (!string.IsNullOrEmpty(user)) filterCriteria["user"] = user;
            if (!string.IsNullOrEmpty(source)) filterCriteria["source"] = source;

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
            // Simple parsing - in production this would be more robust
            return new[]
            {
                new IPEnrichmentDto
                {
                    IP = "Unknown",
                    Country = "Unknown",
                    City = "Unknown",  
                    ASN = "Unknown",
                    IsHighRisk = false
                }
            };
        }
        catch
        {
            return Array.Empty<IPEnrichmentDto>();
        }
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