import axios, { AxiosError } from 'axios';
import { useAuth } from '@clerk/clerk-react';

const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL || 'https://localhost:5001/api/v1';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Token getter that will be set by the auth provider
let getToken: (() => Promise<string | null>) | null = null;

export const setTokenGetter = (getter: () => Promise<string | null>) => {
  getToken = getter;
};

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  async (config) => {
    if (getToken) {
      try {
        const token = await getToken();
        if (token && config.headers) {
          config.headers.Authorization = `Bearer ${token}`;
        }
      } catch (error) {
        console.error('Error getting auth token:', error);
      }
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response) {
      switch (error.response.status) {
        case 401:
          // Don't redirect if already on sign-in page or if signing out
          // This prevents infinite loops during sign-out
          if (!window.location.pathname.startsWith('/sign-in') &&
              !window.location.pathname.startsWith('/sign-up')) {
            window.location.href = '/sign-in';
          }
          break;
        case 403:
          // Forbidden
          console.error('Access denied');
          break;
        case 500:
          // Server error
          console.error('Server error:', error.response.data);
          break;
      }
    }
    return Promise.reject(error);
  }
);

// Hook to initialize the token getter with Clerk
export const useInitializeApiClient = () => {
  const { getToken: clerkGetToken } = useAuth();

  // Set the token getter to use Clerk's getToken
  setTokenGetter(async () => {
    try {
      return await clerkGetToken();
    } catch {
      return null;
    }
  });
};

export default apiClient;
