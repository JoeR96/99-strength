import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { pushApi } from '@/api/push';
import { fakePushClient, installPushHandlers, pushHandlers, STORY_ENDPOINT } from '@/mocks/handlers/push';
import { PushSettings } from './PushSettings';
import { toTimeInput } from './pushKeys';
import { detectPushSupport, readSupportEnv, urlBase64ToUint8Array, type SupportEnv } from './pushSupport';

vi.mock('react-hot-toast', () => ({ default: { success: vi.fn(), error: vi.fn() } }));

const desktopChrome: SupportEnv = { hasPushManager: true, hasServiceWorker: true, hasNotification: true, isIos: false, standalone: false };
const iosSafariTab: SupportEnv = { hasPushManager: false, hasServiceWorker: true, hasNotification: false, isIos: true, standalone: false };
const iosStandalone: SupportEnv = { hasPushManager: true, hasServiceWorker: true, hasNotification: true, isIos: true, standalone: true };
const oldBrowser: SupportEnv = { hasPushManager: false, hasServiceWorker: false, hasNotification: false, isIos: false, standalone: false };

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('push support matrix', () => {
  it('desktop Chrome: supported', () => expect(detectPushSupport(desktopChrome)).toBe('supported'));
  it('iOS Safari in a tab: needs installing first', () => expect(detectPushSupport(iosSafariTab)).toBe('ios-needs-install'));
  it('iOS Safari installed to the Home Screen: supported', () => expect(detectPushSupport(iosStandalone)).toBe('supported'));
  it('a browser without PushManager: unsupported', () => expect(detectPushSupport(oldBrowser)).toBe('unsupported'));

  it('readSupportEnv reads iOS from the user agent and standalone from navigator.standalone', () => {
    const win = {
      navigator: { userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Safari/604.1', standalone: true, serviceWorker: {} },
      matchMedia: () => ({ matches: false }),
      PushManager: function PushManager() {},
      Notification: function Notification() {},
    } as unknown as Window;
    expect(readSupportEnv(win)).toEqual({ hasPushManager: true, hasServiceWorker: true, hasNotification: true, isIos: true, standalone: true });
  });

  it('readSupportEnv treats a touch MacIntel as iPadOS and a plain desktop as not iOS', () => {
    const ipad = { navigator: { userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X) Safari', platform: 'MacIntel', maxTouchPoints: 5 }, matchMedia: () => ({ matches: false }) } as unknown as Window;
    const mac = { navigator: { userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X) Chrome', platform: 'MacIntel', maxTouchPoints: 0 }, matchMedia: () => ({ matches: false }) } as unknown as Window;
    expect(readSupportEnv(ipad).isIos).toBe(true);
    expect(readSupportEnv(mac).isIos).toBe(false);
  });

  it('decodes a base64url key to bytes', () => {
    expect(Array.from(urlBase64ToUint8Array('AQID'))).toEqual([1, 2, 3]);
    expect(Array.from(urlBase64ToUint8Array('_-8'))).toEqual([255, 239]);
  });

  it('toTimeInput trims seconds and defaults to 17:30', () => {
    expect(toTimeInput('06:45:00')).toBe('06:45');
    expect(toTimeInput(null)).toBe('17:30');
  });
});

describe('PushSettings', () => {
  beforeEach(() => installPushHandlers(pushHandlers.default()));

  it('iOS Safari in a tab shows the install steps and no subscribe button', () => {
    render(<PushSettings env={iosSafariTab} client={fakePushClient()} />, { wrapper });
    expect(screen.getByTestId('push-install-steps')).toHaveTextContent(/add to home screen/i);
    expect(screen.queryByTestId('push-enable')).not.toBeInTheDocument();
  });

  it('an unsupported browser says so', () => {
    render(<PushSettings env={oldBrowser} client={fakePushClient()} />, { wrapper });
    expect(screen.getByTestId('push-unsupported')).toBeInTheDocument();
  });

  it('desktop Chrome: turning on asks permission, subscribes with the server key and stores it', async () => {
    const client = fakePushClient();
    const subscribeSpy = vi.spyOn(pushApi, 'subscribe');
    render(<PushSettings env={desktopChrome} client={client} />, { wrapper });

    fireEvent.click(await screen.findByTestId('push-enable'));

    await waitFor(() => expect(subscribeSpy).toHaveBeenCalledTimes(1));
    expect(subscribeSpy.mock.calls[0][0]).toMatchObject({
      endpoint: STORY_ENDPOINT,
      p256dh: 'story-p256dh',
      auth: 'story-auth',
      sessionStartTime: '17:30',
    });
    expect(await screen.findByTestId('push-enabled')).toBeInTheDocument();
    expect(screen.getByLabelText(/training nudge time/i)).toHaveValue('17:30');
  });

  it('permission refused: nothing is stored and the button stays', async () => {
    const client = fakePushClient({ grant: 'denied' });
    const subscribeSpy = vi.spyOn(pushApi, 'subscribe');
    render(<PushSettings env={desktopChrome} client={client} />, { wrapper });

    fireEvent.click(await screen.findByTestId('push-enable'));

    await waitFor(() => expect(screen.getByTestId('push-denied')).toBeInTheDocument());
    expect(subscribeSpy).not.toHaveBeenCalled();
  });

  it('already subscribed: shows the stored time, saves a new one, and can turn off', async () => {
    installPushHandlers(pushHandlers.subscribed());
    const client = fakePushClient({ permission: 'granted', subscribed: true });
    const subscribeSpy = vi.spyOn(pushApi, 'subscribe');
    const unsubscribeSpy = vi.spyOn(pushApi, 'unsubscribe');
    render(<PushSettings env={iosStandalone} client={client} />, { wrapper });

    expect(await screen.findByTestId('push-enabled')).toBeInTheDocument();
    const time = screen.getByLabelText(/training nudge time/i);
    fireEvent.change(time, { target: { value: '06:45' } });
    fireEvent.click(screen.getByRole('button', { name: /save time/i }));
    await waitFor(() => expect(subscribeSpy).toHaveBeenCalledWith(expect.objectContaining({ endpoint: STORY_ENDPOINT, sessionStartTime: '06:45' })));

    fireEvent.click(screen.getByTestId('push-disable'));
    await waitFor(() => expect(unsubscribeSpy).toHaveBeenCalledWith(STORY_ENDPOINT));
    expect(await screen.findByTestId('push-enable')).toBeInTheDocument();
    expect(client.subscription).toBeNull();
  });

  it('a server without VAPID keys says notifications are not set up', async () => {
    installPushHandlers(pushHandlers.unconfigured());
    render(<PushSettings env={desktopChrome} client={fakePushClient()} />, { wrapper });
    expect(await screen.findByTestId('push-not-configured')).toBeInTheDocument();
  });
});
