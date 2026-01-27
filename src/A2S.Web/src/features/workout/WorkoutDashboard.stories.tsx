import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { WorkoutDashboard } from './WorkoutDashboard';
import { ProgramVariant, WorkoutStatus, ExerciseCategory, EquipmentType, DayNumber, WeightUnit } from '@/types/workout';
import type { WorkoutDto, ExerciseDto, LinearProgressionDto, RepsPerSetProgressionDto } from '@/types/workout';

// Helper functions to create mock exercises
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

// Mock workout data
const mockFourDayWorkout: WorkoutDto = {
  id: '1',
  name: 'My A2S Program',
  variant: ProgramVariant.FourDay,
  status: WorkoutStatus.Active,
  currentWeek: 5,
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
};

/**
 * WorkoutDashboard is the main hub for viewing and managing an active workout program.
 *
 * ## Features
 * - Displays program name, current week, and overall progress
 * - Visual progress bar showing completion percentage
 * - Week overview with daily exercise schedule
 * - Complete exercise summary with training maxes
 * - Block and week information (A2S uses 3 blocks of 7 weeks)
 * - Loading and error states
 * - Empty state for users without an active workout
 *
 * ## Layout Sections
 * 1. **Header** - Program name, week info, and variant
 * 2. **Progress Bar** - Visual indicator of program completion
 * 3. **Week Overview** - Current week's training schedule by day
 * 4. **Exercises Summary** - List of all exercises with their training maxes
 */
const meta = {
  title: 'Workout/WorkoutDashboard',
  component: WorkoutDashboard,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component: 'Main dashboard for viewing and managing an active A2S workout program.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <MemoryRouter>
        <div className="min-h-screen bg-background">
          <Story />
        </div>
      </MemoryRouter>
    ),
  ],
} satisfies Meta<typeof WorkoutDashboard>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default dashboard showing an active 4-day program in Week 5.
 */
export const Default: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      queryClient.setQueryData(['workouts', 'current'], mockFourDayWorkout);

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Dashboard at the start of the program (Week 1).
 */
export const Week1: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      queryClient.setQueryData(['workouts', 'current'], {
        ...mockFourDayWorkout,
        currentWeek: 1,
      });

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Dashboard halfway through the program (Week 10).
 */
export const Week10: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      queryClient.setQueryData(['workouts', 'current'], {
        ...mockFourDayWorkout,
        currentWeek: 10,
      });

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Dashboard near completion (Week 20 of 21).
 */
export const Week20: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      queryClient.setQueryData(['workouts', 'current'], {
        ...mockFourDayWorkout,
        currentWeek: 20,
      });

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * 5-day program with higher volume and more exercises.
 */
export const FiveDayProgram: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });

      const fiveDayWorkout: WorkoutDto = {
        id: '2',
        name: 'High Volume A2S',
        variant: ProgramVariant.FiveDay,
        status: WorkoutStatus.Active,
        currentWeek: 8,
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
      };

      queryClient.setQueryData(['workouts', 'current'], fiveDayWorkout);

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * 6-day program for high frequency training.
 */
export const SixDayProgram: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });

      const sixDayWorkout: WorkoutDto = {
        id: '3',
        name: 'High Frequency A2S',
        variant: ProgramVariant.SixDay,
        status: WorkoutStatus.Active,
        currentWeek: 12,
        totalWeeks: 21,
        startDate: '2026-01-01',
        exercises: [
          createLinearExercise('1', 'Squat', 1, 1, 120, true, 4),
          createRepsPerSetExercise('2', 'Leg Curl', ExerciseCategory.Accessory, EquipmentType.Machine, 1, 2, 3, 8),
          createLinearExercise('3', 'Bench Press', 2, 1, 95, true, 4),
          createRepsPerSetExercise('4', 'Tricep Extension', ExerciseCategory.Accessory, EquipmentType.Cable, 2, 2, 3, 8),
          createLinearExercise('5', 'Deadlift', 3, 1, 150, true, 4),
          createRepsPerSetExercise('6', 'Lat Pulldown', ExerciseCategory.Accessory, EquipmentType.Cable, 3, 2, 3, 8),
          createLinearExercise('7', 'Overhead Press', 4, 1, 70, true, 4),
          createRepsPerSetExercise('8', 'Lateral Raise', ExerciseCategory.Accessory, EquipmentType.Dumbbell, 4, 2, 3, 12),
          createLinearExercise('9', 'Squat', 5, 1, 120, true, 4),
          createRepsPerSetExercise('10', 'Leg Extension', ExerciseCategory.Accessory, EquipmentType.Machine, 5, 2, 3, 8),
          createLinearExercise('11', 'Bench Press', 6, 1, 95, true, 4),
          createRepsPerSetExercise('12', 'Face Pull', ExerciseCategory.Accessory, EquipmentType.Cable, 6, 2, 3, 12),
        ],
      };

      queryClient.setQueryData(['workouts', 'current'], sixDayWorkout);

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Advanced lifter with high training maxes in pounds.
 */
export const AdvancedLifter: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });

      const advancedWorkout: WorkoutDto = {
        id: '4',
        name: 'Powerlifting Peak',
        variant: ProgramVariant.FourDay,
        status: WorkoutStatus.Active,
        currentWeek: 18,
        totalWeeks: 21,
        startDate: '2026-01-01',
        exercises: [
          {
            ...createLinearExercise('1', 'Squat', 1, 1, 450, true, 4),
            progression: {
              ...createLinearExercise('1', 'Squat', 1, 1, 450, true, 4).progression,
              trainingMax: { value: 450, unit: WeightUnit.Pounds },
            } as LinearProgressionDto,
          },
          {
            ...createLinearExercise('2', 'Bench Press', 1, 2, 335, true, 4),
            progression: {
              ...createLinearExercise('2', 'Bench Press', 1, 2, 335, true, 4).progression,
              trainingMax: { value: 335, unit: WeightUnit.Pounds },
            } as LinearProgressionDto,
          },
          {
            ...createLinearExercise('3', 'Deadlift', 2, 1, 550, true, 4),
            progression: {
              ...createLinearExercise('3', 'Deadlift', 2, 1, 550, true, 4).progression,
              trainingMax: { value: 550, unit: WeightUnit.Pounds },
            } as LinearProgressionDto,
          },
          {
            ...createLinearExercise('4', 'Overhead Press', 2, 2, 225, true, 4),
            progression: {
              ...createLinearExercise('4', 'Overhead Press', 2, 2, 225, true, 4).progression,
              trainingMax: { value: 225, unit: WeightUnit.Pounds },
            } as LinearProgressionDto,
          },
        ],
      };

      queryClient.setQueryData(['workouts', 'current'], advancedWorkout);

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Loading state while fetching workout data.
 */
export const Loading: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      // Don't set any data to simulate loading

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Error state when workout fails to load.
 */
export const Error: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });

      // Mock an error
      queryClient.setQueryDefaults(['workouts', 'current'], {
        queryFn: () => Promise.reject(new Error('Failed to load workout')),
      });

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Empty state when user has no active workout.
 */
export const NoWorkout: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      queryClient.setQueryData(['workouts', 'current'], null);

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Custom program name and shorter duration.
 */
export const CustomProgram: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });

      queryClient.setQueryData(['workouts', 'current'], {
        ...mockFourDayWorkout,
        name: 'Summer Strength Block 2026',
        totalWeeks: 12,
        currentWeek: 4,
      });

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
};

/**
 * Mobile responsive view.
 */
export const MobileView: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      queryClient.setQueryData(['workouts', 'current'], mockFourDayWorkout);

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet responsive view.
 */
export const TabletView: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
      });
      queryClient.setQueryData(['workouts', 'current'], mockFourDayWorkout);

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
