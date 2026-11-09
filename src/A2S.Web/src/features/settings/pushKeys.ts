/** Query keys and small helpers for the Notifications card (kept out of the component file for fast refresh). */

export const pushKeys = {
  all: ['push'] as const,
  vapid: () => [...pushKeys.all, 'vapid'] as const,
  subscriptions: () => [...pushKeys.all, 'subscriptions'] as const,
  browser: () => [...pushKeys.all, 'browser'] as const,
};

export const DEFAULT_SESSION_START = '17:30';

/** "17:30:00" → "17:30" for the time input. */
export function toTimeInput(value: string | null | undefined): string {
  return value ? value.slice(0, 5) : DEFAULT_SESSION_START;
}
