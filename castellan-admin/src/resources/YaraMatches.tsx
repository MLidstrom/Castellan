import React from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  Show,
  SimpleShowLayout,
  useRecordContext,
  FunctionField,
  ChipField,
  ArrayField,
  SingleFieldList,
  NumberField,
  ReferenceField,
  TopToolbar,
  ExportButton,
  FilterButton,
  ShowButton,
  SelectInput,
  TextInput,
} from 'react-admin';
import {
  Card,
  CardContent,
  Typography,
  Chip,
  Box,
  Grid,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Alert,
} from '@mui/material';
import {
  Security as SecurityIcon,
  CheckCircle as MatchIcon,
  Warning as ThreatIcon,
  Description as FileIcon,
} from '@mui/icons-material';

// Custom field to display threat level with color coding
const ThreatLevelField = () => {
  const record = useRecordContext();
  if (!record) return null;

  const getColor = (metadata: any) => {
    const threatLevel = metadata?.threat_level?.toLowerCase();
    switch (threatLevel) {
      case 'critical': return 'error';
      case 'high': return 'warning';
      case 'medium': return 'info';
      case 'low': return 'success';
      default: return 'default';
    }
  };

  return (
    <Chip
      label={record.metadata?.threat_level || 'Unknown'}
      color={getColor(record.metadata) as any}
      size="small"
    />
  );
};

// Custom field to display file information
const FileInfoField = () => {
  const record = useRecordContext();
  if (!record) return null;

  return (
    <Box>
      <Typography variant="body2" noWrap>
        {record.targetFile}
      </Typography>
      <Typography variant="caption" color="textSecondary">
        Hash: {record.targetHash?.substring(0, 16)}...
      </Typography>
    </Box>
  );
};

// Custom field to display matched strings details
const MatchedStringsField = () => {
  const record = useRecordContext();
  if (!record || !record.matchedStrings || record.matchedStrings.length === 0) {
    return <Typography variant="caption">No strings matched</Typography>;
  }

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Identifier</TableCell>
            <TableCell>Offset</TableCell>
            <TableCell>Value</TableCell>
            <TableCell>Type</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {record.matchedStrings.map((match: any, index: number) => (
            <TableRow key={index}>
              <TableCell>
                <Chip
                  label={match.identifier}
                  size="small"
                  variant="outlined"
                  color="primary"
                />
              </TableCell>
              <TableCell>
                <Typography variant="caption" fontFamily="monospace">
                  0x{match.offset.toString(16).toUpperCase()}
                </Typography>
              </TableCell>
              <TableCell>
                <Typography
                  variant="caption"
                  fontFamily="monospace"
                  sx={{
                    maxWidth: '300px',
                    display: 'block',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                  }}
                >
                  {match.value}
                </Typography>
              </TableCell>
              <TableCell>
                <Chip
                  label={match.isHex ? 'HEX' : 'TEXT'}
                  size="small"
                  color={match.isHex ? 'secondary' : 'default'}
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
};

// Custom field to display rule information
const RuleInfoField = () => {
  const record = useRecordContext();
  if (!record) return null;

  return (
    <Box>
      <Typography variant="body2" fontWeight="bold">
        {record.ruleName}
      </Typography>
      <Typography variant="caption" color="textSecondary">
        ID: {record.ruleId}
      </Typography>
      {record.metadata?.category && (
        <Chip
          label={record.metadata.category}
          size="small"
          variant="outlined"
          sx={{ ml: 1 }}
        />
      )}
    </Box>
  );
};

// Custom field to display execution performance
const PerformanceField = () => {
  const record = useRecordContext();
  if (!record) return null;

  const getPerformanceColor = (timeMs: number) => {
    if (timeMs < 100) return 'success';
    if (timeMs < 1000) return 'warning';
    return 'error';
  };

  return (
    <Chip
      label={`${record.executionTimeMs}ms`}
      size="small"
      color={getPerformanceColor(record.executionTimeMs) as any}
    />
  );
};

// List view for YARA matches
export const YaraMatchesList = () => (
  <List
    filters={[
      <TextInput source="q" label="Search" alwaysOn />,
      <TextInput source="ruleName" label="Rule Name" />,
      <TextInput source="targetFile" label="File Path" />,
      <SelectInput
        source="metadata.threat_level"
        label="Threat Level"
        choices={[
          { id: 'Critical', name: 'Critical' },
          { id: 'High', name: 'High' },
          { id: 'Medium', name: 'Medium' },
          { id: 'Low', name: 'Low' },
        ]}
      />,
    ]}
    sort={{ field: 'matchTime', order: 'DESC' }}
    perPage={25}
  >
    <Datagrid rowClick="show">
      <DateField source="matchTime" label="Detection Time" showTime />
      <FunctionField
        label="Rule"
        render={(record: any) => <RuleInfoField />}
      />
      <FunctionField
        label="Target"
        render={(record: any) => <FileInfoField />}
      />
      <FunctionField
        label="Threat Level"
        render={(record: any) => <ThreatLevelField />}
      />
      <FunctionField
        label="Strings"
        render={(record: any) => 
          record.matchedStrings ? record.matchedStrings.length : 0
        }
      />
      <FunctionField
        label="Scan Time"
        render={(record: any) => <PerformanceField />}
      />
      <ShowButton />
    </Datagrid>
  </List>
);

// Metadata display component
const MetadataDisplay = () => {
  const record = useRecordContext();
  
  if (!record || !record.metadata || Object.keys(record.metadata).length === 0) {
    return <Typography variant="caption">No metadata available</Typography>;
  }
  
  return (
    <Table size="small">
      <TableBody>
        {Object.entries(record.metadata).map(([key, value]) => (
          <TableRow key={key}>
            <TableCell component="th" scope="row">
              {key}
            </TableCell>
            <TableCell>{String(value)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
};

// Show view for individual YARA match
export const YaraMatchesShow = () => (
  <Show>
    <SimpleShowLayout>
      <Grid container spacing={2}>
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Match Information
              </Typography>
              <TextField source="id" label="Match ID" />
              <DateField source="matchTime" label="Detection Time" showTime />
              <TextField source="ruleName" label="Rule Name" />
              <TextField source="ruleId" label="Rule ID" />
              <FunctionField
                label="Execution Time"
                render={(record: any) => <PerformanceField />}
              />
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Target Information
              </Typography>
              <TextField source="targetFile" label="File Path" />
              <TextField source="targetHash" label="File Hash" />
              <FunctionField
                label="Threat Level"
                render={(record: any) => <ThreatLevelField />}
              />
              <Box mt={2}>
                <Typography variant="subtitle2" gutterBottom>
                  Metadata
                </Typography>
                <MetadataDisplay />
              </Box>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Matched Strings
              </Typography>
              <FunctionField
                render={(record: any) => <MatchedStringsField />}
              />
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={12}>
          <FunctionField
            render={(record: any) => (
              <Alert severity="info">
                <strong>Note:</strong> This match was detected by the YARA Rule Engine 
                at {new Date(record?.matchTime).toLocaleString()}. 
                The rule executed in {record?.executionTimeMs}ms.
              </Alert>
            )}
          />
        </Grid>
      </Grid>
    </SimpleShowLayout>
  </Show>
);
