import { useAuth } from '@clerk/clerk-react';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { API_BASE_URL_EXPORT, getAuthToken } from '@/api/apiClient';
import { queryKeysFor, runSyncStream } from './syncEvents';

/**
 * While signed in, listens to `/sync/events` and refetches the queries of each changed area (a habit ticked
 * in Terran CC shows up here without a reload). Silent when the stream is unavailable. Mounted once, in
 * `AppContent`.
 */
export function useSyncEvents() {
  const { isSignedIn } = useAuth();
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!isSignedIn || typeof ReadableStream === 'undefined' || typeof TextDecoder === 'undefined') return;
    const controller = new AbortController();
    void runSyncStream({
      url: `${API_BASE_URL_EXPORT}/sync/events`,
      getToken: getAuthToken,
      signal: controller.signal,
      onChanged: (change) => {
        for (const queryKey of queryKeysFor(change.areas)) {
          void queryClient.invalidateQueries({ queryKey });
        }
      },
    });
    return () => controller.abort();
  }, [isSignedIn, queryClient]);
}
