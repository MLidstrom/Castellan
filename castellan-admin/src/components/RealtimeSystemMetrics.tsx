import React from 'react';
import { 
  Card, 
  CardContent, 
  CardHeader, 
  Typography, 
  Box
} from '@mui/material';

export const RealtimeSystemMetrics: React.FC = () => {
  return (
    <Card sx={{ width: '100%' }}>
      <CardHeader title="System Metrics" />
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: 200 }}>
          <Typography color="text.secondary">
            System metrics will be displayed here (caching removed for development)
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
};

export default RealtimeSystemMetrics;
