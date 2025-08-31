import { AuthProvider } from 'react-admin';

export const authProvider: AuthProvider = {
  login: ({ username, password }) => {
    // Authenticate against backend API
    return fetch('http://localhost:5000/api/auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ username, password }),
    })
      .then(response => {
        if (response.ok) {
          return response.json();
        }
        throw new Error('Invalid credentials');
      })
      .then(data => {
        localStorage.setItem('auth_token', data.token);
        localStorage.setItem('user', JSON.stringify(data.user));
        return Promise.resolve();
      })
      .catch(error => {
        return Promise.reject(error);
      });
  },
  
  logout: () => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('user');
    return Promise.resolve();
  },
  
  checkError: (error) => {
    const status = error.status;
    if (status === 401 || status === 403) {
      localStorage.removeItem('auth_token');
      return Promise.reject();
    }
    return Promise.resolve();
  },
  
  checkAuth: () => {
    return localStorage.getItem('auth_token') ? Promise.resolve() : Promise.reject();
  },
  
  getPermissions: () => {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    return Promise.resolve(user.permissions || []);
  },
  
  getIdentity: () => {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    return Promise.resolve({
      id: user.id,
      fullName: '',
      avatar: user.avatar,
    });
  },
};
