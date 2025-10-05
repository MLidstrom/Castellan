import React, { useState, useEffect } from 'react';
import {
  Card,
  CardContent,
  Typography,
  Box,
  CircularProgress,
  LinearProgress,
} from '@mui/material';
import {
  Security as YaraIcon,
  CheckCircle as ValidIcon,
  Error as InvalidIcon,
} from '@mui/icons-material';
import { useDataProvider } from 'react-admin';

// API Configuration - Use same base URL as data provider
const API_URL = process.env.REACT_APP_CASTELLANPRO_API_URL || 'http://localhost:5000/api';

// Simplified YARA statistics for summary card
interface YaraSummary {
  totalRules: number;
  enabledRules: number;
  validRules: number;
  recentMatches: number;
  engineStatus: 'healthy' | 'warning' | 'error';
}

export const YaraSummaryCard = React.memo(() => {
  const [summary, setSummary] = useState<YaraSummary | null>(null);
  const [loading, setLoading] = useState(true);

  const dataProvider = useDataProvider();

  const loadYaraSummary = async () => {
    try {
      // Load YARA rules data to calculate summary
      const rulesResponse = await dataProvider.getList('yara-rules', {
        pagination: { page: 1, perPage: 1000 },
        sort: { field: 'createdAt', order: 'DESC' },
        filter: {}
      });
      
      const rules = rulesResponse.data;
      
      // Calculate summary statistics
      const totalRules = rules.length;
      const enabledRules = rules.filter(r => r.isEnabled).length;
      const validRules = rules.filter(r => r.isValid).length;
      const totalMatches = rules.reduce((sum, r) => sum + (r.hitCount || 0), 0);
      
      // Load YARA service status
      const authToken = localStorage.getItem('auth_token');
      const statusResponse = await fetch(`${API_URL}/yara-rules/status`, {
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json'
        }
      });
      
      let engineStatus: 'healthy' | 'warning' | 'error' = 'error';
      if (statusResponse.ok) {
        const status = await statusResponse.json();
        engineStatus = status.isHealthy ? 'healthy' : (status.error ? 'error' : 'warning');
      }
      
      setSummary({
        totalRules,
        enabledRules,
        validRules,
        recentMatches: totalMatches,
        engineStatus
      });
    } catch (error) {
      console.error('Failed to load YARA summary:', error);
      // Set minimal fallback data
      setSummary({
        totalRules: 0,
        enabledRules: 0,
        validRules: 0,
        recentMatches: 0,
        engineStatus: 'error'
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadYaraSummary();
    // Refresh every 60 seconds for summary
    const interval = setInterval(loadYaraSummary, 60000);
    return () => clearInterval(interval);
  }, [loadYaraSummary]);

  if (loading) {
    return (
      <Card sx={{ height: '150px' }}>
        <CardContent sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', marginBottom: 1 }}>
            <YaraIcon sx={{ marginRight: 1, color: 'primary.main' }} />
            <Typography variant="h6" color="textSecondary">
              YARA Engine
            </Typography>
          </Box>
          <Box sx={{ flexGrow: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <CircularProgress size={24} />
          </Box>
        </CardContent>
      </Card>
    );
  }

  if (!summary) {
    return (
      <Card sx={{ height: '150px' }}>
        <CardContent sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', marginBottom: 1 }}>
            <InvalidIcon sx={{ marginRight: 1, color: 'error.main' }} />
            <Typography variant="h6" color="textSecondary">
              YARA Engine
            </Typography>
          </Box>
          <Typography variant="h3" color="error.main" sx={{ flexGrow: 1, display: 'flex', alignItems: 'center' }}>
            N/A
          </Typography>
          <Typography variant="body2" color="textSecondary">
            Engine unavailable
          </Typography>
        </CardContent>
      </Card>
    );
  }

  const validationPercentage = summary.totalRules > 0 ? (summary.validRules / summary.totalRules) * 100 : 0;
  const enabledPercentage = summary.totalRules > 0 ? (summary.enabledRules / summary.totalRules) * 100 : 0;

  return (
    <Card sx={{ height: '150px' }}>
      <CardContent sx={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', marginBottom: 1 }}>
          {summary.engineStatus === 'healthy' ? 
            <ValidIcon sx={{ marginRight: 1, color: 'success.main' }} /> :
            <InvalidIcon sx={{ marginRight: 1, color: summary.engineStatus === 'warning' ? 'warning.main' : 'error.main' }} />
          }
          <Typography variant="h6" color="textSecondary">
            YARA Rules
          </Typography>
        </Box>
        <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1, flexGrow: 1 }}>
          <Typography 
            variant="h3" 
            sx={{ color: summary.engineStatus === 'healthy' ? 'primary.main' : 'error.main' }}
          >
            {summary.totalRules}
          </Typography>
          <Typography variant="h6" color="textSecondary">
            / {summary.enabledRules} active
          </Typography>
        </Box>
        <Box sx={{ mb: 1 }}>
          <LinearProgress 
            variant="determinate" 
            value={validationPercentage} 
            color={validationPercentage > 90 ? 'success' : validationPercentage > 70 ? 'warning' : 'error'}
            sx={{ width: '100%', height: 4, borderRadius: 2, mb: 0.5 }}
          />
          <Typography variant="body2" color="textSecondary">
            {validationPercentage.toFixed(0)}% validated • {summary.recentMatches} recent matches
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
});
