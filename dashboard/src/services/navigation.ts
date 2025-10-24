import { NavigateFunction } from 'react-router-dom';

/**
 * Navigation service to handle programmatic navigation
 * This allows non-React code (like API service) to trigger navigation
 */
class NavigationService {
  private navigate: NavigateFunction | null = null;

  /**
   * Set the navigate function (called from App.tsx)
   */
  setNavigate(navigateFn: NavigateFunction) {
    this.navigate = navigateFn;
  }

  /**
   * Navigate to login page
   */
  toLogin() {
    if (this.navigate) {
      this.navigate('/login', { replace: true });
    } else {
      console.warn('[Navigation] Navigate function not set, using window.location');
      window.location.href = '/login';
    }
  }

  /**
   * Navigate to a specific path
   */
  to(path: string, options?: { replace?: boolean; state?: any }) {
    if (this.navigate) {
      this.navigate(path, options);
    } else {
      console.warn('[Navigation] Navigate function not set, using window.location');
      window.location.href = path;
    }
  }

  /**
   * Go back in history
   */
  back() {
    if (this.navigate) {
      this.navigate(-1);
    } else {
      window.history.back();
    }
  }
}

export const navigationService = new NavigationService();
