import React from 'react';
import { Resource } from 'react-admin';
import { usePermissions } from 'react-admin';
import { Alert, Box, Typography } from '@mui/material';
import { Lock as LockIcon } from '@mui/icons-material';

interface ProtectedResourceProps {
  name: string;
  requiredPermissions?: string[];
  requiredRoles?: string[];
  fallback?: React.ReactNode;
  [key: string]: any; // For other Resource props
}

export const ProtectedResource: React.FC<ProtectedResourceProps> = ({ 
  name, 
  requiredPermissions = [], 
  requiredRoles = [],
  fallback,
  ...resourceProps 
}) => {
  const { permissions, isLoading } = usePermissions();

  if (isLoading) {
    return null;
  }

  // Check if user has required permissions
  const hasRequiredPermissions = requiredPermissions.length === 0 || 
    requiredPermissions.every(permission => permissions?.includes(permission));

  // Check if user has required roles
  const hasRequiredRoles = requiredRoles.length === 0 || 
    requiredRoles.some(role => permissions?.roles?.includes(role));

  // If user doesn't have access, show fallback or nothing
  if (!hasRequiredPermissions || !hasRequiredRoles) {
    if (fallback) {
      return <>{fallback}</>;
    }
    return null;
  }

  // User has access, render the resource
  return <Resource name={name} {...resourceProps} />;
};

// Component for protecting individual UI elements
interface ProtectedComponentProps {
  permissions?: string[];
  roles?: string[];
  children: React.ReactNode;
  fallback?: React.ReactNode;
  showAccessDenied?: boolean;
}

export const ProtectedComponent: React.FC<ProtectedComponentProps> = ({
  permissions = [],
  roles = [],
  children,
  fallback,
  showAccessDenied = false
}) => {
  const { permissions: userPermissions } = usePermissions();

  const hasAccess = (
    (permissions.length === 0 || permissions.every(p => userPermissions?.includes(p))) &&
    (roles.length === 0 || roles.some(r => userPermissions?.roles?.includes(r)))
  );

  if (!hasAccess) {
    if (fallback) {
      return <>{fallback}</>;
    }
    
    if (showAccessDenied) {
      return (
        <Box sx={{ p: 2 }}>
          <Alert severity="warning" icon={<LockIcon />}>
            <Typography variant="body1">
              Access Denied
            </Typography>
            <Typography variant="body2">
              You don't have permission to view this content.
            </Typography>
          </Alert>
        </Box>
      );
    }
    
    return null;
  }

  return <>{children}</>;
};

// Higher-order component for protecting entire components
export const withPermissions = <P extends object>(
  Component: React.ComponentType<P>,
  requiredPermissions: string[] = [],
  requiredRoles: string[] = []
) => {
  return (props: P) => (
    <ProtectedComponent permissions={requiredPermissions} roles={requiredRoles} showAccessDenied>
      <Component {...props} />
    </ProtectedComponent>
  );
};