import React from 'react';
import { Admin, Resource } from 'react-admin';
// Using production providers with real backend API
import { enhancedCastellanDataProvider } from './dataProvider/castellanDataProvider';
import { enhancedAuthProvider } from './auth/authProvider';
import { Layout } from './components/Layout';
import { Dashboard } from './components/Dashboard';

// Import resources
import {
  SecurityEventList,
  SecurityEventShow,
  SecurityEventEdit,
} from './resources/SecurityEvents';
import {
  SystemStatusList,
  SystemStatusShow,
} from './resources/SystemStatus';

// Import disabled Pro resource components
import {
  DisabledComplianceReports,
  DisabledThreatScanner,
} from './resources/DisabledProResource';

// CastellanProFree - No edition detection needed

// Import Material-UI icons for resources
import {
  Security as SecurityIcon,
  Assessment as ComplianceIcon,
  Computer as SystemIcon,
  BugReport as ThreatScannerIcon,
} from '@mui/icons-material';

const App = () => (
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
      // create={SecurityEventCreate} // Disabled due to TypeScript issues
      icon={SecurityIcon}
      recordRepresentation={(record) => `${record.eventType} - ${record.id}`}
    />
    
    {/* Compliance Reports Resource - Premium Feature (Disabled in CastellanProFree) */}
    <Resource
      name="compliance-reports"
      list={DisabledComplianceReports}
      show={DisabledComplianceReports}
      create={DisabledComplianceReports}
      icon={ComplianceIcon}
      recordRepresentation={() => 'Compliance Reports - Premium Feature'}
    />
    
    {/* System Status Resource - Available in CastellanProFree */}
    <Resource
      name="system-status"
      list={SystemStatusList}
      show={SystemStatusShow}
      icon={SystemIcon}
      recordRepresentation={(record) => `${record.component} - ${record.status}`}
    />
    
    {/* Threat Scanner Resource - Premium Feature (Disabled in CastellanProFree) */}
    <Resource
      name="threat-scanner"
      list={DisabledThreatScanner}
      show={DisabledThreatScanner}
      icon={ThreatScannerIcon}
      recordRepresentation={() => 'Threat Scanner - Premium Feature'}
    />
  </Admin>
);

export default App;
