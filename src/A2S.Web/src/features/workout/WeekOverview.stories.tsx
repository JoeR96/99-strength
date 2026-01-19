import type { Meta, StoryObj } from '@storybook/react';
import { WeekOverview } from './WeekOverview';
import { ProgramVariant, WorkoutStatus, ExerciseCategory, EquipmentType, DayNumber, WeightUnit } from '@/types/workout';
import type { WorkoutDto, ExerciseDto, LinearProgressionDto, RepsPerSetProgressionDto } from '@/types/workout';

// Helper to create mock linear progression exercises
const createLinearExercise = (
  id: string,
  name: string,
  day: number,
  order: number,
  trainingMaxValue: number,
  useAmrap: boolean = true,
  sets: number = 4
): ExerciseDto => ({
  id,
  name,
  category: ExerciseCategory.MainLift,
  equipment: EquipmentType.Barbell,
  assignedDay: day,
  orderInDay: order,
  progression: {
    type: 'Linear',
    trainingMax: { value: trainingMaxValue, unit: WeightUnit.Kilograms },
    useAmrap,
    baseSetsPerExercise: sets,
  } as LinearProgressionDto,
});

// Helper to create mock reps-per-set exercises
const createRepsPerSetExercise = (
  id: string,
  name: string,
  category: ExerciseCategory,
  equipment: EquipmentType,
  day: number,
  order: number,
  currentSets: number = 3,
  targetReps: number = 8
): ExerciseDto => ({
  id,
  name,
  category,
  equipment,
  assignedDay: day,
  orderInDay: order,
  progression: {
    type: 'RepsPerSet',
    repRange: { minimum: targetReps - 3, target: targetReps, maximum: targetReps + 3 },
    currentSets,
    targetSets: currentSets + 2,
    currentWeight: 20,
    weightUnit: WeightUnit.Kilograms,
  } as RepsPerSetProgressionDto,
});

/**
 * WeekOverview displays the current week's training schedule organized by day.
 * Each day shows the exercises assigned to it and provides a button to start the workout.
 *
 * ## Features
 * - Displays exercises grouped by training day
 * - Shows exercise progression details (sets, AMRAP, rep ranges)
 * - Day completion status with visual indicators
 * - Start workout buttons for each day
 * - Responsive grid layout
 */
const meta = {
  title: 'Workout/WeekOverview',
  component: WeekOverview,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Displays the current week\'s training schedule with exercises organized by day.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[1200px] p-6 bg-background">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    workout: {
      control: 'object',
      description: 'The workout data containing exercises and schedule',
    },
  },
} satisfies Meta<typeof WeekOverview>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Standard 4-day program with the main lifts.
 */
export const FourDayProgram: Story = {
  args: {
    workout: {
      id: '1',
      name: 'My A2S Program',
      variant: ProgramVariant.FourDay,
      status: WorkoutStatus.InProgress,
      currentWeek: 1,
      totalWeeks: 21,
      startDate: '2026-01-01',
      exercises: [
        // Day 1
        createLinearExercise('1', 'Squat', 1, 1, 100, true, 4),
        createLinearExercise('2', 'Bench Press', 1, 2, 80, true, 4),
        createRepsPerSetExercise('3', 'Lat Pulldown', ExerciseCategory.Accessory, EquipmentType.Cable, 1, 3, 3, 8),

        // Day 2
        createLinearExercise('4', 'Deadlift', 2, 1, 120, true, 4),
        createLinearExercise('5', 'Overhead Press', 2, 2, 60, true, 4),
        createRepsPerSetExercise('6', 'Face Pull', ExerciseCategory.Accessory, EquipmentType.Cable, 2, 3, 3, 12),

        // Day 3
        createLinearExercise('7', 'Squat', 3, 1, 100, true, 4),
        createLinearExercise('8', 'Bench Press', 3, 2, 80, true, 4),
        createRepsPerSetExercise('9', 'Bicep Curl', ExerciseCategory.Accessory, EquipmentType.Dumbbell, 3, 3, 3, 8),

        // Day 4
        createLinearExercise('10', 'Deadlift', 4, 1, 120, true, 4),
        createLinearExercise('11', 'Overhead Press', 4, 2, 60, true, 4),
        createRepsPerSetExercise('12', 'Lateral Raise', ExerciseCategory.Accessory, EquipmentType.Dumbbell, 4, 3, 3, 12),
      ],
    },
  },
};

/**
 * 5-day program with more volume and accessory work.
 */
export const FiveDayProgram: Story = {
  args: {
    workout: {
      id: '2',
      name: 'High Volume A2S',
      variant: ProgramVariant.FiveDay,
      status: WorkoutStatus.InProgress,
      currentWeek: 5,
      totalWeeks: 21,
      startDate: '2026-01-01',
      exercises: [
        // Day 1: Chest/Triceps
        createLinearExercise('1', 'Bench Press', 1, 1, 90, true, 4),
        createLinearExercise('2', 'Incline Bench Press', 1, 2, 70, false, 4),
        createRepsPerSetExercise('3', 'Tricep Extension', ExerciseCategory.Accessory, EquipmentType.Cable, 1, 3, 3, 8),
        createRepsPerSetExercise('4', 'Lateral Raise', ExerciseCategory.Accessory, EquipmentType.Dumbbell, 1, 4, 3, 12),

        // Day 2: Back/Biceps
        createLinearExercise('5', 'Deadlift', 2, 1, 140, true, 4),
        createLinearExercise('6', 'Barbell Row', 2, 2, 80, false, 4),
        createRepsPerSetExercise('7', 'Lat Pulldown', ExerciseCategory.Accessory, EquipmentType.Cable, 2, 3, 3, 8),
        createRepsPerSetExercise('8', 'Bicep Curl', ExerciseCategory.Accessory, EquipmentType.Dumbbell, 2, 4, 3, 8),

        // Day 3: Legs
        createLinearExercise('9', 'Squat', 3, 1, 110, true, 4),
        createLinearExercise('10', 'Romanian Deadlift', 3, 2, 90, false, 3),
        createRepsPerSetExercise('11', 'Leg Press', ExerciseCategory.Auxiliary, EquipmentType.Machine, 3, 3, 3, 8),
        createRepsPerSetExercise('12', 'Leg Curl', ExerciseCategory.Accessory, EquipmentType.Machine, 3, 4, 3, 8),

        // Day 4: Shoulders/Arms
        createLinearExercise('13', 'Overhead Press', 4, 1, 65, true, 4),
        createRepsPerSetExercise('14', 'Face Pull', ExerciseCategory.Accessory, EquipmentType.Cable, 4, 2, 3, 12),
        createRepsPerSetExercise('15', 'Tricep Pushdown', ExerciseCategory.Accessory, EquipmentType.Cable, 4, 3, 3, 8),

        // Day 5: Full Body
        createLinearExercise('16', 'Front Squat', 5, 1, 80, true, 4),
        createRepsPerSetExercise('17', 'Dumbbell Row', ExerciseCategory.Auxiliary, EquipmentType.Dumbbell, 5, 2, 3, 8),
        createRepsPerSetExercise('18', 'Cable Crunch', ExerciseCategory.Accessory, EquipmentType.Cable, 5, 3, 3, 8),
      ],
    },
  },
};

/**
 * 6-day program for maximum frequency.
 */
export const SixDayProgram: Story = {
  args: {
    workout: {
      id: '3',
      name: 'High Frequency A2S',
      variant: ProgramVariant.SixDay,
      status: WorkoutStatus.InProgress,
      currentWeek: 12,
      totalWeeks: 21,
      startDate: '2026-01-01',
      exercises: [
        // Day 1
        createLinearExercise('1', 'Squat', 1, 1, 120, true, 4),
        createRepsPerSetExercise('2', 'Leg Curl', ExerciseCategory.Accessory, EquipmentType.Machine, 1, 2, 3, 8),

        // Day 2
        createLinearExercise('3', 'Bench Press', 2, 1, 95, true, 4),
        createRepsPerSetExercise('4', 'Tricep Extension', ExerciseCategory.Accessory, EquipmentType.Cable, 2, 2, 3, 8),

        // Day 3
        createLinearExercise('5', 'Deadlift', 3, 1, 150, true, 4),
        createRepsPerSetExercise('6', 'Lat Pulldown', ExerciseCategory.Accessory, EquipmentType.Cable, 3, 2, 3, 8),

        // Day 4
        createLinearExercise('7', 'Overhead Press', 4, 1, 70, true, 4),
        createRepsPerSetExercise('8', 'Lateral Raise', ExerciseCategory.Accessory, EquipmentType.Dumbbell, 4, 2, 3, 12),

        // Day 5
        createLinearExercise('9', 'Squat', 5, 1, 120, true, 4),
        createRepsPerSetExercise('10', 'Leg Extension', ExerciseCategory.Accessory, EquipmentType.Machine, 5, 2, 3, 8),

        // Day 6
        createLinearExercise('11', 'Bench Press', 6, 1, 95, true, 4),
        createRepsPerSetExercise('12', 'Face Pull', ExerciseCategory.Accessory, EquipmentType.Cable, 6, 2, 3, 12),
      ],
    },
  },
};

/**
 * Minimal program with only the main lifts.
 */
export const MinimalProgram: Story = {
  args: {
    workout: {
      id: '4',
      name: 'Minimalist A2S',
      variant: ProgramVariant.FourDay,
      status: WorkoutStatus.InProgress,
      currentWeek: 1,
      totalWeeks: 21,
      startDate: '2026-01-01',
      exercises: [
        createLinearExercise('1', 'Squat', 1, 1, 100, true, 4),
        createLinearExercise('2', 'Bench Press', 1, 2, 80, true, 4),
        createLinearExercise('3', 'Deadlift', 2, 1, 120, true, 4),
        createLinearExercise('4', 'Overhead Press', 2, 2, 60, true, 4),
        createLinearExercise('5', 'Squat', 3, 1, 100, true, 4),
        createLinearExercise('6', 'Bench Press', 3, 2, 80, true, 4),
        createLinearExercise('7', 'Deadlift', 4, 1, 120, true, 4),
        createLinearExercise('8', 'Overhead Press', 4, 2, 60, true, 4),
      ],
    },
  },
};

/**
 * Day with many exercises (high volume day).
 */
export const HighVolumeDay: Story = {
  args: {
    workout: {
      id: '5',
      name: 'Volume Block',
      variant: ProgramVariant.FourDay,
      status: WorkoutStatus.InProgress,
      currentWeek: 3,
      totalWeeks: 21,
      startDate: '2026-01-01',
      exercises: [
        // Day 1 with lots of accessories
        createLinearExercise('1', 'Squat', 1, 1, 100, true, 4),
        createLinearExercise('2', 'Bench Press', 1, 2, 80, true, 4),
        createRepsPerSetExercise('3', 'Leg Press', ExerciseCategory.Auxiliary, EquipmentType.Machine, 1, 3, 3, 8),
        createRepsPerSetExercise('4', 'Lat Pulldown', ExerciseCategory.Accessory, EquipmentType.Cable, 1, 4, 3, 8),
        createRepsPerSetExercise('5', 'Leg Curl', ExerciseCategory.Accessory, EquipmentType.Machine, 1, 5, 3, 8),
        createRepsPerSetExercise('6', 'Leg Extension', ExerciseCategory.Accessory, EquipmentType.Machine, 1, 6, 3, 8),
        createRepsPerSetExercise('7', 'Lateral Raise', ExerciseCategory.Accessory, EquipmentType.Dumbbell, 1, 7, 3, 12),
        createRepsPerSetExercise('8', 'Cable Crunch', ExerciseCategory.Accessory, EquipmentType.Cable, 1, 8, 3, 8),

        // Other days with minimal work
        createLinearExercise('9', 'Deadlift', 2, 1, 120, true, 4),
        createLinearExercise('10', 'Squat', 3, 1, 100, true, 4),
        createLinearExercise('11', 'Overhead Press', 4, 1, 60, true, 4),
      ],
    },
  },
};

/**
 * Advanced lifter with high training maxes in pounds.
 */
export const AdvancedLifterPounds: Story = {
  args: {
    workout: {
      id: '6',
      name: 'Advanced Strength',
      variant: ProgramVariant.FourDay,
      status: WorkoutStatus.InProgress,
      currentWeek: 15,
      totalWeeks: 21,
      startDate: '2026-01-01',
      exercises: [
        // Day 1
        {
          ...createLinearExercise('1', 'Squat', 1, 1, 405, true, 4),
          progression: {
            ...createLinearExercise('1', 'Squat', 1, 1, 405, true, 4).progression,
            trainingMax: { value: 405, unit: WeightUnit.Pounds },
          } as LinearProgressionDto,
        },
        {
          ...createLinearExercise('2', 'Bench Press', 1, 2, 315, true, 4),
          progression: {
            ...createLinearExercise('2', 'Bench Press', 1, 2, 315, true, 4).progression,
            trainingMax: { value: 315, unit: WeightUnit.Pounds },
          } as LinearProgressionDto,
        },

        // Day 2
        {
          ...createLinearExercise('3', 'Deadlift', 2, 1, 500, true, 4),
          progression: {
            ...createLinearExercise('3', 'Deadlift', 2, 1, 500, true, 4).progression,
            trainingMax: { value: 500, unit: WeightUnit.Pounds },
          } as LinearProgressionDto,
        },
        {
          ...createLinearExercise('4', 'Overhead Press', 2, 2, 205, true, 4),
          progression: {
            ...createLinearExercise('4', 'Overhead Press', 2, 2, 205, true, 4).progression,
            trainingMax: { value: 205, unit: WeightUnit.Pounds },
          } as LinearProgressionDto,
        },
      ],
    },
  },
};

/**
 * Week overview early in the program (Week 1).
 */
export const Week1: Story = {
  args: {
    workout: {
      ...FourDayProgram.args!.workout,
      currentWeek: 1,
    },
  },
};

/**
 * Week overview in the middle of the program (Week 10).
 */
export const Week10: Story = {
  args: {
    workout: {
      ...FourDayProgram.args!.workout,
      currentWeek: 10,
    },
  },
};

/**
 * Week overview near the end (Week 20).
 */
export const Week20: Story = {
  args: {
    workout: {
      ...FourDayProgram.args!.workout,
      currentWeek: 20,
    },
  },
};

/**
 * Mobile responsive view.
 */
export const MobileView: Story = {
  args: FourDayProgram.args,
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  decorators: [
    (Story) => (
      <div className="w-full p-4 bg-background">
        <Story />
      </div>
    ),
  ],
};

/**
 * Tablet responsive view.
 */
export const TabletView: Story = {
  args: FourDayProgram.args,
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
  decorators: [
    (Story) => (
      <div className="w-full p-6 bg-background">
        <Story />
      </div>
    ),
  ],
};
