import React from 'react';
import {
  Box,
  TextField,
  Grid,
  Button,
  Typography
} from '@mui/material';
import {
  Clear as ClearIcon
} from '@mui/icons-material';

export interface DateRangePickerProps {
  startDate?: Date;
  endDate?: Date;
  onStartDateChange: (date: Date | undefined) => void;
  onEndDateChange: (date: Date | undefined) => void;
  disabled?: boolean;
}

export const DateRangePicker: React.FC<DateRangePickerProps> = ({
  startDate,
  endDate,
  onStartDateChange,
  onEndDateChange,
  disabled = false
}) => {
  const formatDateForInput = (date?: Date): string => {
    if (!date) return '';
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  };

  const parseInputDate = (value: string): Date | undefined => {
    if (!value) return undefined;
    const date = new Date(value);
    return isNaN(date.getTime()) ? undefined : date;
  };

  const handleStartDateChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const date = parseInputDate(event.target.value);
    onStartDateChange(date);
  };

  const handleEndDateChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const date = parseInputDate(event.target.value);
    onEndDateChange(date);
  };

  const handleClearDates = () => {
    onStartDateChange(undefined);
    onEndDateChange(undefined);
  };

  const setQuickRange = (hours: number) => {
    const end = new Date();
    const start = new Date();
    start.setHours(end.getHours() - hours);
    onStartDateChange(start);
    onEndDateChange(end);
  };

  const quickRangeOptions = [
    { label: 'Last 1h', hours: 1 },
    { label: 'Last 6h', hours: 6 },
    { label: 'Last 12h', hours: 12 },
    { label: 'Last 24h', hours: 24 }
  ];

  const formatDisplayDate = (date?: Date): string => {
    if (!date) return 'Not set';
    return date.toLocaleString();
  };

  return (
    <Box>
      <Grid container spacing={2}>
        <Grid item xs={12} sm={6}>
          <TextField
            fullWidth
            label="Start Date"
            type="datetime-local"
            value={formatDateForInput(startDate)}
            onChange={handleStartDateChange}
            disabled={disabled}
            size="small"
            InputLabelProps={{
              shrink: true,
            }}
            sx={{
              '& .MuiOutlinedInput-root': {
                backgroundColor: 'background.paper'
              }
            }}
          />
        </Grid>
        <Grid item xs={12} sm={6}>
          <TextField
            fullWidth
            label="End Date"
            type="datetime-local"
            value={formatDateForInput(endDate)}
            onChange={handleEndDateChange}
            disabled={disabled}
            size="small"
            InputLabelProps={{
              shrink: true,
            }}
            sx={{
              '& .MuiOutlinedInput-root': {
                backgroundColor: 'background.paper'
              }
            }}
          />
        </Grid>
      </Grid>

      {/* Quick Range Buttons */}
      <Box sx={{ mt: 2 }}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Quick ranges:
        </Typography>
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
          {quickRangeOptions.map((option) => (
            <Button
              key={option.label}
              size="small"
              variant="outlined"
              onClick={() => setQuickRange(option.hours)}
              disabled={disabled}
              sx={{ fontSize: '0.75rem' }}
            >
              {option.label}
            </Button>
          ))}
          {(startDate || endDate) && (
            <Button
              size="small"
              variant="outlined"
              startIcon={<ClearIcon />}
              onClick={handleClearDates}
              disabled={disabled}
              color="error"
              sx={{ fontSize: '0.75rem' }}
            >
              Clear
            </Button>
          )}
        </Box>
      </Box>

      {/* Date Range Summary */}
      {(startDate || endDate) && (
        <Box sx={{ mt: 2, p: 1, bgcolor: 'action.hover', borderRadius: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Selected range:
          </Typography>
          <Typography variant="body2">
            <strong>From:</strong> {formatDisplayDate(startDate)}
          </Typography>
          <Typography variant="body2">
            <strong>To:</strong> {formatDisplayDate(endDate)}
          </Typography>
          {startDate && endDate && (
            <Typography variant="body2" color="primary">
              Duration: {Math.ceil((endDate.getTime() - startDate.getTime()) / (1000 * 60 * 60 * 24))} days
            </Typography>
          )}
        </Box>
      )}
    </Box>
  );
};
