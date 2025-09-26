import React, { useState, useEffect } from 'react';
import {
  Card,
  CardContent,
  Typography,
  Box,
  Chip,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  LinearProgress,
} from '@mui/material';
import {
  ExpandMore as ExpandMoreIcon,
  Speed as SpeedIcon,
  Memory as MemoryIcon,
  Timeline as TimelineIcon,
  Assessment as AssessmentIcon,
} from '@mui/icons-material';
import PreloadManager from '../utils/PreloadManager';

interface PreloadDebugPanelProps {
  show?: boolean;
}

export const PreloadDebugPanel: React.FC<PreloadDebugPanelProps> = ({ show = false }) => {
  const [stats, setStats] = useState<any>(null);
  const [performanceReport, setPerformanceReport] = useState<any>(null);

  useEffect(() => {
    if (!show) return;

    const updateStats = () => {
      const preloadManager = PreloadManager.getInstance();
      setStats(preloadManager.getStats());
      setPerformanceReport(preloadManager.getPerformanceReport());
    };

    // Initial update
    updateStats();

    // Update every 5 seconds
    const interval = setInterval(updateStats, 5000);

    return () => clearInterval(interval);
  }, [show]);

  if (!show || !stats) {
    return null;
  }

  return (
    <Card sx={{ mb: 2, border: '2px dashed #orange', backgroundColor: '#fffbf0' }}>
      <CardContent>
        <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
          <SpeedIcon sx={{ mr: 1, color: 'orange' }} />
          <Typography variant="h6" component="h2">
            Preload Performance Debug Panel
          </Typography>
          <Chip
            label="DEV MODE"
            size="small"
            sx={{ ml: 2, backgroundColor: 'orange', color: 'white' }}
          />
        </Box>

        {/* Quick Stats */}
        <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
          <Chip
            icon={<MemoryIcon />}
            label={`${stats.preloadedCount} Components Cached`}
            color="primary"
            variant="outlined"
          />
          <Chip
            icon={<TimelineIcon />}
            label={`${stats.cacheHitRate}% Cache Hit Rate`}
            color={stats.cacheHitRate > 70 ? 'success' : stats.cacheHitRate > 40 ? 'warning' : 'error'}
            variant="outlined"
          />
          <Chip
            label={`${stats.queueLength} Pending`}
            color={stats.queueLength > 0 ? 'warning' : 'default'}
            variant="outlined"
          />
          <Chip
            label={performanceReport?.estimatedMemoryUsage?.estimated || '0 MB'}
            color="info"
            variant="outlined"
          />
        </Box>

        {/* Detailed Analytics */}
        <Accordion>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="subtitle1">
              <AssessmentIcon sx={{ mr: 1, verticalAlign: 'middle' }} />
              Performance Analytics
            </Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Box sx={{ mb: 2 }}>
              <Typography variant="subtitle2" gutterBottom>
                Navigation Pattern Accuracy
              </Typography>
              <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                <LinearProgress
                  variant="determinate"
                  value={performanceReport?.navigationPatternAccuracy || 0}
                  sx={{ flex: 1, mr: 2 }}
                />
                <Typography variant="body2">
                  {performanceReport?.navigationPatternAccuracy || 0}%
                </Typography>
              </Box>
              <Typography variant="caption" color="textSecondary">
                How often our predictions match actual navigation
              </Typography>
            </Box>

            {performanceReport?.topNavigationPaths?.length > 0 && (
              <Box sx={{ mb: 2 }}>
                <Typography variant="subtitle2" gutterBottom>
                  Top Navigation Paths
                </Typography>
                {performanceReport.topNavigationPaths.map((path: any, index: number) => (
                  <Box key={path.path} sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                    <Typography variant="body2" sx={{ minWidth: 150 }}>
                      {path.path}
                    </Typography>
                    <LinearProgress
                      variant="determinate"
                      value={path.percentage}
                      sx={{ flex: 1, mx: 2 }}
                    />
                    <Typography variant="body2">
                      {path.percentage}%
                    </Typography>
                  </Box>
                ))}
              </Box>
            )}
          </AccordionDetails>
        </Accordion>

        <Accordion>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="subtitle1">Component Load Times</Typography>
          </AccordionSummary>
          <AccordionDetails>
            {performanceReport?.slowestComponents?.length > 0 ? (
              <TableContainer component={Paper} variant="outlined">
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Component</TableCell>
                      <TableCell align="right">Avg Load Time</TableCell>
                      <TableCell align="right">Status</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {performanceReport.slowestComponents.map((comp: any) => (
                      <TableRow key={comp.component}>
                        <TableCell>{comp.component}</TableCell>
                        <TableCell align="right">{comp.avgTime}ms</TableCell>
                        <TableCell align="right">
                          <Chip
                            label={comp.avgTime > 1000 ? 'Slow' : comp.avgTime > 500 ? 'Medium' : 'Fast'}
                            color={comp.avgTime > 1000 ? 'error' : comp.avgTime > 500 ? 'warning' : 'success'}
                            size="small"
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            ) : (
              <Typography variant="body2" color="textSecondary">
                No load time data available yet. Navigate between pages to collect data.
              </Typography>
            )}
          </AccordionDetails>
        </Accordion>

        <Accordion>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="subtitle1">Preloaded Components</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
              {stats.preloadedComponents.map((component: string) => (
                <Chip
                  key={component}
                  label={component}
                  size="small"
                  color="success"
                  variant="outlined"
                />
              ))}
              {stats.preloadedComponents.length === 0 && (
                <Typography variant="body2" color="textSecondary">
                  No components preloaded yet
                </Typography>
              )}
            </Box>
          </AccordionDetails>
        </Accordion>

        <Accordion>
          <AccordionSummary expandIcon={<ExpandMoreIcon />}>
            <Typography variant="subtitle1">Recent Navigation History</Typography>
          </AccordionSummary>
          <AccordionDetails>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
              {stats.navigationHistory.map((page: string, index: number) => (
                <Chip
                  key={`${page}-${index}`}
                  label={`${index + 1}. ${page}`}
                  size="small"
                  variant="outlined"
                />
              ))}
              {stats.navigationHistory.length === 0 && (
                <Typography variant="body2" color="textSecondary">
                  No navigation history yet
                </Typography>
              )}
            </Box>
          </AccordionDetails>
        </Accordion>

        {/* Quick Actions */}
        <Box sx={{ mt: 2, pt: 2, borderTop: '1px solid #ddd' }}>
          <Typography variant="caption" color="textSecondary">
            💡 This panel is for development monitoring. To enable in production, add ?preload-debug=true to the URL.
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
};

export default PreloadDebugPanel;