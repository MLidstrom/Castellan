import { Link, useLocation } from 'react-router-dom';
import { Shield, LayoutDashboard, AlertTriangle, Clock, Target, Search, Activity, Settings, ChevronRight, Bell, User } from 'lucide-react';
import { ReactNode } from 'react';

const navigationItems = [
  { name: 'Dashboard', href: '/', icon: LayoutDashboard, description: 'Mission Control Center' },
  { name: 'Security Events', href: '/security-events', icon: AlertTriangle, description: 'Event Investigation' },
  { name: 'Timeline', href: '/timeline', icon: Clock, description: 'Attack Visualization' },
  { name: 'MITRE ATT&CK', href: '/mitre-attack', icon: Target, description: 'Threat Intelligence' },
  { name: 'YARA Rules', href: '/yara-rules', icon: Search, description: 'Malware Detection' },
  { name: 'System Status', href: '/system-status', icon: Activity, description: 'Platform Health' },
  { name: 'Configuration', href: '/configuration', icon: Settings, description: 'Settings & Integrations' },
];

export function MainLayout({ children }: { children: ReactNode }) {
  const location = useLocation();

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      <div className="fixed inset-y-0 left-0 z-50 w-72 bg-white dark:bg-gray-800 shadow-xl border-r border-gray-200 dark:border-gray-700">
        <div className="flex h-16 items-center justify-between px-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center space-x-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-600">
              <Shield className="h-6 w-6 text-white" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-gray-900 dark:text-white">Castellan</h1>
              <p className="text-xs text-gray-500 dark:text-gray-400">Security Platform</p>
            </div>
          </div>
          <div className="flex items-center space-x-2">
            <Bell className="h-5 w-5 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 cursor-pointer" />
            <User className="h-5 w-5 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 cursor-pointer" />
          </div>
        </div>

        <nav className="mt-6 px-3">
          <ul className="space-y-1">
            {navigationItems.map((item) => {
              const isActive = location.pathname === item.href;
              const Icon = item.icon;
              return (
                <li key={item.name}>
                  <Link to={item.href} className={`group flex items-center justify-between rounded-lg px-3 py-2.5 text-sm font-medium transition-all duration-200 ${isActive ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300' : 'text-gray-700 hover:bg-gray-50 hover:text-gray-900 dark:text-gray-300 dark:hover:bg-gray-700 dark:hover:text-white'}`}>
                    <div className="flex items-center space-x-3">
                      <Icon className={`h-5 w-5 ${isActive ? 'text-blue-600 dark:text-blue-400' : 'text-gray-400 group-hover:text-gray-600 dark:group-hover:text-gray-300'}`} />
                      <div>
                        <div className="font-medium">{item.name}</div>
                        <div className="text-xs text-gray-500 dark:text-gray-400">{item.description}</div>
                      </div>
                    </div>
                    {isActive && <ChevronRight className="h-4 w-4 text-blue-600 dark:text-blue-400" />}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>

        <div className="absolute bottom-6 left-6 right-6">
          <div className="rounded-lg bg-green-50 dark:bg-green-900/20 p-3 border border-green-200 dark:border-green-800">
            <div className="flex items-center space-x-2">
              <div className="h-2 w-2 rounded-full bg-green-500 animate-pulse"></div>
              <span className="text-sm font-medium text-green-700 dark:text-green-300">System Operational</span>
            </div>
            <p className="text-xs text-green-600 dark:text-green-400 mt-1">All services running normally</p>
          </div>
        </div>
      </div>

      <div className="pl-72">
        <main className="min-h-screen">{children}</main>
      </div>
    </div>
  );
}


