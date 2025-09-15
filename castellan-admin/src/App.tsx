import React, { useEffect, Suspense } from 'react';
import { Admin, Resource } from 'react-admin';
import { Box, CircularProgress, Typography } from '@mui/material';
import {
  Security as SecurityIcon,
  Assessment as ComplianceIcon,
  Computer as SystemIcon,
  BugReport as ThreatScannerIcon,
  Notifications as NotificationsIcon,
  Gavel as MitreIcon,
  Settings as ConfigurationIcon,
  Shield as YaraRulesIcon,
  FindInPage as YaraMatchesIcon,
  Timeline as TimelineIcon,
} from '@mui/icons-material';
// Using production providers with real backend API
import { enhancedCastellanDataProvider } from './dataProvider/castellanDataProvider';
import { enhancedAuthProvider } from './auth/authProvider';
import { Layout } from './components/Layout';
import { SignalRProvider } from './contexts/SignalRContext';
import { DashboardDataProvider } from './contexts/DashboardDataContext';

// Lazy load Dashboard for better performance
const Dashboard = React.lazy(() => import('./components/Dashboard').then(module => ({ default: module.Dashboard })));

// Import custom Login component
const Login = React.lazy(() => import('./components/Login').then(module => ({ default: module.Login })));

// Lazy-loaded resource imports for better performance and code splitting
const SecurityEventList = React.lazy(() => import('./resources/SecurityEvents').then(module => ({ default: module.SecurityEventList })));
const SecurityEventShow = React.lazy(() => import('./resources/SecurityEvents').then(module => ({ default: module.SecurityEventShow })));
const SecurityEventEdit = React.lazy(() => import('./resources/SecurityEvents').then(module => ({ default: module.SecurityEventEdit })));
const SecurityEventCreate = React.lazy(() => import('./resources/SecurityEvents').then(module => ({ default: module.SecurityEventCreate })));

const SystemStatusList = React.lazy(() => import('./resources/SystemStatus').then(module => ({ default: module.SystemStatusList })));
const SystemStatusShow = React.lazy(() => import('./resources/SystemStatus').then(module => ({ default: module.SystemStatusShow })));

const ComplianceReportList = React.lazy(() => import('./resources/ComplianceReports').then(module => ({ default: module.ComplianceReportList })));
const ComplianceReportShow = React.lazy(() => import('./resources/ComplianceReports').then(module => ({ default: module.ComplianceReportShow })));
const ComplianceReportCreate = React.lazy(() => import('./resources/ComplianceReports').then(module => ({ default: module.ComplianceReportCreate })));

const ThreatScannerList = React.lazy(() => import('./resources/ThreatScanner').then(module => ({ default: module.ThreatScannerList })));
const ThreatScannerShow = React.lazy(() => import('./resources/ThreatScanner').then(module => ({ default: module.ThreatScannerShow })));

const MitreTechniquesList = React.lazy(() => import('./resources/MitreTechniques').then(module => ({ default: module.MitreTechniquesList })));
const MitreTechniquesShow = React.lazy(() => import('./resources/MitreTechniques').then(module => ({ default: module.MitreTechniquesShow })));

const YaraRulesList = React.lazy(() => import('./resources/YaraRules').then(module => ({ default: module.YaraRulesList })));
const YaraRulesShow = React.lazy(() => import('./resources/YaraRules').then(module => ({ default: module.YaraRulesShow })));
const YaraRulesCreate = React.lazy(() => import('./resources/YaraRules').then(module => ({ default: module.YaraRulesCreate })));
const YaraRulesEdit = React.lazy(() => import('./resources/YaraRules').then(module => ({ default: module.YaraRulesEdit })));

const YaraMatchesList = React.lazy(() => import('./resources/YaraMatches').then(module => ({ default: module.YaraMatchesList })));
const YaraMatchesShow = React.lazy(() => import('./resources/YaraMatches').then(module => ({ default: module.YaraMatchesShow })));

const TimelineList = React.lazy(() => import('./resources/Timeline').then(module => ({ default: module.TimelineList })));

const NotificationSettingsList = React.lazy(() => import('./resources/NotificationSettings').then(module => ({ default: module.NotificationSettingsList })));
const NotificationSettingsShow = React.lazy(() => import('./resources/NotificationSettings').then(module => ({ default: module.NotificationSettingsShow })));
const NotificationSettingsCreate = React.lazy(() => import('./resources/NotificationSettings').then(module => ({ default: module.NotificationSettingsCreate })));
const NotificationSettingsEdit = React.lazy(() => import('./resources/NotificationSettings').then(module => ({ default: module.NotificationSettingsEdit })));

const ConfigurationList = React.lazy(() => import('./resources/Configuration').then(module => ({ default: module.ConfigurationList })));
const ConfigurationShow = React.lazy(() => import('./resources/Configuration').then(module => ({ default: module.ConfigurationShow })));

// CastellanProFree - No edition detection needed

// Loading fallback component for lazy-loaded resources
const LoadingFallback = () => (
  <Box
    sx={{
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      height: '200px',
      gap: 2
    }}
  >
    <CircularProgress size={40} />
    <Typography variant="body2" color="textSecondary">
      Loading component...
    </Typography>
  </Box>
);

const App = () => {
  return (
    <Suspense fallback={<LoadingFallback />}>
      <SignalRProvider>
        <DashboardDataProvider>
          <Admin
            dataProvider={enhancedCastellanDataProvider}
            authProvider={enhancedAuthProvider}
            layout={Layout}
            dashboard={Dashboard}
            loginPage={Login}
          >
            {/* Security Events Resource - Available in CastellanProFree */}
                    <Resource
              name="security-events"
              list={SecurityEventList}
              show={SecurityEventShow}
              edit={SecurityEventEdit}
              create={SecurityEventCreate}
              icon={SecurityIcon}
              recordRepresentation={(record) => `${record.eventType} - ${record.id}`}
            />
    
            {/* MITRE ATT&CK Techniques Resource - Available in CastellanProFree */}
            <Resource
              name="mitre-techniques"
              list={MitreTechniquesList}
              show={MitreTechniquesShow}
              icon={MitreIcon}
              recordRepresentation={(record) => `${record.techniqueId} - ${record.name}`}
            />
    
            {/* YARA Rules Resource - Available in CastellanProFree */}
            <Resource
              name="yara-rules"
              list={YaraRulesList}
              show={YaraRulesShow}
              create={YaraRulesCreate}
              edit={YaraRulesEdit}
              icon={YaraRulesIcon}
              recordRepresentation={(record) => `${record.name} - ${record.category}`}
            />
    
            {/* YARA Matches Resource - Available in CastellanProFree */}
            <Resource
              name="yara-matches"
              list={YaraMatchesList}
              show={YaraMatchesShow}
              icon={YaraMatchesIcon}
              recordRepresentation={(record) => `${record.ruleName} - ${record.targetFile}`}
            />
    
            {/* Timeline Resource - Available in CastellanProFree */}
            <Resource
              name="timeline"
              list={TimelineList}
              icon={TimelineIcon}
              recordRepresentation={() => 'Security Event Timeline'}
            />
    
            {/* Compliance Reports Resource - Now Available */}
            <Resource
              name="compliance-reports"
              list={ComplianceReportList}
              show={ComplianceReportShow}
              create={ComplianceReportCreate}
              icon={ComplianceIcon}
              recordRepresentation={(record) => `${record.framework} - ${record.reportType}`}
            />
    
            {/* System Status Resource - Available in CastellanProFree */}
            <Resource
              name="system-status"
              list={SystemStatusList}
              show={SystemStatusShow}
              icon={SystemIcon}
              recordRepresentation={(record) => `${record.component} - ${record.status}`}
            />
    
            {/* Threat Scanner Resource - Now Available */}
            <Resource
              name="threat-scanner"
              list={ThreatScannerList}
              show={ThreatScannerShow}
              icon={ThreatScannerIcon}
              recordRepresentation={(record) => `${record.scanType} - ${record.id}`}
            />
    
            {/* Notification Settings Resource - Available in CastellanProFree */}
            <Resource
              name="notification-settings"
              list={NotificationSettingsList}
              show={NotificationSettingsShow}
              create={NotificationSettingsCreate}
              edit={NotificationSettingsEdit}
              icon={NotificationsIcon}
              recordRepresentation={(record) => `${record.name || 'Notification Config'}`}
            />
    
            {/* Configuration Resource - Available in CastellanProFree */}
            <Resource
              name="configuration"
              list={ConfigurationList}
              show={ConfigurationShow}
              icon={ConfigurationIcon}
              recordRepresentation={() => 'System Configuration'}
            />
          </Admin>
        </DashboardDataProvider>
      </SignalRProvider>
    </Suspense>
  );
};

export default App;
