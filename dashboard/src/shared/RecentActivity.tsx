import { AlertTriangle, Shield, Clock, ExternalLink } from 'lucide-react';
import { Link } from 'react-router-dom';

interface SecurityEvent {
  id: string;
  timestamp: Date | string;
  eventType: string;
  severity: 'CRITICAL' | 'HIGH' | 'MEDIUM' | 'LOW';
  description: string;
  riskScore: number;
  yaraMatches?: Array<{ rule: { name: string } }>;
}

export function RecentActivity({ events, isLoading }: { events: SecurityEvent[]; isLoading?: boolean; }) {
  const colors = (severity: string) => {
    switch (severity) {
      case 'CRITICAL': return { bg: 'bg-red-50 dark:bg-red-900/20', border: 'border-red-200 dark:border-red-800', text: 'text-red-700 dark:text-red-300', badge: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200' };
      case 'HIGH': return { bg: 'bg-orange-50 dark:bg-orange-900/20', border: 'border-orange-200 dark:border-orange-800', text: 'text-orange-700 dark:text-orange-300', badge: 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200' };
      case 'MEDIUM': return { bg: 'bg-yellow-50 dark:bg-yellow-900/20', border: 'border-yellow-200 dark:border-yellow-800', text: 'text-yellow-700 dark:text-yellow-300', badge: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200' };
      default: return { bg: 'bg-gray-50 dark:bg-gray-800', border: 'border-gray-200 dark:border-gray-700', text: 'text-gray-700 dark:text-gray-300', badge: 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-200' };
    }
  };

  if (isLoading) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Recent Activity</h3>
        <div className="space-y-4">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="animate-pulse">
              <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded w-3/4 mb-2"></div>
              <div className="h-3 bg-gray-200 dark:bg-gray-700 rounded w-1/2"></div>
            </div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Recent Activity</h3>
        <Link to="/security-events" className="text-sm text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 flex items-center space-x-1">
          <span>View all</span>
          <ExternalLink className="h-4 w-4" />
        </Link>
      </div>

      <div className="space-y-4">
        {(!events || events.length === 0) ? (
          <div className="text-center py-8">
            <Shield className="h-12 w-12 text-gray-400 mx-auto mb-3" />
            <p className="text-gray-500 dark:text-gray-400">No recent security events</p>
          </div>
        ) : (
          events.map((event) => {
            const c = colors(event.severity);
            return (
              <div key={event.id} className={`rounded-lg border p-4 transition-all duration-200 hover:shadow-md ${c.bg} ${c.border}`}>
                <div className="flex items-start justify-between">
                  <div className="flex-1">
                    <div className="flex items-center space-x-2 mb-2">
                      <AlertTriangle className={`h-4 w-4 ${c.text}`} />
                      <span className={`text-sm font-medium ${c.badge} px-2 py-1 rounded-full`}>{event.severity}</span>
                      <span className="text-sm text-gray-600 dark:text-gray-400">{event.eventType}</span>
                    </div>
                    <p className="text-sm text-gray-900 dark:text-gray-100 mb-2">{event.description}</p>
                    <div className="flex items-center space-x-4 text-xs text-gray-500 dark:text-gray-400">
                      <div className="flex items-center space-x-1">
                        <Clock className="h-3 w-3" />
                        <span>{new Date(event.timestamp).toLocaleString()}</span>
                      </div>
                      <span>Risk: {event.riskScore}/100</span>
                      {event.yaraMatches && event.yaraMatches.length > 0 && (
                        <span>YARA: {event.yaraMatches.length} matches</span>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}


