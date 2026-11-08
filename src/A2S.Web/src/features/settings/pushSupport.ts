/**
 * Can this browser receive web push? Desktop and Android browsers can when they
 * expose `PushManager` and a service worker. iOS Safari only can once the app is
 * installed to the Home Screen (iOS 16.4+), so a Safari tab gets install steps
 * instead of a button.
 */

export type PushSupport = 'supported' | 'ios-needs-install' | 'unsupported';

export interface SupportEnv {
  hasPushManager: boolean;
  hasServiceWorker: boolean;
  hasNotification: boolean;
  isIos: boolean;
  /** Running from the Home Screen (iOS `navigator.standalone`, or display-mode: standalone). */
  standalone: boolean;
}

interface NavigatorLike {
  userAgent?: string;
  platform?: string;
  maxTouchPoints?: number;
  standalone?: boolean;
  serviceWorker?: unknown;
}

/** Reads the environment from a window; split out so tests can hand in a fake. */
export function readSupportEnv(win: Window = window): SupportEnv {
  const nav = win.navigator as NavigatorLike;
  const ua = nav.userAgent ?? '';
  const iPadOs = nav.platform === 'MacIntel' && (nav.maxTouchPoints ?? 0) > 1;
  const isIos = /iPad|iPhone|iPod/.test(ua) || iPadOs;
  const standalone =
    nav.standalone === true ||
    (typeof win.matchMedia === 'function' && win.matchMedia('(display-mode: standalone)').matches);
  return {
    hasPushManager: 'PushManager' in win,
    hasServiceWorker: 'serviceWorker' in win.navigator,
    hasNotification: 'Notification' in win,
    isIos,
    standalone,
  };
}

export function detectPushSupport(env: SupportEnv): PushSupport {
  if (env.isIos && !env.standalone) return 'ios-needs-install';
  if (!env.hasPushManager || !env.hasServiceWorker || !env.hasNotification) return 'unsupported';
  return 'supported';
}

/** Web Push wants the VAPID key as raw bytes. */
export function urlBase64ToUint8Array(base64Url: string): Uint8Array {
  const padding = '='.repeat((4 - (base64Url.length % 4)) % 4);
  const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(base64);
  const bytes = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  return bytes;
}
