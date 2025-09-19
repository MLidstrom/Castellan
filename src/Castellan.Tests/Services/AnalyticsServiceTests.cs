using FluentAssertions;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Services;
using Moq;
using Xunit;

namespace Castellan.Tests.Services;

[Trait("Category", "Unit")]
public class AnalyticsServiceTests
{
    private readonly Mock<ISecurityEventStore> _mockSecurityEventStore;
    private readonly AnalyticsService _service;

    public AnalyticsServiceTests()
    {
        _mockSecurityEventStore = new Mock<ISecurityEventStore>();
        _service = new AnalyticsService(_mockSecurityEventStore.Object);
    }

    [Fact]
    public async Task GetHistoricalDataAsync_WithValidParameters_ReturnsData()
    {
        // Arrange
        var metric = "TotalEvents";
        var timeRange = "7d";
        var groupBy = "day";

        // Act
        var result = await _service.GetHistoricalDataAsync(metric, timeRange, groupBy);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(7); // 7 days of data
        result.Should().BeInAscendingOrder(x => x.Timestamp);

        foreach (var dataPoint in result)
        {
            dataPoint.Timestamp.Should().BeBefore(DateTime.UtcNow);
            dataPoint.Value.Should().BeInRange(100, 500); // Based on the random range in implementation
        }
    }

    [Theory]
    [InlineData("7d", 7)]
    [InlineData("30d", 30)]
    [InlineData("90d", 90)]
    [InlineData("invalid", 7)] // Should default to 7 days
    public async Task GetHistoricalDataAsync_WithDifferentTimeRanges_ReturnsCorrectDataCount(string timeRange, int expectedDays)
    {
        // Arrange
        var metric = "TotalEvents";
        var groupBy = "day";

        // Act
        var result = await _service.GetHistoricalDataAsync(metric, timeRange, groupBy);

        // Assert
        result.Should().HaveCount(expectedDays);
        result.Should().BeInAscendingOrder(x => x.Timestamp);
    }

    [Fact]
    public async Task GetHistoricalDataAsync_WithEmptyMetric_StillReturnsData()
    {
        // Arrange
        var metric = "";
        var timeRange = "7d";
        var groupBy = "day";

        // Act
        var result = await _service.GetHistoricalDataAsync(metric, timeRange, groupBy);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task GenerateForecastAsync_WithValidParameters_ReturnsForecastResult()
    {
        // Arrange
        var metric = "TotalEvents";
        var forecastPeriod = 7;

        // Act
        var result = await _service.GenerateForecastAsync(metric, forecastPeriod);

        // Assert
        result.Should().NotBeNull();
        result.HistoricalData.Should().NotBeNull();
        result.HistoricalData.Should().HaveCount(90); // Uses 90 days of historical data
        result.HistoricalData.Should().BeInAscendingOrder(x => x.Timestamp);

        result.ForecastedData.Should().NotBeNull();
        result.ForecastedData.Should().HaveCount(forecastPeriod);

        foreach (var forecastPoint in result.ForecastedData)
        {
            forecastPoint.Timestamp.Should().BeAfter(result.HistoricalData.Last().Timestamp);
            forecastPoint.ForecastValue.Should().BeGreaterThan(0);
            forecastPoint.LowerBound.Should().BeLessOrEqualTo(forecastPoint.ForecastValue);
            forecastPoint.UpperBound.Should().BeGreaterOrEqualTo(forecastPoint.ForecastValue);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public async Task GenerateForecastAsync_WithDifferentForecastPeriods_ReturnsCorrectForecastCount(int forecastPeriod)
    {
        // Arrange
        var metric = "TotalEvents";

        // Act
        var result = await _service.GenerateForecastAsync(metric, forecastPeriod);

        // Assert
        result.Should().NotBeNull();
        result.ForecastedData.Should().HaveCount(forecastPeriod);
    }

    [Fact]
    public async Task GenerateForecastAsync_ForecastTimestamps_AreConsecutiveDaysAfterHistorical()
    {
        // Arrange
        var metric = "TotalEvents";
        var forecastPeriod = 3;

        // Act
        var result = await _service.GenerateForecastAsync(metric, forecastPeriod);

        // Assert
        var lastHistoricalDate = result.HistoricalData.Last().Timestamp.Date;
        var forecastDates = result.ForecastedData.Select(f => f.Timestamp.Date).ToList();

        for (int i = 0; i < forecastPeriod; i++)
        {
            var expectedDate = lastHistoricalDate.AddDays(i + 1);
            forecastDates[i].Should().Be(expectedDate);
        }
    }

    [Fact]
    public async Task GenerateForecastAsync_WithNegativeForecastPeriod_ReturnsEmptyForecast()
    {
        // Arrange
        var metric = "TotalEvents";
        var forecastPeriod = -1;

        // Act
        var result = await _service.GenerateForecastAsync(metric, forecastPeriod);

        // Assert
        result.Should().NotBeNull();
        result.HistoricalData.Should().NotBeEmpty();
        result.ForecastedData.Should().BeEmpty();
    }

    [Fact]
    public void HistoricalDataPoint_Properties_AreSetCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var value = 123.45;

        // Act
        var dataPoint = new HistoricalDataPoint
        {
            Timestamp = timestamp,
            Value = value
        };

        // Assert
        dataPoint.Timestamp.Should().Be(timestamp);
        dataPoint.Value.Should().Be(value);
    }

    [Fact]
    public void ForecastDataPoint_Properties_AreSetCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var forecastValue = 100.5f;
        var lowerBound = 80.0f;
        var upperBound = 120.0f;

        // Act
        var dataPoint = new ForecastDataPoint
        {
            Timestamp = timestamp,
            ForecastValue = forecastValue,
            LowerBound = lowerBound,
            UpperBound = upperBound
        };

        // Assert
        dataPoint.Timestamp.Should().Be(timestamp);
        dataPoint.ForecastValue.Should().Be(forecastValue);
        dataPoint.LowerBound.Should().Be(lowerBound);
        dataPoint.UpperBound.Should().Be(upperBound);
    }

    [Fact]
    public void ForecastResult_Properties_AreSetCorrectly()
    {
        // Arrange
        var historicalData = new List<HistoricalDataPoint>
        {
            new() { Timestamp = DateTime.UtcNow.AddDays(-1), Value = 100 }
        };
        var forecastedData = new List<ForecastDataPoint>
        {
            new() { Timestamp = DateTime.UtcNow, ForecastValue = 110, LowerBound = 90, UpperBound = 130 }
        };

        // Act
        var result = new ForecastResult
        {
            HistoricalData = historicalData,
            ForecastedData = forecastedData
        };

        // Assert
        result.HistoricalData.Should().BeSameAs(historicalData);
        result.ForecastedData.Should().BeSameAs(forecastedData);
    }

    [Fact]
    public void TimeSeriesData_Value_IsSetCorrectly()
    {
        // Arrange
        var value = 42.7f;

        // Act
        var data = new TimeSeriesData { Value = value };

        // Assert
        data.Value.Should().Be(value);
    }

    [Fact]
    public void TimeSeriesPrediction_Properties_AreSetCorrectly()
    {
        // Arrange
        var forecast = new float[] { 100, 110, 120 };
        var lowerBound = new float[] { 80, 90, 100 };
        var upperBound = new float[] { 120, 130, 140 };

        // Act
        var prediction = new TimeSeriesPrediction
        {
            Forecast = forecast,
            LowerBound = lowerBound,
            UpperBound = upperBound
        };

        // Assert
        prediction.Forecast.Should().BeEquivalentTo(forecast);
        prediction.LowerBound.Should().BeEquivalentTo(lowerBound);
        prediction.UpperBound.Should().BeEquivalentTo(upperBound);
    }

}