import React from 'react';
import { Layout as RaLayout, AppBar } from 'react-admin';
import { Typography, Box } from '@mui/material';
import { Security } from '@mui/icons-material';
import { MenuWithPreloading } from './MenuWithPreloading';

const CustomAppBar = () => (
  <AppBar
    sx={{
      backgroundColor: (theme) => theme.palette.mode === 'light' ? '#1976d2' : theme.palette.primary.main,
      '& .RaUserMenu-userButton': {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        maxWidth: '300px'
      },
      '& .RaLoadingIndicator-loader': {
        display: 'none !important'
      },
      '& .RaLoadingIndicator-root': {
        display: 'none !important'
      },
      '& [class*="RaLoadingIndicator"]': {
        display: 'none !important'
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
