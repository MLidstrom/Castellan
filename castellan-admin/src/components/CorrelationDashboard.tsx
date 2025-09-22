import React, { useState, useEffect } from 'react';
import {
  Box,
  Grid,
  Card,
  CardHeader,
  CardContent,
  Typography,
  Button,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  LinearProgress,
  Alert,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  IconButton,
  Tooltip,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  TextField,
  CircularProgress,
} from '@mui/material';
import {
  ExpandMore as ExpandMoreIcon,
  Security as SecurityIcon,
  Timeline as TimelineIcon,
  Assessment as AssessmentIcon,
  Refresh as RefreshIcon,
  Analytics as AnalyticsIcon,
  AccountTree as ChainIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  CheckCircle as SuccessIcon,
} from '@mui/icons-material';
import { useDataProvider, useNotify } from 'react-admin';

// Types for correlation data
interface EventCorrelation {
  id: string;
  detectedAt: string;
  correlationType: string;
  confidenceScore: number;
  pattern: string;
  eventIds: string[];
  timeWindow: string;
  attackChainStage?: string;
  mitreTechniques: string[];
  riskLevel: string;
  summary: string;
  recommendedActions: string[];
  metadata: Record<string, any>;
}

interface CorrelationStatistics {
  totalEventsProcessed: number;
  correlationsDetected: number;
  correlationsByType: Record<string, number>;
  averageConfidenceScore: number;
  averageProcessingTime: string;
  lastUpdated: string;
  topPatterns: string[];
}

interface AttackChain {
  id: string;
  name: string;
  stages: AttackStage[];
  confidenceScore: number;
  startTime: string;
  endTime: string;
  attackType: string;
  mitreTechniques: string[];
  riskLevel: string;
  affectedAssets: string[];
}

interface AttackStage {
  sequence: number;
  name: string;
  eventId: string;
  timestamp: string;
  description: string;
  mitreTechnique?: string;
}

const CorrelationDashboard: React.FC = () => {
  const [correlations, setCorrelations] = useState<EventCorrelation[]>([]);
  const [statistics, setStatistics] = useState<CorrelationStatistics | null>(null);
  const [attackChains, setAttackChains] = useState<AttackChain[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [timeRange, setTimeRange] = useState('1h');
  const [correlationType, setCorrelationType] = useState('');

  const dataProvider = useDataProvider();
  const notify = useNotify();

  useEffect(() => {
    loadData();
  }, [timeRange, correlationType]);

  const loadData = async () => {
    setLoading(true);
    setError(null);

    try {
      // Load correlations
      const correlationsResponse = await dataProvider.getList('correlation', {
        pagination: { page: 1, perPage: 100 },
        sort: { field: 'detectedAt', order: 'DESC' },
        filter: {
          ...(timeRange && { timeRange }),
          ...(correlationType && { correlationType })
        }
      });
      setCorrelations(correlationsResponse.data);

      // Load statistics
      const statsResponse = await dataProvider.getOne('correlation/statistics', { id: '' });
      setStatistics(statsResponse.data);

      // Load attack chains
      const chainsResponse = await dataProvider.create('correlation/attack-chains', {
        data: { timeWindow: timeRange }
      });
      setAttackChains(chainsResponse.data.attackChains || []);

    } catch (error: any) {
      console.error('Error loading correlation data:', error);
      setError(error.message || 'Failed to load correlation data');
      notify('Failed to load correlation data', { type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  const handleAnalyzeEvents = async () => {
    setLoading(true);
    try {
      const response = await dataProvider.create('correlation/analyze', {
        data: { timeWindow: timeRange }
      });

      notify(`Analysis complete: ${response.data.correlationsFound} correlations found`,
        { type: 'success' });

      await loadData();
    } catch (error: any) {
      notify('Analysis failed: ' + error.message, { type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  const getRiskLevelColor = (riskLevel: string) => {
    switch (riskLevel.toLowerCase()) {
      case 'critical': return 'error';
      case 'high': return 'warning';
      case 'medium': return 'info';
      case 'low': return 'success';
      default: return 'default';
    }
  };

  const getCorrelationTypeIcon = (type: string) => {
    switch (type.toLowerCase()) {
      case 'temporalburst': return <TimelineIcon />;
      case 'attackchain': return <ChainIcon />;
      case 'lateralmovement': return <ChainIcon />;
      case 'privilegeescalation': return <WarningIcon />;
      default: return <SecurityIcon />;
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
        <AnalyticsIcon sx={{ mr: 1, color: 'primary.main' }} />
        <Typography variant="h4" component="h1">
          Correlation Engine
        </Typography>
        <Box sx={{ ml: 'auto', display: 'flex', gap: 2 }}>
          <FormControl size="small" sx={{ minWidth: 120 }}>
            <InputLabel>Time Range</InputLabel>
            <Select
              value={timeRange}
              label="Time Range"
              onChange={(e) => setTimeRange(e.target.value)}
            >
              <MenuItem value="1h">Last Hour</MenuItem>
              <MenuItem value="6h">Last 6 Hours</MenuItem>
              <MenuItem value="24h">Last 24 Hours</MenuItem>
              <MenuItem value="7d">Last 7 Days</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 140 }}>
            <InputLabel>Type</InputLabel>
            <Select
              value={correlationType}
              label="Type"
              onChange={(e) => setCorrelationType(e.target.value)}
            >
              <MenuItem value="">All Types</MenuItem>
              <MenuItem value="TemporalBurst">Temporal Burst</MenuItem>
              <MenuItem value="AttackChain">Attack Chain</MenuItem>
              <MenuItem value="LateralMovement">Lateral Movement</MenuItem>
              <MenuItem value="PrivilegeEscalation">Privilege Escalation</MenuItem>
            </Select>
          </FormControl>

          <Button
            variant="contained"
            startIcon={<AnalyticsIcon />}
            onClick={handleAnalyzeEvents}
            disabled={loading}
          >
            Analyze Events
          </Button>

          <IconButton onClick={loadData} disabled={loading}>
            <RefreshIcon />
          </IconButton>
        </Box>
      </Box>

      {loading && <LinearProgress sx={{ mb: 2 }} />}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* Statistics Cards */}
        {statistics && (
          <>
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="textSecondary" gutterBottom>
                    Total Correlations
                  </Typography>
                  <Typography variant="h4">
                    {statistics.correlationsDetected}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="textSecondary" gutterBottom>
                    Events Processed
                  </Typography>
                  <Typography variant="h4">
                    {statistics.totalEventsProcessed.toLocaleString()}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="textSecondary" gutterBottom>
                    Avg. Confidence
                  </Typography>
                  <Typography variant="h4">
                    {(statistics.averageConfidenceScore * 100).toFixed(1)}%
                  </Typography>
                </CardContent>
              </Card>
            </Grid>

            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Typography color="textSecondary" gutterBottom>
                    Attack Chains
                  </Typography>
                  <Typography variant="h4">
                    {attackChains.length}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          </>
        )}

        {/* Attack Chains */}
        {attackChains.length > 0 && (
          <Grid item xs={12}>
            <Card>
              <CardHeader
                title="Attack Chains Detected"
                avatar={<ChainIcon />}
              />
              <CardContent>
                {attackChains.map((chain) => (
                  <Accordion key={chain.id}>
                    <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                      <Box sx={{ display: 'flex', alignItems: 'center', width: '100%' }}>
                        <Typography variant="h6" sx={{ flexGrow: 1 }}>
                          {chain.name}
                        </Typography>
                        <Chip
                          label={chain.riskLevel}
                          color={getRiskLevelColor(chain.riskLevel) as any}
                          size="small"
                          sx={{ mr: 2 }}
                        />
                        <Typography variant="body2" color="textSecondary">
                          {(chain.confidenceScore * 100).toFixed(1)}% confidence
                        </Typography>
                      </Box>
                    </AccordionSummary>
                    <AccordionDetails>
                      <Grid container spacing={2}>
                        <Grid item xs={12} md={6}>
                          <Typography variant="subtitle2" gutterBottom>
                            Attack Stages
                          </Typography>
                          {chain.stages.map((stage) => (
                            <Box key={stage.sequence} sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                              <Chip
                                label={stage.sequence}
                                size="small"
                                sx={{ mr: 1, minWidth: 30 }}
                              />
                              <Typography variant="body2">
                                {stage.name}: {stage.description}
                              </Typography>
                            </Box>
                          ))}
                        </Grid>
                        <Grid item xs={12} md={6}>
                          <Typography variant="subtitle2" gutterBottom>
                            Details
                          </Typography>
                          <Typography variant="body2" gutterBottom>
                            <strong>Type:</strong> {chain.attackType}
                          </Typography>
                          <Typography variant="body2" gutterBottom>
                            <strong>Duration:</strong> {new Date(chain.endTime).getTime() - new Date(chain.startTime).getTime()}ms
                          </Typography>
                          <Typography variant="body2" gutterBottom>
                            <strong>Affected Assets:</strong> {chain.affectedAssets.join(', ')}
                          </Typography>
                          {chain.mitreTechniques.length > 0 && (
                            <Box sx={{ mt: 1 }}>
                              <Typography variant="body2" gutterBottom>
                                <strong>MITRE Techniques:</strong>
                              </Typography>
                              {chain.mitreTechniques.map((technique) => (
                                <Chip key={technique} label={technique} size="small" sx={{ mr: 0.5, mb: 0.5 }} />
                              ))}
                            </Box>
                          )}
                        </Grid>
                      </Grid>
                    </AccordionDetails>
                  </Accordion>
                ))}
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* Correlations List */}
        <Grid item xs={12}>
          <Card>
            <CardHeader
              title="Event Correlations"
              avatar={<SecurityIcon />}
            />
            <CardContent>
              <TableContainer>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>Type</TableCell>
                      <TableCell>Pattern</TableCell>
                      <TableCell>Risk Level</TableCell>
                      <TableCell>Confidence</TableCell>
                      <TableCell>Events</TableCell>
                      <TableCell>Detected At</TableCell>
                      <TableCell>Actions</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {correlations.map((correlation) => (
                      <TableRow key={correlation.id}>
                        <TableCell>
                          <Box sx={{ display: 'flex', alignItems: 'center' }}>
                            {getCorrelationTypeIcon(correlation.correlationType)}
                            <Typography variant="body2" sx={{ ml: 1 }}>
                              {correlation.correlationType}
                            </Typography>
                          </Box>
                        </TableCell>
                        <TableCell>{correlation.pattern}</TableCell>
                        <TableCell>
                          <Chip
                            label={correlation.riskLevel}
                            color={getRiskLevelColor(correlation.riskLevel) as any}
                            size="small"
                          />
                        </TableCell>
                        <TableCell>
                          {(correlation.confidenceScore * 100).toFixed(1)}%
                        </TableCell>
                        <TableCell>{correlation.eventIds.length}</TableCell>
                        <TableCell>
                          {new Date(correlation.detectedAt).toLocaleString()}
                        </TableCell>
                        <TableCell>
                          <Tooltip title="View Details">
                            <IconButton size="small">
                              <AssessmentIcon />
                            </IconButton>
                          </Tooltip>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>

              {correlations.length === 0 && !loading && (
                <Box sx={{ textAlign: 'center', py: 3 }}>
                  <Typography color="textSecondary">
                    No correlations detected in the selected time range
                  </Typography>
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
};

export default CorrelationDashboard;