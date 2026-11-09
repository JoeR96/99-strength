/** Story/test handlers for the parts of `workoutsApi` the new pages read. */

import { workoutsApi } from '@/api/workouts';
import type { WorkoutDto } from '@/types/workout';
import { installHandlers, never, rejectWith } from './install';

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
  none: (): WorkoutsHandlers => ({ getCurrentWorkout: async () => null }),
  loading: (): WorkoutsHandlers => ({ getCurrentWorkout: never }),
  error: (): WorkoutsHandlers => ({ getCurrentWorkout: rejectWith(500) }),
};

export function installWorkoutsHandlers(handlers: WorkoutsHandlers = workoutsHandlers.default()): void {
  installHandlers(workoutsApi, handlers);
}
