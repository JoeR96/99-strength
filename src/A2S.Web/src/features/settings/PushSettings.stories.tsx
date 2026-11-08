import type { Meta, StoryObj } from '@storybook/react';
import { PushSettings } from './PushSettings';
import type { SupportEnv } from './pushSupport';
import { withAppProviders, withApiMocks } from '@/mocks/storyProviders';
import { fakePushClient, installPushHandlers, pushHandlers } from '@/mocks/handlers/push';

const desktopChrome: SupportEnv = { hasPushManager: true, hasServiceWorker: true, hasNotification: true, isIos: false, standalone: false };
const iosSafariTab: SupportEnv = { hasPushManager: false, hasServiceWorker: true, hasNotification: false, isIos: true, standalone: false };
const iosStandalone: SupportEnv = { hasPushManager: true, hasServiceWorker: true, hasNotification: true, isIos: true, standalone: true };
const oldBrowser: SupportEnv = { hasPushManager: false, hasServiceWorker: false, hasNotification: false, isIos: false, standalone: false };

const meta = {
  title: 'Features/Settings/PushSettings',
  component: PushSettings,
  parameters: { layout: 'padded', viewport: { defaultViewport: 'mobile2' } },
  decorators: [withAppProviders('/settings')],
} satisfies Meta<typeof PushSettings>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Desktop Chrome, nothing subscribed yet: one button. Pressing it "grants" and subscribes. */
export const DesktopChrome: Story = {
  args: { env: desktopChrome, client: fakePushClient() },
  decorators: [withApiMocks(() => installPushHandlers(pushHandlers.default()))],
};

/** Installed to the iOS Home Screen and already on: the nudge time and Turn off. */
export const IosStandaloneSubscribed: Story = {
  args: { env: iosStandalone, client: fakePushClient({ permission: 'granted', subscribed: true }) },
  decorators: [withApiMocks(() => installPushHandlers(pushHandlers.subscribed()))],
};

/** iOS Safari in a tab: no button, the Home Screen install steps instead. */
export const IosSafariTab: Story = {
  args: { env: iosSafariTab, client: fakePushClient() },
  decorators: [withApiMocks(() => installPushHandlers(pushHandlers.default()))],
};

/** Permission already refused at the browser level. */
export const Denied: Story = {
  args: { env: desktopChrome, client: fakePushClient({ permission: 'denied' }) },
  decorators: [withApiMocks(() => installPushHandlers(pushHandlers.default()))],
};

/** The server has no VAPID keys configured. */
export const ServerNotConfigured: Story = {
  args: { env: desktopChrome, client: fakePushClient() },
  decorators: [withApiMocks(() => installPushHandlers(pushHandlers.unconfigured()))],
};

export const Unsupported: Story = {
  args: { env: oldBrowser, client: fakePushClient() },
};
