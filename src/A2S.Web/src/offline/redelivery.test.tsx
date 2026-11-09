/**
 * The server recognises a tap delivered twice by its `clientTimestamp`: a tap whose
 * timestamp equals the stored log's last-write timestamp is a duplicate and changes
 * nothing. That only works when a retry is the same request, byte for byte: the
 * timestamp is taken once, at the tap, and never again when the op is resent.
 * These tests go down to the HTTP client, so they see the URL and body on the wire.
 */

import { describe, it, expect, vi, afterEach, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import apiClient from '@/api/apiClient';
import { useIncrementHabit } from '@/hooks/useHabits';
import { OfflineContext } from './context';
import { createQueue, type OfflineQueue } from './queue';
import { createSyncManager, type SyncManager } from './syncManager';
import type { NewOfflineOp } from './types';

vi.mock('@/api/apiClient', () => {
  const client = { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() };
  return { default: client, apiClient: client };
});

vi.mock('@/lib/localDate', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/lib/localDate')>();
  return { ...actual, todayLocal: vi.fn(() => '2026-11-09') };
});

vi.mock('react-hot-toast', () => ({ default: { error: vi.fn(), success: vi.fn() } }));

const WATER = 'habit-water';
const URL = `/habits/${WATER}/logs/2026-11-09/increment`;
const TAPPED_AT = '2026-11-09T23:58:10.123Z';

const networkError = () => Object.assign(new Error('Network Error'), { isAxiosError: true, code: 'ERR_NETWORK' });
const ok = (value: number) => ({ data: { habitId: WATER, date: '2026-11-09', value, updatedAtUtc: TAPPED_AT } });

const waterTap: NewOfflineOp = {
  kind: 'habit.increment',
  localDate: '2026-11-09',
  target: { habitId: WATER },
  payload: { delta: 1, clientTimestamp: TAPPED_AT },
  label: 'Water',
};

/** Lets the manager's IndexedDB steps and promise chains run. */
async function settle(steps = 20) {
  for (let i = 0; i < steps; i++) await new Promise<void>((resolve) => setTimeout(resolve, 0));
}

describe('a retried tap is the same request', () => {
  const post = vi.mocked(apiClient.post);
  const opened: OfflineQueue[] = [];
  const managers: SyncManager[] = [];
  let online = true;

  const open = (name: string) => {
    const queue = createQueue(name);
    opened.push(queue);
    // No `execute` override: ops go through the real executeOp and habitsApi to the HTTP client.
    const manager = createSyncManager({ queue, isOnline: () => online });
    managers.push(manager);
    return { queue, manager };
  };
  const freshName = () => `redelivery-${Math.random().toString(36).slice(2)}`;

  beforeEach(() => {
    online = true;
    post.mockReset();
  });

  afterEach(async () => {
    for (const m of managers.splice(0)) m.stop();
    for (const q of opened.splice(0)) await q.close();
    vi.useRealTimers();
  });

  it('a queued tap whose response was lost is resent with the identical URL and body, minutes later', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-11-09T23:58:11Z'));
    // The first delivery reaches the server but its answer never comes back.
    post.mockRejectedValueOnce(networkError()).mockResolvedValueOnce(ok(3));
    const { manager, queue } = open(freshName());
    online = false;
    await manager.enqueue(waterTap);
    online = true;

    await manager.replayNow();
    expect(post).toHaveBeenCalledTimes(1);
    expect(manager.getPendingCount()).toBe(1);
    expect((await queue.peek())?.attempts).toBe(1);

    // Past midnight, long after the tap: the retry must not pick up the clock.
    vi.setSystemTime(new Date('2026-11-10T00:07:00Z'));
    await manager.replayNow();
    await settle();

    expect(post).toHaveBeenCalledTimes(2);
    const [first, retry] = post.mock.calls;
    expect(first).toEqual([URL, { delta: 1, clientTimestamp: TAPPED_AT }]);
    expect(retry).toEqual(first);
    expect(JSON.stringify(retry[1])).toBe(JSON.stringify(first[1]));
    expect(manager.getPendingCount()).toBe(0);
  });

  it('every backoff retry repeats the same body; only the attempt counter moves', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-11-09T23:58:11Z'));
    post
      .mockRejectedValueOnce(networkError())
      .mockRejectedValueOnce(Object.assign(new Error('Bad Gateway'), { isAxiosError: true, response: { status: 502 } }))
      .mockResolvedValueOnce(ok(3));
    const { manager, queue } = open(freshName());
    online = false;
    const queued = await manager.enqueue(waterTap);
    online = true;

    await manager.replayNow();
    await vi.advanceTimersByTimeAsync(1_000);
    await settle();
    const waiting = await queue.peek();
    await vi.advanceTimersByTimeAsync(2_000);
    await settle();

    expect(post).toHaveBeenCalledTimes(3);
    expect(new Set(post.mock.calls.map((call) => JSON.stringify(call))).size).toBe(1);
    expect(waiting).toEqual({ ...queued, attempts: 2 });
    expect(manager.getPendingCount()).toBe(0);
  });

  it('the app is killed mid-request: after a restart the stored op is sent again unchanged', async () => {
    const name = freshName();
    // The request leaves and the app dies before any answer: this promise never settles.
    post.mockReturnValueOnce(new Promise(() => {}));
    const before = open(name);
    await before.manager.enqueue(waterTap);
    await settle();
    expect(post).toHaveBeenCalledTimes(1);
    before.manager.stop();
    await before.queue.close();

    post.mockResolvedValueOnce(ok(3));
    const after = open(name);
    expect(await after.manager.refreshPending()).toBe(1);
    await after.manager.replayNow();

    expect(post).toHaveBeenCalledTimes(2);
    expect(post.mock.calls[1]).toEqual(post.mock.calls[0]);
    expect(post.mock.calls[1]).toEqual([URL, { delta: 1, clientTimestamp: TAPPED_AT }]);
    expect(after.manager.getPendingCount()).toBe(0);
  });

  it('an online tap that dies on the wire is queued and resent with the timestamp taken at the tap', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const tappedAt = new Date('2026-11-09T12:00:00.250Z');
    vi.setSystemTime(tappedAt);
    post
      .mockImplementationOnce(async () => {
        // The request hangs for a minute and a half before the connection gives up.
        vi.setSystemTime(new Date(tappedAt.getTime() + 90_000));
        throw networkError();
      })
      .mockResolvedValueOnce(ok(3));
    const { manager } = open(freshName());
    const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>
        <OfflineContext.Provider value={manager}>{children}</OfflineContext.Provider>
      </QueryClientProvider>
    );
    const { result } = renderHook(() => useIncrementHabit(), { wrapper });

    act(() => result.current.tap(WATER, 1));

    // First the direct attempt, then the queue's replay of the op it left behind.
    await waitFor(() => expect(post).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(manager.getPendingCount()).toBe(0));
    const [direct, replayed] = post.mock.calls;
    expect(direct).toEqual([URL, { delta: 1, clientTimestamp: tappedAt.toISOString() }]);
    expect(replayed).toEqual(direct);
  });
});
