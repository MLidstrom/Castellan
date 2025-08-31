import React from 'react';
import { Layout as RaLayout, AppBar } from 'react-admin';
import { Typography, Box, Chip } from '@mui/material';
import { Security } from '@mui/icons-material';

const CustomAppBar = () => (
  <AppBar
    sx={{
      '& .RaUserMenu-userButton': {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        maxWidth: '300px'
      }
    }}
  >
    <Box sx={{ 
      display: 'flex', 
      alignItems: 'center', 
      width: '100%',
      px: 2,
      py: 1
    }}>
      <Security sx={{ mr: 1 }} />
      <Typography variant="h6" component="div" sx={{ mr: 2 }}>
        Castellan
      </Typography>
      <Chip 
        label="Free Edition"
        size="small"
        sx={{
          backgroundColor: 'rgba(255, 255, 255, 0.9)',
          color: '#1976d2',
          fontSize: '0.75rem',
          fontWeight: 'bold'
        }}
      />
    </Box>
  </AppBar>
);

export const Layout = (props: any) => (
  <RaLayout {...props} appBar={CustomAppBar} />
);
