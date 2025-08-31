import React from 'react';
import { usePermissions } from 'react-admin';
import { 
  Menu as ReactAdminMenu, 
  MenuProps,
  MenuItemLink,
  MenuItemLinkProps,
  useResourceDefinitions 
} from 'react-admin';
import { 
  Dashboard as DashboardIcon,
  Security as SecurityIcon,
  Assessment as ComplianceIcon,
  Computer as SystemIcon,
  Settings as SettingsIcon,
  People as UsersIcon,
  Report as ReportsIcon,
  Analytics as AnalyticsIcon
} from '@mui/icons-material';

interface ProtectedMenuItemProps extends Omit<MenuItemLinkProps, 'to'> {
  to: string;
  requiredPermissions?: string[];
  requiredRoles?: string[];
  primaryText: string;
  leftIcon?: React.ReactElement;
}

// Protected Menu Item Component
export const ProtectedMenuItem: React.FC<ProtectedMenuItemProps> = ({
  to,
  requiredPermissions = [],
  requiredRoles = [],
  primaryText,
  leftIcon,
  ...props
}) => {
  const { permissions } = usePermissions();

  // Check if user has required permissions
  const hasRequiredPermissions = requiredPermissions.length === 0 || 
    requiredPermissions.every(permission => permissions?.includes(permission));

  // Check if user has required roles
  const hasRequiredRoles = requiredRoles.length === 0 || 
    requiredRoles.some(role => permissions?.roles?.includes(role));

  // Don't render if user doesn't have access
  if (!hasRequiredPermissions || !hasRequiredRoles) {
    return null;
  }

  return (
    <MenuItemLink
      to={to}
      primaryText={primaryText}
      leftIcon={leftIcon}
      {...props}
    />
  );
};

// Main Protected Menu Component
export const ProtectedMenu: React.FC<MenuProps> = (props) => {
  const { permissions } = usePermissions();
  const resources = useResourceDefinitions();

  // Helper function to check if user can access a resource
  const canAccessResource = (resourceName: string) => {
    const resource = resources[resourceName];
    if (!resource) return false;

    // Check if resource has access control requirements
    const requiredPermissions = resource.options?.requiredPermissions || [];
    const requiredRoles = resource.options?.requiredRoles || [];

    if (requiredPermissions.length === 0 && requiredRoles.length === 0) {
      return true; // No restrictions
    }

    // Check permissions
    const hasPermissions = requiredPermissions.length === 0 || 
      requiredPermissions.every((permission: string) => permissions?.includes(permission));

    // Check roles
    const hasRoles = requiredRoles.length === 0 || 
      requiredRoles.some((role: string) => permissions?.roles?.includes(role));

    return hasPermissions && hasRoles;
  };

  return (
    <ReactAdminMenu {...props}>
      {/* Always show Dashboard */}
      <MenuItemLink
        to="/dashboard"
        primaryText="Dashboard"
        leftIcon={<DashboardIcon />}
      />

      {/* Security Events - requires security permissions */}
      <ProtectedMenuItem
        to="/security-events"
        primaryText="Security Events"
        leftIcon={<SecurityIcon />}
        requiredPermissions={['security.read']}
      />

      {/* Compliance Reports - requires compliance permissions */}
      <ProtectedMenuItem
        to="/compliance-reports"
        primaryText="Compliance Reports"
        leftIcon={<ComplianceIcon />}
        requiredPermissions={['compliance.read']}
      />

      {/* System Status - requires system monitoring permissions */}
      <ProtectedMenuItem
        to="/system-status"
        primaryText="System Status"
        leftIcon={<SystemIcon />}
        requiredPermissions={['system.read']}
      />

      {/* Analytics - requires analytics permissions */}
      <ProtectedMenuItem
        to="/analytics"
        primaryText="Analytics"
        leftIcon={<AnalyticsIcon />}
        requiredPermissions={['analytics.read']}
      />

      {/* Reports - requires reporting permissions */}
      <ProtectedMenuItem
        to="/reports"
        primaryText="Reports"
        leftIcon={<ReportsIcon />}
        requiredPermissions={['reports.read']}
      />

      {/* User Management - requires admin role */}
      <ProtectedMenuItem
        to="/users"
        primaryText="Users"
        leftIcon={<UsersIcon />}
        requiredRoles={['admin', 'user_manager']}
      />

      {/* Settings - requires admin role */}
      <ProtectedMenuItem
        to="/settings"
        primaryText="Settings"
        leftIcon={<SettingsIcon />}
        requiredRoles={['admin']}
      />

      {/* Dynamically render protected resources */}
      {Object.keys(resources).map(resourceName => {
        const resource = resources[resourceName];
        
        // Skip if already handled above
        const skipResources = ['security-events', 'compliance-reports', 'system-status', 'users', 'settings'];
        if (skipResources.includes(resourceName)) {
          return null;
        }

        // Check if user can access this resource
        if (!canAccessResource(resourceName)) {
          return null;
        }

        return (
          <MenuItemLink
            key={resourceName}
            to={`/${resourceName}`}
            primaryText={resource.options?.label || resourceName}
            leftIcon={resource.icon}
          />
        );
      })}
    </ReactAdminMenu>
  );
};

// Enhanced Menu with Role-based Sections
export const RoleBasedMenu: React.FC<MenuProps> = (props) => {
  const { permissions } = usePermissions();

  const isAdmin = permissions?.roles?.includes('admin');
  const isSecurity = permissions?.roles?.includes('security_analyst');
  const isCompliance = permissions?.roles?.includes('compliance_officer');
  const isViewer = permissions?.roles?.includes('viewer');

  return (
    <ReactAdminMenu {...props}>
      {/* Core Navigation - Available to all users */}
      <MenuItemLink
        to="/dashboard"
        primaryText="Dashboard"
        leftIcon={<DashboardIcon />}
      />

      {/* Security Section - Available to security analysts and admins */}
      {(isSecurity || isAdmin) && (
        <>
          <ProtectedMenuItem
            to="/security-events"
            primaryText="Security Events"
            leftIcon={<SecurityIcon />}
            requiredRoles={['security_analyst', 'admin']}
          />
          <ProtectedMenuItem
            to="/analytics"
            primaryText="Security Analytics"
            leftIcon={<AnalyticsIcon />}
            requiredRoles={['security_analyst', 'admin']}
          />
        </>
      )}

      {/* Compliance Section - Available to compliance officers and admins */}
      {(isCompliance || isAdmin) && (
        <>
          <ProtectedMenuItem
            to="/compliance-reports"
            primaryText="Compliance Reports"
            leftIcon={<ComplianceIcon />}
            requiredRoles={['compliance_officer', 'admin']}
          />
          <ProtectedMenuItem
            to="/reports"
            primaryText="Custom Reports"
            leftIcon={<ReportsIcon />}
            requiredRoles={['compliance_officer', 'admin']}
          />
        </>
      )}

      {/* System Section - Available to all authenticated users */}
      <ProtectedMenuItem
        to="/system-status"
        primaryText="System Status"
        leftIcon={<SystemIcon />}
        requiredRoles={['viewer', 'security_analyst', 'compliance_officer', 'admin']}
      />

      {/* Admin Section - Available only to admins */}
      {isAdmin && (
        <>
          <ProtectedMenuItem
            to="/users"
            primaryText="User Management"
            leftIcon={<UsersIcon />}
            requiredRoles={['admin']}
          />
          <ProtectedMenuItem
            to="/settings"
            primaryText="System Settings"
            leftIcon={<SettingsIcon />}
            requiredRoles={['admin']}
          />
        </>
      )}
    </ReactAdminMenu>
  );
};