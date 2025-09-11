import React, { useEffect, useState } from 'react';
import {
  useDataProvider,
  useNotify,
  Loading,
} from 'react-admin';
import {
  Card,
  CardContent,
  CardHeader,
  Typography,
  Box,
  Grid,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  LinearProgress,
  IconButton,
  Tooltip,
  Alert,
  AlertTitle,
  List,
  ListItem,
  ListItemText,
  ListItemIcon,
  Button,
  CircularProgress,
} from '@mui/material';
import {
  Monitor as HealthIcon,
  CheckCircle as HealthyIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  Refresh as RefreshIcon,
  Speed as PerformanceIcon,
  Build as CompilationIcon,
  Timeline as UptimeIcon,
  BugReport as IssueIcon,
} from '@mui/icons-material';

interface YaraRuleHealth {
  ruleId: string;
  ruleName: string;
  isCompiled: boolean;
  isValid: boolean;
  validationError?: string;
  compilationTimeMs: number;
  lastValidated: string;
  healthStatus: 'Healthy' | 'Warning' | 'Critical';
  warnings: string[];
}

interface YaraSystemHealth {
  isHealthy: boolean;
  compiledRulesCount: number;
  healthyRulesCount: number;
  unhealthyRulesCount: number;
  averageCompilationTime: number;
  lastCompilation: string;
  systemError?: string;
  unhealthyRules: YaraRuleHealth[];
}

export const YaraHealthMonitor: React.FC = () => {
  const [healthData, setHealthData] = useState<YaraSystemHealth | null>(null);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [lastRefresh, setLastRefresh] = useState<Date>(new Date());

  const dataProvider = useDataProvider();
  const notify = useNotify();

  const loadHealthData = async () => {
    setLoading(true);
    try {
      const result = await dataProvider.getOne('yara-rules/health', { id: 'health' });
      setHealthData(result.data.data);
      setLastRefresh(new Date());
    } catch (error: any) {
      notify(`Failed to load health data: ${error.message}`, { type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  const handleRefreshRules = async () => {
    setRefreshing(true);
    try {
      await dataProvider.create('yara-rules/refresh', { data: {} });
      notify('YARA rules refreshed successfully', { type: 'success' });
      await loadHealthData(); // Reload health data after refresh
    } catch (error: any) {
      notify(`Failed to refresh rules: ${error.message}`, { type: 'error' });
    } finally {
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadHealthData();
    
    // Auto-refresh every 30 seconds
    const interval = setInterval(loadHealthData, 30000);
    return () => clearInterval(interval);
  }, []);

  if (loading && !healthData) {
    return <Loading />;
  }

  const getHealthIcon = (status: string) => {
    switch (status) {
      case 'Healthy': return <HealthyIcon sx={{ color: 'success.main' }} />;
      case 'Warning': return <WarningIcon sx={{ color: 'warning.main' }} />;
      case 'Critical': return <ErrorIcon sx={{ color: 'error.main' }} />;
      default: return <IssueIcon sx={{ color: 'info.main' }} />;
    }
  };

  const getHealthColor = (status: string) => {
    switch (status) {
      case 'Healthy': return 'success';
      case 'Warning': return 'warning';
      case 'Critical': return 'error';
      default: return 'info';
    }
  };

  const getSystemHealthColor = () => {
    if (!healthData) return 'info';
    if (healthData.isHealthy && healthData.unhealthyRulesCount === 0) return 'success';
    if (healthData.unhealthyRulesCount > 0 && healthData.unhealthyRulesCount <= 3) return 'warning';
    return 'error';
  };

  const healthPercentage = healthData ? 
    Math.round((healthData.healthyRulesCount / (healthData.healthyRulesCount + healthData.unhealthyRulesCount)) * 100) : 0;

  return (
    <Box sx={{ p: 3 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4" sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <HealthIcon /> YARA System Health
        </Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
          <Typography variant="caption" color="textSecondary">
            Last updated: {lastRefresh.toLocaleString()}
          </Typography>
          <Button
            onClick={handleRefreshRules}
            disabled={refreshing}
            startIcon={refreshing ? <CircularProgress size={16} /> : <RefreshIcon />}
            variant="contained"
            color="primary"
          >
            Refresh Rules
          </Button>
          <IconButton onClick={loadHealthData} disabled={loading}>
            <RefreshIcon />
          </IconButton>
        </Box>
      </Box>

      {/* System Overview */}
      {healthData && (
        <>
          <Grid container spacing={3} sx={{ mb: 3 }}>
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                    {getHealthIcon(healthData.isHealthy ? 'Healthy' : 'Critical')}
                    <Typography variant="h6" color="textSecondary" sx={{ ml: 1 }}>
                      System Status
                    </Typography>
                  </Box>
                  <Typography variant="h3" color={`${getSystemHealthColor()}.main`}>
                    {healthPercentage}%
                  </Typography>
                  <Typography variant="body2" color="textSecondary">
                    {healthData.isHealthy ? 'System Healthy' : 'Issues Detected'}
                  </Typography>
                  <LinearProgress
                    variant="determinate"
                    value={healthPercentage}
                    color={getSystemHealthColor() as any}
                    sx={{ mt: 1 }}
                  />
                </CardContent>
              </Card>
            </Grid>
            
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                    <CompilationIcon sx={{ mr: 1, color: 'primary.main' }} />
                    <Typography variant="h6" color="textSecondary">Compiled Rules</Typography>
                  </Box>
                  <Typography variant="h3">{healthData.compiledRulesCount}</Typography>
                  <Typography variant="body2" color="textSecondary">
                    Last compiled: {new Date(healthData.lastCompilation).toLocaleString()}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
            
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                    <PerformanceIcon sx={{ mr: 1, color: 'info.main' }} />
                    <Typography variant="h6" color="textSecondary">Avg Compilation</Typography>
                  </Box>
                  <Typography variant="h3" color="info.main">
                    {healthData.averageCompilationTime.toFixed(1)}ms
                  </Typography>
                  <Typography variant="body2" color="textSecondary">
                    Average compilation time
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
            
            <Grid item xs={12} md={3}>
              <Card>
                <CardContent>
                  <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                    <IssueIcon sx={{ mr: 1, color: 'warning.main' }} />
                    <Typography variant="h6" color="textSecondary">Issues</Typography>
                  </Box>
                  <Typography variant="h3" color="warning.main">
                    {healthData.unhealthyRulesCount}
                  </Typography>
                  <Typography variant="body2" color="textSecondary">
                    Rules with issues
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* System-wide Issues */}
          {healthData.systemError && (
            <Alert severity="error" sx={{ mb: 3 }}>
              <AlertTitle>System Error</AlertTitle>
              {healthData.systemError}
            </Alert>
          )}

          {/* Health Status Distribution */}
          <Grid container spacing={3} sx={{ mb: 3 }}>
            <Grid item xs={12} md={6}>
              <Card>
                <CardHeader title="Health Distribution" />
                <CardContent>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
                    <Box sx={{ textAlign: 'center' }}>
                      <Typography variant="h4" color="success.main">
                        {healthData.healthyRulesCount}
                      </Typography>
                      <Typography variant="body2" color="textSecondary">
                        Healthy
                      </Typography>
                    </Box>
                    <Box sx={{ textAlign: 'center' }}>
                      <Typography variant="h4" color="warning.main">
                        {healthData.unhealthyRules.filter(r => r.healthStatus === 'Warning').length}
                      </Typography>
                      <Typography variant="body2" color="textSecondary">
                        Warning
                      </Typography>
                    </Box>
                    <Box sx={{ textAlign: 'center' }}>
                      <Typography variant="h4" color="error.main">
                        {healthData.unhealthyRules.filter(r => r.healthStatus === 'Critical').length}
                      </Typography>
                      <Typography variant="body2" color="textSecondary">
                        Critical
                      </Typography>
                    </Box>
                  </Box>
                  
                  <LinearProgress
                    variant="determinate"
                    value={100}
                    sx={{
                      height: 20,
                      borderRadius: 10,
                      backgroundColor: 'error.light',
                      '& .MuiLinearProgress-bar': {
                        background: `linear-gradient(to right, 
                          #4caf50 0%, 
                          #4caf50 ${(healthData.healthyRulesCount / (healthData.healthyRulesCount + healthData.unhealthyRulesCount)) * 100}%, 
                          #ff9800 ${(healthData.healthyRulesCount / (healthData.healthyRulesCount + healthData.unhealthyRulesCount)) * 100}%, 
                          #ff9800 ${((healthData.healthyRulesCount + healthData.unhealthyRules.filter(r => r.healthStatus === 'Warning').length) / (healthData.healthyRulesCount + healthData.unhealthyRulesCount)) * 100}%, 
                          #f44336 ${((healthData.healthyRulesCount + healthData.unhealthyRules.filter(r => r.healthStatus === 'Warning').length) / (healthData.healthyRulesCount + healthData.unhealthyRulesCount)) * 100}%)`
                      }
                    }}
                  />
                </CardContent>
              </Card>
            </Grid>
            
            <Grid item xs={12} md={6}>
              <Card>
                <CardHeader title="Quick Actions" />
                <CardContent>
                  <List>
                    <ListItem>
                      <ListItemIcon>
                        <RefreshIcon />
                      </ListItemIcon>
                      <ListItemText
                        primary="Refresh Compilation"
                        secondary="Recompile all rules and update health status"
                      />
                      <Button
                        onClick={handleRefreshRules}
                        disabled={refreshing}
                        size="small"
                        variant="outlined"
                      >
                        {refreshing ? 'Refreshing...' : 'Refresh'}
                      </Button>
                    </ListItem>
                  </List>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Unhealthy Rules */}
          {healthData.unhealthyRules.length > 0 && (
            <Card>
              <CardHeader 
                title={`Unhealthy Rules (${healthData.unhealthyRules.length})`}
                action={
                  <Chip
                    label={`${healthData.unhealthyRules.length} issues`}
                    color="warning"
                    size="small"
                  />
                }
              />
              <CardContent>
                <TableContainer>
                  <Table>
                    <TableHead>
                      <TableRow>
                        <TableCell>Status</TableCell>
                        <TableCell>Rule Name</TableCell>
                        <TableCell>Issues</TableCell>
                        <TableCell>Compilation Time</TableCell>
                        <TableCell>Last Validated</TableCell>
                        <TableCell>Actions</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {healthData.unhealthyRules.map((rule) => (
                        <TableRow key={rule.ruleId}>
                          <TableCell>
                            <Chip
                              icon={getHealthIcon(rule.healthStatus)}
                              label={rule.healthStatus}
                              color={getHealthColor(rule.healthStatus) as any}
                              size="small"
                            />
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2">
                              {rule.ruleName}
                            </Typography>
                            <Typography variant="caption" color="textSecondary">
                              ID: {rule.ruleId}
                            </Typography>
                          </TableCell>
                          <TableCell>
                            {rule.validationError && (
                              <Alert severity="error" sx={{ mb: 1 }}>
                                <Typography variant="caption">
                                  {rule.validationError}
                                </Typography>
                              </Alert>
                            )}
                            {rule.warnings.map((warning, index) => (
                              <Alert key={index} severity="warning" sx={{ mb: 1 }}>
                                <Typography variant="caption">
                                  {warning}
                                </Typography>
                              </Alert>
                            ))}
                          </TableCell>
                          <TableCell>
                            <Chip
                              label={`${rule.compilationTimeMs.toFixed(1)}ms`}
                              color={rule.compilationTimeMs > 100 ? 'error' : 
                                     rule.compilationTimeMs > 50 ? 'warning' : 'success'}
                              size="small"
                            />
                          </TableCell>
                          <TableCell>
                            <Typography variant="body2">
                              {rule.lastValidated ? 
                                new Date(rule.lastValidated).toLocaleString() : 
                                'Never'
                              }
                            </Typography>
                          </TableCell>
                          <TableCell>
                            <Button
                              size="small"
                              variant="outlined"
                              href={`#/yara-rules/${rule.ruleId}/show`}
                            >
                              View Rule
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              </CardContent>
            </Card>
          )}

          {/* All Healthy Message */}
          {healthData.unhealthyRules.length === 0 && (
            <Alert severity="success">
              <AlertTitle>All Rules Healthy</AlertTitle>
              All YARA rules are compiled, validated, and functioning properly. 
              System performance is optimal.
            </Alert>
          )}
        </>
      )}
    </Box>
  );
};
