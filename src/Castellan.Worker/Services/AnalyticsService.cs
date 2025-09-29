
using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Castellan.Worker.Services
{
    public class AnalyticsService
    {
        private readonly ISecurityEventStore _securityEventStore;
        private readonly MLContext _mlContext;

        public AnalyticsService(ISecurityEventStore securityEventStore)
        {
            _securityEventStore = securityEventStore;
            _mlContext = new MLContext();
        }

        public async Task<IEnumerable<HistoricalDataPoint>> GetHistoricalDataAsync(string metric, string timeRange, string groupBy)
        {
            // This is a simplified implementation. In a real scenario, you would fetch
            // and aggregate data from your security event store based on the parameters.
            
            // For demonstration, we'll generate some sample data.
            var data = new List<HistoricalDataPoint>();
            var now = System.DateTime.UtcNow;
            var random = new System.Random();
            int days = timeRange switch
            {
                "7d" => 7,
                "30d" => 30,
                "90d" => 90,
                _ => 7
            };

            for (int i = 0; i < days; i++)
            {
                data.Add(new HistoricalDataPoint
                {
                    Timestamp = now.AddDays(-i),
                    Value = random.Next(100, 500)
                });
            }

            return await Task.FromResult(data.OrderBy(d => d.Timestamp));
        }

        public async Task<ForecastResult> GenerateForecastAsync(string metric, int forecastPeriod)
        {
            var historicalData = await GetHistoricalDataAsync(metric, "90d", "day"); // Use 90 days of data for a better forecast

            var result = new ForecastResult
            {
                HistoricalData = historicalData,
                ForecastedData = new List<ForecastDataPoint>()
            };

            // If forecast period is 0 or negative, return empty forecast
            if (forecastPeriod <= 0)
            {
                return result;
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(historicalData.Select(h => new TimeSeriesData { Value = (float)h.Value }));

            var pipeline = _mlContext.Forecasting.ForecastBySsa(
                outputColumnName: "Forecast",
                inputColumnName: "Value",
                windowSize: 7, // Weekly seasonality
                seriesLength: historicalData.Count(),
                trainSize: historicalData.Count(),
                horizon: forecastPeriod,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: "LowerBound",
                confidenceUpperBoundColumn: "UpperBound");

            var model = pipeline.Fit(dataView);

            var forecastingEngine = model.CreateTimeSeriesEngine<TimeSeriesData, TimeSeriesPrediction>(_mlContext);
            var forecast = forecastingEngine.Predict();

            result.ForecastedData = forecast.Forecast.Select((f, i) => new ForecastDataPoint
            {
                Timestamp = historicalData.Last().Timestamp.AddDays(i + 1),
                ForecastValue = f,
                LowerBound = forecast.LowerBound[i],
                UpperBound = forecast.UpperBound[i]
            }).ToList();

            return result;
        }
    }

    public class HistoricalDataPoint
    {
        public System.DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class ForecastDataPoint
    {
        public System.DateTime Timestamp { get; set; }
        public float ForecastValue { get; set; }
        public float LowerBound { get; set; }
        public float UpperBound { get; set; }
    }

    public class ForecastResult
    {
        public IEnumerable<HistoricalDataPoint> HistoricalData { get; set; } = new List<HistoricalDataPoint>();
        public IEnumerable<ForecastDataPoint> ForecastedData { get; set; } = new List<ForecastDataPoint>();
    }

    public class TimeSeriesData
    {
        public float Value { get; set; }
    }

    public class TimeSeriesPrediction
    {
        public float[] Forecast { get; set; } = Array.Empty<float>();
        public float[] LowerBound { get; set; } = Array.Empty<float>();
        public float[] UpperBound { get; set; } = Array.Empty<float>();
    }
}

