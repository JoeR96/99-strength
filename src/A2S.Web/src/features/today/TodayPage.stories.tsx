import type { Decorator, Meta, StoryObj } from '@storybook/react';
import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { TodayPage } from './TodayPage';
import { withAppProviders, withApiMocks, withHevyStatus, phoneParameters } from '@/mocks/storyProviders';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { installSuggestedPlansHandlers, suggestedPlansHandlers } from '@/mocks/handlers/suggestedPlans';
import { addDays, todayLocal } from '@/lib/localDate';
import { OfflineStoryHarness } from '@/mocks/offlineStoryHarness';
import { hevySyncHandlers, installHevySyncHandlers, installPushFlowHandlers } from '@/mocks/handlers/hevySync';
import { cardioTracksHandlers, installCardioTracksHandlers } from '@/mocks/handlers/cardioTracks';

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

/** Upper A finished in gym mode; Hevy was unreachable, so the card says "Not in Hevy yet" with Retry. */
export const NotInHevyYet: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.doneNotInHevy());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

/** Rest day: "Up next" shows the next day with a session (tomorrow's Upper A, bench 5×5 @ 75 kg) and Preview. */
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

/** A Hevy key is stored and Upper A was pushed: Open in Hevy sits beside Start. */
export const WithHevyRoutine: Story = {
  decorators: [
    withHevyStatus(true),
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.withHevyRoutine());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
      installHevySyncHandlers(hevySyncHandlers.empty());
    }),
  ],
};

/** A key is stored but Upper A has no routine yet: Push to Hevy, which turns into Open in Hevy. */
export const PushAvailable: Story = {
  decorators: [
    withHevyStatus(true),
    withApiMocks(() => {
      installPushFlowHandlers();
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
  ],
};

/** Two pulled workouts matched nothing: the chip links to the Hevy inbox. */
export const HevyInboxWaiting: Story = {
  decorators: [
    withHevyStatus(true),
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.withHevyRoutine());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
      installHevySyncHandlers(hevySyncHandlers.twoItems());
    }),
  ],
};

/** Hevy is unreachable: the pull on open fails quietly with a retry; the plan still shows. */
export const HevyPullFailed: Story = {
  decorators: [
    withHevyStatus(true),
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
      installHevySyncHandlers(hevySyncHandlers.writesFail());
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

/** Cardio tracks under the sessions: each prescription, sessions to the next step, and the zone 4–5 guardrail. */
export const WithCardioTracks: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
      installCardioTracksHandlers(cardioTracksHandlers.default());
    }),
  ],
};

/** Moves the story's router to `/today{search}` before rendering (the meta's router starts at /today). */
function withSearch(search: string): Decorator {
  return function SearchDecorator(Story) {
    const navigate = useNavigate();
    const location = useLocation();
    useEffect(() => {
      if (location.search !== search) navigate(`/today${search}`, { replace: true });
    }, [location.search, navigate]);
    return location.search === search ? <Story /> : <></>;
  };
}

/** Stepped two days ahead (`/today?date=…`): read-only, Preview instead of Start, Back to today, no habits. */
export const SteppedToFutureDay: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.week());
      installHabitsHandlers(habitsHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
    }),
    withSearch(`?date=${addDays(todayLocal(), 2)}`),
  ],
};
