/** Story/test handlers for the parts of `workoutsApi` the new pages read. */

import { workoutsApi } from '@/api/workouts';
import type { WorkoutDto } from '@/types/workout';
import { installHandlers, never, rejectWith } from './install';
import { cycleFixture, type CycleOptions } from '../fixtures/cycle';
import { startingNumbers } from '../fixtures/startingNumbers';
import type { StartingNumbersSuggestions } from '@/api/workouts';

type WorkoutsHandlers = Partial<typeof workoutsApi>;

export const activeWorkoutFixture: WorkoutDto = {
  id: 'w-1',
  name: 'Cut to 85 — A2S2',
  variant: 4,
  status: 2,
  currentWeek: 2,
  currentBlock: 1,
  currentDay: 2,
  daysPerWeek: 4,
  completedDaysInCurrentWeek: [1],
  isWeekComplete: false,
  totalWeeks: 21,
  blockSequence: [1, 2, 3],
  startDate: '2026-11-09T00:00:00Z',
  createdAt: '2026-11-08T00:00:00Z',
  startedAt: '2026-11-09T00:00:00Z',
  exerciseCount: 0,
  exercises: [],
};

export const workoutsHandlers = {
  default: (): WorkoutsHandlers => ({ getCurrentWorkout: async () => activeWorkoutFixture }),
  none: (): WorkoutsHandlers => ({ getCurrentWorkout: async () => null, getCurrentCycle: rejectWith(404) }),
  /** The Current workout page's cycle (week 1 session 1 unless told otherwise); week plans answer from the same data; `suggestions` answers the Hevy starting numbers. */
  cycle: (options: CycleOptions & { suggestions?: StartingNumbersSuggestions } = {}): WorkoutsHandlers => {
    const cycle = cycleFixture(options);
    return {
      getCurrentWorkout: async () => ({ ...activeWorkoutFixture, currentWeek: cycle.currentWeek, currentDay: cycle.currentDay }),
      getCurrentCycle: async () => cycle,
      getStartingNumberSuggestions: async () => options.suggestions ?? startingNumbers.none(),
      getWeekPlan: async (_id: string, week: number, day: number) =>
        (cycle.sessions.find((s) => s.week === week && s.day === day) ?? cycle.sessions[0]).plan,
    };
  },
  cycleLoading: (): WorkoutsHandlers => ({ getCurrentCycle: never }),
  loading: (): WorkoutsHandlers => ({ getCurrentWorkout: never }),
  error: (): WorkoutsHandlers => ({ getCurrentWorkout: rejectWith(500) }),
};

export function installWorkoutsHandlers(handlers: WorkoutsHandlers = workoutsHandlers.default()): void {
  installHandlers(workoutsApi, handlers);
}
