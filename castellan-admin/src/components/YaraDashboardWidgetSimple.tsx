import React, { useState, useEffect } from 'react';
import {
  Card,
  CardContent,
  CardHeader,
  Typography,
  Box,
  Button,
  Chip,
  LinearProgress,
  CircularProgress,
  IconButton,
  Tooltip,
  Divider,
  Grid,
} from '@mui/material';
import {
  Security as YaraIcon,
  CheckCircle as ValidIcon,
  Error as InvalidIcon,
  Refresh as RefreshIcon,
} from '@mui/icons-material';

// Simple YARA Dashboard Widget with minimal dependencies
export const YaraDashboardWidgetSimple = () => {
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [lastRefresh, setLastRefresh] = useState(new Date());
  
  const loadData = async () => {
    setLoading(true);
    try {
      const authToken = localStorage.getItem('auth_token');
      if (!authToken) {
        throw new Error('No authentication token found');
      }

      // Direct API calls without data provider
      const [rulesResponse, statusResponse] = await Promise.all([
        fetch('http://localhost:5000/api/yara-rules', {
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        }),
        fetch('http://localhost:5000/api/yara-rules/status', {
          headers: {
            'Authorization': `Bearer ${authToken}`,
            'Content-Type': 'application/json'
          }
        })
      ]);

      if (!rulesResponse.ok) {
        throw new Error(`Rules API failed: ${rulesResponse.status}`);
      }
      if (!statusResponse.ok) {
        throw new Error(`Status API failed: ${statusResponse.status}`);
      }

      const rulesData = await rulesResponse.json();
      const statusData = await statusResponse.json();
      
      const rules = rulesData.data || [];
      
      setData({
        totalRules: rules.length,
        enabledRules: rules.filter((r: any) => r.isEnabled).length,
        validRules: rules.filter((r: any) => r.isValid).length,
        isHealthy: statusData.isHealthy,
        compiledRules: statusData.compiledRules,
        error: statusData.error
      });
      
      setLastRefresh(new Date());
    } catch (error: any) {
      console.error('Failed to load YARA data:', error);
      setData({
        totalRules: 0,
        enabledRules: 0,
        validRules: 0,
        isHealthy: false,
        compiledRules: 0,
        error: error.message
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    const interval = setInterval(loadData, 30000);
    return () => clearInterval(interval);
  }, []);

  if (loading && !data) {
    return (
      <Card>
        <CardHeader 
          title="YARA Rule Engine (Simple)"
          avatar={<YaraIcon color="primary" />}
        />
        <CardContent>
          <Box display="flex" justifyContent="center" alignItems="center" minHeight={200}>
            <CircularProgress />
          </Box>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader 
        title="YARA Rule Engine (Simple)"
        avatar={<YaraIcon color="primary" />}
        action={
          <Box display="flex" gap={1}>
            <Tooltip title={`Last updated: ${lastRefresh.toLocaleTimeString()}`}>
              <IconButton onClick={loadData} disabled={loading} size="small">
                {loading ? <CircularProgress size={16} /> : <RefreshIcon />}
              </IconButton>
            </Tooltip>
          </Box>
        }
      />
      <CardContent>
        {data ? (
          <>
            <Grid container spacing={2} sx={{ mb: 3 }}>
              <Grid item xs={12} sm={6} md={3}>
                <Box textAlign="center" p={1} bgcolor="rgba(33, 150, 243, 0.1)" borderRadius={1}>
                  <Typography variant="h4" color="info.main">
                    {data.totalRules}
                  </Typography>
                  <Typography variant="caption">Total Rules</Typography>
                </Box>
              </Grid>
              <Grid item xs={12} sm={6} md={3}>
                <Box textAlign="center" p={1} bgcolor="rgba(76, 175, 80, 0.1)" borderRadius={1}>
                  <Typography variant="h4" color="success.main">
                    {data.enabledRules}
                  </Typography>
                  <Typography variant="caption">Enabled</Typography>
                </Box>
              </Grid>
              <Grid item xs={12} sm={6} md={3}>
                <Box textAlign="center" p={1} bgcolor="rgba(156, 39, 176, 0.1)" borderRadius={1}>
                  <Typography variant="h4" color="secondary.main">
                    {data.validRules}
                  </Typography>
                  <Typography variant="caption">Valid</Typography>
                </Box>
              </Grid>
              <Grid item xs={12} sm={6} md={3}>
                <Box textAlign="center" p={1} bgcolor="rgba(255, 152, 0, 0.1)" borderRadius={1}>
                  <Typography variant="h4" color="warning.main">
                    {data.compiledRules}
                  </Typography>
                  <Typography variant="caption">Compiled</Typography>
                </Box>
              </Grid>
            </Grid>

            <Box mb={3}>
              <Box display="flex" justifyContent="space-between" alignItems="center" mb={1}>
                <Typography variant="subtitle2">Engine Status</Typography>
                <Chip 
                  label={data.isHealthy ? 'Healthy' : 'Unhealthy'}
                  color={data.isHealthy ? 'success' : 'error'}
                  size="small"
                  icon={data.isHealthy ? <ValidIcon /> : <InvalidIcon />}
                />
              </Box>
              
              {data.error && (
                <Box mb={2}>
                  <Typography variant="body2" color="error">
                    Error: {data.error}
                  </Typography>
                </Box>
              )}
              
              <Box mb={2}>
                <Box display="flex" justifyContent="space-between" mb={1}>
                  <Typography variant="body2">Rule Validation</Typography>
                  <Typography variant="body2" fontWeight="bold">
                    {data.totalRules > 0 ? ((data.validRules / data.totalRules) * 100).toFixed(1) : 0}%
                  </Typography>
                </Box>
                <LinearProgress 
                  variant="determinate" 
                  value={data.totalRules > 0 ? (data.validRules / data.totalRules) * 100 : 0}
                  color={data.totalRules > 0 && (data.validRules / data.totalRules) > 0.9 ? 'success' : 'warning'}
                  sx={{ height: 8, borderRadius: 4 }}
                />
                <Typography variant="caption" color="textSecondary">
                  {data.validRules} valid / {data.totalRules} total rules
                </Typography>
              </Box>
            </Box>

            <Box display="flex" gap={1} flexWrap="wrap">
              <Button 
                size="small" 
                onClick={() => window.location.href = '#/yara-rules'}
                color="primary"
              >
                View Rules
              </Button>
            </Box>
          </>
        ) : (
          <Box textAlign="center" py={3}>
            <YaraIcon sx={{ fontSize: 48, color: 'text.secondary', mb: 2 }} />
            <Typography variant="h6" color="textSecondary" gutterBottom>
              YARA Engine Unavailable
            </Typography>
            <Button 
              variant="contained" 
              size="small" 
              onClick={loadData}
              startIcon={<RefreshIcon />}
              sx={{ mt: 1 }}
            >
              Retry Connection
            </Button>
          </Box>
        )}
      </CardContent>
    </Card>
  );
};
