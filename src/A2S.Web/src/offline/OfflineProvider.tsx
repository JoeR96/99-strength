/**
 * Starts the sync manager for the app and keeps TanStack Query honest about it:
 * when a replayed op lands, the queries it touched are invalidated so the
 * server's answer replaces the optimistic value; when the server rejects one,
 * the same refetch happens and a toast names what was lost.
 */

import { useEffect, useMemo, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { OfflineContext, getDefaultSyncManager } from './context';
import { describeDrop, invalidateFor } from './reconcile';
import type { SyncManager } from './syncManager';

export interface OfflineProviderProps {
  children: ReactNode;
  /** A manager of your own (stories and tests); defaults to the app-wide one. */
  manager?: SyncManager;
}

export function OfflineProvider({ children, manager }: OfflineProviderProps) {
  const queryClient = useQueryClient();
  const sync = useMemo(() => manager ?? getDefaultSyncManager(), [manager]);

  useEffect(() => {
    const offDone = sync.on('done', ({ op }) => void invalidateFor(queryClient, op));
    const offDropped = sync.on('dropped', ({ op }) => {
      toast.error(describeDrop(op));
      void invalidateFor(queryClient, op);
    });
    const stop = sync.start();
    return () => {
      offDone();
      offDropped();
      stop();
    };
  }, [sync, queryClient]);

  return <OfflineContext.Provider value={sync}>{children}</OfflineContext.Provider>;
}
