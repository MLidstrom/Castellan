
import React, { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, Typography, Box, CircularProgress, Alert, Chip, Grid } from '@mui/material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { getTrends, getForecast, HistoricalDataPoint } from '../dataProvider/analyticsService';

const TrendAnalysisPage: React.FC = () => {
  const [data, setData] = useState<HistoricalDataPoint[]>([]);
  const [forecastData, setForecastData] = useState<any>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        setError(null);

        // Fetch historical trends - using 30 days for better context
        const trendsResponse = await getTrends('TotalEvents', '30d', 'day');
        console.log('Fetched trend data:', trendsResponse.data);
        setData(trendsResponse.data);

        // Fetch ML.NET forecast predictions
        const forecastResponse = await getForecast('TotalEvents', 7);
        console.log('Fetched forecast data:', forecastResponse.data);
        setForecastData(forecastResponse.data);

      } catch (e: any) {
        setError(e.message || 'Failed to fetch data.');
        console.error('Error fetching data:', e);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, []);

  const renderContent = () => {
    if (loading) {
      return (
        <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
          <CircularProgress />
        </Box>
      );
    }

    if (error) {
      return (
        <Typography color="error" sx={{ textAlign: 'center' }}>
          Error: {error}
        </Typography>
      );
    }

    // Transform data for Recharts - format timestamp for display
    const chartData = data.map(point => ({
      ...point,
      formattedDate: new Date(point.timestamp).toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric'
      })
    }));

    return (
      <Grid container spacing={3}>
        {/* Historical Data Section */}
        <Grid item xs={12} md={6}>
          <Box sx={{ p: 2, border: '1px solid #e0e0e0', borderRadius: 2, bgcolor: '#f9f9f9' }}>
            <Typography variant="h6" sx={{ mb: 2, display: 'flex', alignItems: 'center' }}>
              📊 Historical Data (Last {data.length} days)
            </Typography>
            <Box sx={{ maxHeight: 300, overflow: 'auto' }}>
              {data.map((point, index) => (
                <Box key={index} sx={{ mb: 1, p: 1, bgcolor: 'white', borderRadius: 1, border: '1px solid #e0e0e0' }}>
                  <Typography variant="body2">
                    <strong>{new Date(point.timestamp).toLocaleDateString('en-US', {
                      month: 'short',
                      day: 'numeric'
                    })}</strong> - {point.value} events
                  </Typography>
                </Box>
              ))}
            </Box>
          </Box>
        </Grid>

        {/* ML.NET Predictions Section */}
        <Grid item xs={12} md={6}>
          <Box sx={{ p: 2, border: '1px solid #e8f5e8', borderRadius: 2, bgcolor: '#f0f8f0' }}>
            <Typography variant="h6" sx={{ mb: 2, display: 'flex', alignItems: 'center' }}>
              🔮 AI Predictions (ML.NET Forecasting)
              <Chip
                label="Powered by ML.NET"
                size="small"
                color="success"
                sx={{ ml: 1 }}
              />
            </Typography>

            {forecastData ? (
              <Box>
                {/* Historical baseline for comparison */}
                <Alert severity="info" sx={{ mb: 2 }}>
                  <Typography variant="body2">
                    <strong>Based on analysis of {forecastData.historicalData?.length || 90} historical data points</strong>
                  </Typography>
                </Alert>

                {/* Forecast predictions */}
                <Typography variant="subtitle2" sx={{ mb: 1, fontWeight: 'bold' }}>
                  Next 7 Days Predictions:
                </Typography>
                <Box sx={{ maxHeight: 200, overflow: 'auto' }}>
                  {forecastData.forecastedData?.map((forecast: any, index: number) => (
                    <Box key={index} sx={{ mb: 1, p: 1, bgcolor: 'white', borderRadius: 1, border: '1px solid #c8e6c9' }}>
                      <Typography variant="body2">
                        <strong>Day +{index + 1}</strong> - Predicted: <strong>{Math.round(forecast.forecastValue)}</strong> events
                        <br />
                        <span style={{ fontSize: '0.8em', color: '#666' }}>
                          Range: {Math.round(forecast.lowerBound)} - {Math.round(forecast.upperBound)}
                        </span>
                      </Typography>
                    </Box>
                  )) || (
                    <Typography variant="body2" color="text.secondary">
                      Generating predictions...
                    </Typography>
                  )}
                </Box>

                {/* Summary insights */}
                <Alert severity="success" sx={{ mt: 2 }}>
                  <Typography variant="body2">
                    <strong>AI Insights:</strong> The ML.NET model uses Singular Spectrum Analysis (SSA) to predict future security event volumes based on historical patterns.
                  </Typography>
                </Alert>
              </Box>
            ) : (
              <Box sx={{ textAlign: 'center', py: 4 }}>
                <CircularProgress size={24} />
                <Typography variant="body2" sx={{ mt: 1 }}>
                  Loading ML.NET predictions...
                </Typography>
              </Box>
            )}
          </Box>
        </Grid>
      </Grid>
    );
  };

  return (
    <Card>
      <CardHeader
        title="Trend Analysis & AI Predictions"
        subheader="Historical data analysis with ML.NET forecasting predictions"
      />
      <CardContent>
        <Typography variant="h6" sx={{ mb: 2 }}>
          Security Event Trends & AI-Powered Forecasting
        </Typography>
        {renderContent()}
      </CardContent>
    </Card>
  );
};

export default TrendAnalysisPage;

