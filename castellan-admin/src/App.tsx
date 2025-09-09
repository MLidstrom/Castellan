import React, { useEffect } from 'react';
import { Admin, Resource } from 'react-admin';
// Using production providers with real backend API
import { enhancedCastellanDataProvider } from './dataProvider/castellanDataProvider';
import { enhancedAuthProvider } from './auth/authProvider';
import { Layout } from './components/Layout';
import { Dashboard } from './components/Dashboard';
import { initializeCachePreloader } from './utils/cachePreloader';
import './utils/cacheInspector'; // Load cache debugging tools

// Import resources
import {
  SecurityEventList,
  SecurityEventShow,
  SecurityEventEdit,
  SecurityEventCreate,
} from './resources/SecurityEvents'; // Reverted to original for stability
import {
  SystemStatusList,
  SystemStatusShow,
} from './resources/SystemStatus';

// Import functional compliance reports components
import {
  ComplianceReportList,
  ComplianceReportShow,
  ComplianceReportCreate,
} from './resources/ComplianceReports';

// Import threat scanner resource
import {
  ThreatScannerList,
  ThreatScannerShow,
} from './resources/ThreatScanner';

// Import MITRE techniques resource
import {
  MitreTechniquesList,
  MitreTechniquesShow,
} from './resources/MitreTechniques';

// Import notification settings resource
import {
  NotificationSettingsList,
  NotificationSettingsShow,
  NotificationSettingsCreate,
  NotificationSettingsEdit,
} from './resources/NotificationSettings';

// Import configuration resource
import {
  ConfigurationList,
  ConfigurationShow,
} from './resources/Configuration';

// CastellanProFree - No edition detection needed

// Import Material-UI icons for resources
import {
  Security as SecurityIcon,
  Assessment as ComplianceIcon,
  Computer as SystemIcon,
  BugReport as ThreatScannerIcon,
  Notifications as NotificationsIcon,
  Gavel as MitreIcon,
  Settings as ConfigurationIcon,
} from '@mui/icons-material';

const App = () => {
  // Initialize cache preloader for immediate data availability
  useEffect(() => {
    initializeCachePreloader();
  }, []);

  return (
    <Admin
      dataProvider={enhancedCastellanDataProvider}
      authProvider={enhancedAuthProvider}
      layout={Layout}
      dashboard={Dashboard}
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
  );
};

export default App;
