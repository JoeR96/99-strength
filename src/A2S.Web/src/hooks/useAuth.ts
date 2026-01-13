import { useAuth as useClerkAuth } from '@clerk/clerk-react';

/**
 * Custom hook that wraps Clerk's useAuth and provides
 * a consistent auth interface for the application.
 */
export function useAuth() {
  const { isLoaded, isSignedIn, userId, getToken } = useClerkAuth();

  const getAccessToken = async () => {
    if (!isSignedIn) return null;
    return await getToken();
  };

  return {
    isLoading: !isLoaded,
    isAuthenticated: isSignedIn ?? false,
    userId,
    getAccessToken,
  };
}
