import React, { Suspense } from 'react';
import { Admin, Resource } from 'react-admin';
import { Box, CircularProgress, Typography, createTheme } from '@mui/material';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import {
  Security as SecurityIcon,
  Computer as SystemIcon,
  BugReport as ThreatScannerIcon,
  Gavel as MitreIcon,
  Settings as ConfigurationIcon,
  Shield as YaraRulesIcon,
  FindInPage as YaraMatchesIcon,
  CalendarMonth as TimelineIcon,
  TrendingUp as TrendAnalysisIcon,
  Rule as RuleIcon,
} from '@mui/icons-material';
// Using production providers with real backend API
import { enhancedCastellanDataProvider } from './dataProvider/castellanDataProvider';
import { createSimplifiedDataProvider } from './dataProvider/simplifiedDataProvider';
import { createConfiguredQueryClient, setupCachePersistence } from './config/reactQueryConfig';
import { enhancedAuthProvider } from './auth/authProvider';
import { Layout } from './components/Layout';
import { SignalRProvider } from './contexts/SignalRContext';
import { DashboardDataProvider } from './contexts/DashboardDataContext';
import { useDashboardWarmup } from './hooks/useDashboardWarmup';

// Create custom themes with enhanced active menu item visibility
const lightTheme = createTheme({
  palette: {
    mode: 'light',
  },
  components: {
    RaMenuItemLink: {
      styleOverrides: {
        root: {
          '&.RaMenuItemLink-active': {
            backgroundColor: '#1976d2',
            color: '#ffffff',
            fontWeight: 600,
            borderLeft: '4px solid #ffffff',
            '& .MuiListItemIcon-root': {
              color: '#ffffff',
            },
            '&:hover': {
              backgroundColor: '#1565c0',
            },
          },
          '&:hover': {
            backgroundColor: 'rgba(25, 118, 210, 0.08)',
          },
        },
      },
    },
  },
});

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#1e1e1e', // Darker gray for dark theme (between #121212 and #202124)
    },
  },
  components: {
    RaMenuItemLink: {
      styleOverrides: {
        root: {
          '&.RaMenuItemLink-active': {
            backgroundColor: '#1e1e1e', // Darker gray
            color: '#ffffff',
            fontWeight: 600,
            borderLeft: '4px solid #ffffff',
            '& .MuiListItemIcon-root': {
              color: '#ffffff',
            },
            '&:hover': {
              backgroundColor: '#2a2a2a',
            },
          },
          '&:hover': {
            backgroundColor: 'rgba(30, 30, 30, 0.15)',
          },
        },
      },
    },
  },
});

// Lazy load Dashboard with webpack prefetch for better performance
const Dashboard = React.lazy(() => import(/* webpackPrefetch: true */ './components/Dashboard').then(module => ({ default: module.Dashboard })));

// Import custom Login component with prefetch
const Login = React.lazy(() => import(/* webpackPrefetch: true */ './components/Login').then(module => ({ default: module.Login })));

// Lazy-loaded resource imports with webpack prefetch for instant loading
const SecurityEventList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEvents').then(module => ({ default: module.SecurityEventList })));
const SecurityEventShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEvents').then(module => ({ default: module.SecurityEventShow })));
const SecurityEventEdit = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEvents').then(module => ({ default: module.SecurityEventEdit })));
const SecurityEventCreate = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEvents').then(module => ({ default: module.SecurityEventCreate })));

const SystemStatusList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SystemStatus').then(module => ({ default: module.SystemStatusList })));
const SystemStatusShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SystemStatus').then(module => ({ default: module.SystemStatusShow })));


const ThreatScannerList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/ThreatScanner').then(module => ({ default: module.ThreatScannerList })));
const ThreatScannerShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/ThreatScanner').then(module => ({ default: module.ThreatScannerShow })));

const MitreTechniquesList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/MitreTechniques').then(module => ({ default: module.MitreTechniquesList })));
const MitreTechniquesShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/MitreTechniques').then(module => ({ default: module.MitreTechniquesShow })));

const YaraRulesList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/YaraRules').then(module => ({ default: module.YaraRulesList })));
const YaraRulesShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/YaraRules').then(module => ({ default: module.YaraRulesShow })));
const YaraRulesCreate = React.lazy(() => import(/* webpackPrefetch: true */ './resources/YaraRules').then(module => ({ default: module.YaraRulesCreate })));
const YaraRulesEdit = React.lazy(() => import(/* webpackPrefetch: true */ './resources/YaraRules').then(module => ({ default: module.YaraRulesEdit })));

const YaraMatchesList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/YaraMatches').then(module => ({ default: module.YaraMatchesList })));
const YaraMatchesShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/YaraMatches').then(module => ({ default: module.YaraMatchesShow })));

const SecurityEventRuleList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEventRules').then(module => ({ default: module.SecurityEventRuleList })));
const SecurityEventRuleShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEventRules').then(module => ({ default: module.SecurityEventRuleShow })));
const SecurityEventRuleCreate = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEventRules').then(module => ({ default: module.SecurityEventRuleCreate })));
const SecurityEventRuleEdit = React.lazy(() => import(/* webpackPrefetch: true */ './resources/SecurityEventRules').then(module => ({ default: module.SecurityEventRuleEdit })));

const TimelineList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/Timelines').then(module => ({ default: module.TimelineList })));

const TrendAnalysisPage = React.lazy(() => import(/* webpackPrefetch: true */ './components/TrendAnalysisPage'));


const ConfigurationList = React.lazy(() => import(/* webpackPrefetch: true */ './resources/Configuration').then(module => ({ default: module.ConfigurationList })));
const ConfigurationShow = React.lazy(() => import(/* webpackPrefetch: true */ './resources/Configuration').then(module => ({ default: module.ConfigurationShow })));

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

// Dashboard Warmup Initializer - runs early prefetch after login
// This component must be inside Admin component to access React Admin context
const WarmupInitializer: React.FC = () => {
  useDashboardWarmup();
  return null;
};

// Create configured QueryClient (SINGLE instance for entire app)
// This is the single source of truth for all data caching
const queryClient = createConfiguredQueryClient();

// Setup cache persistence to localStorage (survives page refresh)
// Snapshots persist for 24 hours, treated as fresh for 5 minutes
setupCachePersistence(queryClient);

const App = () => {
  return (
    <QueryClientProvider client={queryClient}>
      <Suspense fallback={<LoadingFallback />}>
        <SignalRProvider>
          <DashboardDataProvider>
            <Admin
              dataProvider={createSimplifiedDataProvider(enhancedCastellanDataProvider)}
              authProvider={enhancedAuthProvider}
              layout={Layout}
              dashboard={Dashboard}
              loginPage={Login}
              lightTheme={lightTheme}
              darkTheme={darkTheme}
              queryClient={queryClient}
            >
            <WarmupInitializer />
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

            {/* Security Event Rules Resource - Detection Rule Management */}
            <Resource
              name="security-event-rules"
              list={SecurityEventRuleList}
              show={SecurityEventRuleShow}
              create={SecurityEventRuleCreate}
              edit={SecurityEventRuleEdit}
              icon={RuleIcon}
              recordRepresentation={(record) => `Event ${record.eventId} - ${record.summary}`}
            />

            {/* Timeline Resource - Available in CastellanProFree */}
            <Resource
              name="timeline"
              list={TimelineList}
              icon={TimelineIcon}
              recordRepresentation={() => 'Security Event Timeline'}
            />

            {/* Trend Analysis Page - New in v0.6.0 */}
            <Resource
              name="trend-analysis"
              list={TrendAnalysisPage}
              icon={TrendAnalysisIcon}
              recordRepresentation={() => 'Trend Analysis'}
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

    {/* React Query DevTools - useful for debugging cache (dev mode only) */}
    {process.env.NODE_ENV === 'development' && (
      <ReactQueryDevtools initialIsOpen={false} position="bottom" />
    )}
  </QueryClientProvider>
  );
};

export default App;
