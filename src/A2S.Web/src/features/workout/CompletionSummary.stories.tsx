import type { Meta, StoryObj } from '@storybook/react-vite';
import { fn } from 'storybook/test';
import { CompletionSummary } from './CompletionSummary';
import type {
  ExerciseEntry,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
} from './workoutSessionTypes';
import { currentWorkout, CURRENT_WEEK } from '@/mocks/fixtures/program';
import { getWeekParameters, roundToGymIncrement } from '@/utils/weekParameters';
import type { CompleteDayResult, DayNumber } from '@/types/workout';

const params = getWeekParameters(CURRENT_WEEK);
const day2 = currentWorkout.exercises
  .filter((e) => e.assignedDay === 2)
  .sort((a, b) => a.orderInDay - b.orderInDay);

/** The bench-day session, fully logged: bench AMRAP beat the target by three. */
const entries: ExerciseEntry[] = day2.map((exercise) => {
  if (exercise.progression.type === 'Linear') {
    const w = roundToGymIncrement(
      (exercise.progression as LinearProgressionDto).trainingMax.value * params.intensity
    );
    return {
      exercise,
      targetSets: params.sets,
      targetReps: params.targetReps,
      targetWeight: w,
      weightUnit: 'kg',
      isAmrapExercise: true,
      sets: Array.from({ length: params.sets }, (_, i) => ({
        setNumber: i + 1,
        weight: w,
        reps: i === params.sets - 1 ? (params.repOutTarget ?? 12) + 3 : params.targetReps,
        isAmrap: i === params.sets - 1,
        completed: true,
      })),
    };
  }
  const prog = exercise.progression as RepsPerSetProgressionDto | MinimalSetsProgressionDto;
  const sets = prog.currentSetCount;
  const reps = 'repRange' in prog ? prog.repRange.maximum : Math.ceil(prog.targetTotalReps / sets);
  return {
    exercise,
    targetSets: sets,
    targetReps: reps,
    targetWeight: prog.currentWeight,
    weightUnit: 'kg',
    isAmrapExercise: false,
    sets: Array.from({ length: sets }, (_, i) => ({
      setNumber: i + 1,
      weight: prog.currentWeight,
      reps,
      isAmrap: false,
      completed: true,
    })),
  };
});

const result: CompleteDayResult = {
  workoutId: currentWorkout.id,
  day: 2 as DayNumber,
  weekNumber: CURRENT_WEEK,
  blockNumber: 2,
  exercisesCompleted: entries.length,
  progressionChanges: [
    {
      exerciseId: 'ex-bench',
      exerciseName: 'Bench Press (Barbell)',
      change: 'Training max increased 2% (AMRAP +3)',
    },
    {
      exerciseId: 'ex-row',
      exerciseName: 'Bent Over Row (Barbell)',
      change: 'Added a set (all sets hit 12)',
    },
    {
      exerciseId: 'ex-lateral',
      exerciseName: 'Lateral Raise (Dumbbell)',
      change: 'No change (maintained)',
    },
    {
      exerciseId: 'ex-pushdown',
      exerciseName: 'Triceps Rope Pushdown',
      change: 'Added a set (all sets hit 15)',
    },
    {
      exerciseId: 'ex-pullup',
      exerciseName: 'Pull Up (Weighted)',
      change: 'No change (maintained)',
    },
  ],
  newCurrentWeek: CURRENT_WEEK,
  newCurrentDay: 3,
  weekProgressed: false,
  programComplete: false,
  isDeloadWeek: false,
  exercisesPendingWeightConfirmation: [],
  nextSessionExercises: entries.map((e) => ({
    exerciseId: e.exercise.id,
    exerciseName: e.exercise.name,
    setCount:
      e.exercise.id === 'ex-row' || e.exercise.id === 'ex-pushdown'
        ? e.targetSets + 1
        : e.targetSets,
    targetReps:
      e.exercise.progression.type === 'Linear'
        ? getWeekParameters(CURRENT_WEEK + 1).targetReps
        : e.targetReps,
    weight:
      e.exercise.progression.type === 'Linear'
        ? roundToGymIncrement(
            (e.exercise.progression as LinearProgressionDto).trainingMax.value *
              1.02 *
              getWeekParameters(CURRENT_WEEK + 1).intensity
          )
        : e.targetWeight,
    weightUnit: 'Kilograms',
    hasAmrap: e.isAmrapExercise,
  })),
};

const start = new Date();
start.setHours(7, 30, 0, 0);
const end = new Date(start.getTime() + 74 * 60000);

/** After "Complete Workout": what changed and what next week's session looks like. */
const meta = {
  title: 'Features/Workout/Completion Summary',
  component: CompletionSummary,
  parameters: { route: { path: '/workout/session/:day', url: '/workout/session/2' } },
  args: {
    result,
    workout: currentWorkout,
    dayNumber: 2 as DayNumber,
    dayName: 'Tuesday',
    exerciseEntries: entries,
    workoutStartTime: start,
    workoutEndTime: end,
    onContinue: fn(),
  },
} satisfies Meta<typeof CompletionSummary>;

export default meta;
type Story = StoryObj<typeof meta>;

export const BenchDayComplete: Story = {};
