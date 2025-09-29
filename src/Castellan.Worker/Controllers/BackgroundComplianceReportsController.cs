using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Castellan.Worker.Services.Compliance;

namespace Castellan.Worker.Controllers;

[Authorize]
[ApiController]
[Route("api/background-compliance-reports")]
public class BackgroundComplianceReportsController : ControllerBase
{
    private readonly IBackgroundComplianceReportService _backgroundService;
    private readonly ILogger<BackgroundComplianceReportsController> _logger;

    public BackgroundComplianceReportsController(
        IBackgroundComplianceReportService backgroundService,
        ILogger<BackgroundComplianceReportsController> logger)
    {
        _backgroundService = backgroundService;
        _logger = logger;
    }

    /// <summary>
    /// Queue a compliance report for background generation
    /// </summary>
    [HttpPost("queue")]
    public async Task<IActionResult> QueueReportGeneration([FromBody] BackgroundReportRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            var jobId = await _backgroundService.QueueReportGenerationAsync(
                request.Framework,
                request.Format,
                request.Audience,
                userId);

            _logger.LogInformation("Queued background report generation for user {UserId}, framework {Framework}",
                userId, request.Framework);

            return Ok(new { jobId, status = "queued", message = "Report generation queued successfully" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for background report generation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing background report generation");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get the status of a background report job
    /// </summary>
    [HttpGet("status/{jobId}")]
    public async Task<IActionResult> GetReportStatus(string jobId)
    {
        try
        {
            var status = await _backgroundService.GetReportStatusAsync(jobId);

            if (status == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            return Ok(new { jobId, status = status.ToString().ToLowerInvariant() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report status for job {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Download a completed background report
    /// </summary>
    [HttpGet("download/{jobId}")]
    public async Task<IActionResult> DownloadReport(string jobId)
    {
        try
        {
            var status = await _backgroundService.GetReportStatusAsync(jobId);

            if (status == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            if (status != BackgroundReportStatus.Completed)
            {
                return BadRequest(new { error = $"Report is not ready. Status: {status}" });
            }

            var reportData = await _backgroundService.GetCompletedReportAsync(jobId);

            if (reportData == null)
            {
                return NotFound(new { error = "Report data not found" });
            }

            // Determine content type and filename based on job details
            var contentType = "application/octet-stream";
            var fileName = $"compliance-report-{jobId}.dat";

            // Note: In a real implementation, you might want to store the original format
            // with the job to provide proper content type and filename

            return File(reportData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading report for job {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// List all background report jobs for the current user
    /// </summary>
    [HttpGet("jobs")]
    public async Task<IActionResult> GetUserJobs()
    {
        try
        {
            // In a full implementation, this would filter jobs by user
            // For now, we'll return a simple message
            return Ok(new { message = "User job listing not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user jobs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Cancel a queued background report job
    /// </summary>
    [HttpDelete("cancel/{jobId}")]
    public async Task<IActionResult> CancelReportJob(string jobId)
    {
        try
        {
            var status = await _backgroundService.GetReportStatusAsync(jobId);

            if (status == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            if (status != BackgroundReportStatus.Queued)
            {
                return BadRequest(new { error = $"Cannot cancel job in status: {status}" });
            }

            // In a full implementation, you would mark the job as cancelled
            return Ok(new { message = "Job cancellation not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

public class BackgroundReportRequest
{
    public required string Framework { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Json;
    public ReportAudience Audience { get; set; } = ReportAudience.Technical;
}