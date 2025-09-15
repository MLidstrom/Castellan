import React from 'react';
import { Box, CircularProgress, Typography, Alert, Button } from '@mui/material';
import { Lock as LockIcon, Home as HomeIcon } from '@mui/icons-material';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

interface ProtectedRouteProps {
  children: React.ReactNode;
  requiredPermissions?: string[];
  requiredRoles?: string[];
  redirectTo?: string;
  showAccessDenied?: boolean;
  fallback?: React.ReactNode;
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
  children,
  requiredPermissions = [],
  requiredRoles = [],
  redirectTo = '/login',
  showAccessDenied = true,
  fallback
}) => {
  const { isAuthenticated, isLoading, user } = useAuth();
  const location = useLocation();

  // Show loading state while permissions are being fetched
  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
        <CircularProgress />
        <Typography sx={{ ml: 2 }}>Loading...</Typography>
      </Box>
    );
  }

  // Check if user is authenticated
  if (!isAuthenticated) {
    return <Navigate to={redirectTo} replace state={{ from: location }} />;
  }

  // Check if user has required permissions
  const hasRequiredPermissions = requiredPermissions.length === 0 || 
    requiredPermissions.every(permission => user?.permissions?.includes(permission));

  // Check if user has required roles
  const hasRequiredRoles = requiredRoles.length === 0 || 
    requiredRoles.some(role => user?.roles?.includes(role));

  // If user doesn't have access
  if (!hasRequiredPermissions || !hasRequiredRoles) {
    // Use custom fallback if provided
    if (fallback) {
      return <>{fallback}</>;
    }

    // Show access denied message
    if (showAccessDenied) {
      return (
        <Box sx={{ 
          display: 'flex', 
          flexDirection: 'column', 
          alignItems: 'center', 
          justifyContent: 'center',
          minHeight: '60vh',
          px: 3
        }}>
          <LockIcon sx={{ fontSize: 64, color: 'warning.main', mb: 2 }} />
          
          <Typography variant="h4" gutterBottom color="text.primary">
            Access Restricted
          </Typography>
          
          <Typography variant="body1" color="text.secondary" paragraph align="center" sx={{ maxWidth: 600 }}>
            You don't have the required permissions to access this page. 
            Please contact your system administrator if you believe this is an error.
          </Typography>

          <Alert severity="warning" sx={{ mt: 2, mb: 3, maxWidth: 600 }}>
            <Typography variant="body2">
              <strong>Required Access:</strong>
            </Typography>
            {requiredPermissions.length > 0 && (
              <Typography variant="body2">
                • Permissions: {requiredPermissions.join(', ')}
              </Typography>
            )}
            {requiredRoles.length > 0 && (
              <Typography variant="body2">
                • Roles: {requiredRoles.join(', ')}
              </Typography>
            )}
            <Typography variant="body2" sx={{ mt: 1 }}>
              • Current path: {location.pathname}
            </Typography>
          </Alert>

          <Box sx={{ display: 'flex', gap: 2 }}>
            <Button
              variant="contained"
              startIcon={<HomeIcon />}
              onClick={() => window.history.back()}
            >
              Go Back
            </Button>
            <Button
              variant="outlined"
              onClick={() => (window.location.href = redirectTo)}
            >
              Go to Dashboard
            </Button>
          </Box>
        </Box>
      );
    }

    // Redirect to specified route
    return <Navigate to={redirectTo} replace state={{ from: location }} />;
  }

  // User has access, render children
  return <>{children}</>;
};

// Higher-order component for route protection
export const withRouteProtection = <P extends object>(
  Component: React.ComponentType<P>,
  requiredPermissions: string[] = [],
  requiredRoles: string[] = [],
  options?: {
    redirectTo?: string;
    showAccessDenied?: boolean;
  }
) => {
  return (props: P) => (
    <ProtectedRoute 
      requiredPermissions={requiredPermissions} 
      requiredRoles={requiredRoles}
      {...options}
    >
      <Component {...props} />
    </ProtectedRoute>
  );
};