/**
 * The browser side of push behind a small interface, so Settings can be tested
 * and told stories without a real service worker: permission, the current
 * subscription, subscribing with the server's key, unsubscribing.
 */

import { urlBase64ToUint8Array } from './pushSupport';

/** The bits of a browser `PushSubscription` the app needs. */
export interface BrowserSubscription {
  endpoint: string;
  /** Base64url client keys the server encrypts for. */
  keys: { p256dh: string; auth: string };
  unsubscribe(): Promise<boolean>;
}

export interface PushClient {
  permission(): NotificationPermission;
  requestPermission(): Promise<NotificationPermission>;
  getSubscription(): Promise<BrowserSubscription | null>;
  subscribe(vapidPublicKey: string): Promise<BrowserSubscription>;
}

function wrap(subscription: PushSubscription): BrowserSubscription {
  const json = subscription.toJSON();
  return {
    endpoint: subscription.endpoint,
    keys: { p256dh: json.keys?.p256dh ?? '', auth: json.keys?.auth ?? '' },
    unsubscribe: () => subscription.unsubscribe(),
  };
}

/** The real thing: Notification API plus the registered service worker's PushManager. */
export const browserPushClient: PushClient = {
  permission: () => Notification.permission,
  requestPermission: () => Notification.requestPermission(),
  async getSubscription() {
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();
    return subscription ? wrap(subscription) : null;
  },
  async subscribe(vapidPublicKey) {
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(vapidPublicKey) as BufferSource,
    });
    return wrap(subscription);
  },
};
