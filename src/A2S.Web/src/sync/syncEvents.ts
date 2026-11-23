/**
 * Live updates from `GET /sync/events` (Server-Sent Events).
 *
 * The browser's `EventSource` cannot send an `Authorization` header, so the stream is read with `fetch` and a
 * `ReadableStream` instead, carrying the same Clerk bearer token as every other call. That keeps the token out of
 * URLs (server and proxy logs, history), needs no extra "stream ticket" endpoint, and gets a fresh token on every
 * reconnect. When anything fails the connection backs off and retries quietly; the app keeps working on its normal
 * refetches (window focus, mutations), so a missing stream is never an error the user sees.
 */

export type SyncArea = 'habits' | 'plan' | 'workout' | 'cardio' | 'mobility';

export interface SseMessage {
  id?: string;
  event: string;
  data: string;
}

/** Incremental SSE parser: feed it text chunks, get complete messages back (comments ignored). */
export function createSseParser() {
  let buffer = '';
  return (chunk: string): SseMessage[] => {
    buffer += chunk.replace(/\r\n?/g, '\n');
    const messages: SseMessage[] = [];
    let end = buffer.indexOf('\n\n');
    while (end >= 0) {
      const block = buffer.slice(0, end);
      buffer = buffer.slice(end + 2);
      const message: SseMessage = { event: 'message', data: '' };
      const data: string[] = [];
      for (const line of block.split('\n')) {
        if (line === '' || line.startsWith(':')) continue;
        const colon = line.indexOf(':');
        const field = colon < 0 ? line : line.slice(0, colon);
        const value = colon < 0 ? '' : line.slice(colon + 1).replace(/^ /, '');
        if (field === 'event') message.event = value;
        else if (field === 'id') message.id = value;
        else if (field === 'data') data.push(value);
      }
      if (data.length > 0) {
        message.data = data.join('\n');
        messages.push(message);
      }
      end = buffer.indexOf('\n\n');
    }
    return messages;
  };
}

/**
 * The TanStack Query key prefixes to refetch when an area changes. Week plans and the cycle live under
 * `workouts` and carry the cardio finisher, so a cardio change refreshes them too.
 */
export const AREA_QUERY_KEYS: Record<SyncArea, readonly (readonly string[])[]> = {
  habits: [['habits']],
  plan: [['plans'], ['readiness'], ['budget']],
  workout: [['workouts'], ['workout'], ['workout-history'], ['budget'], ['plans']],
  cardio: [['cardio-tracks'], ['cardio-protocols'], ['workouts'], ['plans']],
  mobility: [['mobility-routines'], ['plans']],
};

/** The distinct key prefixes for a set of changed areas (unknown areas ignored). */
export function queryKeysFor(areas: readonly string[]): string[][] {
  const seen = new Map<string, string[]>();
  for (const area of areas) {
    for (const key of AREA_QUERY_KEYS[area as SyncArea] ?? []) {
      seen.set(key.join('/'), [...key]);
    }
  }
  return [...seen.values()];
}

export interface SyncChanged {
  version: number;
  areas: string[];
}

export interface SyncStreamOptions {
  url: string;
  getToken: () => Promise<string | null>;
  onChanged: (change: SyncChanged) => void;
  signal: AbortSignal;
  fetchImpl?: typeof fetch;
  /** Waits between reconnects (ms); grows on repeated failures. */
  backoff?: { initial: number; max: number };
  sleep?: (ms: number, signal: AbortSignal) => Promise<void>;
}

const defaultSleep = (ms: number, signal: AbortSignal) =>
  new Promise<void>((resolve) => {
    const timer = setTimeout(resolve, ms);
    signal.addEventListener('abort', () => {
      clearTimeout(timer);
      resolve();
    }, { once: true });
  });

/**
 * Keeps one stream open until `signal` aborts: connects, reports `changed` events, and on any failure waits
 * (2 s, doubling to a minute) and reconnects with `Last-Event-ID`, so changes made while it was away arrive
 * straight after the server's hello. Never throws.
 */
export async function runSyncStream(options: SyncStreamOptions): Promise<void> {
  const { url, getToken, onChanged, signal } = options;
  const fetchImpl = options.fetchImpl ?? (typeof fetch === 'function' ? fetch.bind(globalThis) : undefined);
  const sleep = options.sleep ?? defaultSleep;
  const { initial, max } = options.backoff ?? { initial: 2000, max: 60000 };
  if (!fetchImpl) return;

  let lastId: string | undefined;
  let wait = initial;
  while (!signal.aborted) {
    try {
      const token = await getToken();
      if (!token) throw new Error('signed out');
      const headers: Record<string, string> = { Authorization: `Bearer ${token}`, Accept: 'text/event-stream' };
      if (lastId) headers['Last-Event-ID'] = lastId;
      const response = await fetchImpl(url, { headers, signal, cache: 'no-store' });
      if (!response.ok || !response.body) throw new Error(`sync stream ${response.status}`);

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      const parse = createSseParser();
      for (;;) {
        const { value, done } = await reader.read();
        if (done) break;
        for (const message of parse(decoder.decode(value, { stream: true }))) {
          if (message.id) lastId = message.id;
          if (message.event === 'hello') {
            wait = initial;
          } else if (message.event === 'changed') {
            const change = JSON.parse(message.data) as SyncChanged;
            onChanged({ version: change.version, areas: Array.isArray(change.areas) ? change.areas : [] });
          }
        }
      }
    } catch {
      // Offline, signed out, a proxy cutting the stream: retry later, never surface it.
    }

    if (signal.aborted) return;
    await sleep(wait, signal);
    wait = Math.min(max, wait * 2);
  }
}
