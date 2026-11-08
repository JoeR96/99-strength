import type { Meta, StoryObj } from '@storybook/react';
import { TodayPage } from './TodayPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { installSuggestedPlansHandlers, suggestedPlansHandlers } from '@/mocks/handlers/suggestedPlans';
import { OfflineStoryHarness } from '@/mocks/offlineStoryHarness';

const meta = {
  title: 'Features/Today/TodayPage',
  component: TodayPage,
  parameters: phoneParameters,
  decorators: [withAppProviders('/today')],
} satisfies Meta<typeof TodayPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Monday: Upper A planned, water 2/6, walks 1/2, weigh-in logged, streak 5. */
export const Default: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

export const Done: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.done());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

export const RestDay: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.rest());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

/** Wednesday: Norwegian 4×4 plus Mobility B, both with Mark done. */
export const CardioDay: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.cardio());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

/** A Pause phase: banner with the end date, no Start, rings show nothing planned. */
export const Paused: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.paused());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

/** First run: no plan, so the "Start this plan" card. Habits and weigh-in still work. */
export const NoPlan: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.noPlan());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
      installSuggestedPlansHandlers(suggestedPlansHandlers.default());
    }),
  ],
};

/** No habits defined yet, weigh-in empty. */
export const Empty: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHabitsHandlers(habitsHandlers.empty());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

/** Badge slot for PR 7's offline queue. */
export const OfflinePending: Story = {
  args: { pendingSyncCount: 3 },
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

/**
 * The offline journey: water starts at 0. Press "Go offline", tap + twice: the counter reads 2 / 6
 * and the badge says 2 pending while the mock server still holds 0. Press "Go online": the two
 * taps replay in order, the badge clears and the server shows 2. Driven by tests/storybook/offline.spec.ts.
 */
export const Offline: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
    (Story) => (
      <OfflineStoryHarness>
        <Story />
      </OfflineStoryHarness>
    ),
  ],
};

/** Every write fails: taps roll back with a toast. */
export const WritesFail: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHabitsHandlers(habitsHandlers.writesFail());
      installBodyWeightHandlers(bodyWeightHandlers.error());
    }),
  ],
};

export const Loading: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.loading());
      installHabitsHandlers(habitsHandlers.loading());
      installBodyWeightHandlers(bodyWeightHandlers.loading());
    }),
  ],
};

export const Error: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.error());
      installHabitsHandlers(habitsHandlers.error());
      installBodyWeightHandlers(bodyWeightHandlers.error());
    }),
  ],
};
