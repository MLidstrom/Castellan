// Use full URLs for production builds, relative paths for dev (Vite proxy)
const isDev = (import.meta as any).env?.DEV as boolean | undefined;
export const API_URL = ((import.meta as any).env?.VITE_API_URL as string) || (isDev ? '/api' : 'http://localhost:5000/api');
export const SIGNALR_BASE_URL = ((import.meta as any).env?.VITE_SIGNALR_URL as string) || (isDev ? '/hubs' : 'http://localhost:5000/hubs');
export const SIGNALR_HUB_PATH = ((import.meta as any).env?.VITE_SIGNALR_HUB as string) || 'scan-progress';

export const AUTH_STORAGE_KEY = 'auth_token';


