import { addDays, todayLocal } from '@/lib/localDate';
import { PhaseType, type PhaseHeadsUpDto } from '@/types/plan';

/** "Build" (a Cut phase) starting in `daysUntil` days, with notes and a squat swap. */
export function buildHeadsUp(daysUntil: number, swapDone = false): PhaseHeadsUpDto {
  const from = addDays(todayLocal(), daysUntil);
  return {
    type: PhaseType.Cut,
    label: 'Build',
    from,
    to: addDays(from, 55),
    daysUntil,
    changes: [
      'Back squat replaces pause squat',
      'Stair finisher: start intervals',
      'Home routines: move to harder versions, +15 s holds',
    ],
    swaps: [
      {
        fromExercise: 'Pause Squat',
        toExercise: 'Squat (Barbell)',
        workoutId: 'workout-1',
        exerciseId: swapDone ? null : 'exercise-pause-squat',
        done: swapDone,
      },
    ],
  };
}
