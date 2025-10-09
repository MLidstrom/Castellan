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
  components/layout/   # MainLayout (sidebar), shared header
  pages/               # Dashboard page
  services/            # api.ts, auth.ts, signalr.ts, constants.ts
  shared/              # MetricCard, RecentActivity, ThreatDistribution
  styles/              # globals.css (Tailwind layers)
```

## API usage
- GET /api/dashboarddata/consolidated?timeRange=24h — dashboard metrics & threat distribution
- GET /api/security-events?limit=8&sort=timestamp&order=desc — recent activity

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
