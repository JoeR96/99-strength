import { describe, expect, it, vi } from 'vitest';
import { createSseParser, queryKeysFor, runSyncStream, type SyncChanged } from './syncEvents';

function streamOf(chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk));
      controller.close();
    },
  });
}

describe('createSseParser', () => {
  it('parses events split across chunks and ignores comments', () => {
    const parse = createSseParser();

    expect(parse('retry: 5000\n\nid: 3\nevent: hel')).toEqual([]);
    expect(parse('lo\ndata: {"version":3}\n\n:keepalive\n\nid: 4\r\nevent: changed\r\ndata: {"version":4,"areas":["habits"]}\r\n\r\n')).toEqual([
      { id: '3', event: 'hello', data: '{"version":3}' },
      { id: '4', event: 'changed', data: '{"version":4,"areas":["habits"]}' },
    ]);
  });
});

describe('queryKeysFor', () => {
  it('maps areas to the query keys to refetch, without duplicates', () => {
    expect(queryKeysFor(['habits'])).toEqual([['habits']]);
    const keys = queryKeysFor(['cardio', 'workout', 'nope']).map((k) => k.join('/'));
    expect(keys).toContain('cardio-tracks');
    expect(keys).toContain('workouts');
    expect(new Set(keys).size).toBe(keys.length);
  });
});

describe('runSyncStream', () => {
  it('sends the bearer token, reports changed events, and resumes with Last-Event-ID', async () => {
    const controller = new AbortController();
    const changes: SyncChanged[] = [];
    const calls: RequestInit[] = [];
    const fetchImpl = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      calls.push(init ?? {});
      if (calls.length === 1) {
        return new Response(streamOf([
          'id: 7\nevent: hello\ndata: {"version":7}\n\n',
          'id: 8\nevent: changed\ndata: {"version":8,"areas":["habits"]}\n\n',
        ]), { status: 200 });
      }
      controller.abort();
      return new Response(null, { status: 503 });
    });

    await runSyncStream({
      url: '/api/v1/sync/events',
      getToken: async () => 'tok',
      onChanged: (c) => changes.push(c),
      signal: controller.signal,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      sleep: async () => {},
    });

    expect(changes).toEqual([{ version: 8, areas: ['habits'] }]);
    expect((calls[0].headers as Record<string, string>).Authorization).toBe('Bearer tok');
    expect((calls[1].headers as Record<string, string>)['Last-Event-ID']).toBe('8');
  });

  it('fails silently and backs off when the stream is unavailable', async () => {
    const controller = new AbortController();
    const waits: number[] = [];
    const fetchImpl = vi.fn(async () => {
      throw new TypeError('Failed to fetch');
    });

    await runSyncStream({
      url: '/x',
      getToken: async () => 'tok',
      onChanged: () => {},
      signal: controller.signal,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      backoff: { initial: 100, max: 400 },
      sleep: async (ms) => {
        waits.push(ms);
        if (waits.length === 4) controller.abort();
      },
    });

    expect(waits).toEqual([100, 200, 400, 400]);
  });

  it('does not connect without a token', async () => {
    const controller = new AbortController();
    const fetchImpl = vi.fn();

    await runSyncStream({
      url: '/x',
      getToken: async () => null,
      onChanged: () => {},
      signal: controller.signal,
      fetchImpl: fetchImpl as unknown as typeof fetch,
      sleep: async () => controller.abort(),
    });

    expect(fetchImpl).not.toHaveBeenCalled();
  });
});
