using Microsoft.AspNetCore.Mvc;

namespace Castellan.Worker.Controllers;

[ApiController]
[Route("")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpGet("")]
    public IActionResult GetRoot()
    {
        return Ok(new {
            service = "Castellan Security Platform",
            version = "1.0.0",
            status = "running",
            timestamp = DateTime.UtcNow
        });
    }
}