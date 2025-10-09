import { LucideIcon } from 'lucide-react';

interface MetricCardProps {
  title: string;
  value: string | number;
  change?: { value: number; period: string };
  icon: LucideIcon;
  color: 'blue' | 'green' | 'yellow' | 'red' | 'gray';
  description?: string;
}

const colorVariants = {
  blue: { bg: 'bg-blue-50 dark:bg-blue-900/20', border: 'border-blue-200 dark:border-blue-800', icon: 'text-blue-600 dark:text-blue-400', text: 'text-blue-900 dark:text-blue-100', change: 'text-blue-600 dark:text-blue-400' },
  green: { bg: 'bg-green-50 dark:bg-green-900/20', border: 'border-green-200 dark:border-green-800', icon: 'text-green-600 dark:text-green-400', text: 'text-green-900 dark:text-green-100', change: 'text-green-600 dark:text-green-400' },
  yellow:{ bg: 'bg-yellow-50 dark:bg-yellow-900/20', border:'border-yellow-200 dark:border-yellow-800', icon:'text-yellow-600 dark:text-yellow-400', text:'text-yellow-900 dark:text-yellow-100', change:'text-yellow-600 dark:text-yellow-400'},
  red:   { bg: 'bg-red-50 dark:bg-red-900/20', border: 'border-red-200 dark:border-red-800', icon: 'text-red-600 dark:text-red-400', text: 'text-red-900 dark:text-red-100', change: 'text-red-600 dark:text-red-400' },
  gray:  { bg: 'bg-gray-50 dark:bg-gray-800', border: 'border-gray-200 dark:border-gray-700', icon: 'text-gray-600 dark:text-gray-400', text: 'text-gray-900 dark:text-gray-100', change: 'text-gray-600 dark:text-gray-400' },
};

export function MetricCard({ title, value, change, icon: Icon, color, description }: MetricCardProps) {
  const colors = colorVariants[color];
  return (
    <div className={`rounded-xl border p-6 transition-all duration-200 hover:shadow-md ${colors.bg} ${colors.border}`}>
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-3">
          <div className={`rounded-lg p-2 ${colors.bg}`}>
            <Icon className={`h-6 w-6 ${colors.icon}`} />
          </div>
          <div>
            <p className="text-sm font-medium text-gray-600 dark:text-gray-400">{title}</p>
            <p className={`text-2xl font-bold ${colors.text}`}>{typeof value === 'number' ? value.toLocaleString() : value}</p>
          </div>
        </div>
        {change && (
          <div className="text-right">
            <div className={`text-sm font-medium ${colors.change}`}>{change.value > 0 ? '+' : ''}{change.value}</div>
            <div className="text-xs text-gray-500 dark:text-gray-400">{change.period}</div>
          </div>
        )}
      </div>
      {description && (<p className="mt-3 text-sm text-gray-600 dark:text-gray-400">{description}</p>)}
    </div>
  );
}


