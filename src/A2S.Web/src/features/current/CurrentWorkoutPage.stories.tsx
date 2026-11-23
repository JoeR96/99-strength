import type { Decorator, Meta, StoryObj } from '@storybook/react';
import { CurrentWorkoutPage } from './CurrentWorkoutPage';
import { withAppProviders, withApiMocks, withHevyStatus, phoneParameters } from '@/mocks/storyProviders';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installHabitsHandlers, habitsHandlers } from '@/mocks/handlers/habits';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { installWorkoutsHandlers, workoutsHandlers } from '@/mocks/handlers/workouts';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';
import { installSuggestedPlansHandlers, suggestedPlansHandlers } from '@/mocks/handlers/suggestedPlans';
import { hevySyncHandlers, installHevySyncHandlers } from '@/mocks/handlers/hevySync';
import { OfflineStoryHarness } from '@/mocks/offlineStoryHarness';
import type { CycleOptions } from '@/mocks/fixtures/cycle';
import type { StartingNumbersSuggestions } from '@/api/workouts';
import { startingNumbers } from '@/mocks/fixtures/startingNumbers';

const meta = {
  title: 'Features/Current/CurrentWorkoutPage',
  component: CurrentWorkoutPage,
  parameters: phoneParameters,
  decorators: [withAppProviders('/today')],
} satisfies Meta<typeof CurrentWorkoutPage>;

export default meta;
type Story = StoryObj<typeof meta>;

function mocks(cycle: CycleOptions & { suggestions?: StartingNumbersSuggestions }, extra?: () => void): Decorator {
  return withApiMocks(() => {
    installPlansHandlers(plansHandlers.default());
    installHabitsHandlers(habitsHandlers.default());
    installBodyWeightHandlers(bodyWeightHandlers.empty());
    installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default());
    installWorkoutsHandlers(workoutsHandlers.cycle(cycle));
    extra?.();
  });
}

/** Week 1, session 1 of 4: Upper A with the stair finisher as the last item, Mobility B after it, the cycle below. */
export const FirstSession: Story = { decorators: [mocks({})] };

/** A fresh program: bench has no training max yet, so it shows Set TM instead of kilos. */
export const TrainingMaxNeeded: Story = { decorators: [mocks({ pendingTm: true })] };
/** A pending lift with a Hevy-based suggestion and the Use all suggestions action. */
export const TrainingMaxSuggested: Story = { decorators: [mocks({ pendingTm: true, suggestions: startingNumbers.fresh() })] };
export const TrainingMaxOlderHistory: Story = { decorators: [mocks({ pendingTm: true, suggestions: startingNumbers.stale() })] };
export const TrainingMaxHevyUnavailable: Story = { decorators: [mocks({ pendingTm: true, suggestions: startingNumbers.unavailable() })] };

/** Mid-cycle: week 3, session 2 (Lower A, treadmill cool-down), two sessions left this week. */
export const MidCycle: Story = { decorators: [mocks({ week: 3, day: 2 })] };

/** Deload week: four sets, no AMRAP. */
export const DeloadWeek: Story = { decorators: [mocks({ week: 7, day: 1, deload: true })] };

/** Upper A just finished and is in Hevy; the page already shows the next session in the cycle. */
export const JustFinishedInHevy: Story = {
  decorators: [
    mocks({ day: 2 }, () => {
      installPlansHandlers(plansHandlers.done());
      installHevySyncHandlers(hevySyncHandlers.empty());
    }),
    withHevyStatus(true),
  ],
};

/** Finished in gym mode, Hevy unreachable: "Not in Hevy yet" with Retry above the next session. */
export const NotInHevyYet: Story = {
  decorators: [
    mocks({ day: 2 }, () => {
      installPlansHandlers(plansHandlers.doneNotInHevy());
      installHevySyncHandlers(hevySyncHandlers.empty());
    }),
    withHevyStatus(true),
  ],
};

/** No active workout: the suggested plan card and the templates link. */
export const NoPlan: Story = {
  decorators: [
    mocks({}, () => {
      installWorkoutsHandlers(workoutsHandlers.none());
      installPlansHandlers(plansHandlers.noPlan());
      installSuggestedPlansHandlers(suggestedPlansHandlers.default());
    }),
  ],
};

export const Loading: Story = { decorators: [mocks({}, () => installWorkoutsHandlers(workoutsHandlers.cycleLoading()))] };

/** Badge slot for the offline queue. */
export const OfflinePending: Story = { args: { pendingSyncCount: 3 }, decorators: [mocks({})] };

/**
 * The offline journey (tests/storybook/offline.spec.ts): water starts at 0. "Go offline", tap + twice:
 * 2 / 6 and 2 pending while the mock server holds 0. "Go online": the taps replay and the server shows 2.
 */
export const Offline: Story = {
  decorators: [
    withApiMocks(() => {
      installPlansHandlers(plansHandlers.default());
      installBodyWeightHandlers(bodyWeightHandlers.empty());
      installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default());
      installWorkoutsHandlers(workoutsHandlers.cycle({}));
    }),
    (Story) => (
      <OfflineStoryHarness>
        <Story />
      </OfflineStoryHarness>
    ),
  ],
};
