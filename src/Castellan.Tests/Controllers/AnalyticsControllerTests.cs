using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Controllers;
using Castellan.Worker.Services;
using Castellan.Worker.Abstractions;
using Moq;
using Xunit;

namespace Castellan.Tests.Controllers;

[Trait("Category", "Unit")]
public class AnalyticsControllerTests
{
    private readonly Mock<AnalyticsService> _mockAnalyticsService;
    private readonly AnalyticsController _controller;

    public AnalyticsControllerTests()
    {
        var mockSecurityEventStore = new Mock<ISecurityEventStore>();
        _mockAnalyticsService = new Mock<AnalyticsService>(mockSecurityEventStore.Object);
        _controller = new AnalyticsController(_mockAnalyticsService.Object);
    }

    [Fact]
    public async Task GetTrends_WithDefaultParameters_ReturnsOkResult()
    {
        // Arrange
        var mockData = new List<HistoricalDataPoint>
        {
            new() { Timestamp = DateTime.UtcNow.AddDays(-1), Value = 100 },
            new() { Timestamp = DateTime.UtcNow, Value = 120 }
        };

        _mockAnalyticsService
            .Setup(s => s.GetHistoricalDataAsync("TotalEvents", "7d", "day"))
            .ReturnsAsync(mockData);

        // Act
        var result = await _controller.GetTrends();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        var value = okResult.Value as dynamic;
        value!.data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTrends_WithCustomParameters_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var metric = "CriticalEvents";
        var timeRange = "30d";
        var groupBy = "hour";
        var mockData = new List<HistoricalDataPoint>();

        _mockAnalyticsService
            .Setup(s => s.GetHistoricalDataAsync(metric, timeRange, groupBy))
            .ReturnsAsync(mockData);

        // Act
        var result = await _controller.GetTrends(metric, timeRange, groupBy);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockAnalyticsService.Verify(
            s => s.GetHistoricalDataAsync(metric, timeRange, groupBy),
            Times.Once);
    }

    [Fact]
    public async Task GetTrends_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        var expectedException = new Exception("Database connection failed");
        _mockAnalyticsService
            .Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _controller.GetTrends();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);

        var value = objectResult.Value as dynamic;
        value!.message.Should().Be("An error occurred while fetching trend data.");
        value!.details.Should().Be(expectedException.Message);
    }

    [Fact]
    public async Task GetForecast_WithDefaultParameters_ReturnsOkResult()
    {
        // Arrange
        var mockForecast = new ForecastResult
        {
            HistoricalData = new List<HistoricalDataPoint>
            {
                new() { Timestamp = DateTime.UtcNow.AddDays(-1), Value = 100 }
            },
            ForecastedData = new List<ForecastDataPoint>
            {
                new() { Timestamp = DateTime.UtcNow, ForecastValue = 110, LowerBound = 90, UpperBound = 130 }
            }
        };

        _mockAnalyticsService
            .Setup(s => s.GenerateForecastAsync("TotalEvents", 7))
            .ReturnsAsync(mockForecast);

        // Act
        var result = await _controller.GetForecast();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        var value = okResult.Value as dynamic;
        value!.data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetForecast_WithCustomParameters_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var metric = "HighRiskEvents";
        var forecastPeriod = 30;
        var mockForecast = new ForecastResult
        {
            HistoricalData = new List<HistoricalDataPoint>(),
            ForecastedData = new List<ForecastDataPoint>()
        };

        _mockAnalyticsService
            .Setup(s => s.GenerateForecastAsync(metric, forecastPeriod))
            .ReturnsAsync(mockForecast);

        // Act
        var result = await _controller.GetForecast(metric, forecastPeriod);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockAnalyticsService.Verify(
            s => s.GenerateForecastAsync(metric, forecastPeriod),
            Times.Once);
    }

    [Fact]
    public async Task GetForecast_WhenServiceThrowsException_Returns500()
    {
        // Arrange
        var expectedException = new Exception("ML model training failed");
        _mockAnalyticsService
            .Setup(s => s.GenerateForecastAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _controller.GetForecast();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);

        var value = objectResult.Value as dynamic;
        value!.message.Should().Be("An error occurred while generating the forecast.");
        value!.details.Should().Be(expectedException.Message);
    }

    [Theory]
    [InlineData("", "7d", "day")]
    [InlineData("TotalEvents", "", "day")]
    [InlineData("TotalEvents", "7d", "")]
    [InlineData(null, "7d", "day")]
    [InlineData("TotalEvents", null, "day")]
    [InlineData("TotalEvents", "7d", null)]
    public async Task GetTrends_WithNullOrEmptyParameters_StillCallsService(string metric, string timeRange, string groupBy)
    {
        // Arrange
        var mockData = new List<HistoricalDataPoint>();
        _mockAnalyticsService
            .Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(mockData);

        // Act
        var result = await _controller.GetTrends(metric, timeRange, groupBy);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockAnalyticsService.Verify(
            s => s.GetHistoricalDataAsync(
                metric ?? "TotalEvents",
                timeRange ?? "7d",
                groupBy ?? "day"),
            Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(365)]
    public async Task GetForecast_WithVariousForecastPeriods_CallsService(int forecastPeriod)
    {
        // Arrange
        var mockForecast = new ForecastResult
        {
            HistoricalData = new List<HistoricalDataPoint>(),
            ForecastedData = new List<ForecastDataPoint>()
        };

        _mockAnalyticsService
            .Setup(s => s.GenerateForecastAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(mockForecast);

        // Act
        var result = await _controller.GetForecast("TotalEvents", forecastPeriod);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockAnalyticsService.Verify(
            s => s.GenerateForecastAsync("TotalEvents", forecastPeriod),
            Times.Once);
    }

    [Fact]
    public async Task GetTrends_ReturnsDataInCorrectFormat()
    {
        // Arrange
        var mockData = new List<HistoricalDataPoint>
        {
            new() { Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Value = 100 },
            new() { Timestamp = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc), Value = 120 }
        };

        _mockAnalyticsService
            .Setup(s => s.GetHistoricalDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(mockData);

        // Act
        var result = await _controller.GetTrends();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        // Verify the response structure matches the expected API format
        var responseValue = okResult.Value!.GetType().GetProperty("data")?.GetValue(okResult.Value);
        responseValue.Should().NotBeNull();
        responseValue.Should().BeSameAs(mockData);
    }

    [Fact]
    public async Task GetForecast_ReturnsDataInCorrectFormat()
    {
        // Arrange
        var mockForecast = new ForecastResult
        {
            HistoricalData = new List<HistoricalDataPoint>
            {
                new() { Timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), Value = 100 }
            },
            ForecastedData = new List<ForecastDataPoint>
            {
                new() {
                    Timestamp = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    ForecastValue = 110,
                    LowerBound = 90,
                    UpperBound = 130
                }
            }
        };

        _mockAnalyticsService
            .Setup(s => s.GenerateForecastAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(mockForecast);

        // Act
        var result = await _controller.GetForecast();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();

        // Verify the response structure matches the expected API format
        var responseValue = okResult.Value!.GetType().GetProperty("data")?.GetValue(okResult.Value);
        responseValue.Should().NotBeNull();
        responseValue.Should().BeSameAs(mockForecast);
    }
}