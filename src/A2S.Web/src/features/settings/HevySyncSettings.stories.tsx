import type { Meta, StoryObj } from '@storybook/react';
import { HevySyncSettings } from './HevySyncSettings';
import { withAppProviders, withApiMocks, withHevyStatus } from '@/mocks/storyProviders';
import { hevySyncHandlers, installHevySyncHandlers } from '@/mocks/handlers/hevySync';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';

const meta = {
  title: 'Features/Settings/HevySyncSettings',
  component: HevySyncSettings,
  parameters: { layout: 'padded', viewport: { defaultViewport: 'mobile2' } },
  decorators: [withAppProviders('/settings')],
} satisfies Meta<typeof HevySyncSettings>;

export default meta;
type Story = StoryObj<typeof meta>;

async function press(canvasElement: HTMLElement, testId: string) {
  const deadline = Date.now() + 5000;
  let button: HTMLButtonElement | null = null;
  while (Date.now() < deadline) {
    button = canvasElement.querySelector<HTMLButtonElement>(`[data-testid="${testId}"]`);
    if (button && !button.disabled) break;
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
  button?.click();
}

/** A key is stored: the two buttons, nothing run yet. */
export const Ready: Story = {
  decorators: [
    withHevyStatus(true),
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHevySyncHandlers(hevySyncHandlers.empty());
    }),
  ],
};

/** After Push this week: each day's session with Created / Updated, and Saturday's open cardio slot not pushed. */
export const WeekPushed: Story = {
  ...Ready,
  play: async ({ canvasElement }) => press(canvasElement, 'hevy-push-week'),
};

/** After Pull now, with one workout matched. */
export const Pulled: Story = {
  decorators: [
    withHevyStatus(true),
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHevySyncHandlers(hevySyncHandlers.pullMatchesOne());
    }),
  ],
  play: async ({ canvasElement }) => press(canvasElement, 'hevy-pull-now'),
};

/** Hevy is unreachable: both actions say why. */
export const HevyDown: Story = {
  decorators: [
    withHevyStatus(true),
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHevySyncHandlers(hevySyncHandlers.writesFail());
    }),
  ],
  play: async ({ canvasElement }) => {
    await press(canvasElement, 'hevy-push-week');
    await press(canvasElement, 'hevy-pull-now');
  },
};

/** No key stored: the section is not shown at all. */
export const NoKey: Story = {};
