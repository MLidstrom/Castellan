import { Routes, Route, Navigate } from 'react-router-dom';
import { MainLayout } from './components/layout/MainLayout';
import { DashboardPage } from './pages/Dashboard';
import { LoginPage } from './pages/Login';
import { SecurityEventsPage } from './pages/SecurityEvents';
import { SecurityEventDetailPage } from './pages/SecurityEventDetail';
import { TimelinePage } from './pages/Timeline';
import { MitreAttackPage } from './pages/MitreAttack';
import { MalwareRulesPage } from './pages/MalwareRules';
import { ThreatScannerPage } from './pages/ThreatScanner';
import { SystemStatusPage } from './pages/SystemStatus';
import { ConfigurationPage } from './pages/Configuration';
import { ChatPage } from './pages/Chat';
import { SignalRProvider } from './contexts/SignalRContext';

export default function App() {
  return (
    <SignalRProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/*"
          element={
            <MainLayout>
              <Routes>
                <Route path="/" element={<DashboardPage />} />
                <Route path="/security-events" element={<SecurityEventsPage />} />
                <Route path="/security-events/:id" element={<SecurityEventDetailPage />} />
                <Route path="/timeline" element={<TimelinePage />} />
                <Route path="/mitre-attack" element={<MitreAttackPage />} />
                <Route path="/malware-rules" element={<MalwareRulesPage />} />
                <Route path="/threat-scanner" element={<ThreatScannerPage />} />
                <Route path="/system-status" element={<SystemStatusPage />} />
                <Route path="/configuration" element={<ConfigurationPage />} />
                <Route path="/chat" element={<ChatPage />} />
              </Routes>
            </MainLayout>
          }
        />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </SignalRProvider>
  );
}


