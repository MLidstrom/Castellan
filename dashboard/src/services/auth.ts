import { API_URL, AUTH_STORAGE_KEY } from './constants';

export interface AuthTokens {
  accessToken: string;
  refreshToken?: string;
  expiresAt?: string;
  tokenType?: string;
}

export class AuthService {
  static async login(username: string, password: string): Promise<AuthTokens> {
    const res = await fetch(`${API_URL}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });
    if (!res.ok) throw new Error('Login failed');
    const data = await res.json().catch(() => ({}));

    // Try to extract token from common shapes
    const headerAuth = res.headers.get('authorization') || res.headers.get('Authorization');
    let token: string | undefined = undefined;

    const candidates: any[] = [data, data?.data];
    for (const obj of candidates) {
      if (!obj || typeof obj !== 'object') continue;
      token = obj.accessToken || obj.token || obj.jwt || obj.bearerToken || obj.id_token || obj.idToken;
      if (token) break;
    }
    if (!token && typeof headerAuth === 'string') {
      // Expect formats like: Bearer <token>
      const parts = headerAuth.split(' ');
      token = parts.length === 2 ? parts[1] : headerAuth;
    }

    if (token) {
      localStorage.setItem(AUTH_STORAGE_KEY, token);
    }

    return { accessToken: token || '', ...data } as AuthTokens;
  }

  static logout() {
    localStorage.removeItem(AUTH_STORAGE_KEY);
  }

  static getToken(): string | null {
    return localStorage.getItem(AUTH_STORAGE_KEY);
  }
}


