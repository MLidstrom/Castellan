import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AuthService } from '../services/auth';

export function LoginPage() {
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await AuthService.login(username, password);
      navigate('/', { replace: true });
    } catch (err: any) {
      setError(err?.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900">
      <form onSubmit={onSubmit} className="w-full max-w-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl p-6 shadow-sm">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white mb-4">Sign in</h1>
        <label className="block text-sm text-gray-700 dark:text-gray-300 mb-1">Username</label>
        <input value={username} onChange={(e)=>setUsername(e.target.value)} className="w-full mb-3 px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100" placeholder="admin" />
        <label className="block text-sm text-gray-700 dark:text-gray-300 mb-1">Password</label>
        <input value={password} onChange={(e)=>setPassword(e.target.value)} type="password" className="w-full mb-4 px-3 py-2 rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100" placeholder="••••••••" />
        {error && <div className="text-sm text-red-600 mb-3">{error}</div>}
        <button type="submit" disabled={loading} className="w-full py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-medium disabled:opacity-60">
          {loading ? 'Signing in…' : 'Sign in'}
        </button>
        <div className="text-xs text-gray-500 dark:text-gray-400 mt-3">Local demo credentials: admin / CastellanAdmin2024!</div>
      </form>
    </div>
  );
}


