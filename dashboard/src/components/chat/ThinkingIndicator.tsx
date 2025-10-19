import { useEffect, useState } from 'react';
import { Brain, Loader2 } from 'lucide-react';

interface ThinkingIndicatorProps {
  message?: string;
  showProgress?: boolean;
  estimatedTimeMs?: number;
}

export function ThinkingIndicator({
  message = 'CastellanAI is thinking...',
  showProgress = false,
  estimatedTimeMs = 3000
}: ThinkingIndicatorProps) {
  const [progress, setProgress] = useState(0);
  const [dots, setDots] = useState('');

  // Animated dots effect
  useEffect(() => {
    const dotsInterval = setInterval(() => {
      setDots(prev => {
        if (prev.length >= 3) return '';
        return prev + '.';
      });
    }, 500);

    return () => clearInterval(dotsInterval);
  }, []);

  // Progress bar animation
  useEffect(() => {
    if (!showProgress) return;

    const increment = 100 / (estimatedTimeMs / 100); // Update every 100ms
    const progressInterval = setInterval(() => {
      setProgress(prev => {
        const next = prev + increment;
        // Slow down as we approach 90% to avoid completing before actual response
        if (next >= 90) return Math.min(prev + increment / 4, 95);
        return Math.min(next, 95);
      });
    }, 100);

    return () => {
      clearInterval(progressInterval);
      setProgress(0);
    };
  }, [showProgress, estimatedTimeMs]);

  return (
    <div className="flex justify-start mb-4">
      <div className="max-w-[80%]">
        <div className="rounded-lg p-4 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700">
          {/* Thinking Header */}
          <div className="flex items-center gap-3">
            <div className="relative">
              <Brain className="w-5 h-5 text-blue-600 dark:text-blue-400" />
              <Loader2 className="w-3 h-3 text-blue-600 dark:text-blue-400 animate-spin absolute -top-1 -right-1" />
            </div>
            <div className="flex-1">
              <div className="text-sm font-medium text-gray-900 dark:text-gray-100">
                {message}{dots}
              </div>
              {showProgress && (
                <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">
                  Analyzing security context...
                </div>
              )}
            </div>
          </div>

          {/* Progress Bar */}
          {showProgress && (
            <div className="mt-3">
              <div className="h-1.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                <div
                  className="h-full bg-gradient-to-r from-blue-500 to-blue-600 transition-all duration-300 ease-out rounded-full"
                  style={{ width: `${progress}%` }}
                >
                  <div className="h-full w-full bg-gradient-to-r from-transparent via-white/30 to-transparent animate-shimmer" />
                </div>
              </div>
            </div>
          )}

          {/* Pulsing Dots Animation */}
          <div className="flex items-center gap-1.5 mt-3">
            <div className="flex gap-1">
              <span className="w-2 h-2 bg-blue-600 dark:bg-blue-400 rounded-full animate-pulse" style={{ animationDelay: '0ms' }} />
              <span className="w-2 h-2 bg-blue-600 dark:bg-blue-400 rounded-full animate-pulse" style={{ animationDelay: '150ms' }} />
              <span className="w-2 h-2 bg-blue-600 dark:bg-blue-400 rounded-full animate-pulse" style={{ animationDelay: '300ms' }} />
            </div>
          </div>
        </div>

        {/* Timestamp placeholder */}
        <div className="text-xs text-gray-500 dark:text-gray-400 mt-1">Just now</div>
      </div>

      {/* Add shimmer animation to tailwind config if not already present */}
      <style>{`
        @keyframes shimmer {
          0% { transform: translateX(-100%); }
          100% { transform: translateX(100%); }
        }
        .animate-shimmer {
          animation: shimmer 1.5s infinite;
        }
      `}</style>
    </div>
  );
}
