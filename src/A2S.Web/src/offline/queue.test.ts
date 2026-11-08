import { describe, it, expect, afterEach } from 'vitest';
import { createQueue, type OfflineQueue } from './queue';
import type { NewOfflineOp } from './types';

const tap = (habitId: string, date = '2026-11-09'): NewOfflineOp => ({
  kind: 'habit.increment',
  localDate: date,
  target: { habitId },
  payload: { delta: 1, clientTimestamp: '2026-11-09T08:00:00.000Z' },
});

describe('offline queue (IndexedDB)', () => {
  const open: OfflineQueue[] = [];
  const fresh = () => {
    const queue = createQueue(`queue-test-${Math.random().toString(36).slice(2)}`);
    open.push(queue);
    return queue;
  };

  afterEach(async () => {
    for (const queue of open.splice(0)) await queue.close();
  });

  it('fills in id, createdAt and attempts, and counts', async () => {
    const queue = fresh();
    const op = await queue.enqueue(tap('water'));
    expect(op.id).toMatch(/\w+/);
    expect(op.createdAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    expect(op.attempts).toBe(0);
    expect(await queue.count()).toBe(1);
  });

  it('peek returns the oldest op and all() keeps FIFO order', async () => {
    const queue = fresh();
    const a = await queue.enqueue(tap('water'));
    const b = await queue.enqueue(tap('walks'));
    const c = await queue.enqueue({ kind: 'bodyweight.put', localDate: '2026-11-09', target: {}, payload: { kg: 94.2 } });

    expect((await queue.peek())?.id).toBe(a.id);
    expect((await queue.all()).map((o) => o.id)).toEqual([a.id, b.id, c.id]);
  });

  it('remove takes one op out by id; the next becomes the head', async () => {
    const queue = fresh();
    const a = await queue.enqueue(tap('water'));
    const b = await queue.enqueue(tap('walks'));
    await queue.remove(a.id);
    await queue.remove('not-there');

    expect((await queue.peek())?.id).toBe(b.id);
    expect(await queue.count()).toBe(1);
  });

  it('update replaces the stored copy without changing its place in the line', async () => {
    const queue = fresh();
    const a = await queue.enqueue(tap('water'));
    await queue.enqueue(tap('walks'));
    await queue.update({ ...a, attempts: 3 });

    const head = await queue.peek();
    expect(head?.id).toBe(a.id);
    expect(head?.attempts).toBe(3);
    expect(await queue.count()).toBe(2);
  });

  it('survives closing and reopening the same database', async () => {
    const name = `queue-persist-${Math.random().toString(36).slice(2)}`;
    const first = createQueue(name);
    const a = await first.enqueue(tap('water'));
    await first.close();

    const second = createQueue(name);
    open.push(second);
    expect((await second.all()).map((o) => o.id)).toEqual([a.id]);
  });

  it('clear empties the store', async () => {
    const queue = fresh();
    await queue.enqueue(tap('water'));
    await queue.clear();
    expect(await queue.count()).toBe(0);
    expect(await queue.peek()).toBeUndefined();
  });
});
