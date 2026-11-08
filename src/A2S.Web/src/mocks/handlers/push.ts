/** Story/test handlers for `pushApi`, plus a fake browser `PushClient`. */

import { pushApi } from '@/api/push';
import type { BrowserSubscription, PushClient } from '@/features/settings/pushClient';
import type { PushSubscriptionDto } from '@/types/push';
import { installHandlers, never, rejectWith } from './install';

type PushHandlers = Partial<typeof pushApi>;

export const STORY_VAPID_KEY = 'BNoRealKeyHere_StoryPlaceholder_000000000000000000000000000000000000000000000000000000000000000';
export const STORY_ENDPOINT = 'https://push.example.com/send/story-device';

export interface StatefulPushOptions {
  /** Start with this device already stored on the server. */
  subscribed?: boolean;
  /** Server has no VAPID key. */
  unconfigured?: boolean;
  sessionStartTime?: string;
}

export function statefulPushHandlers(options: StatefulPushOptions = {}): PushHandlers {
  const { subscribed = false, unconfigured = false, sessionStartTime = '17:30:00' } = options;
  let subscriptions: PushSubscriptionDto[] = subscribed
    ? [{ id: 'sub-1', endpoint: STORY_ENDPOINT, userAgent: 'Story', sessionStartTime, createdAtUtc: '2026-11-09T08:00:00Z' }]
    : [];

  return {
    getVapidPublicKey: async () => (unconfigured ? null : STORY_VAPID_KEY),
    listSubscriptions: async () => subscriptions,
    subscribe: async (request) => {
      const existing = subscriptions.find((s) => s.endpoint === request.endpoint);
      const time = request.sessionStartTime ? `${request.sessionStartTime.slice(0, 5)}:00` : existing?.sessionStartTime ?? '17:30:00';
      const dto: PushSubscriptionDto = existing
        ? { ...existing, sessionStartTime: time }
        : { id: `sub-${subscriptions.length + 1}`, endpoint: request.endpoint, userAgent: request.userAgent ?? null, sessionStartTime: time, createdAtUtc: new Date().toISOString() };
      subscriptions = existing ? subscriptions.map((s) => (s.endpoint === dto.endpoint ? dto : s)) : [...subscriptions, dto];
      return dto;
    },
    unsubscribe: async (endpoint) => {
      subscriptions = subscriptions.filter((s) => s.endpoint !== endpoint);
    },
  };
}

export const pushHandlers = {
  default: (): PushHandlers => statefulPushHandlers(),
  subscribed: (): PushHandlers => statefulPushHandlers({ subscribed: true }),
  unconfigured: (): PushHandlers => statefulPushHandlers({ unconfigured: true }),
  loading: (): PushHandlers => ({ getVapidPublicKey: never, listSubscriptions: never }),
  error: (): PushHandlers => ({ getVapidPublicKey: rejectWith(500), listSubscriptions: rejectWith(500) }),
};

export function installPushHandlers(handlers: PushHandlers = pushHandlers.default()): void {
  installHandlers(pushApi, handlers);
}

export interface FakePushClientOptions {
  permission?: NotificationPermission;
  /** What `requestPermission` answers. */
  grant?: NotificationPermission;
  /** Start with a browser subscription in place. */
  subscribed?: boolean;
}

/** A browser that grants (or refuses) permission and hands out a fixed subscription. */
export function fakePushClient(options: FakePushClientOptions = {}): PushClient & { subscription: BrowserSubscription | null } {
  const { permission = 'default', grant = 'granted', subscribed = false } = options;
  let current = permission;
  const make = (): BrowserSubscription => ({
    endpoint: STORY_ENDPOINT,
    keys: { p256dh: 'story-p256dh', auth: 'story-auth' },
    unsubscribe: async () => {
      client.subscription = null;
      return true;
    },
  });
  const client = {
    subscription: subscribed ? make() : null,
    permission: () => current,
    requestPermission: async () => {
      current = grant;
      return grant;
    },
    getSubscription: async () => client.subscription,
    subscribe: async () => {
      client.subscription = make();
      return client.subscription;
    },
  };
  return client;
}
