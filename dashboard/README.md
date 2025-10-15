# DASHBOARD - Castellan Dashboard (React + Tailwind)

Modern dashboard UI that connects to the Castellan backend via REST API and SignalR. Matches the Mission Control design (sidebar, metrics, recent activity, threat distribution).

## Prerequisites
- Node 18+
- Castellan backend running at http://localhost:5000

## Quick start (PowerShell-safe)
```powershell
cd .\dashboard; npm install; npm run dev
# Open http://localhost:3000
```

## Scripts
- npm run dev — start Vite dev server (port 3000)
- npm run build — type-check and build production bundle
- npm run preview — serve the production build locally
- npm run type-check — run TypeScript project references build

## Environment variables
Create `.env.local` (optional):
```
VITE_API_URL=/api
VITE_SIGNALR_URL=/hubs
VITE_SIGNALR_HUB=scan-progress
```

## Structure
```
src/
  components/
    layout/              # MainLayout (sidebar navigation)
    YaraConfigComponent.tsx  # YARA auto-update configuration
  pages/
    Dashboard.tsx        # Main dashboard with metrics and activity
    SecurityEvents.tsx   # Security events list
    SecurityEventDetail.tsx  # Individual event details
    Timeline.tsx         # Event timeline visualization
    MitreAttack.tsx      # MITRE ATT&CK techniques management
    MalwareRules.tsx        # malware detection rules management
    SystemStatus.tsx     # System component health monitoring
    Configuration.tsx    # Multi-tab configuration interface
    Login.tsx            # Authentication page
  services/              # api.ts, auth.ts, signalr.ts, constants.ts
  shared/                # MetricCard, RecentActivity, ThreatDistribution
  styles/                # globals.css (Tailwind layers)
```

## Pages & Routes
- `/` - Dashboard (metrics, recent activity, threat distribution)
- `/security-events` - Security events list with all fields
- `/security-events/:id` - Event detail page
- `/timeline` - Event timeline visualization
- `/mitre-attack` - MITRE ATT&CK techniques (list, search, import, detail)
- `/malware-rules` - malware detection rules (list, enable/disable, import, detail)
- `/system-status` - Component health monitoring
- `/configuration` - Settings (Threat Intel, Notifications, IP Enrichment, YARA)
- `/login` - Authentication

## API usage
- GET /api/dashboarddata/consolidated?timeRange=24h — dashboard metrics & threat distribution
- GET /api/security-events?limit=8&sort=timestamp&order=desc — recent activity
- GET /api/mitre/techniques — MITRE techniques with pagination
- GET /api/mitre/statistics — MITRE database statistics
- GET /api/malware-rules — malware detection rules with filters
- GET /api/malware-rules/statistics — YARA statistics
- GET /api/system-status — System component health
- GET /api/settings/threat-intelligence — Threat intel configuration
- GET /api/notifications/config — Notification settings
- GET /api/yara-configuration — YARA auto-update configuration

## Authentication
- Visit http://localhost:3000/login and sign in (admin / CastellanAdmin2024!)
- A token is stored in localStorage as `auth_token` and sent as `Authorization: Bearer …`.

## SignalR
- Hub: `/hubs/scan-progress` (proxied by Vite)
- Dashboard subscribes to: `DashboardUpdate`, `SecurityEvent`, `SystemStatusUpdate`
- Security Events subscribes to: `SecurityEvent`
- Verify in DevTools:
  - XHR: POST `/hubs/scan-progress/negotiate` → 200
  - WS: connected socket to `/hubs/scan-progress` with incoming frames

## Data mapping (consolidated schema)
- Open Events: recent events where status ∈ {OPEN, INVESTIGATING}
- Critical Threats: `securityEvents.riskLevelCounts.CRITICAL`
- Events/Week: uses `securityEvents.totalEvents` until a weekly field is provided
- System Status: `OPERATIONAL` if `healthyComponents === totalComponents`, else `DEGRADED`
- Threat Distribution: displayed in order Critical, High, Medium, Low, Unknown (title-cased labels)

## PostCSS/Tailwind (Node 22, ESM-compatible)
- Configs used:
  - `postcss.config.cjs` (CommonJS)
  - `tailwind.config.js` (module.exports)
- Restart `npm run dev` after config changes.
