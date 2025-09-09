import React, { useState, useEffect, useMemo } from 'react';
import {
  Card,
  CardContent,
  CardHeader,
  Typography,
  Box,
  Chip,
  Tooltip,
  LinearProgress,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  SelectChangeEvent,
  Grid
} from '@mui/material';
import { useGetList } from 'react-admin';
import {
  Public as WorldIcon,
  TrendingUp as TrendingUpIcon,
  Security as SecurityIcon,
  Warning as WarningIcon
} from '@mui/icons-material';

// Interface for geographic threat data
interface GeographicThreat {
  country: string;
  countryCode: string;
  city?: string;
  eventCount: number;
  highRiskCount: number;
  criticalCount: number;
  riskLevel: 'low' | 'medium' | 'high' | 'critical';
  events: any[];
}

interface CountryThreatSummary {
  countryCode: string;
  country: string;
  totalThreats: number;
  riskLevel: 'low' | 'medium' | 'high' | 'critical';
  cities: string[];
  eventTypes: string[];
}

export const GeographicThreatMap: React.FC = () => {
  const [timeRange, setTimeRange] = useState('24h');
  const [selectedCountry, setSelectedCountry] = useState<string>('');

  // Fetch security events
  const { data: securityEvents } = useGetList('security-events', {
    pagination: { page: 1, perPage: 10000 },
    sort: { field: 'timestamp', order: 'DESC' },
  });

  // Process geographic threat data
  const geographicThreats = useMemo(() => {
    if (!securityEvents || securityEvents.length === 0) return [];

    const threatMap = new Map<string, GeographicThreat>();

    securityEvents.forEach((event) => {
      // Extract IP enrichment data from the event
      const enrichmentData = event.ipEnrichment || event.enrichmentData;
      
      if (!enrichmentData) return;

      let country = enrichmentData.country || 'Unknown';
      let countryCode = enrichmentData.countryCode || enrichmentData.country_code || 'XX';
      let city = enrichmentData.city;

      // Handle cases where enrichment might be a string (JSON)
      if (typeof enrichmentData === 'string') {
        try {
          const parsed = JSON.parse(enrichmentData);
          country = parsed.country || 'Unknown';
          countryCode = parsed.countryCode || parsed.country_code || 'XX';
          city = parsed.city;
        } catch {
          // If parsing fails, skip this event
          return;
        }
      }

      if (country === 'Unknown' || country === 'Private Network') return;

      const key = `${countryCode}-${country}`;
      
      if (!threatMap.has(key)) {
        threatMap.set(key, {
          country,
          countryCode,
          city,
          eventCount: 0,
          highRiskCount: 0,
          criticalCount: 0,
          riskLevel: 'low',
          events: []
        });
      }

      const threat = threatMap.get(key)!;
      threat.eventCount++;
      threat.events.push(event);

      if (event.riskLevel === 'high') threat.highRiskCount++;
      if (event.riskLevel === 'critical') threat.criticalCount++;

      // Determine overall risk level for this country
      if (threat.criticalCount > 0) {
        threat.riskLevel = 'critical';
      } else if (threat.highRiskCount > 0) {
        threat.riskLevel = 'high';
      } else if (threat.eventCount > 10) {
        threat.riskLevel = 'medium';
      } else {
        threat.riskLevel = 'low';
      }

      // Update city if we have one
      if (city && !threat.city) {
        threat.city = city;
      }
    });

    return Array.from(threatMap.values()).sort((a, b) => b.eventCount - a.eventCount);
  }, [securityEvents]);

  // Get top threat countries for summary
  const topThreatCountries = geographicThreats.slice(0, 10);

  // Get risk level color
  const getRiskColor = (riskLevel: string) => {
    switch (riskLevel) {
      case 'critical': return '#f44336';
      case 'high': return '#ff9800';
      case 'medium': return '#2196f3';
      case 'low': return '#4caf50';
      default: return '#9e9e9e';
    }
  };

  // Get risk level chip color
  const getRiskChipColor = (riskLevel: string): 'error' | 'warning' | 'info' | 'success' | 'default' => {
    switch (riskLevel) {
      case 'critical': return 'error';
      case 'high': return 'warning';
      case 'medium': return 'info';
      case 'low': return 'success';
      default: return 'default';
    }
  };

  const handleCountrySelect = (event: SelectChangeEvent) => {
    setSelectedCountry(event.target.value);
  };

  const selectedThreat = selectedCountry 
    ? geographicThreats.find(t => t.countryCode === selectedCountry)
    : null;

  const totalThreats = geographicThreats.reduce((sum, threat) => sum + threat.eventCount, 0);
  const uniqueCountries = geographicThreats.length;
  const criticalCountries = geographicThreats.filter(t => t.riskLevel === 'critical').length;

  return (
    <Card>
      <CardHeader
        title={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <WorldIcon color="primary" />
            <Typography variant="h6">Geographic Threat Analysis</Typography>
          </Box>
        }
        action={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <FormControl size="small" sx={{ minWidth: 120 }}>
              <InputLabel>Country</InputLabel>
              <Select
                value={selectedCountry}
                label="Country"
                onChange={handleCountrySelect}
              >
                <MenuItem value="">All Countries</MenuItem>
                {geographicThreats.map((threat) => (
                  <MenuItem key={threat.countryCode} value={threat.countryCode}>
                    {threat.country} ({threat.eventCount})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Box>
        }
      />
      <CardContent>
        {/* Summary Statistics */}
        <Grid container spacing={3} sx={{ mb: 3 }}>
          <Grid item xs={12} sm={3}>
            <Box sx={{ textAlign: 'center', p: 2, bgcolor: 'primary.main', color: 'white', borderRadius: 1 }}>
              <Typography variant="h4">{totalThreats}</Typography>
              <Typography variant="body2">Total Threats</Typography>
            </Box>
          </Grid>
          <Grid item xs={12} sm={3}>
            <Box sx={{ textAlign: 'center', p: 2, bgcolor: 'info.main', color: 'white', borderRadius: 1 }}>
              <Typography variant="h4">{uniqueCountries}</Typography>
              <Typography variant="body2">Countries</Typography>
            </Box>
          </Grid>
          <Grid item xs={12} sm={3}>
            <Box sx={{ textAlign: 'center', p: 2, bgcolor: 'error.main', color: 'white', borderRadius: 1 }}>
              <Typography variant="h4">{criticalCountries}</Typography>
              <Typography variant="body2">Critical Regions</Typography>
            </Box>
          </Grid>
          <Grid item xs={12} sm={3}>
            <Box sx={{ textAlign: 'center', p: 2, bgcolor: 'success.main', color: 'white', borderRadius: 1 }}>
              <Typography variant="h4">
                {geographicThreats.filter(t => t.riskLevel === 'low').length}
              </Typography>
              <Typography variant="body2">Low Risk</Typography>
            </Box>
          </Grid>
        </Grid>

        {/* Selected Country Details */}
        {selectedThreat && (
          <Box sx={{ mb: 3, p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
            <Typography variant="h6" gutterBottom>
              {selectedThreat.country} Threat Details
            </Typography>
            <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
              <Chip
                label={`${selectedThreat.eventCount} Events`}
                color="primary"
                icon={<SecurityIcon />}
              />
              <Chip
                label={`${selectedThreat.criticalCount} Critical`}
                color="error"
                icon={<WarningIcon />}
              />
              <Chip
                label={`${selectedThreat.highRiskCount} High Risk`}
                color="warning"
                icon={<TrendingUpIcon />}
              />
              <Chip
                label={(selectedThreat.riskLevel?.toUpperCase?.() || 'UNKNOWN')}
                color={getRiskChipColor(selectedThreat.riskLevel)}
              />
            </Box>
            {selectedThreat.city && (
              <Typography variant="body2" color="textSecondary" sx={{ mt: 1 }}>
                Primary City: {selectedThreat.city}
              </Typography>
            )}
          </Box>
        )}

        {/* Threat Map - Text-based representation */}
        <Box>
          <Typography variant="h6" gutterBottom>
            Top Threat Sources by Country
          </Typography>
          
          {geographicThreats.length === 0 ? (
            <Box sx={{ textAlign: 'center', py: 4 }}>
              <WorldIcon sx={{ fontSize: 48, color: 'text.secondary', mb: 2 }} />
              <Typography variant="body1" color="textSecondary">
                No geographic threat data available
              </Typography>
              <Typography variant="body2" color="textSecondary">
                Threats will appear here once IP enrichment data is available
              </Typography>
            </Box>
          ) : (
            <Box sx={{ maxHeight: 400, overflowY: 'auto' }}>
              {topThreatCountries.map((threat, index) => (
                <Box
                  key={`${threat.countryCode}-${index}`}
                  sx={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    p: 2,
                    mb: 1,
                    border: 1,
                    borderColor: 'grey.200',
                    borderRadius: 1,
                    '&:hover': {
                      bgcolor: 'grey.50'
                    }
                  }}
                >
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                    <Box
                      sx={{
                        width: 24,
                        height: 24,
                        bgcolor: getRiskColor(threat.riskLevel),
                        borderRadius: '50%',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: 'white',
                        fontSize: 12,
                        fontWeight: 'bold'
                      }}
                    >
                      {index + 1}
                    </Box>
                    <Box>
                      <Typography variant="body1" fontWeight="medium">
                        {threat.country}
                      </Typography>
                      {threat.city && (
                        <Typography variant="body2" color="textSecondary">
                          {threat.city}
                        </Typography>
                      )}
                    </Box>
                  </Box>
                  
                  <Box sx={{ textAlign: 'right', minWidth: 150 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                      <Typography variant="body2" fontWeight="bold">
                        {threat.eventCount} threats
                      </Typography>
                      <Chip
                        size="small"
                        label={(threat.riskLevel?.toUpperCase?.() || 'UNKNOWN')}
                        color={getRiskChipColor(threat.riskLevel)}
                      />
                    </Box>
                    <Box sx={{ width: 100 }}>
                      <LinearProgress
                        variant="determinate"
                        value={(threat.eventCount / topThreatCountries[0].eventCount) * 100}
                        color={
                          threat.riskLevel === 'critical' ? 'error' :
                          threat.riskLevel === 'high' ? 'warning' :
                          threat.riskLevel === 'medium' ? 'info' : 'success'
                        }
                        sx={{ height: 6, borderRadius: 3 }}
                      />
                    </Box>
                    <Typography variant="caption" color="textSecondary">
                      {threat.criticalCount} critical, {threat.highRiskCount} high
                    </Typography>
                  </Box>
                </Box>
              ))}
            </Box>
          )}

          {geographicThreats.length > 10 && (
            <Box sx={{ mt: 2, textAlign: 'center' }}>
              <Typography variant="body2" color="textSecondary">
                Showing top 10 of {geographicThreats.length} countries with threats
              </Typography>
            </Box>
          )}
        </Box>
      </CardContent>
    </Card>
  );
};
