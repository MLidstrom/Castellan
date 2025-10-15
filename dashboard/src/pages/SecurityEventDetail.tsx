import { useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, AlertTriangle } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';

type Severity = 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW' | 'UNKNOWN';

interface SecurityEvent {
  id: string;
  eventType?: string;
  timestamp?: string | Date;
  source?: string;
  eventId?: number;
  level?: string;
  riskLevel?: string;
  machine?: string;
  user?: string;
  message?: string;
  mitreAttack?: string[];
  correlationScore?: number;
  burstScore?: number;
  anomalyScore?: number;
  confidence?: number;
  status?: string;
  assignedTo?: string | null;
  notes?: string | null;
  ipAddresses?: string[];
  enrichedIPs?: any[];
}

function severityBadge(sev: Severity | undefined) {
  switch (sev) {
    case 'CRITICAL':
      return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200 border-red-300';
    case 'HIGH':
      return 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200 border-orange-300';
    case 'MEDIUM':
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200 border-yellow-300';
    case 'LOW':
      return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200 border-green-300';
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-200 border-gray-300';
  }
}

export function SecurityEventDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { token, loading } = useAuth();

  useEffect(() => {
    if (!loading && !token) {
      navigate('/login');
    }
  }, [token, loading, navigate]);

  const eventQuery = useQuery({
    queryKey: ['security-event', id],
    queryFn: async () => {
      const response = await fetch(`/api/security-events/${id}`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      if (!response.ok) throw new Error('Failed to fetch event');
      const data = await response.json();
      return data.data || data;
    },
    enabled: !loading && !!token && !!id,
  });

  const event = eventQuery.data as SecurityEvent | undefined;
  const riskLevel = (event?.riskLevel || event?.level || 'MEDIUM').toUpperCase() as Severity;
  const hasCorrelation = (event?.correlationScore || 0) > 0;

  if (eventQuery.isLoading) {
    return (
      <div className="min-h-screen bg-gray-50 dark:bg-gray-900 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
          <p className="text-gray-600 dark:text-gray-300">Loading event details...</p>
        </div>
      </div>
    );
  }

  if (eventQuery.isError || !event) {
    return (
      <div className="min-h-screen bg-gray-50 dark:bg-gray-900 flex items-center justify-center">
        <div className="text-center">
          <AlertTriangle className="h-12 w-12 text-red-500 mx-auto mb-4" />
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white mb-2">Event Not Found</h2>
          <p className="text-gray-600 dark:text-gray-300 mb-4">The security event you're looking for doesn't exist.</p>
          <button
            onClick={() => navigate('/security-events')}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          >
            Back to Events
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Header */}
      <div className="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
        <div className="px-8 py-6">
          <button
            onClick={() => navigate('/security-events')}
            className="flex items-center gap-2 text-gray-600 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white mb-4"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Security Events
          </button>
          
          <div className="flex items-center justify-between">
            <div>
              <div className="flex items-center gap-3 mb-2">
                <span className={`px-3 py-1 rounded-full text-sm font-semibold border ${severityBadge(riskLevel)}`}>
                  {riskLevel}
                </span>
                <h1 className="text-3xl font-bold text-gray-900 dark:text-white">
                  {event.eventType || 'Security Event'}
                </h1>
                {hasCorrelation && (
                  <span className="px-3 py-1 bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200 rounded-full text-sm flex items-center gap-1">
                    🔗 Correlated Event
                  </span>
                )}
              </div>
              <p className="text-gray-600 dark:text-gray-400">Event ID: {id}</p>
            </div>
          </div>
        </div>
      </div>

      <div className="p-8 max-w-7xl mx-auto">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main Content */}
          <div className="lg:col-span-2 space-y-6">
            {/* Message Card */}
            <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Event Summary</h2>
              <p className="text-gray-700 dark:text-gray-300 leading-relaxed">
                {event.message || 'No message available'}
              </p>
            </div>

            {/* MITRE Techniques */}
            {event.mitreAttack && event.mitreAttack.length > 0 && (
              <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
                <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">MITRE ATT&CK Techniques</h2>
                <div className="flex flex-wrap gap-2">
                  {event.mitreAttack.map((technique, idx) => (
                    <div
                      key={idx}
                      className="px-3 py-2 bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200 rounded-lg border border-purple-200 dark:border-purple-800"
                    >
                      {technique}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* IP Addresses */}
            {event.ipAddresses && event.ipAddresses.length > 0 && (
              <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
                <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">IP Addresses</h2>
                <div className="space-y-2">
                  {event.ipAddresses.map((ip, idx) => {
                    const enrichment = event.enrichedIPs?.find((e: any) => e.IP === ip || e.ip === ip);
                    return (
                      <div key={idx} className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-900 rounded-lg">
                        <span className="font-mono text-gray-900 dark:text-white">{ip}</span>
                        {enrichment && (
                          <div className="flex items-center gap-4 text-sm text-gray-600 dark:text-gray-400">
                            {enrichment.country && <span>{enrichment.country}</span>}
                            {enrichment.city && <span>{enrichment.city}</span>}
                            {enrichment.isHighRisk && (
                              <span className="px-2 py-1 bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200 rounded text-xs font-semibold">
                                High Risk
                              </span>
                            )}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>

          {/* Sidebar */}
          <div className="space-y-6">
            {/* Event Details */}
            <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Event Details</h2>
              <div className="space-y-3 text-sm">
                <div>
                  <span className="text-gray-500 dark:text-gray-400">Timestamp</span>
                  <p className="text-gray-900 dark:text-white font-medium mt-1">
                    {event.timestamp ? new Date(event.timestamp).toLocaleString() : '—'}
                  </p>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400">Machine</span>
                  <p className="text-gray-900 dark:text-white font-medium mt-1">{event.machine || '—'}</p>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400">User</span>
                  <p className="text-gray-900 dark:text-white font-medium mt-1">{event.user || '—'}</p>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400">Source</span>
                  <p className="text-gray-900 dark:text-white font-medium mt-1">{event.source || '—'}</p>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400">Windows Event ID</span>
                  <p className="text-gray-900 dark:text-white font-medium mt-1 font-mono">{event.eventId || '—'}</p>
                </div>
                <div>
                  <span className="text-gray-500 dark:text-gray-400">Status</span>
                  <p className="text-blue-600 dark:text-blue-400 font-medium mt-1">{event.status || 'Open'}</p>
                </div>
              </div>
            </div>

            {/* Analysis Scores */}
            <div className="bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6">
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Analysis Scores</h2>
              <div className="space-y-4">
                <div>
                  <div className="flex justify-between items-center mb-1">
                    <span className="text-sm text-gray-600 dark:text-gray-400">Confidence</span>
                    <span className="text-sm font-semibold text-gray-900 dark:text-white">
                      {Math.round(event.confidence || 0)}%
                    </span>
                  </div>
                  <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
                    <div
                      className="bg-blue-600 h-2 rounded-full"
                      style={{ width: `${event.confidence || 0}%` }}
                    ></div>
                  </div>
                </div>

                {hasCorrelation && (
                  <div>
                    <div className="flex justify-between items-center mb-1">
                      <span className="text-sm text-gray-600 dark:text-gray-400">Correlation</span>
                      <span className="text-sm font-semibold text-blue-600 dark:text-blue-400">
                        {(event.correlationScore || 0).toFixed(2)}
                      </span>
                    </div>
                    <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
                      <div
                        className="bg-blue-600 h-2 rounded-full"
                        style={{ width: `${Math.min((event.correlationScore || 0) * 100, 100)}%` }}
                      ></div>
                    </div>
                  </div>
                )}

                {(event.burstScore || 0) > 0 && (
                  <div>
                    <div className="flex justify-between items-center mb-1">
                      <span className="text-sm text-gray-600 dark:text-gray-400">Burst</span>
                      <span className="text-sm font-semibold text-orange-600 dark:text-orange-400">
                        {(event.burstScore || 0).toFixed(2)}
                      </span>
                    </div>
                    <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
                      <div
                        className="bg-orange-600 h-2 rounded-full"
                        style={{ width: `${Math.min((event.burstScore || 0) * 100, 100)}%` }}
                      ></div>
                    </div>
                  </div>
                )}

                {(event.anomalyScore || 0) > 0 && (
                  <div>
                    <div className="flex justify-between items-center mb-1">
                      <span className="text-sm text-gray-600 dark:text-gray-400">Anomaly</span>
                      <span className="text-sm font-semibold text-red-600 dark:text-red-400">
                        {(event.anomalyScore || 0).toFixed(2)}
                      </span>
                    </div>
                    <div className="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-2">
                      <div
                        className="bg-red-600 h-2 rounded-full"
                        style={{ width: `${Math.min((event.anomalyScore || 0) * 100, 100)}%` }}
                      ></div>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

