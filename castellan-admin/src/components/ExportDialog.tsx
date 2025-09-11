import React, { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormGroup,
  FormControlLabel,
  Switch,
  Box,
  Typography,
  Card,
  CardContent,
  Grid,
  Chip,
  CircularProgress,
  Alert,
  Divider,
  List,
  ListItem,
  ListItemText,
  ListItemIcon,
  Stepper,
  Step,
  StepLabel,
  StepContent,
  Paper,
} from '@mui/material';
import {
  Download as DownloadIcon,
  FileDownload as FileDownloadIcon,
  Assessment as AssessmentIcon,
  Settings as SettingsIcon,
  Preview as PreviewIcon,
  GetApp as GetAppIcon,
  CheckCircle as CheckCircleIcon,
  TableChart as TableChartIcon,
  Code as CodeIcon,
  PictureAsPdf as PictureAsPdfIcon,
} from '@mui/icons-material';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns';
import { useDataProvider, useNotify } from 'react-admin';

interface ExportFormat {
  id: string;
  name: string;
  extension: string;
  mimeType: string;
  supportsRawData: boolean;
  description: string;
}

interface ExportStats {
  totalEvents: number;
  riskLevelDistribution: { [key: string]: number };
  eventTypeDistribution: { [key: string]: number };
  dateRange: {
    earliest: string;
    latest: string;
  } | null;
  averageConfidence: number;
  enhancedEvents: number;
  deterministicEvents: number;
  correlationBasedEvents: number;
}

interface ExportDialogProps {
  open: boolean;
  onClose: () => void;
  filters?: any;
}

const ExportDialog: React.FC<ExportDialogProps> = ({ open, onClose, filters = {} }) => {
  // State management
  const [activeStep, setActiveStep] = useState(0);
  const [selectedFormat, setSelectedFormat] = useState('');
  const [availableFormats, setAvailableFormats] = useState<ExportFormat[]>([]);
  const [exportOptions, setExportOptions] = useState({
    includeRawData: false,
    includeSummary: true,
    includeRecommendations: true,
    startDate: null as Date | null,
    endDate: null as Date | null,
    riskLevels: [] as string[],
    eventTypes: [] as string[],
  });
  const [exportStats, setExportStats] = useState<ExportStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [statsLoading, setStatsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const dataProvider = useDataProvider();
  const notify = useNotify();

  // Format icons
  const getFormatIcon = (formatId: string) => {
    switch (formatId) {
      case 'csv':
        return <TableChartIcon />;
      case 'json':
        return <CodeIcon />;
      case 'pdf':
        return <PictureAsPdfIcon />;
      default:
        return <FileDownloadIcon />;
    }
  };

  // Risk level choices
  const RISK_LEVELS = ['low', 'medium', 'high', 'critical'];
  const EVENT_TYPES = [
    'Failed Login', 'Successful Login', 'Privilege Escalation', 
    'Malware Detection', 'Suspicious Process', 'Registry Modification',
    'Network Anomaly', 'Data Exfiltration', 'Command Execution', 'Service Creation'
  ];

  // Load available export formats
  useEffect(() => {
    if (open) {
      loadExportFormats();
      loadExportStats();
    }
  }, [open, exportOptions.startDate, exportOptions.endDate, exportOptions.riskLevels, exportOptions.eventTypes]);

  const loadExportFormats = async () => {
    try {
      const response = await dataProvider.getOne('export/formats', { id: '' });
      setAvailableFormats(response.data.data || []);
      if (response.data.data?.length > 0) {
        setSelectedFormat(response.data.data[0].id);
      }
    } catch (error) {
      console.error('Error loading export formats:', error);
      setError('Failed to load export formats');
    }
  };

  const loadExportStats = async () => {
    setStatsLoading(true);
    try {
      const queryParams = new URLSearchParams();
      
      if (exportOptions.startDate) {
        queryParams.append('startDate', exportOptions.startDate.toISOString());
      }
      if (exportOptions.endDate) {
        queryParams.append('endDate', exportOptions.endDate.toISOString());
      }
      if (exportOptions.riskLevels.length > 0) {
        queryParams.append('riskLevels', exportOptions.riskLevels.join(','));
      }
      if (exportOptions.eventTypes.length > 0) {
        queryParams.append('eventTypes', exportOptions.eventTypes.join(','));
      }

      // Apply current filters from the list view
      Object.keys(filters).forEach(key => {
        if (filters[key] && filters[key] !== '') {
          queryParams.append(key, filters[key].toString());
        }
      });

      const response = await fetch(`http://localhost:5000/api/export/stats?${queryParams}`, {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`,
          'Accept': 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error('Failed to load export statistics');
      }

      const data = await response.json();
      setExportStats(data.data);
    } catch (error) {
      console.error('Error loading export stats:', error);
      setError('Failed to load export statistics');
    } finally {
      setStatsLoading(false);
    }
  };

  const handleExport = async () => {
    if (!selectedFormat) {
      setError('Please select an export format');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const exportRequest = {
        format: selectedFormat,
        includeRawData: exportOptions.includeRawData,
        includeSummary: exportOptions.includeSummary,
        includeRecommendations: exportOptions.includeRecommendations,
        startDate: exportOptions.startDate?.toISOString(),
        endDate: exportOptions.endDate?.toISOString(),
        riskLevels: exportOptions.riskLevels,
        eventTypes: exportOptions.eventTypes,
        filters: filters, // Apply current list view filters
      };

      const response = await fetch('http://localhost:5000/api/export/security-events', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('auth_token')}`,
        },
        body: JSON.stringify(exportRequest),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || 'Export failed');
      }

      // Get filename from response headers
      const contentDisposition = response.headers.get('Content-Disposition');
      const filename = contentDisposition 
        ? contentDisposition.split('filename=')[1]?.replace(/"/g, '')
        : `security-events.${selectedFormat}`;

      // Download the file
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      notify(`Export completed successfully: ${filename}`, { type: 'success' });
      onClose();
    } catch (error: any) {
      console.error('Export error:', error);
      setError(error.message || 'Export failed');
      notify('Export failed', { type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  const handleNext = () => {
    setActiveStep((prevActiveStep) => prevActiveStep + 1);
  };

  const handleBack = () => {
    setActiveStep((prevActiveStep) => prevActiveStep - 1);
  };

  const handleReset = () => {
    setActiveStep(0);
    setSelectedFormat('');
    setExportOptions({
      includeRawData: false,
      includeSummary: true,
      includeRecommendations: true,
      startDate: null,
      endDate: null,
      riskLevels: [],
      eventTypes: [],
    });
    setError(null);
  };

  const steps = [
    {
      label: 'Select Format',
      content: (
        <Box>
          <Typography variant="h6" gutterBottom>
            Choose Export Format
          </Typography>
          <Grid container spacing={2}>
            {availableFormats.map((format) => (
              <Grid item xs={12} sm={4} key={format.id}>
                <Card 
                  variant={selectedFormat === format.id ? 'outlined' : 'elevation'}
                  sx={{ 
                    cursor: 'pointer',
                    border: selectedFormat === format.id ? 2 : 1,
                    borderColor: selectedFormat === format.id ? 'primary.main' : 'divider',
                  }}
                  onClick={() => setSelectedFormat(format.id)}
                >
                  <CardContent>
                    <Box display="flex" alignItems="center" mb={1}>
                      {getFormatIcon(format.id)}
                      <Typography variant="h6" sx={{ ml: 1 }}>
                        {format.name}
                      </Typography>
                    </Box>
                    <Typography variant="body2" color="text.secondary">
                      {format.description}
                    </Typography>
                    {format.supportsRawData && (
                      <Chip label="Raw Data Support" size="small" sx={{ mt: 1 }} />
                    )}
                  </CardContent>
                </Card>
              </Grid>
            ))}
          </Grid>
        </Box>
      ),
    },
    {
      label: 'Configure Options',
      content: (
        <Box>
          <Typography variant="h6" gutterBottom>
            Export Options
          </Typography>
          
          <FormGroup sx={{ mb: 3 }}>
            <FormControlLabel
              control={
                <Switch
                  checked={exportOptions.includeRawData}
                  onChange={(e) => setExportOptions(prev => ({ ...prev, includeRawData: e.target.checked }))}
                  disabled={!availableFormats.find(f => f.id === selectedFormat)?.supportsRawData}
                />
              }
              label="Include Raw Event Data"
            />
            <FormControlLabel
              control={
                <Switch
                  checked={exportOptions.includeSummary}
                  onChange={(e) => setExportOptions(prev => ({ ...prev, includeSummary: e.target.checked }))}
                />
              }
              label="Include Executive Summary"
            />
            <FormControlLabel
              control={
                <Switch
                  checked={exportOptions.includeRecommendations}
                  onChange={(e) => setExportOptions(prev => ({ ...prev, includeRecommendations: e.target.checked }))}
                />
              }
              label="Include Recommendations"
            />
          </FormGroup>

          <LocalizationProvider dateAdapter={AdapterDateFns}>
            <Grid container spacing={2} sx={{ mb: 3 }}>
              <Grid item xs={6}>
                <DatePicker
                  label="Start Date"
                  value={exportOptions.startDate}
                  onChange={(date) => setExportOptions(prev => ({ ...prev, startDate: date }))}
                  slotProps={{ textField: { fullWidth: true } }}
                />
              </Grid>
              <Grid item xs={6}>
                <DatePicker
                  label="End Date"
                  value={exportOptions.endDate}
                  onChange={(date) => setExportOptions(prev => ({ ...prev, endDate: date }))}
                  slotProps={{ textField: { fullWidth: true } }}
                />
              </Grid>
            </Grid>
          </LocalizationProvider>

          <Grid container spacing={2}>
            <Grid item xs={6}>
              <FormControl fullWidth>
                <InputLabel>Risk Levels</InputLabel>
                <Select
                  multiple
                  value={exportOptions.riskLevels}
                  onChange={(e) => setExportOptions(prev => ({ 
                    ...prev, 
                    riskLevels: Array.isArray(e.target.value) ? e.target.value : [e.target.value]
                  }))}
                  renderValue={(selected) => (
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                      {(selected as string[]).map((value) => (
                        <Chip key={value} label={value} size="small" />
                      ))}
                    </Box>
                  )}
                >
                  {RISK_LEVELS.map((level) => (
                    <MenuItem key={level} value={level}>
                      {level.charAt(0).toUpperCase() + level.slice(1)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={6}>
              <FormControl fullWidth>
                <InputLabel>Event Types</InputLabel>
                <Select
                  multiple
                  value={exportOptions.eventTypes}
                  onChange={(e) => setExportOptions(prev => ({ 
                    ...prev, 
                    eventTypes: Array.isArray(e.target.value) ? e.target.value : [e.target.value]
                  }))}
                  renderValue={(selected) => (
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                      {(selected as string[]).map((value) => (
                        <Chip key={value} label={value} size="small" />
                      ))}
                    </Box>
                  )}
                >
                  {EVENT_TYPES.map((type) => (
                    <MenuItem key={type} value={type}>
                      {type}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
          </Grid>
        </Box>
      ),
    },
    {
      label: 'Preview & Export',
      content: (
        <Box>
          <Typography variant="h6" gutterBottom>
            Export Preview
          </Typography>
          
          {statsLoading ? (
            <Box display="flex" justifyContent="center" p={3}>
              <CircularProgress />
            </Box>
          ) : exportStats ? (
            <Grid container spacing={2}>
              <Grid item xs={12} md={6}>
                <Card>
                  <CardContent>
                    <Typography variant="h6" gutterBottom>
                      <AssessmentIcon sx={{ mr: 1, verticalAlign: 'middle' }} />
                      Export Statistics
                    </Typography>
                    <List>
                      <ListItem>
                        <ListItemText
                          primary="Total Events"
                          secondary={exportStats.totalEvents.toLocaleString()}
                        />
                      </ListItem>
                      <ListItem>
                        <ListItemText
                          primary="Average Confidence"
                          secondary={`${exportStats.averageConfidence}%`}
                        />
                      </ListItem>
                      <ListItem>
                        <ListItemText
                          primary="Enhanced Events"
                          secondary={`${exportStats.enhancedEvents} (${((exportStats.enhancedEvents / exportStats.totalEvents) * 100).toFixed(1)}%)`}
                        />
                      </ListItem>
                    </List>
                  </CardContent>
                </Card>
              </Grid>
              
              <Grid item xs={12} md={6}>
                <Card>
                  <CardContent>
                    <Typography variant="h6" gutterBottom>
                      Risk Level Distribution
                    </Typography>
                    <Box>
                      {Object.entries(exportStats.riskLevelDistribution).map(([level, count]) => (
                        <Box key={level} display="flex" justifyContent="space-between" alignItems="center" mb={1}>
                          <Chip 
                            label={level.toUpperCase()} 
                            size="small"
                            color={
                              level === 'critical' ? 'error' :
                              level === 'high' ? 'warning' :
                              level === 'medium' ? 'info' :
                              level === 'low' ? 'success' : 'default'
                            }
                          />
                          <Typography variant="body2">
                            {count} events
                          </Typography>
                        </Box>
                      ))}
                    </Box>
                  </CardContent>
                </Card>
              </Grid>
            </Grid>
          ) : null}
        </Box>
      ),
    },
  ];

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        <Box display="flex" alignItems="center">
          <DownloadIcon sx={{ mr: 1 }} />
          Export Security Events
        </Box>
      </DialogTitle>
      
      <DialogContent>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        
        <Stepper activeStep={activeStep} orientation="vertical">
          {steps.map((step, index) => (
            <Step key={step.label}>
              <StepLabel>{step.label}</StepLabel>
              <StepContent>
                {step.content}
                <Box sx={{ mb: 2, mt: 2 }}>
                  <div>
                    {index === steps.length - 1 ? (
                      <Button
                        variant="contained"
                        onClick={handleExport}
                        disabled={loading || !selectedFormat}
                        startIcon={loading ? <CircularProgress size={20} /> : <GetAppIcon />}
                        sx={{ mt: 1, mr: 1 }}
                      >
                        {loading ? 'Exporting...' : 'Export'}
                      </Button>
                    ) : (
                      <Button
                        variant="contained"
                        onClick={handleNext}
                        sx={{ mt: 1, mr: 1 }}
                        disabled={index === 0 && !selectedFormat}
                      >
                        Continue
                      </Button>
                    )}
                    <Button
                      disabled={index === 0}
                      onClick={handleBack}
                      sx={{ mt: 1, mr: 1 }}
                    >
                      Back
                    </Button>
                  </div>
                </Box>
              </StepContent>
            </Step>
          ))}
        </Stepper>
      </DialogContent>
      
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleReset}>Reset</Button>
      </DialogActions>
    </Dialog>
  );
};

export default ExportDialog;
