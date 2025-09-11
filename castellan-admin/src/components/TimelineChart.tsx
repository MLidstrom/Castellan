import React from 'react';
import { Box, Typography } from '@mui/material';

// Placeholder chart component to render a simple textual visualization for now
// This avoids adding a new charting dependency. We can swap this with recharts or MUI X Charts later.

export interface TimelinePoint {
  timestamp: string;
  count: number;
}

export const TimelineChart: React.FC<{ data: TimelinePoint[]; loading?: boolean }>
= ({ data, loading }) => {
  if (loading) {
    return <Typography variant="body2" sx={{ p: 2 }}>Loading timeline...</Typography>;
  }

  if (!data || data.length === 0) {
    return <Typography variant="body2" sx={{ p: 2 }}>No data for selected range.</Typography>;
  }

  // Render a very simple bar-like visualization using flex boxes
  const max = Math.max(...data.map((d) => d.count));

  return (
    <Box sx={{ p: 2 }}>
      {data.map((d) => (
        <Box key={d.timestamp} sx={{ display: 'flex', alignItems: 'center', mb: 0.5 }}>
          <Typography variant="caption" sx={{ width: 140, mr: 1, color: 'text.secondary' }}>
            {new Date(d.timestamp).toLocaleString()}
          </Typography>
          <Box sx={{ flex: 1, height: 8, backgroundColor: 'action.hover', position: 'relative' }}>
            <Box
              sx={{
                position: 'absolute',
                left: 0,
                top: 0,
                bottom: 0,
                width: `${(d.count / (max || 1)) * 100}%`,
                backgroundColor: 'primary.main',
              }}
            />
          </Box>
          <Typography variant="caption" sx={{ width: 40, textAlign: 'right', ml: 1 }}>
            {d.count}
          </Typography>
        </Box>
      ))}
    </Box>
  );
};
