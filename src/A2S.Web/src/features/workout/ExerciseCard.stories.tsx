import type { Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { fn } from 'storybook/test';
import { ExerciseCard } from './ExerciseCard';
import type {
  ExerciseEntry,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
} from './workoutSessionTypes';
import { currentWorkout, CURRENT_WEEK } from '@/mocks/fixtures/program';
import { getWeekParameters, roundToGymIncrement } from '@/utils/weekParameters';

const params = getWeekParameters(CURRENT_WEEK);
const bench = currentWorkout.exercises.find((e) => e.id === 'ex-bench')!;
const row = currentWorkout.exercises.find((e) => e.id === 'ex-row')!;
const benchWeight = roundToGymIncrement(
  (bench.progression as LinearProgressionDto).trainingMax.value * params.intensity
);
const rowProg = row.progression as RepsPerSetProgressionDto;

function benchEntry(completed: number, amrapReps?: number): ExerciseEntry {
  return {
    exercise: bench,
    targetSets: params.sets,
    targetReps: params.targetReps,
    targetWeight: benchWeight,
    weightUnit: 'kg',
    isAmrapExercise: true,
    sets: Array.from({ length: params.sets }, (_, i) => ({
      setNumber: i + 1,
      weight: benchWeight,
      reps: i === params.sets - 1 && amrapReps ? amrapReps : params.targetReps,
      isAmrap: i === params.sets - 1,
      completed: i < completed,
    })),
  };
}

const rowEntry: ExerciseEntry = {
  exercise: row,
  targetSets: rowProg.currentSetCount,
  targetReps: rowProg.repRange.maximum,
  targetWeight: rowProg.currentWeight,
  weightUnit: 'kg',
  isAmrapExercise: false,
  sets: Array.from({ length: rowProg.currentSetCount }, (_, i) => ({
    setNumber: i + 1,
    weight: rowProg.currentWeight,
    reps: [12, 12, 11, 10, 10][i] ?? 10,
    isAmrap: false,
    completed: true,
  })),
};

/** Interactive wrapper so "Log" actually ticks sets off in the story. */
function LiveCard({ initial }: { initial: ExerciseEntry }) {
  const [entry, setEntry] = useState(initial);
  return (
    <ExerciseCard
      entry={entry}
      exerciseIndex={0}
      onSetChange={(_e, setIndex, field, value) =>
        setEntry((prev) => ({
          ...prev,
          sets: prev.sets.map((s, i) => (i === setIndex ? { ...s, [field]: value } : s)),
        }))
      }
      onSetComplete={(_e, setIndex) =>
        setEntry((prev) => ({
          ...prev,
          sets: prev.sets.map((s, i) => (i === setIndex ? { ...s, completed: !s.completed } : s)),
        }))
      }
      onSubstitute={fn()}
      onEdit={fn()}
    />
  );
}

/** Set logging for one exercise in a session: weights from the training max, AMRAP last. */
const meta = {
  title: 'Features/Workout/Set Logging',
  component: LiveCard,
  decorators: [
    (Story) => (
      <div className="bg-background p-6">
        <div className="mx-auto max-w-4xl space-y-6">
          <Story />
        </div>
      </div>
    ),
  ],
  args: { initial: benchEntry(0) },
} satisfies Meta<typeof LiveCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const FreshBench: Story = {};

export const AmrapUpNext: Story = { args: { initial: benchEntry(3, 14) } };

export const AccessoryDone: Story = { args: { initial: rowEntry } };
