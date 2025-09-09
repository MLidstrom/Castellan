import React from 'react';
import { Card, CardContent, Typography, Box, Chip } from '@mui/material';

interface ApiDiagnosticProps {
  securityEventsData: any;
  complianceReportsData: any;
  systemStatusData: any;
  threatScannerData: any;
  securityEventsError?: string | null;
  complianceReportsError?: string | null;
  systemStatusError?: string | null;
  threatScannerError?: string | null;
}

export const ApiDiagnostic: React.FC<ApiDiagnosticProps> = ({
  securityEventsData,
  complianceReportsData,
  systemStatusData,
  threatScannerData,
  securityEventsError,
  complianceReportsError,
  systemStatusError,
  threatScannerError
}) => {
  const getStatusColor = (data: any, error: string | null | undefined) => {
    if (error) return 'error';
    if (data && Array.isArray(data) && data.length > 0) return 'success';
    if (data && Array.isArray(data) && data.length === 0) return 'warning';
    return 'default';
  };

  const getStatusText = (data: any, error: string | null | undefined) => {
    if (error) return `Error: ${error}`;
    if (data && Array.isArray(data)) return `${data.length} items`;
    if (data) return 'Data loaded';
    return 'No data';
  };

  return (
    <Card sx={{ mb: 3, bgcolor: 'rgba(0,0,0,0.02)' }}>
      <CardContent>
        <Typography variant="h6" sx={{ mb: 2 }}>🔍 API Diagnostic</Typography>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 2 }}>
          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>Security Events API</Typography>
            <Chip 
              label={getStatusText(securityEventsData, securityEventsError)}
              color={getStatusColor(securityEventsData, securityEventsError)}
              size="small"
            />
          </Box>
          
          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>Compliance Reports API</Typography>
            <Chip 
              label={getStatusText(complianceReportsData, complianceReportsError)}
              color={getStatusColor(complianceReportsData, complianceReportsError)}
              size="small"
            />
          </Box>
          
          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>System Status API</Typography>
            <Chip 
              label={getStatusText(systemStatusData, systemStatusError)}
              color={getStatusColor(systemStatusData, systemStatusError)}
              size="small"
            />
          </Box>
          
          <Box>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>Threat Scanner API</Typography>
            <Chip 
              label={getStatusText(threatScannerData, threatScannerError)}
              color={getStatusColor(threatScannerData, threatScannerError)}
              size="small"
            />
          </Box>
        </Box>
        
        <Typography variant="caption" color="textSecondary" sx={{ mt: 2, display: 'block' }}>
          Check browser console (F12) for detailed API logs
        </Typography>
      </CardContent>
    </Card>
  );
};
