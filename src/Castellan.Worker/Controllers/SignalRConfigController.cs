using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("api/config")]
public class SignalRConfigController : ControllerBase
{
    private readonly SignalROptions _signalROptions;
    private readonly ILogger<SignalRConfigController> _logger;

    public SignalRConfigController(
        IOptions<SignalROptions> signalROptions,
        ILogger<SignalRConfigController> logger)
    {
        _signalROptions = signalROptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Get SignalR configuration for frontend
    /// </summary>
    [HttpGet("signalr")]
    public IActionResult GetSignalRConfig()
    {
        try
        {
            _logger.LogDebug("Getting SignalR configuration");

            var config = new SignalRConfigDto
            {
                RetryIntervalsMs = _signalROptions.RetryIntervalsMs
            };

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SignalR configuration");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}

public class SignalRConfigDto
{
    public List<int> RetryIntervalsMs { get; set; } = new();
}
