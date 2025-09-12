import React, { useState } from 'react';
import {
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Checkbox,
  ListItemText,
  Chip,
  Box,
  OutlinedInput,
  SelectChangeEvent,
  Typography
} from '@mui/material';
import {
  Clear as ClearIcon
} from '@mui/icons-material';

export interface MultiSelectOption {
  id: string;
  name: string;
}

export interface MultiSelectFilterProps {
  label: string;
  options: MultiSelectOption[];
  selectedValues: string[];
  onChange: (values: string[]) => void;
  disabled?: boolean;
  colorMapping?: Record<string, string>;
  maxHeight?: number;
}

export const MultiSelectFilter: React.FC<MultiSelectFilterProps> = ({
  label,
  options,
  selectedValues,
  onChange,
  disabled = false,
  colorMapping,
  maxHeight = 200
}) => {
  const [open, setOpen] = useState(false);

  const handleChange = (event: SelectChangeEvent<typeof selectedValues>) => {
    const value = event.target.value;
    onChange(typeof value === 'string' ? value.split(',') : value);
  };

  const handleDelete = (valueToDelete: string) => {
    onChange(selectedValues.filter(value => value !== valueToDelete));
  };

  const getOptionName = (value: string): string => {
    const option = options.find(opt => opt.id === value);
    return option?.name || value;
  };

  const getChipColor = (value: string): string => {
    return colorMapping?.[value] || '#1976d2';
  };

  return (
    <FormControl fullWidth size="small">
      <InputLabel id={`${label.toLowerCase().replace(/\s+/g, '-')}-label`}>
        {label}
      </InputLabel>
      <Select
        labelId={`${label.toLowerCase().replace(/\s+/g, '-')}-label`}
        multiple
        value={selectedValues}
        onChange={handleChange}
        input={<OutlinedInput label={label} />}
        disabled={disabled}
        open={open}
        onOpen={() => setOpen(true)}
        onClose={() => setOpen(false)}
        renderValue={(selected) => (
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
            {selected.map((value) => (
              <Chip
                key={value}
                label={getOptionName(value)}
                size="small"
                onDelete={() => handleDelete(value)}
                onMouseDown={(event) => {
                  event.stopPropagation();
                }}
                sx={{
                  backgroundColor: colorMapping ? getChipColor(value) : undefined,
                  color: colorMapping ? '#fff' : undefined,
                  '& .MuiChip-deleteIcon': {
                    color: colorMapping ? 'rgba(255, 255, 255, 0.8)' : undefined
                  }
                }}
              />
            ))}
          </Box>
        )}
        MenuProps={{
          PaperProps: {
            style: {
              maxHeight: maxHeight,
              width: 250,
            },
          },
        }}
        sx={{
          '& .MuiOutlinedInput-root': {
            backgroundColor: 'background.paper'
          }
        }}
      >
        {options.length === 0 ? (
          <MenuItem disabled>
            <Typography variant="body2" color="text.secondary">
              No options available
            </Typography>
          </MenuItem>
        ) : (
          options.map((option) => (
            <MenuItem key={option.id} value={option.id}>
              <Checkbox checked={selectedValues.includes(option.id)} />
              <ListItemText 
                primary={option.name}
                sx={{
                  '& .MuiTypography-root': {
                    fontSize: '0.875rem'
                  }
                }}
              />
              {colorMapping && colorMapping[option.id] && (
                <Box
                  sx={{
                    width: 16,
                    height: 16,
                    borderRadius: '50%',
                    backgroundColor: colorMapping[option.id],
                    ml: 1,
                    border: '1px solid rgba(0, 0, 0, 0.1)'
                  }}
                />
              )}
            </MenuItem>
          ))
        )}
      </Select>
      
      {selectedValues.length > 0 && (
        <Box sx={{ mt: 1, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography variant="body2" color="text.secondary">
            {selectedValues.length} selected
          </Typography>
          <Chip
            label="Clear all"
            size="small"
            variant="outlined"
            color="error"
            deleteIcon={<ClearIcon />}
            onDelete={() => onChange([])}
            onClick={() => onChange([])}
            sx={{ fontSize: '0.75rem' }}
          />
        </Box>
      )}
    </FormControl>
  );
};
