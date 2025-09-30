import React from 'react';
import { 
  Card, 
  CardContent, 
  CardHeader, 
  Typography, 
  Box
} from '@mui/material';

export const ThreatIntelligenceHealthDashboard: React.FC = React.memo(() => {
  return (
    <Card sx={{ width: '100%' }}>
      <CardHeader title="Threat Intelligence Health" />
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: 200 }}>
          <Typography color="text.secondary">
            Threat intelligence health dashboard will be displayed here (caching removed for development)
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
});

export default ThreatIntelligenceHealthDashboard;
