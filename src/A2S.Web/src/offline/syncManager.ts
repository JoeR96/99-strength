/**
 * Replays the queue in order. One op at a time, oldest first; a network or 5xx
 * failure keeps the op at the head and waits (1 s, 2 s, 4 s … capped at five
 * minutes) so later ops cannot overtake it. A 4xx means the server will never
 * accept the write, so the op is dropped and reported. Replay runs when the
 * browser comes back online, when the app regains focus or becomes visible, on a
 * 30 s timer, and right after an op is queued while online (the request itself
 * failed, the connection may already be back).
 */

import { executeOp } from './executeOp';
import { isRetryable } from './errors';
import type { OfflineQueue } from './queue';
import type { NewOfflineOp, OfflineOp } from './types';

export type SyncEvent = 'done' | 'dropped' | 'deferred';

export interface SyncEventPayload {
  done: { op: OfflineOp; result: unknown };
  dropped: { op: OfflineOp; error: unknown };
  deferred: { op: OfflineOp; error: unknown; retryInMs: number };
}

export interface SyncManagerOptions {
  queue: OfflineQueue;
  /** Runs one op against the API; defaults to the real API modules. */
  execute?: (op: OfflineOp) => Promise<unknown>;
  /** Whether a request stands a chance right now; defaults to `navigator.onLine`. */
  isOnline?: () => boolean;
  /** Periodic replay interval. */
  intervalMs?: number;
  baseBackoffMs?: number;
  maxBackoffMs?: number;
}

export interface SyncManager {
  readonly queue: OfflineQueue;
  /** Attaches the online/focus/visibility listeners and the timer; returns `stop`. */
  start(): () => void;
  stop(): void;
  /** Queues an op, updates the pending count and kicks a replay when online. */
  enqueue(op: NewOfflineOp): Promise<OfflineOp>;
  /** Drains what it can now; resolves when the queue is empty or blocked. */
  replayNow(): Promise<void>;
  isOnline(): boolean;
  getPendingCount(): number;
  /** Re-reads the count from the store (after a reload, for instance). */
  refreshPending(): Promise<number>;
  onPendingChange(callback: (count: number) => void): () => void;
  on<E extends SyncEvent>(event: E, callback: (payload: SyncEventPayload[E]) => void): () => void;
}

export const DEFAULT_INTERVAL_MS = 30_000;
export const DEFAULT_BASE_BACKOFF_MS = 1_000;
export const DEFAULT_MAX_BACKOFF_MS = 5 * 60_000;

/** 1 s, 2 s, 4 s … for attempts 1, 2, 3 …, never above `max`. */
export function backoffMs(attempts: number, base = DEFAULT_BASE_BACKOFF_MS, max = DEFAULT_MAX_BACKOFF_MS): number {
  return Math.min(max, base * 2 ** Math.max(0, attempts - 1));
}

export function browserIsOnline(): boolean {
  return typeof navigator === 'undefined' || navigator.onLine !== false;
}

export function createSyncManager(options: SyncManagerOptions): SyncManager {
  const {
    queue,
    execute = executeOp,
    isOnline = browserIsOnline,
    intervalMs = DEFAULT_INTERVAL_MS,
    baseBackoffMs = DEFAULT_BASE_BACKOFF_MS,
    maxBackoffMs = DEFAULT_MAX_BACKOFF_MS,
  } = options;

  let pending = 0;
  const pendingListeners = new Set<(count: number) => void>();
  const listeners: { [E in SyncEvent]: Set<(payload: SyncEventPayload[E]) => void> } = {
    done: new Set(),
    dropped: new Set(),
    deferred: new Set(),
  };

  let replaying: Promise<void> | null = null;
  let rerun = false;
  let retryTimer: ReturnType<typeof setTimeout> | null = null;
  let intervalTimer: ReturnType<typeof setInterval> | null = null;
  let stop: (() => void) | null = null;

  const emit = <E extends SyncEvent>(event: E, payload: SyncEventPayload[E]) => {
    for (const cb of listeners[event]) cb(payload);
  };

  const setPending = (count: number) => {
    if (count === pending) return;
    pending = count;
    for (const cb of pendingListeners) cb(count);
  };

  const refreshPending = async () => {
    const count = await queue.count();
    setPending(count);
    return count;
  };

  const clearRetry = () => {
    if (retryTimer !== null) {
      clearTimeout(retryTimer);
      retryTimer = null;
    }
  };

  const scheduleRetry = (ms: number) => {
    clearRetry();
    retryTimer = setTimeout(() => {
      retryTimer = null;
      void replayNow();
    }, ms);
  };

  async function drain(): Promise<void> {
    for (;;) {
      if (!isOnline()) return;
      const op = await queue.peek();
      if (!op) return;

      try {
        const result = await execute(op);
        await queue.remove(op.id);
        await refreshPending();
        emit('done', { op, result });
      } catch (error) {
        if (isRetryable(error)) {
          const next = { ...op, attempts: op.attempts + 1 } as OfflineOp;
          await queue.update(next);
          const retryInMs = backoffMs(next.attempts, baseBackoffMs, maxBackoffMs);
          emit('deferred', { op: next, error, retryInMs });
          scheduleRetry(retryInMs);
          return;
        }
        await queue.remove(op.id);
        await refreshPending();
        emit('dropped', { op, error });
      }
    }
  }

  function replayNow(): Promise<void> {
    if (replaying) {
      rerun = true;
      return replaying;
    }
    clearRetry();
    replaying = drain()
      .catch(() => undefined)
      .finally(() => {
        replaying = null;
        if (rerun) {
          rerun = false;
          void replayNow();
        }
      });
    return replaying;
  }

  const onOnline = () => void replayNow();
  const onVisible = () => {
    if (typeof document === 'undefined' || document.visibilityState === 'visible') void replayNow();
  };

  return {
    queue,

    start() {
      if (stop) return stop;
      if (typeof window !== 'undefined') {
        window.addEventListener('online', onOnline);
        window.addEventListener('focus', onOnline);
        document.addEventListener('visibilitychange', onVisible);
      }
      intervalTimer = setInterval(() => void replayNow(), intervalMs);
      void refreshPending().then(() => replayNow());
      stop = () => {
        if (typeof window !== 'undefined') {
          window.removeEventListener('online', onOnline);
          window.removeEventListener('focus', onOnline);
          document.removeEventListener('visibilitychange', onVisible);
        }
        if (intervalTimer !== null) clearInterval(intervalTimer);
        intervalTimer = null;
        clearRetry();
        stop = null;
      };
      return stop;
    },

    stop() {
      stop?.();
    },

    async enqueue(op) {
      const stored = await queue.enqueue(op);
      await refreshPending();
      if (isOnline()) void replayNow();
      return stored;
    },

    replayNow,
    isOnline: () => isOnline(),
    getPendingCount: () => pending,
    refreshPending,

    onPendingChange(callback) {
      pendingListeners.add(callback);
      return () => {
        pendingListeners.delete(callback);
      };
    },

    on(event, callback) {
      listeners[event].add(callback);
      return () => {
        listeners[event].delete(callback);
      };
    },
  };
}
