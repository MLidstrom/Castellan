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
        // Handle network errors when backend is not available
        if (error.message.includes('fetch') || error.name === 'TypeError') {
          return Promise.reject(new Error('Backend server is not available. Please wait a moment and try again.'));
        }
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
    const token = localStorage.getItem('auth_token');
    if (token) {
      return Promise.resolve();
    }
    // Don't log 'No token found' error - this is expected for login page
    return Promise.reject({ message: 'Please log in', silent: true });
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
