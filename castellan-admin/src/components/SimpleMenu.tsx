import React from 'react';
import { Menu } from 'react-admin';
import {
  Dashboard as DashboardIcon,
  Security as SecurityIcon,
} from '@mui/icons-material';

export const SimpleMenu = () => (
  <Menu>
    <Menu.DashboardItem />
    <Menu.Item to="/security-events" primaryText="Security Events" leftIcon={<SecurityIcon />} />
  </Menu>
);