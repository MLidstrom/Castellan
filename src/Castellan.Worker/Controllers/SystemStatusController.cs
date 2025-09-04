using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/system-status")]
// [Authorize] - Temporarily disabled for testing
public class SystemStatusController : ControllerBase
{
    private readonly ILogger<SystemStatusController> _logger;
    private readonly SystemHealthService _systemHealthService;

    public SystemStatusController(ILogger<SystemStatusController> logger, SystemHealthService systemHealthService)
    {
        _logger = logger;
        _systemHealthService = systemHealthService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int? perPage = null,
        [FromQuery] int? limit = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        [FromQuery] string? filter = null)
    {
        try
        {
            // Use limit parameter if perPage is not provided (for react-admin compatibility)
            int pageSize = perPage ?? limit ?? 10;
            
            _logger.LogInformation("Getting system status list - page: {Page}, pageSize: {PageSize}", page, pageSize);

            // Get real system status data
            var systemStatuses = await _systemHealthService.GetSystemStatusAsync();
            
            // Apply pagination
            var skip = (page - 1) * pageSize;
            var pagedStatuses = systemStatuses.Skip(skip).Take(pageSize).ToList();

            var response = new
            {
                data = pagedStatuses,
                total = systemStatuses.Count,
                page,
                perPage = pageSize
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system status list");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("test")]
    public IActionResult GetTest()
    {
        try
        {
            _logger.LogInformation("Getting test system status data");
            
            var testData = new
            {
                data = new[]
                {
                    new
                    {
                        id = "test-1",
                        component = "Test Component",
                        status = "Healthy",
                        isHealthy = true,
                        lastCheck = DateTime.UtcNow,
                        responseTime = 42,
                        uptime = "99.9%",
                        details = "This is a test component",
                        errorCount = 0,
                        warningCount = 0
                    }
                },
                total = 1,
                page = 1,
                perPage = 10
            };
            
            return Ok(testData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting test system status data");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(string id)
    {
        try
        {
            _logger.LogInformation("Getting system status: {Id}", id);

            var systemStatuses = await _systemHealthService.GetSystemStatusAsync();
            var status = systemStatuses.FirstOrDefault(s => s.Id == id);

            if (status == null)
            {
                return NotFound(new { message = "System status not found" });
            }

            return Ok(new { data = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system status: {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("~/api/system/edition")]
    public IActionResult GetEditionInfo()
    {
        try
        {
            _logger.LogInformation("Getting edition information");

            var editionInfo = new
            {
                edition = "Castellan",
                features = new
                {
                    // Core Castellan features (all available)
                    localMonitoring = EditionFeatures.Features.LocalMonitoring,
                    basicAlerting = EditionFeatures.Features.BasicAlerting,
                    vectorDatabase = EditionFeatures.Features.VectorDatabase,
                    aiThreatDetection = EditionFeatures.Features.AIThreatDetection,
                    securityEventDetection = EditionFeatures.Features.SecurityEventDetection,
                    basicAuditLogging = EditionFeatures.Features.BasicAuditLogging,
                    ipEnrichment = EditionFeatures.Features.IPEnrichment,
                    performanceMonitoring = EditionFeatures.Features.PerformanceMonitoring,
                    notificationServices = EditionFeatures.Features.NotificationServices,
                    windowsEventLogs = EditionFeatures.Features.WindowsEventLogs,
                    powerShellMonitoring = EditionFeatures.Features.PowerShellMonitoring,
                    correlationEngine = EditionFeatures.Features.CorrelationEngine
                },
                buildInfo = new
                {
                    version = EditionFeatures.GetVersionString(),
                    buildDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    commit = "castellan"
                }
            };

            return Ok(editionInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting edition information");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private List<SystemStatusDto> GenerateMockSystemStatuses()
    {
        return new List<SystemStatusDto>
        {
            new SystemStatusDto
            {
                Id = "1",
                Component = "Qdrant Vector Database",
                Status = "Healthy",
                LastCheck = DateTime.UtcNow.AddMinutes(-2),
                ResponseTime = 15,
                Uptime = "99.9%",
                Details = "Connected on localhost:6333, vector operations nominal",
                ErrorCount = 0,
                WarningCount = 0
            },
            new SystemStatusDto
            {
                Id = "2",
                Component = "Ollama LLM Service",
                Status = "Healthy",
                LastCheck = DateTime.UtcNow.AddMinutes(-1),
                ResponseTime = 150,
                Uptime = "98.7%",
                Details = "Model: llama3.1:8b-instruct-q8_0, embedding model active",
                ErrorCount = 0,
                WarningCount = 1
            },
            new SystemStatusDto
            {
                Id = "3",
                Component = "Windows Event Log Collector",
                Status = "Healthy",
                LastCheck = DateTime.UtcNow,
                ResponseTime = 5,
                Uptime = "100%",
                Details = "Monitoring Security, System, and PowerShell channels",
                ErrorCount = 0,
                WarningCount = 0
            },
            new SystemStatusDto
            {
                Id = "4",
                Component = "Security Event Detector",
                Status = "Healthy",
                LastCheck = DateTime.UtcNow,
                ResponseTime = 8,
                Uptime = "100%",
                Details = "25+ detection rules active, PowerShell monitoring enabled",
                ErrorCount = 0,
                WarningCount = 0
            },
            new SystemStatusDto
            {
                Id = "5",
                Component = "IP Enrichment Service",
                Status = "Warning",
                LastCheck = DateTime.UtcNow.AddMinutes(-5),
                ResponseTime = 25,
                Uptime = "95.2%",
                Details = "MaxMind GeoLite2 database requires update",
                ErrorCount = 0,
                WarningCount = 3
            },
            new SystemStatusDto
            {
                Id = "6",
                Component = "Notification Services",
                Status = "Healthy",
                LastCheck = DateTime.UtcNow.AddMinutes(-1),
                ResponseTime = 12,
                Uptime = "99.5%",
                Details = "Desktop notifications operational",
                ErrorCount = 0,
                WarningCount = 0
            }
        };
    }
}

// DTOs
public class SystemStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsHealthy => Status?.Equals("Healthy", StringComparison.OrdinalIgnoreCase) == true;
    public DateTime LastCheck { get; set; }
    public int ResponseTime { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}