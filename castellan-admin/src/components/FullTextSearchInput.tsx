import React, { useState, useEffect } from 'react';
import {
  TextField,
  InputAdornment,
  IconButton,
  FormControlLabel,
  Switch,
  Box,
  Tooltip,
  Chip,
  Typography
} from '@mui/material';
import {
  Search as SearchIcon,
  Clear as ClearIcon,
  Info as InfoIcon
} from '@mui/icons-material';

export interface FullTextSearchInputProps {
  value: string;
  onChange: (value: string) => void;
  exactMatch: boolean;
  onExactMatchChange: (checked: boolean) => void;
  placeholder?: string;
  disabled?: boolean;
}

export const FullTextSearchInput: React.FC<FullTextSearchInputProps> = ({
  value,
  onChange,
  exactMatch,
  onExactMatchChange,
  placeholder = "Enter search terms...",
  disabled = false
}) => {
  const [localValue, setLocalValue] = useState(value);

  useEffect(() => {
    setLocalValue(value);
  }, [value]);

  const handleInputChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const newValue = event.target.value;
    setLocalValue(newValue);
    onChange(newValue);
  };

  const handleClear = () => {
    setLocalValue('');
    onChange('');
  };

  const getSearchTips = () => [
    exactMatch 
      ? "Exact match: Search terms must appear exactly as typed"
      : "Fuzzy search: Matches partial words and similar spellings",
    "Use quotes for exact phrases: \"authentication failure\"",
    "Use AND/OR for multiple terms: malware AND process",
    "Use wildcards: auth* matches authentication, authorize, etc."
  ];

  return (
    <Box>
      <TextField
        fullWidth
        value={localValue}
        onChange={handleInputChange}
        placeholder={placeholder}
        disabled={disabled}
        variant="outlined"
        size="small"
        InputProps={{
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon color="action" />
            </InputAdornment>
          ),
          endAdornment: localValue && (
            <InputAdornment position="end">
              <IconButton
                size="small"
                onClick={handleClear}
                edge="end"
                disabled={disabled}
              >
                <ClearIcon />
              </IconButton>
            </InputAdornment>
          )
        }}
        sx={{
          '& .MuiOutlinedInput-root': {
            backgroundColor: 'background.paper'
          }
        }}
      />

      <Box sx={{ mt: 1, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <FormControlLabel
          control={
            <Switch
              checked={exactMatch}
              onChange={(e) => onExactMatchChange(e.target.checked)}
              size="small"
              disabled={disabled}
            />
          }
          label="Exact match"
          sx={{ mr: 1 }}
        />

        <Tooltip
          title={
            <Box>
              <Typography variant="body2" sx={{ fontWeight: 'bold', mb: 1 }}>
                Search Tips:
              </Typography>
              {getSearchTips().map((tip, index) => (
                <Typography key={index} variant="body2" sx={{ mb: 0.5 }}>
                  • {tip}
                </Typography>
              ))}
            </Box>
          }
          arrow
          placement="top"
        >
          <IconButton size="small">
            <InfoIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Box>

      {/* Search Mode Indicator */}
      <Box sx={{ mt: 1 }}>
        <Chip
          size="small"
          label={exactMatch ? "Exact Match Mode" : "Fuzzy Search Mode"}
          color={exactMatch ? "warning" : "info"}
          variant="outlined"
          sx={{ fontSize: '0.75rem' }}
        />
      </Box>
    </Box>
  );
};
