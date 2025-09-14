import React from 'react';
import { 
  Card, 
  CardContent, 
  CardHeader, 
  Typography, 
  Box
} from '@mui/material';

export const PerformanceDashboard: React.FC = () => {
  return (
    <Card sx={{ width: '100%' }}>
      <CardHeader title="Performance Dashboard" />
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: 200 }}>
          <Typography color="text.secondary">
            Performance dashboard will be displayed here (caching removed for development)
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
};

export default PerformanceDashboard;
