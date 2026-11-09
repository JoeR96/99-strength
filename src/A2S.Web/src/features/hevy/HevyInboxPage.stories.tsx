import type { Decorator, Meta, StoryObj } from '@storybook/react';
import { useEffect, useState, type ReactNode } from 'react';
import { HevyInboxPage } from './HevyInboxPage';
import { withAppProviders, withApiMocks, withHevyStatus, phoneParameters } from '@/mocks/storyProviders';
import { hevySyncHandlers, installHevySyncHandlers } from '@/mocks/handlers/hevySync';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';

const meta = {
  title: 'Features/Hevy/HevyInboxPage',
  component: HevyInboxPage,
  parameters: phoneParameters,
  decorators: [withAppProviders('/hevy/inbox')],
} satisfies Meta<typeof HevyInboxPage>;

export default meta;
type Story = StoryObj<typeof meta>;

const withInbox = (handlers: () => Parameters<typeof installHevySyncHandlers>[0]) =>
  withApiMocks(() => {
    installHevySyncHandlers(handlers());
    installPlansHandlers(plansHandlers.week());
  });

/** The browser reports no connection for as long as the story is mounted. */
function goOffline(): boolean {
  Object.defineProperty(navigator, 'onLine', { configurable: true, get: () => false });
  return true;
}

function OfflineBrowser({ children }: { children: ReactNode }) {
  // In the initialiser so the page's first read of `navigator.onLine` already says offline.
  useState(goOffline);
  useEffect(
    () => () => {
      delete (navigator as { onLine?: boolean }).onLine;
      window.dispatchEvent(new Event('online'));
    },
    []
  );
  return <>{children}</>;
}

const offline: Decorator = (Story) => (
  <OfflineBrowser>
    <Story />
  </OfflineBrowser>
);

/** Every pulled workout matched a session. */
export const Empty: Story = {
  decorators: [withHevyStatus(true), withInbox(hevySyncHandlers.empty)],
};

/** Trained Lower A on the rest day and a push day on the cardio day: each can be assigned or ignored. */
export const TwoItems: Story = {
  decorators: [withHevyStatus(true), withInbox(hevySyncHandlers.twoItems)],
};

/** Assign pressed on the Wednesday workout: that date's sessions (4×4, Mobility B) plus Record as extra. */
export const Assigning: Story = {
  decorators: [withHevyStatus(true), withInbox(hevySyncHandlers.twoItems)],
  play: async ({ canvasElement }) => {
    const deadline = Date.now() + 5000;
    let buttons: HTMLButtonElement[] = [];
    while (buttons.length < 2 && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 50));
      buttons = Array.from(canvasElement.querySelectorAll<HTMLButtonElement>('[data-testid="inbox-assign"]'));
    }
    buttons[1]?.click();
  },
};

/** No connection: the list is still readable but Assign, Ignore and Pull are off. */
export const Offline: Story = {
  decorators: [withHevyStatus(true), withInbox(hevySyncHandlers.twoItems), offline],
};

/** Hevy is unreachable: the list loads, Assign and Ignore fail with the server's reason. */
export const HevyDown: Story = {
  decorators: [withHevyStatus(true), withInbox(hevySyncHandlers.writesFail)],
};

export const Loading: Story = {
  decorators: [withHevyStatus(true), withInbox(hevySyncHandlers.loading)],
};

/** The inbox itself could not be loaded. */
export const LoadError: Story = {
  decorators: [withHevyStatus(true), withInbox(hevySyncHandlers.error)],
};

/** No Hevy key stored: nothing to pull, with the way to Settings. */
export const NotConnected: Story = {
  decorators: [withInbox(hevySyncHandlers.empty)],
};
