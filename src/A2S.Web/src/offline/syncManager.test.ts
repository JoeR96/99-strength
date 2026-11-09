import { describe, it, expect, vi, afterEach, beforeEach } from 'vitest';
import { createQueue, type OfflineQueue } from './queue';
import { backoffMs, createSyncManager, type SyncManager } from './syncManager';
import type { NewOfflineOp, OfflineOp } from './types';

const httpError = (status: number) => Object.assign(new Error(`Request failed with status code ${status}`), { isAxiosError: true, response: { status } });
const networkError = () => Object.assign(new Error('Network Error'), { isAxiosError: true, code: 'ERR_NETWORK' });

const tap = (habitId: string, localDate: string, clientTimestamp: string): NewOfflineOp => ({
  kind: 'habit.increment',
  localDate,
  target: { habitId },
  payload: { delta: 1, clientTimestamp },
  label: habitId === 'water' ? 'Water' : habitId,
});

const completion = (localDate: string): NewOfflineOp => ({
  kind: 'plan.complete',
  localDate,
  target: { planId: 'plan-1' },
  payload: { date: localDate, kind: 2, cardioProtocolId: 'cardio-1' },
  label: 'Norwegian 4×4',
});

/** Drains every pending microtask/timer step the manager awaits on fake-indexeddb. */
async function settle(steps = 20) {
  for (let i = 0; i < steps; i++) await new Promise<void>((resolve) => setTimeout(resolve, 0));
}

describe('sync manager', () => {
  const opened: OfflineQueue[] = [];
  const managers: SyncManager[] = [];
  let online = true;

  const fresh = (name = `sync-${Math.random().toString(36).slice(2)}`) => {
    const queue = createQueue(name);
    opened.push(queue);
    return queue;
  };

  const manager = (queue: OfflineQueue, execute: (op: OfflineOp) => Promise<unknown>) => {
    const m = createSyncManager({ queue, execute, isOnline: () => online, intervalMs: 30_000 });
    managers.push(m);
    return m;
  };

  beforeEach(() => {
    online = true;
  });

  afterEach(async () => {
    for (const m of managers.splice(0)) m.stop();
    for (const q of opened.splice(0)) await q.close();
    vi.useRealTimers();
  });

  it('backoff doubles from 1 s and caps at five minutes', () => {
    expect(backoffMs(1)).toBe(1_000);
    expect(backoffMs(2)).toBe(2_000);
    expect(backoffMs(3)).toBe(4_000);
    expect(backoffMs(20)).toBe(5 * 60_000);
  });

  it('two water taps queued at 23:58 replay after midnight onto yesterday, unchanged', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-11-09T23:58:00Z'));
    const execute = vi.fn().mockResolvedValue({});
    const m = manager(fresh(), execute);
    online = false;

    await m.enqueue(tap('water', '2026-11-09', '2026-11-09T23:58:10.000Z'));
    await m.enqueue(tap('water', '2026-11-09', '2026-11-09T23:58:40.000Z'));
    expect(execute).not.toHaveBeenCalled();
    expect(m.getPendingCount()).toBe(2);

    vi.setSystemTime(new Date('2026-11-10T00:05:00Z'));
    online = true;
    await m.replayNow();

    expect(execute).toHaveBeenCalledTimes(2);
    const sent = execute.mock.calls.map(([op]) => op as OfflineOp);
    expect(sent.map((o) => o.localDate)).toEqual(['2026-11-09', '2026-11-09']);
    expect(sent.map((o) => (o as Extract<OfflineOp, { kind: 'habit.increment' }>).payload.clientTimestamp)).toEqual([
      '2026-11-09T23:58:10.000Z',
      '2026-11-09T23:58:40.000Z',
    ]);
    expect(m.getPendingCount()).toBe(0);
  });

  it('replays strictly FIFO across kinds; a 5xx blocks the queue and later ops wait', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const seen: string[] = [];
    let completionFails = true;
    const execute = vi.fn(async (op: OfflineOp) => {
      seen.push(op.kind);
      if (op.kind === 'plan.complete' && completionFails) throw httpError(503);
      return {};
    });
    const m = manager(fresh(), execute);
    const deferred = vi.fn();
    m.on('deferred', deferred);
    online = false;
    await m.enqueue(tap('water', '2026-11-09', '2026-11-09T09:00:00.000Z'));
    await m.enqueue(completion('2026-11-09'));
    await m.enqueue(tap('walks', '2026-11-09', '2026-11-09T09:01:00.000Z'));
    online = true;

    await m.replayNow();

    // The tap went, the completion failed and the walk is still waiting behind it.
    expect(seen).toEqual(['habit.increment', 'plan.complete']);
    expect(m.getPendingCount()).toBe(2);
    expect(deferred).toHaveBeenCalledWith(expect.objectContaining({ retryInMs: 1_000, op: expect.objectContaining({ attempts: 1 }) }));

    // Still failing after 1 s: attempt 2, next wait 2 s.
    await vi.advanceTimersByTimeAsync(1_000);
    await settle();
    expect(seen).toEqual(['habit.increment', 'plan.complete', 'plan.complete']);
    expect((await m.queue.peek())?.attempts).toBe(2);

    completionFails = false;
    await vi.advanceTimersByTimeAsync(2_000);
    await settle();
    expect(seen).toEqual(['habit.increment', 'plan.complete', 'plan.complete', 'plan.complete', 'habit.increment']);
    expect(m.getPendingCount()).toBe(0);
  });

  it('a 4xx drops the op, reports it with its label and date, and moves on', async () => {
    const execute = vi.fn(async (op: OfflineOp) => {
      if (op.kind === 'habit.increment' && op.target.habitId === 'water') throw httpError(404);
      return { ok: true };
    });
    const m = manager(fresh(), execute);
    const dropped = vi.fn();
    const done = vi.fn();
    m.on('dropped', dropped);
    m.on('done', done);
    online = false;
    await m.enqueue(tap('water', '2026-11-09', '2026-11-09T09:00:00.000Z'));
    await m.enqueue(tap('walks', '2026-11-09', '2026-11-09T09:01:00.000Z'));
    online = true;

    await m.replayNow();

    expect(dropped).toHaveBeenCalledTimes(1);
    expect(dropped.mock.calls[0][0].op).toMatchObject({ label: 'Water', localDate: '2026-11-09' });
    expect(done).toHaveBeenCalledTimes(1);
    expect(done.mock.calls[0][0].op).toMatchObject({ target: { habitId: 'walks' } });
    expect(m.getPendingCount()).toBe(0);
  });

  it('a network error during replay keeps the op and retries', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let failing = true;
    const execute = vi.fn(async () => {
      if (failing) throw networkError();
      return {};
    });
    const m = manager(fresh(), execute);
    online = false;
    await m.enqueue(tap('water', '2026-11-09', '2026-11-09T09:00:00.000Z'));
    online = true;

    await m.replayNow();
    expect(m.getPendingCount()).toBe(1);

    failing = false;
    await vi.advanceTimersByTimeAsync(1_000);
    await settle();
    expect(m.getPendingCount()).toBe(0);
  });

  it('does nothing while offline and replays on the online event once started', async () => {
    const execute = vi.fn().mockResolvedValue({});
    const m = manager(fresh(), execute);
    online = false;
    m.start();
    await m.enqueue(tap('water', '2026-11-09', '2026-11-09T09:00:00.000Z'));
    await m.replayNow();
    expect(execute).not.toHaveBeenCalled();

    online = true;
    window.dispatchEvent(new Event('online'));
    await settle();

    expect(execute).toHaveBeenCalledTimes(1);
    expect(m.getPendingCount()).toBe(0);
  });

  it('enqueueing while online replays straight away (the request itself had just failed)', async () => {
    const execute = vi.fn().mockResolvedValue({});
    const m = manager(fresh(), execute);
    const counts: number[] = [];
    m.onPendingChange((n) => counts.push(n));

    await m.enqueue(tap('water', '2026-11-09', '2026-11-09T09:00:00.000Z'));
    await settle();

    expect(execute).toHaveBeenCalledTimes(1);
    expect(counts).toEqual([1, 0]);
  });

  it('a new manager over the same database picks the queue up where the old one left it', async () => {
    const name = `sync-reload-${Math.random().toString(36).slice(2)}`;
    const first = manager(fresh(name), vi.fn().mockResolvedValue({}));
    online = false;
    await first.enqueue(tap('water', '2026-11-09', '2026-11-09T09:00:00.000Z'));
    await first.enqueue(tap('walks', '2026-11-09', '2026-11-09T09:01:00.000Z'));
    first.stop();
    await opened.pop()!.close();

    // "Service worker updated, page reloaded": fresh manager, same IndexedDB.
    const execute = vi.fn().mockResolvedValue({});
    const second = manager(fresh(name), execute);
    expect(second.getPendingCount()).toBe(0); // not read yet
    expect(await second.refreshPending()).toBe(2);
    online = true;
    await second.replayNow();

    expect(execute.mock.calls.map(([op]) => (op as OfflineOp).target)).toEqual([{ habitId: 'water' }, { habitId: 'walks' }]);
    expect(second.getPendingCount()).toBe(0);
  });
});
