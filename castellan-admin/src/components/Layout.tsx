import React from 'react';
import { Layout as RaLayout, AppBar } from 'react-admin';
import { Typography, Box } from '@mui/material';
import { Security } from '@mui/icons-material';
import { MenuWithPreloading } from './MenuWithPreloading';

const CustomAppBar = () => (
  <AppBar
    sx={{
      backgroundColor: '#1976d2',
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
    </Box>
  </AppBar>
);

export const Layout = (props: any) => (
  <RaLayout {...props} appBar={CustomAppBar} menu={MenuWithPreloading} />
);
