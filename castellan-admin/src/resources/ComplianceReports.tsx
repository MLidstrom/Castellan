import React from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  NumberField,
  Show,
  SimpleShowLayout,
  Filter,
  SelectInput,
  TextInput,
  Create,
  SimpleForm,
  required,
  useRecordContext,
  Button,
  // downloadCSV, // Removed - using custom implementation
  TopToolbar,
  CreateButton,
  ExportButton,
} from 'react-admin';
import { Chip, Box, LinearProgress, Typography } from '@mui/material';
import { 
  Download as DownloadIcon, 
  Assessment as ReportIcon,
  Security as SecurityIcon,
  HealthAndSafety as HealthIcon,
  AccountBalance as GovIcon,
  Business as BusinessIcon
} from '@mui/icons-material';

// Custom header component with shield icon
const ComplianceReportsHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      Compliance Reports
    </Typography>
  </Box>
);

// Custom component for compliance framework with icons
const FrameworkField = ({ source }: any) => {
  const record = useRecordContext();
  const getFrameworkIcon = (framework: string) => {
    switch (framework?.toUpperCase?.() || '') {
      case 'HIPAA': return <HealthIcon fontSize="small" />;
      case 'SOX': return <GovIcon fontSize="small" />;
      case 'PCI DSS': return <SecurityIcon fontSize="small" />;
      case 'SOC2': return <BusinessIcon fontSize="small" />;
      case 'ISO 27001': return <ReportIcon fontSize="small" />;
      case 'ISO27001': return <ReportIcon fontSize="small" />;
      default: return <ReportIcon fontSize="small" />;
    }
  };

  const getFrameworkColor = (framework: string) => {
    switch (framework?.toUpperCase?.() || '') {
      case 'HIPAA': return 'success';
      case 'SOX': return 'primary';
      case 'PCI DSS': return 'warning';
      case 'SOC2': return 'info';
      case 'ISO 27001': return 'secondary';
      case 'ISO27001': return 'secondary';
      default: return 'default';
    }
  };

  const frameworkValue = record?.framework || record?.Framework || 'Unknown';
  
  
  return (
    <Chip 
      icon={getFrameworkIcon(frameworkValue)}
      label={frameworkValue} 
      color={getFrameworkColor(frameworkValue) as any}
      size="small"
      variant="outlined"
    />
  );
};

// Custom component for implementation percentage with progress bar
const ImplementationField = ({ source }: any) => {
  const record = useRecordContext();
  // Try multiple possible field names for implementation percentage
  const percentage = record?.implementationPercentage || record?.complianceScore || record?.ComplianceScore || 0;
  
  
  const getColor = (value: number) => {
    if (value >= 90) return 'success';
    if (value >= 70) return 'info';
    if (value >= 50) return 'warning';
    return 'error';
  };

  return (
    <Box sx={{ display: 'flex', alignItems: 'center', minWidth: 120 }}>
      <Box sx={{ width: '100%', mr: 1 }}>
        <LinearProgress 
          variant="determinate" 
          value={percentage} 
          color={getColor(percentage)}
          sx={{ height: 8, borderRadius: 4 }}
        />
      </Box>
      <Typography variant="body2" color="text.secondary">
        {`${percentage}%`}
      </Typography>
    </Box>
  );
};

// Custom component for report status
const StatusField = ({ source }: any) => {
  const record = useRecordContext();
  const getStatusColor = (status: string) => {
    switch (status?.toLowerCase()) {
      case 'complete': return 'success';
      case 'in_progress': return 'info';
      case 'pending': return 'warning';
      case 'failed': return 'error';
      default: return 'default';
    }
  };

  const statusValue = record?.status || record?.Status || 'Unknown';
  
  return (
    <Chip 
      label={statusValue} 
      color={getStatusColor(statusValue)}
      size="small"
    />
  );
};

// Custom download button for reports
const DownloadReportButton = () => {
  const record = useRecordContext();
  
  const handleDownload = () => {
    if (!record) return;
    
    // In a real implementation, this would download the actual report file
    const csvData = `Framework,Report Type,Implementation %,Controls,Status,Generated\n${record.framework},${record.reportType},${record.implementationPercentage},${record.controlCount},${record.status},${record.generated}`;
    
    const blob = new Blob([csvData], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${record.framework}_${record.reportType}_${record.id}.csv`;
    a.click();
    window.URL.revokeObjectURL(url);
  };
  
  return (
    <Button
      onClick={handleDownload}
      startIcon={<DownloadIcon />}
      label="Download Report"
      variant="outlined"
      size="small"
    />
  );
};

// Filters for the list view
const ComplianceReportFilters = [
  <SelectInput 
    source="framework" 
    label="Framework"
    choices={[
      { id: 'HIPAA', name: 'HIPAA' },
      { id: 'SOX', name: 'SOX' },
      { id: 'PCI DSS', name: 'PCI DSS' },
      { id: 'ISO 27001', name: 'ISO 27001' },
      { id: 'SOC2', name: 'SOC2' },
    ]}
    alwaysOn
  />,
  <SelectInput 
    source="reportType" 
    label="Report Type"
    choices={[
      { id: 'assessment', name: 'Assessment Report' },
      { id: 'gap_analysis', name: 'Gap Analysis' },
      { id: 'implementation', name: 'Implementation Plan' },
      { id: 'audit_ready', name: 'Audit Readiness' },
      { id: 'continuous', name: 'Continuous Monitoring' },
    ]}
  />,
  <SelectInput 
    source="status" 
    label="Status"
    choices={[
      { id: 'complete', name: 'Complete' },
      { id: 'in_progress', name: 'In Progress' },
      { id: 'pending', name: 'Pending' },
      { id: 'failed', name: 'Failed' },
    ]}
  />,
];

// Custom list actions with generate report button
const ComplianceReportListActions = () => (
  <TopToolbar>
    <CreateButton label="Generate Report" />
    <ExportButton />
  </TopToolbar>
);

export const ComplianceReportList = () => (
  <Box>
    <ComplianceReportsHeader />
    <List 
      filters={<Filter>{ComplianceReportFilters}</Filter>}
      sort={{ field: 'generated', order: 'DESC' }}
      perPage={25}
      title=" "
      actions={<ComplianceReportListActions />}
    >
    <Datagrid rowClick="show" size="small">
      <TextField source="id" />
      <FrameworkField source="framework" label="Framework" />
      <TextField source="reportType" />
      <ImplementationField source="implementationPercentage" label="Implementation" sortable={false} />
      <NumberField source="totalControls" label="Controls" />
      <NumberField source="failedControls" label="Gaps" />
      <StatusField source="status" label="Status" />
      <DateField source="createdDate" showTime label="Generated" />
      <DownloadReportButton />
    </Datagrid>
    </List>
  </Box>
);

export const ComplianceReportShow = () => (
  <Show title="Compliance Report Details">
    <SimpleShowLayout>
      <TextField source="id" />
      <FrameworkField source="framework" label="Framework" />
      <TextField source="reportType" />
      <ImplementationField source="implementationPercentage" label="Implementation Progress" />
      <NumberField source="controlCount" label="Total Controls" />
      <NumberField source="implementedControls" label="Implemented Controls" />
      <NumberField source="gapCount" label="Gap Count" />
      <NumberField source="riskScore" label="Risk Score" options={{ minimumFractionDigits: 1, maximumFractionDigits: 1 }} />
      <StatusField source="status" label="Status" />
      <TextField source="version" />
      <DateField source="generated" showTime />
      <DateField source="validUntil" showTime />
      <TextField source="generatedBy" />
      <Box component="div" sx={{ mt: 2, mb: 2 }}>
        <Typography variant="h6">Executive Summary</Typography>
        <TextField source="summary" component="div" sx={{ mt: 1 }} />
      </Box>
      <Box component="div" sx={{ mt: 2, mb: 2 }}>
        <Typography variant="h6">Key Findings</Typography>
        <TextField source="keyFindings" component="div" sx={{ mt: 1 }} />
      </Box>
      <Box component="div" sx={{ mt: 2, mb: 2 }}>
        <Typography variant="h6">Recommendations</Typography>
        <TextField source="recommendations" component="div" sx={{ mt: 1 }} />
      </Box>
      <Box component="div" sx={{ mt: 2, mb: 2 }}>
        <Typography variant="h6">Next Review</Typography>
        <DateField source="nextReview" showTime />
      </Box>
    </SimpleShowLayout>
  </Show>
);

export const ComplianceReportCreate = () => (
  <Create title="Generate Compliance Report">
    <SimpleForm>
      <SelectInput 
        source="framework" 
        validate={required()}
        choices={[
          { id: 'HIPAA', name: 'HIPAA - Health Insurance Portability and Accountability Act' },
          { id: 'SOX', name: 'SOX - Sarbanes-Oxley Act' },
          { id: 'PCI DSS', name: 'PCI DSS - Payment Card Industry Data Security Standard' },
          { id: 'ISO 27001', name: 'ISO 27001 - Information Security Management' },
          { id: 'SOC2', name: 'SOC2 - System and Organization Controls 2' },
        ]}
        sx={{ width: '100%' }}
      />
      <SelectInput 
        source="reportType" 
        validate={required()}
        choices={[
          { id: 'assessment', name: 'Assessment Report - Current state analysis' },
          { id: 'gap_analysis', name: 'Gap Analysis - Identify compliance gaps' },
          { id: 'implementation', name: 'Implementation Plan - Step-by-step guide' },
          { id: 'audit_ready', name: 'Audit Readiness - Pre-audit preparation' },
          { id: 'continuous', name: 'Continuous Monitoring - Ongoing compliance' },
        ]}
        sx={{ width: '100%' }}
      />
      <SelectInput 
        source="scope" 
        label="Assessment Scope"
        choices={[
          { id: 'full', name: 'Full Organization Assessment' },
          { id: 'department', name: 'Department-specific Assessment' },
          { id: 'system', name: 'System-specific Assessment' },
          { id: 'gap_only', name: 'Gap Analysis Only' },
        ]}
        defaultValue="full"
      />
      <SelectInput 
        source="priority" 
        label="Priority Level"
        choices={[
          { id: 'high', name: 'High - Generate immediately' },
          { id: 'medium', name: 'Medium - Generate within 24 hours' },
          { id: 'low', name: 'Low - Generate within 1 week' },
        ]}
        defaultValue="medium"
      />
      <TextInput 
        source="notes" 
        multiline
        rows={3}
        label="Additional Notes"
        helperText="Any specific requirements or focus areas for this report"
        sx={{ width: '100%' }}
      />
    </SimpleForm>
  </Create>
);