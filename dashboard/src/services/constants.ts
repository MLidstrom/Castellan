// Default to relative paths so Vite proxy handles CORS in dev
export const API_URL = ((import.meta as any).env?.VITE_API_URL as string) || '/api';
export const SIGNALR_BASE_URL = ((import.meta as any).env?.VITE_SIGNALR_URL as string) || '/hubs';
export const SIGNALR_HUB_PATH = ((import.meta as any).env?.VITE_SIGNALR_HUB as string) || 'scan-progress';

export const AUTH_STORAGE_KEY = 'auth_token';


