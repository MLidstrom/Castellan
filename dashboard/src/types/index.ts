// ============================================================================
// CastellanAI Dashboard - Shared Type Definitions
// ============================================================================

// API Response Types
export interface ApiResponse<T> {
  data?: T;
  success?: boolean;
  message?: string;
  error?: string;
}

// Security Events
export interface SecurityEvent {
  id: number | string;
  timestamp: string | Date;
  source: string;
  eventId: number;
  level: string;
  message: string;
  machineName?: string;
  machine?: string; // Alias for machineName
  user?: string;
  riskLevel: RiskLevel;
  severity?: string; // Alias for riskLevel
  riskScore: number;
  status: EventStatus;
  mitreTechniques?: string[];
  mitreAttack?: string[]; // Alias for mitreTechniques
  correlationContext?: string;
  correlationScore?: number;
  burstScore?: number;
  anomalyScore?: number;
  confidence?: number;
  aiAnalysis?: string;
  recommendations?: string[];
  eventType?: string;
  assignedTo?: string | null;
  notes?: string | null;
  ipAddresses?: string[];
  enrichedIPs?: any[];
}

export type RiskLevel = 'LOW' | 'MEDIUM' | 'HIGH' | 'CRITICAL';
export type EventStatus = 'OPEN' | 'INVESTIGATING' | 'RESOLVED' | 'CLOSED' | 'FALSE_POSITIVE';

// Dashboard Data
export interface DashboardConsolidated {
  securityEvents: {
    totalEvents: number;
    recentEvents: SecurityEvent[];
    riskLevelCounts: Record<string, number>;
  };
  malware: {
    enabledRules: number;
    totalRules: number;
    recentMatches: number;
  };
  threatScanner: {
    totalScans: number;
    lastScanResult: string;
    lastScanStatus: 'clean' | 'threat' | 'unknown';
  };
  systemStatus: {
    totalComponents: number;
    healthyComponents: number;
  };
  recentActivity: RecentActivityItem[];
}

export interface RecentActivityItem {
  id: number;
  timestamp: string;
  type: string;
  description: string;
  severity: RiskLevel;
  status: EventStatus;
}

// MITRE ATT&CK
export interface MitreTechnique {
  id: string;
  name: string;
  description: string;
  tactics: string[];
  platforms: string[];
  detectionCount?: number;
}

export interface MitreStatistics {
  totalTechniques: number;
  detectedTechniques: number;
  topTactics: Array<{ name: string; count: number }>;
}

// Malware Rules
export interface MalwareRule {
  id: number;
  name: string;
  category: string;
  threatLevel: string;
  author: string;
  isEnabled: boolean;
  isValid: boolean;
  hitCount: number;
  lastModified: string;
  ruleContent?: string;
}

export interface MalwareStatistics {
  totalRules: number;
  enabledRules: number;
  disabledRules: number;
  invalidRules: number;
  totalHits: number;
}

// Threat Scanner
export interface ThreatScan {
  id: number;
  scanType: 'quick' | 'full';
  status: 'running' | 'completed' | 'failed' | 'cancelled';
  startTime: string;
  endTime?: string;
  threatsFound: number;
  filesScanned: number;
}

export interface ScanProgress {
  isScanning: boolean;
  progress: number;
  currentPath?: string;
  filesScanned: number;
  threatsFound: number;
}

// System Status
export interface SystemStatus {
  overallStatus: 'healthy' | 'degraded' | 'critical';
  components: ComponentStatus[];
  uptime: string;
  version: string;
}

export interface ComponentStatus {
  name: string;
  status: 'healthy' | 'degraded' | 'unhealthy';
  message?: string;
  lastCheck: string;
}

// Timeline
export interface TimelineData {
  timestamp: string;
  count: number;
  severity: RiskLevel;
}

export interface TimelineStats {
  totalEvents: number;
  criticalEvents: number;
  averageRiskScore: number;
  topSources: Array<{ source: string; count: number }>;
}

// Notification Templates
export interface NotificationTemplate {
  id: string;
  name: string;
  platform: 'teams' | 'slack';
  eventType: string;
  template: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

// Auth
export interface AuthTokens {
  accessToken: string;
  refreshToken?: string;
  expiresAt?: string;
  tokenType?: string;
}

export interface User {
  username: string;
  email?: string;
  role?: string;
}

// SignalR Events
export type SignalREvent =
  | 'DashboardUpdate'
  | 'SecurityEvent'
  | 'SystemStatusUpdate'
  | 'ScanProgressUpdate'
  | 'MalwareDetection';

export interface SignalRPayload<T = any> {
  event: SignalREvent;
  data: T;
  timestamp: string;
}

// Query Keys (for TanStack Query)
export const QueryKeys = {
  dashboard: (timeRange: string) => ['dashboard', 'consolidated', timeRange] as const,
  securityEvents: (filters?: any) => ['security-events', filters] as const,
  systemStatus: () => ['system-status'] as const,
  mitreTechniques: (params?: any) => ['mitre', 'techniques', params] as const,
  mitreStatistics: () => ['mitre', 'statistics'] as const,
  malwareRules: (params?: any) => ['malware-rules', params] as const,
  malwareStatistics: () => ['malware-rules', 'statistics'] as const,
  threatScans: (params?: any) => ['threat-scanner', params] as const,
  scanProgress: () => ['threat-scanner', 'progress'] as const,
  timeline: (granularity: string, from: string, to: string) =>
    ['timeline', granularity, from, to] as const,
  timelineStats: (from: string, to: string) =>
    ['timeline', 'stats', from, to] as const,
  notificationTemplates: (platform?: string) =>
    ['notification-templates', platform] as const,
} as const;
