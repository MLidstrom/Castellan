import React from 'react';
import {
  Box,
  Typography,
  Slider,
  TextField,
  Grid,
  Button
} from '@mui/material';
import {
  RestartAlt as ResetIcon
} from '@mui/icons-material';

export interface RangeSliderFilterProps {
  label: string;
  min: number;
  max: number;
  value: [number, number];
  onChange: (values: [number, number]) => void;
  step?: number;
  unit?: string;
  disabled?: boolean;
  marks?: boolean;
}

export const RangeSliderFilter: React.FC<RangeSliderFilterProps> = ({
  label,
  min,
  max,
  value,
  onChange,
  step = 1,
  unit = '',
  disabled = false,
  marks = false
}) => {
  const handleSliderChange = (_event: Event, newValue: number | number[]) => {
    const values = Array.isArray(newValue) ? newValue : [newValue, newValue];
    onChange([values[0], values[1]]);
  };

  const handleMinChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const newMin = parseFloat(event.target.value);
    if (!isNaN(newMin) && newMin <= value[1]) {
      onChange([newMin, value[1]]);
    }
  };

  const handleMaxChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const newMax = parseFloat(event.target.value);
    if (!isNaN(newMax) && newMax >= value[0]) {
      onChange([value[0], newMax]);
    }
  };

  const handleReset = () => {
    onChange([min, max]);
  };

  const isDefault = value[0] === min && value[1] === max;

  const formatValue = (val: number): string => {
    if (unit === '%') {
      return `${Math.round(val)}${unit}`;
    }
    return step < 1 ? val.toFixed(2) + unit : Math.round(val) + unit;
  };

  const getSliderMarks = () => {
    if (!marks) return false;
    
    const markCount = 5;
    const stepSize = (max - min) / (markCount - 1);
    const sliderMarks = [];
    
    for (let i = 0; i < markCount; i++) {
      const markValue = min + (stepSize * i);
      sliderMarks.push({
        value: markValue,
        label: formatValue(markValue)
      });
    }
    
    return sliderMarks;
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="body2" sx={{ fontWeight: 'medium' }}>
          {label}
        </Typography>
        {!isDefault && (
          <Button
            size="small"
            startIcon={<ResetIcon />}
            onClick={handleReset}
            disabled={disabled}
            sx={{ fontSize: '0.75rem' }}
          >
            Reset
          </Button>
        )}
      </Box>

      <Box sx={{ px: 1, mb: 2 }}>
        <Slider
          value={value}
          onChange={handleSliderChange}
          min={min}
          max={max}
          step={step}
          disabled={disabled}
          marks={getSliderMarks()}
          valueLabelDisplay="auto"
          valueLabelFormat={formatValue}
          sx={{
            '& .MuiSlider-thumb': {
              width: 20,
              height: 20,
            },
            '& .MuiSlider-valueLabel': {
              fontSize: '0.75rem',
            },
            '& .MuiSlider-markLabel': {
              fontSize: '0.65rem',
            }
          }}
        />
      </Box>

      {/* Manual Input Fields */}
      <Grid container spacing={1}>
        <Grid item xs={6}>
          <TextField
            fullWidth
            label="Min"
            type="number"
            value={value[0]}
            onChange={handleMinChange}
            disabled={disabled}
            size="small"
            inputProps={{
              min: min,
              max: value[1],
              step: step
            }}
            InputProps={{
              endAdornment: unit ? (
                <Typography variant="body2" color="text.secondary">
                  {unit}
                </Typography>
              ) : null
            }}
            sx={{
              '& .MuiOutlinedInput-root': {
                backgroundColor: 'background.paper'
              }
            }}
          />
        </Grid>
        <Grid item xs={6}>
          <TextField
            fullWidth
            label="Max"
            type="number"
            value={value[1]}
            onChange={handleMaxChange}
            disabled={disabled}
            size="small"
            inputProps={{
              min: value[0],
              max: max,
              step: step
            }}
            InputProps={{
              endAdornment: unit ? (
                <Typography variant="body2" color="text.secondary">
                  {unit}
                </Typography>
              ) : null
            }}
            sx={{
              '& .MuiOutlinedInput-root': {
                backgroundColor: 'background.paper'
              }
            }}
          />
        </Grid>
      </Grid>

      {/* Range Summary */}
      <Box sx={{ mt: 1, p: 1, bgcolor: 'action.hover', borderRadius: 1 }}>
        <Typography variant="body2" color="text.secondary" align="center">
          {formatValue(value[0])} - {formatValue(value[1])}
          {!isDefault && (
            <Typography component="span" variant="body2" color="primary" sx={{ ml: 1 }}>
              (filtered)
            </Typography>
          )}
        </Typography>
      </Box>
    </Box>
  );
};
