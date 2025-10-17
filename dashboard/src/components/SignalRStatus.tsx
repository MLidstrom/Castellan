import { useSignalR } from '../contexts/SignalRContext';

export function SignalRStatus() {
  const { isConnected } = useSignalR();

  return (
    <div className="flex items-center space-x-2">
      <div
        className={`h-2 w-2 rounded-full ${
          isConnected
            ? 'bg-green-500 animate-pulse'
            : 'bg-gray-400 dark:bg-gray-600'
        }`}
      ></div>
      <span
        className={`text-sm font-medium ${
          isConnected
            ? 'text-green-600 dark:text-green-400'
            : 'text-gray-500 dark:text-gray-400'
        }`}
      >
        {isConnected ? 'Connected' : 'Connection Lost'}
      </span>
    </div>
  );
}
