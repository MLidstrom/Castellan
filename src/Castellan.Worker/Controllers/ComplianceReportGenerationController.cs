using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services.Compliance;

namespace Castellan.Worker.Controllers
{
    [ApiController]
    [Route("api/compliance-report-generation")]
    [Authorize]
    public class ComplianceReportGenerationController : ControllerBase
    {
        private readonly IComplianceReportGenerationService _reportGenerationService;
        private readonly ILogger<ComplianceReportGenerationController> _logger;

        public ComplianceReportGenerationController(
            IComplianceReportGenerationService reportGenerationService,
            ILogger<ComplianceReportGenerationController> logger)
        {
            _reportGenerationService = reportGenerationService;
            _logger = logger;
        }

        [HttpPost("comprehensive/{framework}")]
        public async Task<IActionResult> GenerateComprehensiveReport(
            string framework,
            [FromBody] GenerateReportRequest? request = null)
        {
            try
            {
                var format = request?.Format != null ? request.Format : ReportFormat.Json;
                var document = await _reportGenerationService.GenerateComprehensiveReportAsync(framework, format);

                if (format == ReportFormat.Json)
                {
                    return Ok(document);
                }

                var exportData = await _reportGenerationService.ExportReportAsync(document, format);
                var contentType = GetContentType(format);
                var fileName = $"compliance-report-{framework}-{DateTime.UtcNow:yyyyMMdd}.{GetFileExtension(format)}";

                return File(exportData, contentType, fileName);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid framework requested: {Framework}", framework);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating comprehensive report for framework: {Framework}", framework);
                return StatusCode(500, new { error = "Internal server error generating report" });
            }
        }

        [HttpPost("executive-summary")]
        public async Task<IActionResult> GenerateExecutiveSummary([FromBody] ExecutiveSummaryRequest? request = null)
        {
            try
            {
                var format = request?.Format != null ? request.Format : ReportFormat.Json;
                var document = await _reportGenerationService.GenerateExecutiveSummaryAsync(request?.Frameworks);

                if (format == ReportFormat.Json)
                {
                    return Ok(document);
                }

                var exportData = await _reportGenerationService.ExportReportAsync(document, format);
                var contentType = GetContentType(format);
                var fileName = $"executive-summary-{DateTime.UtcNow:yyyyMMdd}.{GetFileExtension(format)}";

                return File(exportData, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating executive summary");
                return StatusCode(500, new { error = "Internal server error generating executive summary" });
            }
        }

        [HttpPost("comparison")]
        public async Task<IActionResult> GenerateComparisonReport([FromBody] ComparisonReportRequest request)
        {
            try
            {
                if (request?.Frameworks == null || request.Frameworks.Count < 2)
                {
                    return BadRequest(new { error = "At least two frameworks are required for comparison" });
                }

                var document = await _reportGenerationService.GenerateComparisonReportAsync(request.Frameworks);

                if (request.Format == ReportFormat.Json)
                {
                    return Ok(document);
                }

                var exportData = await _reportGenerationService.ExportReportAsync(document, request.Format);
                var contentType = GetContentType(request.Format);
                var fileName = $"comparison-report-{DateTime.UtcNow:yyyyMMdd}.{GetFileExtension(request.Format)}";

                return File(exportData, contentType, fileName);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid frameworks for comparison: {Frameworks}", string.Join(", ", request?.Frameworks ?? new List<string>()));
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating comparison report");
                return StatusCode(500, new { error = "Internal server error generating comparison report" });
            }
        }

        [HttpPost("trend/{framework}")]
        public async Task<IActionResult> GenerateTrendReport(
            string framework,
            [FromBody] TrendReportRequest? request = null)
        {
            try
            {
                var days = request?.Days ?? 30;
                var format = request?.Format != null ? request.Format : ReportFormat.Json;

                if (days < 1 || days > 365)
                {
                    return BadRequest(new { error = "Days must be between 1 and 365" });
                }

                var document = await _reportGenerationService.GenerateTrendReportAsync(framework, days);

                if (format == ReportFormat.Json)
                {
                    return Ok(document);
                }

                var exportData = await _reportGenerationService.ExportReportAsync(document, format);
                var contentType = GetContentType(format);
                var fileName = $"trend-report-{framework}-{days}days-{DateTime.UtcNow:yyyyMMdd}.{GetFileExtension(format)}";

                return File(exportData, contentType, fileName);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid framework for trend report: {Framework}", framework);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating trend report for framework: {Framework}", framework);
                return StatusCode(500, new { error = "Internal server error generating trend report" });
            }
        }

        [HttpGet("formats")]
        public IActionResult GetSupportedFormats()
        {
            var formats = Enum.GetValues<ReportFormat>()
                .Select(f => new {
                    name = f.ToString(),
                    value = (int)f,
                    description = GetFormatDescription(f)
                })
                .ToList();

            return Ok(new { formats });
        }

        [HttpGet("audiences")]
        public IActionResult GetSupportedAudiences()
        {
            var audiences = Enum.GetValues<ReportAudience>()
                .Select(a => new {
                    name = a.ToString(),
                    value = (int)a,
                    description = GetAudienceDescription(a)
                })
                .ToList();

            return Ok(new { audiences });
        }

        private static string GetContentType(ReportFormat format) => format switch
        {
            ReportFormat.Pdf => "application/pdf",
            ReportFormat.Html => "text/html",
            ReportFormat.Csv => "text/csv",
            ReportFormat.Markdown => "text/markdown",
            ReportFormat.Json => "application/json",
            _ => "application/octet-stream"
        };

        private static string GetFileExtension(ReportFormat format) => format switch
        {
            ReportFormat.Pdf => "pdf",
            ReportFormat.Html => "html",
            ReportFormat.Csv => "csv",
            ReportFormat.Markdown => "md",
            ReportFormat.Json => "json",
            _ => "bin"
        };

        private static string GetFormatDescription(ReportFormat format) => format switch
        {
            ReportFormat.Json => "Structured JSON data format for API consumption",
            ReportFormat.Html => "Rich HTML format with styling and charts",
            ReportFormat.Pdf => "Professional PDF document with charts and formatting",
            ReportFormat.Csv => "Comma-separated values for data analysis",
            ReportFormat.Markdown => "Markdown format for documentation systems",
            _ => "Unknown format"
        };

        private static string GetAudienceDescription(ReportAudience audience) => audience switch
        {
            ReportAudience.Executive => "High-level summary for executives and leadership",
            ReportAudience.Technical => "Detailed technical information for IT teams",
            ReportAudience.Auditor => "Comprehensive evidence for compliance auditors",
            ReportAudience.Operations => "Operational insights for day-to-day management",
            _ => "Unknown audience"
        };
    }

    public class GenerateReportRequest
    {
        public ReportFormat Format { get; set; } = ReportFormat.Json;
        public ReportAudience Audience { get; set; } = ReportAudience.Technical;
    }

    public class ExecutiveSummaryRequest
    {
        public List<string>? Frameworks { get; set; }
        public ReportFormat Format { get; set; } = ReportFormat.Json;
        public ReportAudience Audience { get; set; } = ReportAudience.Executive;
    }

    public class ComparisonReportRequest
    {
        public required List<string> Frameworks { get; set; }
        public ReportFormat Format { get; set; } = ReportFormat.Json;
        public ReportAudience Audience { get; set; } = ReportAudience.Technical;
    }

    public class TrendReportRequest
    {
        public int Days { get; set; } = 30;
        public ReportFormat Format { get; set; } = ReportFormat.Json;
        public ReportAudience Audience { get; set; } = ReportAudience.Technical;
    }
}