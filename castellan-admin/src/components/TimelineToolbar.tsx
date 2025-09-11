import React from 'react';
import { Box, Stack, TextField, MenuItem, Button } from '@mui/material';

export type Granularity = 'minute' | 'hour' | 'day' | 'week' | 'month';

export interface TimelineToolbarProps {
  granularity: Granularity;
  setGranularity: (g: Granularity) => void;
  from: string;
  to: string;
  setFrom: (v: string) => void;
  setTo: (v: string) => void;
  onRefresh: () => void;
}

const granularityOptions: { label: string; value: Granularity }[] = [
  { label: 'Hour', value: 'hour' },
  { label: 'Day', value: 'day' },
  { label: 'Week', value: 'week' },
  { label: 'Month', value: 'month' },
];

export const TimelineToolbar: React.FC<TimelineToolbarProps> = ({
  granularity,
  setGranularity,
  from,
  to,
  setFrom,
  setTo,
  onRefresh,
}) => {
  return (
    <Box sx={{ p: 2, borderBottom: '1px solid', borderColor: 'divider' }}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems="center">
        <TextField
          select
          label="Granularity"
          size="small"
          value={granularity}
          onChange={(e) => setGranularity(e.target.value as Granularity)}
          sx={{ minWidth: 160 }}
        >
          {granularityOptions.map((opt) => (
            <MenuItem key={opt.value} value={opt.value}>
              {opt.label}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          label="From"
          type="datetime-local"
          size="small"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
          InputLabelProps={{ shrink: true }}
        />

        <TextField
          label="To"
          type="datetime-local"
          size="small"
          value={to}
          onChange={(e) => setTo(e.target.value)}
          InputLabelProps={{ shrink: true }}
        />

        <Button variant="contained" onClick={onRefresh}>
          Refresh
        </Button>
      </Stack>
    </Box>
  );
};
