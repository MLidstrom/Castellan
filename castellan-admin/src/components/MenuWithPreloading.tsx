import React, { useCallback, useEffect } from 'react';
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
  Analytics as AnalyticsIcon,
  Gavel as MitreIcon,
  BugReport as YaraIcon,
  FindInPage as YaraMatchesIcon,
  Schedule as TimelineIcon,
  TrendingUp as TrendAnalysisIcon,
  Scanner as ThreatScannerIcon
} from '@mui/icons-material';
import PreloadManager from '../utils/PreloadManager';

interface PreloadMenuItemProps extends Omit<MenuItemLinkProps, 'to'> {
  to: string;
  componentPath: string;
  requiredPermissions?: string[];
  requiredRoles?: string[];
  primaryText: string;
  leftIcon?: React.ReactElement;
}

const PreloadMenuItem: React.FC<PreloadMenuItemProps> = ({
  to,
  componentPath,
  requiredPermissions = [],
  requiredRoles = [],
  primaryText,
  leftIcon,
  ...props
}) => {
  const { permissions } = usePermissions();
  const preloadManager = PreloadManager.getInstance();

  // Handle hover preloading - must be called before conditional returns
  const handleMouseEnter = useCallback(() => {
    preloadManager.preloadOnHover(componentPath);
  }, [componentPath, preloadManager]);

  // Check if user has required permissions
  const hasRequiredPermissions = requiredPermissions.length === 0 ||
    (Array.isArray(permissions) && requiredPermissions.every(permission => permissions.includes(permission)));

  // Check if user has required roles
  const hasRequiredRoles = requiredRoles.length === 0 ||
    (Array.isArray(permissions) && requiredRoles.some(role => permissions.includes(`role:${role}`)));

  // Don't render if user doesn't have access
  if (!hasRequiredPermissions || !hasRequiredRoles) {
    return null;
  }

  return (
    <MenuItemLink
      to={to}
      primaryText={primaryText}
      leftIcon={leftIcon}
      onMouseEnter={handleMouseEnter}
      {...props}
    />
  );
};

// Menu component mapping to track navigation
const MENU_COMPONENT_MAPPINGS: Record<string, string> = {
  '/': 'dashboard',
  '/dashboard': 'dashboard',
  '/security-events': 'security-events',
  '/mitre-techniques': 'mitre-techniques',
  '/yara-rules': 'yara-rules',
  '/yara-matches': 'yara-matches',
  '/timelines': 'timelines',
  '/trend-analysis': 'trend-analysis',
  '/system-status': 'system-status',
  '/threat-scanner': 'threat-scanner',
  '/compliance-reports': 'compliance-reports',
  '/configuration': 'configuration',
};

export const MenuWithPreloading: React.FC<MenuProps> = (props) => {
  const { permissions } = usePermissions();
  const resources = useResourceDefinitions();
  const preloadManager = PreloadManager.getInstance();

  // Initialize preloading on menu mount
  useEffect(() => {
    console.log('[MenuWithPreloading] Initializing component preloading...');

    // Track navigation patterns and start preloading
    const currentPath = window.location.pathname.replace('/#', '') || '/';
    const currentPage = MENU_COMPONENT_MAPPINGS[currentPath] || 'dashboard';

    // Preload components based on current navigation
    preloadManager.preloadForNavigation(currentPage);

    // Add navigation tracking
    const handleLocationChange = () => {
      const newPath = window.location.pathname.replace('/#', '') || '/';
      const newPage = MENU_COMPONENT_MAPPINGS[newPath] || 'dashboard';

      if (newPage !== currentPage) {
        console.log(`[MenuWithPreloading] Navigation: ${currentPage} → ${newPage}`);
        preloadManager.updateNavigationPatterns(currentPage, newPage);
        preloadManager.preloadForNavigation(newPage);
      }
    };

    // Listen for navigation changes
    window.addEventListener('hashchange', handleLocationChange);
    window.addEventListener('popstate', handleLocationChange);

    return () => {
      window.removeEventListener('hashchange', handleLocationChange);
      window.removeEventListener('popstate', handleLocationChange);
    };
  }, [preloadManager]);

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
      requiredRoles.some((role: string) => permissions?.includes(`role:${role}`));

    return hasPermissions && hasRoles;
  };

  return (
    <ReactAdminMenu {...props}>
      {/* Core Pages - Always visible */}
      <PreloadMenuItem
        to="/"
        componentPath="dashboard"
        primaryText="Dashboard"
        leftIcon={<DashboardIcon />}
      />

      {/* Security Events */}
      <PreloadMenuItem
        to="/security-events"
        componentPath="security-events"
        primaryText="Security Events"
        leftIcon={<SecurityIcon />}
        requiredPermissions={['security.read']}
      />

      {/* MITRE Techniques */}
      <PreloadMenuItem
        to="/mitre-techniques"
        componentPath="mitre-techniques"
        primaryText="MITRE Techniques"
        leftIcon={<MitreIcon />}
        requiredPermissions={['security.read']}
      />

      {/* YARA Rules */}
      <PreloadMenuItem
        to="/yara-rules"
        componentPath="yara-rules"
        primaryText="YARA Rules"
        leftIcon={<YaraIcon />}
        requiredPermissions={['security.read']}
      />

      {/* YARA Matches */}
      <PreloadMenuItem
        to="/yara-matches"
        componentPath="yara-matches"
        primaryText="YARA Matches"
        leftIcon={<YaraMatchesIcon />}
        requiredPermissions={['security.read']}
      />

      {/* Timelines */}
      <PreloadMenuItem
        to="/timelines"
        componentPath="timelines"
        primaryText="Timelines"
        leftIcon={<TimelineIcon />}
        requiredPermissions={['security.read']}
      />

      {/* Trend Analysis */}
      <PreloadMenuItem
        to="/trend-analysis"
        componentPath="trend-analysis"
        primaryText="Trend Analysis"
        leftIcon={<TrendAnalysisIcon />}
        requiredPermissions={['analytics.read']}
      />

      {/* System Status */}
      <PreloadMenuItem
        to="/system-status"
        componentPath="system-status"
        primaryText="System Status"
        leftIcon={<SystemIcon />}
        requiredPermissions={['system.read']}
      />

      {/* Threat Scanner */}
      <PreloadMenuItem
        to="/threat-scanner"
        componentPath="threat-scanner"
        primaryText="Threat Scanner"
        leftIcon={<ThreatScannerIcon />}
        requiredPermissions={['security.read']}
      />

      {/* Compliance Reports */}
      <PreloadMenuItem
        to="/compliance-reports"
        componentPath="compliance-reports"
        primaryText="Compliance Reports"
        leftIcon={<ComplianceIcon />}
        requiredPermissions={['compliance.read']}
      />

      {/* Configuration */}
      <PreloadMenuItem
        to="/configuration"
        componentPath="configuration"
        primaryText="Configuration"
        leftIcon={<SettingsIcon />}
        requiredRoles={['admin']}
      />

      {/* Dynamically render other protected resources */}
      {Object.keys(resources).map(resourceName => {
        const resource = resources[resourceName];

        // Skip resources we've already handled above
        const handledResources = [
          'dashboard', 'security-events', 'mitre-techniques', 'yara-rules',
          'yara-matches', 'timelines', 'trend-analysis', 'system-status',
          'threat-scanner', 'compliance-reports', 'configuration'
        ];

        if (handledResources.includes(resourceName)) {
          return null;
        }

        // Check if user can access this resource
        if (!canAccessResource(resourceName)) {
          return null;
        }

        // Use a generic component path for dynamic resources
        const componentPath = resourceName;

        return (
          <PreloadMenuItem
            key={resourceName}
            to={`/${resourceName}`}
            componentPath={componentPath}
            primaryText={resource.options?.label || resourceName}
            leftIcon={resource.icon ? React.createElement(resource.icon) : undefined}
          />
        );
      })}
    </ReactAdminMenu>
  );
};

// Only use named export to avoid confusion
// export default MenuWithPreloading;