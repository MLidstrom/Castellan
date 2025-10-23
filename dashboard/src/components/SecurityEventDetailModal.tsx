import { Shield, X, Clock, User, Monitor, Globe } from 'lucide-react';

type Severity = 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW' | 'UNKNOWN';

export interface SecurityEvent {
  id: string;
  eventType?: string;
  timestamp?: string | Date;
  source?: string;
  eventId?: number;
  level?: string;
  riskLevel?: string;
  severity?: string;
  riskScore?: number;
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
      return 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200';
    case 'HIGH':
      return 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200';
    case 'MEDIUM':
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200';
    case 'LOW':
      return 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200';
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-200';
  }
}

export const SecurityEventDetailModal: React.FC<{
  event: SecurityEvent | null;
  isOpen: boolean;
  onClose: () => void;
}> = ({ event, isOpen, onClose }) => {
  if (!isOpen || !event) return null;

  const riskLevel = (event.riskLevel || event.level || 'MEDIUM').toUpperCase() as Severity;
  const status = (event.status || 'Open').toUpperCase();
  const hasCorrelation = (event.correlationScore || 0) > 0;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50" onClick={onClose}>
      <div
        className="bg-white dark:bg-gray-800 rounded-lg shadow-xl max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <Shield className="h-6 w-6 text-blue-600" />
              <div>
                <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
                  {event.eventType || 'Security Event'}
                </h2>
                <p className="text-sm text-gray-600 dark:text-gray-300 font-mono">
                  ID: {event.id}
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
            >
              <X className="h-6 w-6" />
            </button>
          </div>
        </div>

        <div className="p-6 space-y-6">
          {/* Overview */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-4">
              <div>
                <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Risk Level</label>
                <div className="mt-1">
                  <span className={`px-3 py-1 rounded-full text-sm font-medium ${severityBadge(riskLevel)}`}>
                    {riskLevel}
                  </span>
                </div>
              </div>

              <div>
                <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Status</label>
                <p className="mt-1 text-sm text-gray-900 dark:text-white font-medium">
                  {status}
                </p>
              </div>

              {event.eventId && (
                <div>
                  <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Event ID</label>
                  <p className="mt-1 text-sm text-gray-900 dark:text-white font-mono">
                    {event.eventId}
                  </p>
                </div>
              )}
            </div>

            <div className="space-y-4">
              <div>
                <label className="text-sm font-medium text-gray-500 dark:text-gray-400 flex items-center gap-2">
                  <Clock className="h-4 w-4" />
                  Timestamp
                </label>
                <p className="mt-1 text-sm text-gray-900 dark:text-white">
                  {new Date(event.timestamp || Date.now()).toLocaleString()}
                </p>
              </div>

              {event.source && (
                <div>
                  <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Source</label>
                  <p className="mt-1 text-sm text-gray-900 dark:text-white">
                    {event.source}
                  </p>
                </div>
              )}
            </div>
          </div>

          {/* Message */}
          {event.message && (
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Message</h3>
              <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                <p className="text-gray-700 dark:text-gray-300 leading-relaxed">
                  {event.message}
                </p>
              </div>
            </div>
          )}

          {/* Machine & User Info */}
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">System Information</h3>
            <div className="border-t border-gray-200 dark:border-gray-700 pt-4 grid grid-cols-1 md:grid-cols-2 gap-4">
              {event.machine && (
                <div>
                  <label className="text-sm font-medium text-blue-600 dark:text-blue-400 flex items-center gap-2">
                    <Monitor className="h-4 w-4" />
                    Machine
                  </label>
                  <p className="mt-1 text-sm text-gray-900 dark:text-white font-mono">
                    {event.machine}
                  </p>
                </div>
              )}

              {event.user && (
                <div>
                  <label className="text-sm font-medium text-green-600 dark:text-green-400 flex items-center gap-2">
                    <User className="h-4 w-4" />
                    User
                  </label>
                  <p className="mt-1 text-sm text-gray-900 dark:text-white">
                    {event.user}
                  </p>
                </div>
              )}
            </div>
          </div>

          {/* MITRE ATT&CK Techniques */}
          {event.mitreAttack && event.mitreAttack.length > 0 && (
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">MITRE ATT&CK Techniques</h3>
              <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                <div className="flex flex-wrap gap-2">
                  {event.mitreAttack.map((technique, index) => (
                    <span
                      key={index}
                      className="px-3 py-1 bg-purple-100 dark:bg-purple-900/20 text-purple-700 dark:text-purple-300 text-sm rounded border border-purple-200 dark:border-purple-800"
                    >
                      {technique}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* Scores & Analysis */}
          <div>
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Threat Analysis</h3>
            <div className="border-t border-gray-200 dark:border-gray-700 pt-4 space-y-4">
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div>
                  <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Confidence</label>
                  <p className="mt-1 text-lg font-bold text-gray-900 dark:text-white">
                    {Math.round(event.confidence || 0)}%
                  </p>
                </div>

                {hasCorrelation && (
                  <div>
                    <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Correlation</label>
                    <p className="mt-1 text-lg font-bold text-blue-600 dark:text-blue-400">
                      {(event.correlationScore || 0).toFixed(2)}
                    </p>
                  </div>
                )}

                {(event.burstScore || 0) > 0 && (
                  <div>
                    <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Burst</label>
                    <p className="mt-1 text-lg font-bold text-orange-600 dark:text-orange-400">
                      {(event.burstScore || 0).toFixed(2)}
                    </p>
                  </div>
                )}

                {(event.anomalyScore || 0) > 0 && (
                  <div>
                    <label className="text-sm font-medium text-gray-500 dark:text-gray-400">Anomaly</label>
                    <p className="mt-1 text-lg font-bold text-red-600 dark:text-red-400">
                      {(event.anomalyScore || 0).toFixed(2)}
                    </p>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* IP Addresses */}
          {event.ipAddresses && event.ipAddresses.length > 0 && (
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3 flex items-center gap-2">
                <Globe className="h-5 w-5" />
                IP Addresses
              </h3>
              <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                <div className="flex flex-wrap gap-2">
                  {event.ipAddresses.map((ip, index) => (
                    <span
                      key={index}
                      className="px-3 py-1 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 text-sm rounded font-mono border"
                    >
                      {ip}
                    </span>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* Notes */}
          {event.notes && (
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Notes</h3>
              <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                <p className="text-gray-700 dark:text-gray-300 whitespace-pre-wrap">
                  {event.notes}
                </p>
              </div>
            </div>
          )}

          {/* Assigned To */}
          {event.assignedTo && (
            <div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-3">Assignment</h3>
              <div className="border-t border-gray-200 dark:border-gray-700 pt-4">
                <p className="text-gray-700 dark:text-gray-300">
                  Assigned to: <span className="font-medium">{event.assignedTo}</span>
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
