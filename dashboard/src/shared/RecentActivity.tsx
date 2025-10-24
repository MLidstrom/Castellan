import { memo } from 'react';
import { Shield } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { SecurityEvent } from '../types';

interface RecentActivityProps {
  events: SecurityEvent[];
  isLoading?: boolean;
  onEventClick?: (event: SecurityEvent) => void;
}

function severityBadge(riskLevel: string | undefined) {
  const level = (riskLevel || 'MEDIUM').toUpperCase();
  switch (level) {
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

// ✅ FIX 4.2: Memoize RecentActivity to prevent unnecessary re-renders
export const RecentActivity = memo(function RecentActivity({
  events,
  isLoading,
  onEventClick
}: RecentActivityProps) {
  if (isLoading) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Recent Activity</h3>
        <div className="space-y-3">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-20 rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 animate-pulse"></div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white">Recent Activity</h3>
        <Link
          to="/security-events"
          className="text-sm text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 font-medium"
        >
          View all →
        </Link>
      </div>

      <div className="space-y-3">
        {(!events || events.length === 0) ? (
          <div className="text-center py-12">
            <Shield className="h-12 w-12 text-gray-400 mx-auto mb-3" />
            <p className="text-gray-500 dark:text-gray-400">No recent security events</p>
          </div>
        ) : (
          events.map((event) => {
            const riskLevel = (event.riskLevel || 'MEDIUM').toUpperCase();

            return (
              <div
                key={event.id}
                onClick={() => onEventClick?.(event)}
                className="block rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-4 cursor-pointer hover:shadow-lg hover:border-blue-300 dark:hover:border-blue-600 transition-all duration-200"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1 min-w-0">
                    {/* Header Row */}
                    <div className="flex items-center gap-2 mb-2 flex-wrap">
                      <span className={`text-xs font-semibold px-2 py-1 rounded-full ${severityBadge(event.riskLevel)}`}>
                        {riskLevel}
                      </span>
                      <div className="text-sm font-semibold text-gray-900 dark:text-gray-100 truncate">
                        {event.eventType || 'Security Event'}
                      </div>
                    </div>

                    {/* Details */}
                    <div className="flex flex-wrap items-center gap-3 text-xs text-gray-500 dark:text-gray-400">
                      <div className="flex items-center gap-1">
                        <span className="h-1.5 w-1.5 rounded-full bg-gray-400"></span>
                        <span>{new Date(event.timestamp || Date.now()).toLocaleString()}</span>
                      </div>
                      {event.machine && (
                        <div>
                          <span className="text-gray-500 dark:text-gray-400">Machine:</span>{' '}
                          <span className="text-gray-900 dark:text-gray-100 font-medium">{event.machine}</span>
                        </div>
                      )}
                      {event.source && (
                        <div>
                          <span className="text-gray-500 dark:text-gray-400">Source:</span>{' '}
                          <span className="text-gray-900 dark:text-gray-100 font-medium">{event.source}</span>
                        </div>
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
});
