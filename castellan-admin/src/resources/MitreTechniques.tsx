import React, { useState } from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  Show,
  SimpleShowLayout,
  Filter,
  SelectInput,
  TextInput,
  useRecordContext,
  Button,
  TopToolbar,
  useNotify,
  useRefresh,
  useDataProvider,
  FunctionField,
} from 'react-admin';
import { 
  Chip, 
  Box, 
  Typography, 
  Card,
  CardContent,
  CardHeader,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  CircularProgress,
  Alert,
  LinearProgress,
  Accordion,
  AccordionSummary,
  AccordionDetails,
} from '@mui/material';
import { 
  Security as SecurityIcon,
  Download as ImportIcon,
  BarChart as StatisticsIcon,
  ExpandMore as ExpandMoreIcon,
  CheckCircle as SuccessIcon,
  Error as ErrorIcon,
  Warning as WarningIcon,
  InfoOutlined as InfoIcon,
} from '@mui/icons-material';

// Custom header component with security icon
const MitreTechniquesHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      MITRE ATT&CK Techniques
    </Typography>
  </Box>
);

// Custom component for tactic display with color coding
const TacticField = ({ source }: any) => {
  const record = useRecordContext();
  
  const getTacticColor = (tactic: string) => {
    const tacticColors: { [key: string]: any } = {
      'reconnaissance': 'info',
      'resource-development': 'secondary',
      'initial-access': 'warning',
      'execution': 'error',
      'persistence': 'error',
      'privilege-escalation': 'error',
      'defense-evasion': 'warning',
      'credential-access': 'error',
      'discovery': 'info',
      'lateral-movement': 'warning',
      'collection': 'info',
      'command-and-control': 'warning',
      'exfiltration': 'error',
      'impact': 'error',
    };
    
    const tacticKey = tactic?.toLowerCase().replace(/\s+/g, '-') || '';
    return tacticColors[tacticKey] || 'default';
  };

  const tacticValue = record?.[source] || record?.tactic || 'Unknown';
  
  return (
    <Chip 
      label={tacticValue} 
      color={getTacticColor(tacticValue)}
      size="small"
      variant="outlined"
    />
  );
};

// Custom component for platform display
const PlatformField = ({ source }: any) => {
  const record = useRecordContext();
  const platforms = record?.[source] || record?.platform || '';
  
  if (!platforms) return <Typography variant="body2">-</Typography>;
  
  const platformList = platforms.split(',').map((p: string) => p.trim());
  
  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
      {platformList.slice(0, 3).map((platform: string, index: number) => (
        <Chip 
          key={index}
          label={platform}
          size="small"
          variant="outlined"
          color="default"
        />
      ))}
      {platformList.length > 3 && (
        <Chip 
          label={`+${platformList.length - 3} more`}
          size="small"
          variant="outlined"
          color="primary"
        />
      )}
    </Box>
  );
};

// Import dialog component
const ImportDialog = ({ 
  open, 
  onClose 
}: {
  open: boolean;
  onClose: () => void;
}) => {
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState<any>(null);
  const dataProvider = useDataProvider();
  const notify = useNotify();
  const refresh = useRefresh();

  const handleImport = async () => {
    setImporting(true);
    setResult(null);

    try {
      console.log('[MITRE Import] Starting import request...', {
        token: localStorage.getItem('auth_token') ? 'Present' : 'Missing',
        url: 'mitre/import'
      });
      
      const response = await dataProvider.custom({
        url: 'mitre/import',
        method: 'POST',
      });
      
      console.log('[MITRE Import] Import response received:', response);
      setResult(response.data);
      notify(`Successfully imported ${response.data.result?.techniquesImported || 0} techniques`, { 
        type: 'success' 
      });
      
      // Refresh the list after successful import
      setTimeout(() => {
        refresh();
        onClose();
      }, 2000);
      
    } catch (error: any) {
      console.error('[MITRE Import] Import error details:', {
        error,
        message: error.message,
        status: error.status,
        body: error.body,
        stack: error.stack
      });
      
      // Extract detailed error message
      let errorMessage = 'Import failed';
      let errorDetails = '';
      
      if (error.body) {
        errorDetails = error.body.details || error.body.message || JSON.stringify(error.body);
        errorMessage = error.body.message || errorMessage;
      } else if (error.message) {
        errorMessage = error.message;
      }
      
      setResult({
        success: false,
        message: errorMessage,
        details: errorDetails,
        result: null
      });
      
      notify(`Import failed: ${errorMessage}`, { type: 'error' });
    } finally {
      setImporting(false);
    }
  };

  const renderResult = () => {
    if (!result) return null;
    
    if (result.success !== false) {
      return (
        <Alert 
          severity="success"
          icon={<SuccessIcon />}
          sx={{ mt: 2 }}
        >
          <Typography variant="h6" gutterBottom>Import Successful!</Typography>
          <Typography variant="body2">
            • {result.result?.techniquesImported || 0} new techniques imported
          </Typography>
          <Typography variant="body2">
            • {result.result?.techniquesUpdated || 0} existing techniques updated
          </Typography>
          {result.result?.errors?.length > 0 && (
            <Typography variant="body2" color="warning.main">
              • {result.result.errors.length} techniques had errors
            </Typography>
          )}
        </Alert>
      );
    } else {
      return (
        <Alert 
          severity="error"
          icon={<ErrorIcon />}
          sx={{ mt: 2 }}
        >
          <Typography variant="h6" gutterBottom>Import Failed</Typography>
          <Typography variant="body2" sx={{ mb: 1 }}>{result.message}</Typography>
          {result.details && (
            <Typography variant="body2" color="text.secondary" sx={{ fontSize: '0.85em', fontFamily: 'monospace', bgcolor: 'grey.100', p: 1, borderRadius: 1 }}>
              Details: {result.details}
            </Typography>
          )}
        </Alert>
      );
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>
        Import MITRE ATT&CK Techniques
      </DialogTitle>
      <DialogContent>
        {importing ? (
          <Box>
            <Box display="flex" alignItems="center" justifyContent="center" py={3}>
              <CircularProgress />
              <Typography sx={{ ml: 2 }}>
                Importing MITRE ATT&CK techniques from official source...
              </Typography>
            </Box>
            <LinearProgress sx={{ mt: 2 }} />
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
              This may take a few minutes. Please don't close this dialog.
            </Typography>
          </Box>
        ) : result ? (
          renderResult()
        ) : (
          <Box>
            <Alert severity="info" icon={<InfoIcon />} sx={{ mb: 2 }}>
              This will download and import the latest MITRE ATT&CK techniques from the official MITRE repository.
            </Alert>
            
            <Accordion>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Typography variant="subtitle1">What will be imported?</Typography>
              </AccordionSummary>
              <AccordionDetails>
                <Typography variant="body2" gutterBottom>
                  • All current MITRE ATT&CK techniques and sub-techniques
                </Typography>
                <Typography variant="body2" gutterBottom>
                  • Technique descriptions, tactics, and platforms
                </Typography>
                <Typography variant="body2" gutterBottom>
                  • Data sources, mitigations, and examples
                </Typography>
                <Typography variant="body2" gutterBottom>
                  • Updates to existing techniques in the database
                </Typography>
              </AccordionDetails>
            </Accordion>

            <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
              <strong>Note:</strong> This operation may take several minutes to complete and requires an active internet connection.
            </Typography>
          </Box>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={importing}>
          {result ? 'Close' : 'Cancel'}
        </Button>
        {!result && (
          <Button 
            onClick={handleImport} 
            variant="contained" 
            disabled={importing}
            startIcon={<ImportIcon />}
          >
            Start Import
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};

// Custom list actions with import button
const MitreListActions = () => {
  const [importDialogOpen, setImportDialogOpen] = useState(false);
  
  return (
    <TopToolbar>
      <Button
        onClick={() => setImportDialogOpen(true)}
        startIcon={<ImportIcon />}
        label="Import Techniques"
        variant="contained"
        size="small"
      />
      <Button
        onClick={() => window.location.href = '#/mitre-statistics'}
        startIcon={<StatisticsIcon />}
        label="View Statistics"
        variant="outlined"
        size="small"
      />
      
      <ImportDialog
        open={importDialogOpen}
        onClose={() => setImportDialogOpen(false)}
      />
    </TopToolbar>
  );
};

// Filters for the list view
const MitreTechniqueFilters = [
  <TextInput source="search" label="Search" alwaysOn />,
  <TextInput source="tactic" label="Tactic" />,
  <SelectInput 
    source="platform" 
    label="Platform"
    choices={[
      { id: 'Windows', name: 'Windows' },
      { id: 'Linux', name: 'Linux' },
      { id: 'macOS', name: 'macOS' },
      { id: 'Network', name: 'Network' },
      { id: 'Cloud', name: 'Cloud' },
      { id: 'Mobile', name: 'Mobile' },
    ]}
  />,
];

export const MitreTechniquesList = () => (
  <Box>
    <MitreTechniquesHeader />
    <List 
      filters={<Filter>{MitreTechniqueFilters}</Filter>}
      sort={{ field: 'techniqueId', order: 'ASC' }}
      perPage={25}
      title=" "
      actions={<MitreListActions />}
      resource="mitre/techniques"
    >
      <Datagrid rowClick="show" size="small">
        <TextField source="techniqueId" label="ID" sortable />
        <TextField source="name" label="Name" />
        <TacticField source="tactic" label="Tactic" />
        <PlatformField source="platform" label="Platforms" />
        <FunctionField 
          source="description" 
          label="Description"
          render={(record: any) => 
            record.description 
              ? record.description.substring(0, 100) + (record.description.length > 100 ? '...' : '')
              : '-'
          }
        />
        <DateField source="createdAt" showTime label="Added" />
      </Datagrid>
    </List>
  </Box>
);

export const MitreTechniquesShow = () => (
  <Box>
    <MitreTechniquesHeader />
    <Show title=" " resource="mitre/techniques">
      <SimpleShowLayout>
        <TextField source="techniqueId" label="Technique ID" />
        <TextField source="name" label="Name" />
        <TacticField source="tactic" label="Tactic" />
        <PlatformField source="platform" label="Platforms" />
        
        <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>Description</Typography>
        <TextField source="description" label="" />
        
        {/* Show additional fields if available */}
        <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>Technical Details</Typography>
        <TextField source="dataSources" label="Data Sources" />
        <TextField source="mitigations" label="Mitigations" />
        <TextField source="examples" label="Examples" />
        
        <DateField source="createdAt" showTime label="Added to Database" />
        
        {/* Show associated applications if any */}
        <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>Associated Applications</Typography>
        <FunctionField
          source="associatedApplications"
          label=""
          render={(record: any) => {
            if (!record.associatedApplications || record.associatedApplications.length === 0) {
              return <Typography variant="body2" color="text.secondary">None</Typography>;
            }
            return (
              <Box>
                {record.associatedApplications.map((app: any, index: number) => (
                  <Chip 
                    key={index}
                    label={`${app.ApplicationName} (${app.Confidence})`}
                    size="small"
                    sx={{ mr: 1, mb: 1 }}
                  />
                ))}
              </Box>
            );
          }}
        />
      </SimpleShowLayout>
    </Show>
  </Box>
);

// Statistics dashboard component
export const MitreStatisticsDashboard = () => {
  const [statistics, setStatistics] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [cacheHit, setCacheHit] = useState(false);
  const dataProvider = useDataProvider();
  const notify = useNotify();

  React.useEffect(() => {
    const fetchStatistics = async () => {
      try {
        setLoading(true);
        setCacheHit(false);
        
        console.log('📊 Fetching MITRE statistics (using cached data provider)...');
        const startTime = Date.now();
        
        const [statsResponse, countResponse] = await Promise.all([
          dataProvider.custom({ url: 'mitre/statistics', method: 'GET' }),
          dataProvider.custom({ url: 'mitre/count', method: 'GET' })
        ]);
        
        const endTime = Date.now();
        const fetchTime = endTime - startTime;
        
        // If fetch was very fast (< 100ms), it was likely from cache
        if (fetchTime < 100) {
          setCacheHit(true);
          console.log('⚡ MITRE statistics loaded from cache in', fetchTime, 'ms');
        } else {
          console.log('🌐 MITRE statistics fetched from API in', fetchTime, 'ms');
        }
        
        setStatistics({
          ...statsResponse.data,
          ...countResponse.data,
          _fetchTime: fetchTime,
          _fromCache: fetchTime < 100
        });
      } catch (error) {
        console.error('Failed to fetch MITRE statistics:', error);
        notify('Failed to load MITRE statistics', { type: 'error' });
      } finally {
        setLoading(false);
      }
    };

    fetchStatistics();
  }, [dataProvider, notify]);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="200px">
        <CircularProgress />
      </Box>
    );
  }

  const tacticData = statistics?.techniquesByTactic || {};
  const maxCount = Math.max(...Object.values(tacticData).map(v => Number(v) || 0));

  return (
    <Box>
      <MitreTechniquesHeader />
      
      <Card sx={{ mb: 2 }}>
        <CardHeader 
          title="MITRE ATT&CK Database Statistics" 
          action={
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              {cacheHit && (
                <Chip 
                  icon={<SuccessIcon />} 
                  label="Cached" 
                  color="success" 
                  size="small" 
                  variant="outlined"
                />
              )}
              {statistics?._fetchTime && (
                <Typography variant="caption" color="text.secondary">
                  {statistics._fetchTime}ms
                </Typography>
              )}
            </Box>
          }
        />
        <CardContent>
          <Typography variant="h3" color="primary" gutterBottom>
            {statistics?.totalTechniques || 0}
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Total Techniques in Database
          </Typography>
          
          {statistics?.lastUpdated && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
              Last updated: {new Date(statistics.lastUpdated).toLocaleString()}
            </Typography>
          )}
          
          {statistics?._fromCache && (
            <Typography variant="caption" color="success.main" sx={{ mt: 1, display: 'block' }}>
              ⚡ Instant load from cache
            </Typography>
          )}
          
          {statistics?.shouldImport && (
            <Alert severity="info" sx={{ mt: 2 }}>
              <Typography variant="body2">
                The MITRE database appears to be empty or outdated. Consider importing the latest techniques.
              </Typography>
            </Alert>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader title="Techniques by Tactic" />
        <CardContent>
          {Object.keys(tacticData).length > 0 ? (
            <Box>
              {Object.entries(tacticData).map(([tactic, count]) => (
                <Box key={tactic} sx={{ mb: 2 }}>
                  <Box display="flex" justifyContent="space-between" alignItems="center" mb={1}>
                    <TacticField source="tactic" record={{ tactic }} />
                    <Typography variant="body2" fontWeight="bold">
                      {String(count)}
                    </Typography>
                  </Box>
                  <LinearProgress 
                    variant="determinate" 
                    value={(Number(count) / maxCount) * 100}
                    sx={{ height: 8, borderRadius: 4 }}
                  />
                </Box>
              ))}
            </Box>
          ) : (
            <Typography variant="body2" color="text.secondary">
              No tactic data available. Import MITRE techniques to see statistics.
            </Typography>
          )}
        </CardContent>
      </Card>
    </Box>
  );
};
