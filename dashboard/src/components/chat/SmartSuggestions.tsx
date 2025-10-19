import React from 'react';
import {
  AlertTriangle,
  Search,
  Shield,
  FileText,
  TrendingUp,
  Clock,
  Users,
  Network,
  Lock,
  Activity
} from 'lucide-react';

interface Suggestion {
  id: string;
  text: string;
  icon: React.ElementType;
  category: 'threat' | 'investigation' | 'compliance' | 'analysis';
}

interface SmartSuggestionsProps {
  onSuggestionClick: (text: string) => void;
  context?: 'default' | 'threat' | 'event' | 'compliance';
}

const DEFAULT_SUGGESTIONS: Suggestion[] = [
  {
    id: 'critical-events',
    text: 'Show critical events from last hour',
    icon: AlertTriangle,
    category: 'threat'
  },
  {
    id: 'summarize-threats',
    text: "Summarize today's threats",
    icon: Shield,
    category: 'analysis'
  },
  {
    id: 'compliance-status',
    text: 'Check compliance status',
    icon: FileText,
    category: 'compliance'
  },
  {
    id: 'failed-logins',
    text: 'Find failed login attempts',
    icon: Lock,
    category: 'investigation'
  },
  {
    id: 'network-activity',
    text: 'Show unusual network activity',
    icon: Network,
    category: 'threat'
  },
  {
    id: 'user-activity',
    text: 'Review user activity patterns',
    icon: Users,
    category: 'analysis'
  }
];

const THREAT_SUGGESTIONS: Suggestion[] = [
  {
    id: 'active-threats',
    text: 'What active threats are detected?',
    icon: AlertTriangle,
    category: 'threat'
  },
  {
    id: 'malware-detections',
    text: 'Show recent malware detections',
    icon: Shield,
    category: 'threat'
  },
  {
    id: 'external-ips',
    text: 'Find connections from external IPs',
    icon: Network,
    category: 'investigation'
  },
  {
    id: 'privilege-escalation',
    text: 'Check for privilege escalation attempts',
    icon: TrendingUp,
    category: 'threat'
  }
];

const EVENT_SUGGESTIONS: Suggestion[] = [
  {
    id: 'recent-high-risk',
    text: 'Show recent high-risk events',
    icon: AlertTriangle,
    category: 'threat'
  },
  {
    id: 'event-timeline',
    text: 'Create timeline of recent events',
    icon: Clock,
    category: 'analysis'
  },
  {
    id: 'correlation-patterns',
    text: 'Find correlated event patterns',
    icon: Activity,
    category: 'analysis'
  },
  {
    id: 'search-events',
    text: 'Search events by criteria',
    icon: Search,
    category: 'investigation'
  }
];

const COMPLIANCE_SUGGESTIONS: Suggestion[] = [
  {
    id: 'soc2-report',
    text: 'Generate SOC2 compliance report',
    icon: FileText,
    category: 'compliance'
  },
  {
    id: 'access-violations',
    text: 'Find access control violations',
    icon: Lock,
    category: 'compliance'
  },
  {
    id: 'audit-trail',
    text: 'Review audit trail for changes',
    icon: Activity,
    category: 'compliance'
  },
  {
    id: 'policy-violations',
    text: 'Check for policy violations',
    icon: AlertTriangle,
    category: 'compliance'
  }
];

export function SmartSuggestions({ onSuggestionClick, context = 'default' }: SmartSuggestionsProps) {
  // Select suggestions based on context
  const suggestions = React.useMemo(() => {
    switch (context) {
      case 'threat':
        return THREAT_SUGGESTIONS;
      case 'event':
        return EVENT_SUGGESTIONS;
      case 'compliance':
        return COMPLIANCE_SUGGESTIONS;
      default:
        return DEFAULT_SUGGESTIONS;
    }
  }, [context]);

  const getCategoryColor = (category: string) => {
    switch (category) {
      case 'threat':
        return 'border-red-200 dark:border-red-800 hover:border-red-400 dark:hover:border-red-600 hover:bg-red-50 dark:hover:bg-red-900/20';
      case 'investigation':
        return 'border-orange-200 dark:border-orange-800 hover:border-orange-400 dark:hover:border-orange-600 hover:bg-orange-50 dark:hover:bg-orange-900/20';
      case 'compliance':
        return 'border-blue-200 dark:border-blue-800 hover:border-blue-400 dark:hover:border-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/20';
      case 'analysis':
        return 'border-green-200 dark:border-green-800 hover:border-green-400 dark:hover:border-green-600 hover:bg-green-50 dark:hover:bg-green-900/20';
      default:
        return 'border-gray-200 dark:border-gray-700 hover:border-gray-400 dark:hover:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800';
    }
  };

  return (
    <div className="p-6 bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
      <div className="mb-3">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100 mb-1">
          Quick Start
        </h3>
        <p className="text-xs text-gray-600 dark:text-gray-400">
          Try these common security queries
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
        {suggestions.map((suggestion) => {
          const Icon = suggestion.icon;
          return (
            <button
              key={suggestion.id}
              onClick={() => onSuggestionClick(suggestion.text)}
              className={`flex items-start gap-3 p-3 rounded-lg border-2 transition-all text-left ${getCategoryColor(
                suggestion.category
              )}`}
            >
              <Icon className="w-4 h-4 mt-0.5 flex-shrink-0 text-gray-600 dark:text-gray-400" />
              <span className="text-sm text-gray-900 dark:text-gray-100 leading-tight">
                {suggestion.text}
              </span>
            </button>
          );
        })}
      </div>

      {/* Category Legend */}
      <div className="mt-4 flex flex-wrap gap-3 text-xs text-gray-600 dark:text-gray-400">
        <div className="flex items-center gap-1.5">
          <div className="w-2 h-2 rounded-full bg-red-500" />
          <span>Threat Hunting</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="w-2 h-2 rounded-full bg-orange-500" />
          <span>Investigation</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="w-2 h-2 rounded-full bg-blue-500" />
          <span>Compliance</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="w-2 h-2 rounded-full bg-green-500" />
          <span>Analysis</span>
        </div>
      </div>
    </div>
  );
}
