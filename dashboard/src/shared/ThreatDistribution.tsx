import { memo } from 'react';
import { Shield } from 'lucide-react';

interface ThreatDistributionProps {
  data: Array<{ severity: string; count: number }>;
  isLoading?: boolean;
}

// ✅ FIX 4.3: Memoize ThreatDistribution to prevent unnecessary re-renders
export const ThreatDistribution = memo(function ThreatDistribution({ data, isLoading }: ThreatDistributionProps) {
  if (isLoading) {
    return (
      <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-6">Threat Distribution</h3>
        <div className="space-y-3">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="animate-pulse">
              <div className="h-4 bg-gray-200 dark:bg-gray-700 rounded w-full mb-2"></div>
            </div>
          ))}
        </div>
      </div>
    );
  }

  const severityColors: Record<string, string> = {
    CRITICAL: 'bg-red-500',
    HIGH: 'bg-orange-500',
    MEDIUM: 'bg-yellow-500',
    LOW: 'bg-green-500',
  };

  const total = (data || []).reduce((s, d) => s + d.count, 0) || 1;

  // Enforce explicit ordering: Critical, High, Medium, Low, Unknown
  const orderMap: Record<string, number> = { CRITICAL: 0, HIGH: 1, MEDIUM: 2, LOW: 3, UNKNOWN: 4 };
  const sorted = [...(data || [])].sort((a, b) => {
    const sa = (a.severity || 'UNKNOWN').toUpperCase();
    const sb = (b.severity || 'UNKNOWN').toUpperCase();
    return (orderMap[sa] ?? 999) - (orderMap[sb] ?? 999);
  });

  return (
    <div className="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 p-6">
      <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-6">Threat Distribution</h3>
      <div className="space-y-4">
        {(!data || data.length === 0) ? (
          <div className="text-center py-8">
            <Shield className="h-12 w-12 text-gray-400 mx-auto mb-3" />
            <p className="text-gray-500 dark:text-gray-400">No threat data available</p>
          </div>
        ) : (
          sorted.map((item) => {
            const sev = (item.severity || 'UNKNOWN').toUpperCase();
            const label = sev.charAt(0) + sev.slice(1).toLowerCase();
            const percentage = (item.count / total) * 100;
            const bar = severityColors[sev] || 'bg-gray-500';
            return (
              <div key={item.severity} className="flex items-center justify-between">
                <div className="flex items-center space-x-3">
                  <div className={`w-3 h-3 rounded-full ${bar}`}></div>
                  <span className="text-sm font-medium text-gray-900 dark:text-white">{label}</span>
                </div>
                <div className="flex items-center space-x-2">
                  <div className="w-20 bg-gray-200 dark:bg-gray-700 rounded-full h-2">
                    <div className={`h-2 rounded-full ${bar}`} style={{ width: `${percentage}%` }}></div>
                  </div>
                  <span className="text-sm text-gray-600 dark:text-gray-400 min-w-[3rem] text-right">{item.count}</span>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
});


