
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services;
using System;
using System.Threading.Tasks;

namespace Castellan.Worker.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    [AllowAnonymous] // Explicitly allow anonymous access
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("trends")]
        public async Task<IActionResult> GetTrends(string metric = "TotalEvents", string timeRange = "7d", string groupBy = "day")
        {
            try
            {
                var data = await _analyticsService.GetHistoricalDataAsync(metric, timeRange, groupBy);
                return Ok(new { data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching trend data.", details = ex.Message });
            }
        }

        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast(string metric = "TotalEvents", int forecastPeriod = 7)
        {
            try
            {
                var forecast = await _analyticsService.GenerateForecastAsync(metric, forecastPeriod);
                return Ok(new { data = forecast });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while generating the forecast.", details = ex.Message });
            }
        }
    }
}

