/**
 * Where hooks find the sync manager. Without an `OfflineProvider` (tests, most
 * stories) they fall back to one shared manager over the real `a2s-offline`
 * database, which queues and counts but never starts its listeners.
 */

import { createContext, useContext, useSyncExternalStore } from 'react';
import { createQueue } from './queue';
import { createSyncManager, type SyncManager } from './syncManager';

let defaultManager: SyncManager | null = null;

export function getDefaultSyncManager(): SyncManager {
  return (defaultManager ??= createSyncManager({ queue: createQueue() }));
}

export const OfflineContext = createContext<SyncManager | null>(null);

export function useOfflineSync(): SyncManager {
  return useContext(OfflineContext) ?? getDefaultSyncManager();
}

/** How many writes are waiting to reach the server; 0 when everything is through. */
export function usePendingSyncCount(): number {
  const sync = useOfflineSync();
  return useSyncExternalStore(sync.onPendingChange, sync.getPendingCount, () => 0);
}
