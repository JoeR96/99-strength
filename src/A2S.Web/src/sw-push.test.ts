import { describe, it, expect, vi } from 'vitest';
import { DEFAULT_TITLE, installPushHandlers, notificationOptions, parsePushPayload, targetUrl } from './sw-push';

describe('sw-push helpers', () => {
  it('parses the server JSON payload', () => {
    expect(parsePushPayload('{"title":"Time to train — Upper A","body":"Planned for 17:30.","url":"/today","tag":"SessionStart"}')).toEqual({
      title: 'Time to train — Upper A',
      body: 'Planned for 17:30.',
      url: '/today',
      tag: 'SessionStart',
    });
  });

  it('falls back to plain text as the title, and to the app name when empty', () => {
    expect(parsePushPayload('hello')).toEqual({ title: 'hello' });
    expect(parsePushPayload(null)).toEqual({ title: DEFAULT_TITLE });
    expect(parsePushPayload('{"body":"only"}').title).toBe(DEFAULT_TITLE);
  });

  it('builds notification options with the tag and the target url', () => {
    const options = notificationOptions({ title: 't', body: 'b', tag: 'HabitsUnderMinimum' });
    expect(options.tag).toBe('HabitsUnderMinimum');
    expect(options.data.url).toBe('/today');
    expect(options.icon).toBe('/icons/icon-192.png');
  });

  it('resolves the click target on the app origin and refuses foreign origins', () => {
    expect(targetUrl({ url: '/habits' }, 'https://99-strength.enjoeneer.dev')).toBe('https://99-strength.enjoeneer.dev/habits');
    expect(targetUrl({ url: 'https://evil.example.com/x' }, 'https://99-strength.enjoeneer.dev')).toBe('https://99-strength.enjoeneer.dev/today');
    expect(targetUrl(undefined, 'https://99-strength.enjoeneer.dev')).toBe('https://99-strength.enjoeneer.dev/today');
  });
});

describe('installPushHandlers', () => {
  function fakeWorker() {
    const listeners = new Map<string, (event: unknown) => void>();
    const showNotification = vi.fn().mockResolvedValue(undefined);
    const focus = vi.fn().mockResolvedValue(undefined);
    const navigate = vi.fn().mockResolvedValue(undefined);
    const openWindow = vi.fn().mockResolvedValue(undefined);
    const windows: Array<{ url: string; focus: typeof focus; navigate?: typeof navigate }> = [];
    const sw = {
      addEventListener: (type: string, cb: (event: unknown) => void) => listeners.set(type, cb),
      registration: { showNotification },
      location: { origin: 'https://99-strength.enjoeneer.dev' },
      clients: { matchAll: vi.fn().mockImplementation(async () => windows), openWindow },
    } as unknown as ServiceWorkerGlobalScope;
    installPushHandlers(sw);
    return { listeners, showNotification, focus, navigate, openWindow, windows };
  }

  it('push shows a notification built from the payload', async () => {
    const { listeners, showNotification } = fakeWorker();
    let pending: Promise<unknown> | undefined;
    listeners.get('push')!({
      data: { text: () => '{"title":"Still under minimum","body":"Water 2/4","url":"/today","tag":"HabitsUnderMinimum"}' },
      waitUntil: (p: Promise<unknown>) => {
        pending = p;
      },
    });
    await pending;
    expect(showNotification).toHaveBeenCalledWith('Still under minimum', expect.objectContaining({ body: 'Water 2/4', tag: 'HabitsUnderMinimum', data: { url: '/today' } }));
  });

  it('notificationclick focuses an open app window and navigates it, else opens one', async () => {
    const { listeners, focus, navigate, openWindow, windows } = fakeWorker();
    const close = vi.fn();
    const click = (url: string) => {
      let pending: Promise<unknown> | undefined;
      listeners.get('notificationclick')!({
        notification: { close, data: { url } },
        waitUntil: (p: Promise<unknown>) => {
          pending = p;
        },
      });
      return pending;
    };

    await click('/habits');
    expect(close).toHaveBeenCalled();
    expect(openWindow).toHaveBeenCalledWith('https://99-strength.enjoeneer.dev/habits');

    windows.push({ url: 'https://99-strength.enjoeneer.dev/settings', focus, navigate });
    await click('/today');
    expect(focus).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith('https://99-strength.enjoeneer.dev/today');
    expect(openWindow).toHaveBeenCalledTimes(1);
  });
});
