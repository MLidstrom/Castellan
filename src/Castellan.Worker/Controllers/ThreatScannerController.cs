using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/threat-scanner")]
[Authorize]
public class ThreatScannerController : ControllerBase, IDisposable
{
    private readonly ILogger<ThreatScannerController> _logger;
    private readonly IThreatScanner _threatScanner;
    private readonly IThreatScanProgressStore _progressStore;
    private CancellationTokenSource? _currentScanCancellation;

    public ThreatScannerController(ILogger<ThreatScannerController> logger, IThreatScanner threatScanner, IThreatScanProgressStore progressStore)
    {
        _logger = logger;
        _threatScanner = threatScanner;
        _progressStore = progressStore;
    }

    public void Dispose()
    {
        _currentScanCancellation?.Dispose();
    }

    [HttpPost("full-scan")]
    public async Task<IActionResult> StartFullScan([FromQuery] bool async = true)
    {
        try
        {
            _logger.LogInformation("Starting full threat scan via API (async: {Async})", async);

            if (async)
            {
                // Create cancellation token for this scan
                _currentScanCancellation = new CancellationTokenSource();

                // Start scan asynchronously and return immediately
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _threatScanner.PerformFullScanAsync(_currentScanCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Full scan was cancelled");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background full scan");
                    }
                    finally
                    {
                        _currentScanCancellation?.Dispose();
                        _currentScanCancellation = null;
                    }
                });

                return Ok(new {
                    message = "Full scan started",
                    async = true,
                    note = "Use /api/threat-scanner/progress to monitor scan progress"
                });
            }
            else
            {
                // Wait for scan to complete (original behavior)
                var result = await _threatScanner.PerformFullScanAsync();
                return Ok(new {
                    message = "Full scan completed",
                    data = result
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing full scan");
            return StatusCode(500, new { message = "Error performing full scan", error = ex.Message });
        }
    }

    [HttpPost("quick-scan")]
    public async Task<IActionResult> StartQuickScan([FromQuery] bool async = true)
    {
        try
        {
            _logger.LogInformation("Starting quick threat scan via API (async: {Async})", async);
            
            if (async)
            {
                // Create cancellation token for this scan
                _currentScanCancellation = new CancellationTokenSource();

                // Start scan asynchronously and return immediately
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _threatScanner.PerformQuickScanAsync(_currentScanCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Quick scan was cancelled");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background quick scan");
                    }
                    finally
                    {
                        _currentScanCancellation?.Dispose();
                        _currentScanCancellation = null;
                    }
                });
                
                return Ok(new { 
                    message = "Quick scan started",
                    async = true,
                    note = "Use /api/threat-scanner/progress to monitor scan progress"
                });
            }
            else
            {
                // Wait for scan to complete (original behavior)
                var result = await _threatScanner.PerformQuickScanAsync();
                return Ok(new { 
                    message = "Quick scan completed", 
                    data = result 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing quick scan");
            return StatusCode(500, new { message = "Error performing quick scan", error = ex.Message });
        }
    }

    [HttpPost("scan-directory")]
    public async Task<IActionResult> ScanDirectory([FromBody] ScanDirectoryRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Path) || !Directory.Exists(request.Path))
            {
                return BadRequest(new { message = "Invalid directory path" });
            }

            _logger.LogInformation("Starting directory scan: {Path}", request.Path);
            var result = await _threatScanner.ScanDirectoryAsync(request.Path);
            
            return Ok(new { 
                message = "Directory scan completed", 
                data = result 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning directory: {Path}", request.Path);
            return StatusCode(500, new { message = "Error scanning directory", error = ex.Message });
        }
    }

    [HttpPost("scan-file")]
    public async Task<IActionResult> ScanFile([FromBody] ScanFileRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FilePath) || !System.IO.File.Exists(request.FilePath))
            {
                return BadRequest(new { message = "Invalid file path" });
            }

            _logger.LogInformation("Starting file scan: {FilePath}", request.FilePath);
            var result = await _threatScanner.ScanFileAsync(request.FilePath);
            
            return Ok(new { 
                message = "File scan completed", 
                data = result 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning file: {FilePath}", request.FilePath);
            return StatusCode(500, new { message = "Error scanning file", error = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetScanStatus()
    {
        try
        {
            var status = await _threatScanner.GetScanStatusAsync();
            return Ok(new { 
                status = status.ToString(),
                message = GetStatusMessage(status)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan status");
            return StatusCode(500, new { message = "Error getting scan status", error = ex.Message });
        }
    }
    
    [HttpGet("progress")]
    [AllowAnonymous]
    public IActionResult GetScanProgress()
    {
        try
        {
            _logger.LogInformation("🔍 Getting scan progress from progress store");
            var progress = _progressStore.GetCurrentProgress();

            if (progress == null)
            {
                _logger.LogInformation("🔍 No current progress found in store");
                return Ok(new {
                    progress = (object?)null,
                    message = "No scan in progress"
                });
            }

            _logger.LogInformation("🔍 Found progress for scanId: {ScanId}, status: {Status}, percent: {Percent}%",
                progress.ScanId, progress.Status, progress.PercentComplete);

            return Ok(new {
                progress = new
                {
                    scanId = progress.ScanId,
                    status = progress.Status.ToString(),
                    filesScanned = progress.FilesScanned,
                    totalEstimatedFiles = progress.TotalEstimatedFiles,
                    directoriesScanned = progress.DirectoriesScanned,
                    threatsFound = progress.ThreatsFound,
                    currentFile = progress.CurrentFile,
                    currentDirectory = progress.CurrentDirectory,
                    percentComplete = Math.Round(progress.PercentComplete, 1),
                    startTime = progress.StartTime,
                    elapsedTime = progress.ElapsedTime.ToString(@"mm\:ss"),
                    estimatedTimeRemaining = progress.EstimatedTimeRemaining?.ToString(@"mm\:ss"),
                    bytesScanned = progress.BytesScanned,
                    scanPhase = progress.ScanPhase
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan progress");
            return StatusCode(500, new { message = "Error getting scan progress", error = ex.Message });
        }
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> CancelScan()
    {
        try
        {
            // Cancel the scan using controller's cancellation token
            if (_currentScanCancellation != null)
            {
                _currentScanCancellation.Cancel();
                _logger.LogInformation("Scan cancellation requested via API - controller token cancelled");
            }

            // Also call service cancel for any additional cleanup
            await _threatScanner.CancelScanAsync();
            _logger.LogInformation("Scan cancellation requested via API");
            return Ok(new { message = "Scan cancellation requested" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling scan");
            return StatusCode(500, new { message = "Error cancelling scan", error = ex.Message });
        }
    }

    [HttpGet("last-result")]
    public async Task<IActionResult> GetLastScanResult()
    {
        try
        {
            var result = await _threatScanner.GetLastScanResultAsync();
            if (result == null)
            {
                return NotFound(new { message = "No scan results found" });
            }

            return Ok(new { data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last scan result");
            return StatusCode(500, new { message = "Error getting last scan result", error = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetScanHistory([FromQuery] int count = 10)
    {
        try
        {
            var history = await _threatScanner.GetScanHistoryAsync(count);
            return Ok(new { 
                data = history,
                total = history.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scan history");
            return StatusCode(500, new { message = "Error getting scan history", error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int? perPage = null,
        [FromQuery] int? limit = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        [FromQuery] string? filter = null)
    {
        // This is the default GET endpoint that React-Admin expects
        return await GetList(page, perPage, limit, sort, order, filter);
    }

    [HttpGet("list")]
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
            
            _logger.LogInformation("Getting threat scan results - page: {Page}, pageSize: {PageSize}", page, pageSize);

            var allResults = await _threatScanner.GetScanHistoryAsync(100); // Get more for pagination
            
            // Apply pagination
            var skip = (page - 1) * pageSize;
            var pagedResults = allResults.Skip(skip).Take(pageSize).ToList();

            var response = new
            {
                data = pagedResults.Select(ConvertToDto).ToList(),
                total = allResults.Count(),
                page,
                perPage = pageSize
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat scan list");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(string id)
    {
        try
        {
            _logger.LogInformation("Getting threat scan result: {Id}", id);

            var allResults = await _threatScanner.GetScanHistoryAsync(100);
            var result = allResults.FirstOrDefault(r => r.Id == id);

            if (result == null)
            {
                return NotFound(new { message = "Threat scan result not found" });
            }

            return Ok(new { data = ConvertToDto(result) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat scan result: {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private string GetStatusMessage(ThreatScanStatus status)
    {
        return status switch
        {
            ThreatScanStatus.NotStarted => "No scan in progress",
            ThreatScanStatus.Running => "Scan currently running",
            ThreatScanStatus.Completed => "Scan completed successfully - system clean",
            ThreatScanStatus.CompletedWithThreats => "Scan completed - security findings detected",
            ThreatScanStatus.Failed => "Scan failed",
            ThreatScanStatus.Cancelled => "Scan was cancelled",
            ThreatScanStatus.Paused => "Scan is paused",
            _ => "Unknown status"
        };
    }

    private ThreatScanDto ConvertToDto(ThreatScanResult scanResult)
    {
        return new ThreatScanDto
        {
            Id = scanResult.Id,
            ScanType = scanResult.ScanType.ToString(),
            Status = scanResult.Status.ToString(),
            StartTime = scanResult.StartTime,
            EndTime = scanResult.EndTime,
            Duration = scanResult.Duration.TotalMinutes,
            FilesScanned = scanResult.FilesScanned,
            DirectoriesScanned = scanResult.DirectoriesScanned,
            BytesScanned = scanResult.BytesScanned,
            ThreatsFound = scanResult.ThreatsFound,
            MalwareDetected = scanResult.MalwareDetected,
            BackdoorsDetected = scanResult.BackdoorsDetected,
            SuspiciousFiles = scanResult.SuspiciousFiles,
            RiskLevel = scanResult.RiskLevel.ToString(),
            Summary = scanResult.Summary,
            ErrorMessage = scanResult.ErrorMessage,
            ThreatDetails = scanResult.ThreatDetails.Take(5).ToList() // Limit for API response
        };
    }
}

// DTOs
public class ScanDirectoryRequest
{
    public string Path { get; set; } = string.Empty;
}

public class ScanFileRequest
{
    public string FilePath { get; set; } = string.Empty;
}

public class ThreatScanDto
{
    public string Id { get; set; } = string.Empty;
    public string ScanType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Duration { get; set; } // in minutes
    public int FilesScanned { get; set; }
    public int DirectoriesScanned { get; set; }
    public long BytesScanned { get; set; }
    public int ThreatsFound { get; set; }
    public int MalwareDetected { get; set; }
    public int BackdoorsDetected { get; set; }
    public int SuspiciousFiles { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public List<FileThreatResult> ThreatDetails { get; set; } = new();
}