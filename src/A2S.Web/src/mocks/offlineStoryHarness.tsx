/**
 * Storybook harness for the offline journey. Gives the story its own queue and
 * sync manager, a "Go offline / Go online" switch that flips `navigator.onLine`
 * and fires the browser events (the same signal the app listens for), API
 * stubs that fail with a network error while offline, and a "Mock server" panel
 * showing what the stubbed server holds for Water so a test can watch the
 * replay land.
 */

import { useEffect, useState, type ReactNode } from 'react';
import { Wifi, WifiOff } from 'lucide-react';
import { OfflineProvider } from '@/offline/OfflineProvider';
import { createQueue } from '@/offline/queue';
import { createSyncManager } from '@/offline/syncManager';
import { WATER_ID } from './fixtures/habits';
import { installHabitsHandlers, statefulHabitsHandlers } from './handlers/habits';

const networkError = () => Object.assign(new Error('Network Error'), { isAxiosError: true, code: 'ERR_NETWORK' });

function setNavigatorOnline(online: boolean) {
  Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => online });
  window.dispatchEvent(new Event(online ? 'online' : 'offline'));
}

function restoreNavigatorOnline() {
  delete (navigator as { onLine?: boolean }).onLine;
}

/** The stubbed server's current water value for today, kept outside React so the stubs can write it. */
const serverValues = new Map<string, number>();

/** Stateful habit stubs starting with water at 0 whose writes die on the wire while offline. */
function installOfflineAwareHabits() {
  const base = statefulHabitsHandlers({ latencyMs: 0, values: { [WATER_ID]: 0 } });
  serverValues.clear();
  serverValues.set(WATER_ID, 0);
  installHabitsHandlers({
    ...base,
    increment: async (id, date, request) => {
      if (!navigator.onLine) throw networkError();
      const log = await base.increment!(id, date, request);
      serverValues.set(id, log.value);
      return log;
    },
    setLog: async (id, date, request) => {
      if (!navigator.onLine) throw networkError();
      const log = await base.setLog!(id, date, request);
      serverValues.set(id, log.value);
      return log;
    },
  });
}

let mountCounter = 0;

export function OfflineStoryHarness({ children }: { children: ReactNode }) {
  // A fresh database per mount so one story run never sees another's leftovers. The stubs go in
  // here too: they must exist before the page's first query runs, which is before any effect.
  const [manager] = useState(() => {
    installOfflineAwareHabits();
    return createSyncManager({ queue: createQueue(`a2s-offline-story-${++mountCounter}`) });
  });
  const [offline, setOffline] = useState(false);
  const [serverWater, setServerWater] = useState(0);

  useEffect(() => {
    const timer = window.setInterval(() => setServerWater(serverValues.get(WATER_ID) ?? 0), 300);
    return () => {
      window.clearInterval(timer);
      restoreNavigatorOnline();
      void manager.queue.close();
    };
  }, [manager]);

  const toggle = () => {
    const next = !offline;
    setOffline(next);
    setNavigatorOnline(!next);
  };

  return (
    <OfflineProvider manager={manager}>
      {children}
      <aside
        aria-label="Story controls"
        className="container-page mt-4 space-y-3 border-t border-border py-4"
        data-testid="offline-harness"
      >
        <button
          type="button"
          onClick={toggle}
          aria-pressed={offline}
          className="inline-flex h-11 items-center gap-2 rounded-lg border border-border bg-card px-4 text-sm font-medium text-foreground hover:bg-muted"
          data-testid="toggle-network"
        >
          {offline ? <Wifi className="h-4 w-4" aria-hidden="true" /> : <WifiOff className="h-4 w-4" aria-hidden="true" />}
          {offline ? 'Go online' : 'Go offline'}
        </button>
        <dl className="flex items-center gap-2 text-sm text-muted-foreground">
          <dt>Mock server, Water today:</dt>
          <dd className="font-semibold tabular-nums text-foreground" data-testid="mock-server-water">
            {serverWater}
          </dd>
        </dl>
        <p className="text-xs text-muted-foreground">
          {offline
            ? 'Offline: taps are queued in IndexedDB and the badge counts them.'
            : 'Online: queued taps replay in order and the server value catches up.'}
        </p>
      </aside>
    </OfflineProvider>
  );
}
