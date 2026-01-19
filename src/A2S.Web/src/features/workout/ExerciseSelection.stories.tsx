import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ExerciseSelection } from './ExerciseSelection';
import { EquipmentType, DayNumber } from '@/types/workout';
import type { ExerciseDefinition } from '@/types/workout';

// Create mock exercise library data
const mockMainLifts: ExerciseDefinition[] = [
  {
    name: 'Squat',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day1,
    suggestedOrder: 1,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Back Squat - primary lower body compound movement',
  },
  {
    name: 'Bench Press',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day1,
    suggestedOrder: 2,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Barbell Bench Press - primary pushing movement',
  },
  {
    name: 'Deadlift',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day2,
    suggestedOrder: 1,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Conventional Deadlift - primary pulling movement',
  },
  {
    name: 'Overhead Press',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day2,
    suggestedOrder: 2,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Standing Barbell Overhead Press - vertical pressing movement',
  },
];

const mockAuxiliaryLifts: ExerciseDefinition[] = [
  {
    name: 'Front Squat',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day3,
    suggestedOrder: 1,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Front-loaded squat variation',
  },
  {
    name: 'Incline Bench Press',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day3,
    suggestedOrder: 2,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Incline variation for upper chest emphasis',
  },
  {
    name: 'Romanian Deadlift',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day4,
    suggestedOrder: 1,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Hip-hinge movement targeting hamstrings',
  },
  {
    name: 'Close-Grip Bench Press',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day4,
    suggestedOrder: 2,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Narrow grip variation for triceps emphasis',
  },
  {
    name: 'Leg Press',
    equipment: EquipmentType.Machine,
    suggestedDay: DayNumber.Day1,
    suggestedOrder: 3,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Machine-based leg exercise',
  },
  {
    name: 'Barbell Row',
    equipment: EquipmentType.Barbell,
    suggestedDay: DayNumber.Day2,
    suggestedOrder: 3,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Bent-over barbell row for back thickness',
  },
];

const mockAccessories: ExerciseDefinition[] = [
  {
    name: 'Lat Pulldown',
    equipment: EquipmentType.Cable,
    suggestedDay: DayNumber.Day1,
    suggestedOrder: 5,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Cable lat pulldown for back width',
  },
  {
    name: 'Face Pull',
    equipment: EquipmentType.Cable,
    suggestedDay: DayNumber.Day2,
    suggestedOrder: 5,
    defaultRepRange: { minimum: 8, target: 12, maximum: 16 },
    defaultSets: 3,
    description: 'Cable face pull for rear deltoids',
  },
  {
    name: 'Lateral Raise',
    equipment: EquipmentType.Dumbbell,
    suggestedDay: DayNumber.Day1,
    suggestedOrder: 7,
    defaultRepRange: { minimum: 8, target: 12, maximum: 16 },
    defaultSets: 3,
    description: 'Dumbbell lateral raises for side delts',
  },
  {
    name: 'Bicep Curl',
    equipment: EquipmentType.Dumbbell,
    suggestedDay: DayNumber.Day3,
    suggestedOrder: 5,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Dumbbell bicep curls',
  },
  {
    name: 'Tricep Extension',
    equipment: EquipmentType.Cable,
    suggestedDay: DayNumber.Day3,
    suggestedOrder: 6,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Cable tricep extensions',
  },
  {
    name: 'Leg Curl',
    equipment: EquipmentType.Machine,
    suggestedDay: DayNumber.Day1,
    suggestedOrder: 6,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Machine leg curl for hamstrings',
  },
  {
    name: 'Calf Raise',
    equipment: EquipmentType.Machine,
    suggestedDay: DayNumber.Day2,
    suggestedOrder: 8,
    defaultRepRange: { minimum: 8, target: 12, maximum: 16 },
    defaultSets: 3,
    description: 'Standing or seated calf raises',
  },
  {
    name: 'Cable Crunch',
    equipment: EquipmentType.Cable,
    suggestedDay: DayNumber.Day5,
    suggestedOrder: 6,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Cable crunches for abs',
  },
];

// Mock the useExerciseLibrary hook
const mockLibrary = {
  mainLifts: mockMainLifts,
  auxiliaryLifts: mockAuxiliaryLifts,
  accessories: mockAccessories,
};

/**
 * ExerciseSelection component allows users to browse and select exercises from the library.
 * Main lifts are always included, while auxiliary and accessory exercises are optional.
 *
 * ## Features
 * - Browse exercises by category (Auxiliary vs Accessory)
 * - Toggle exercise selection with visual feedback
 * - View exercise details including equipment, rep ranges, and descriptions
 * - See selection summary at the bottom
 * - Main lifts always included (non-selectable)
 */
const meta = {
  title: 'Workout/ExerciseSelection',
  component: ExerciseSelection,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Exercise selection component for choosing auxiliary and accessory exercises to include in a workout program.',
      },
    },
    msw: {
      handlers: [],
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => {
      // Create a query client for each story
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
          },
        },
      });

      // Mock the exercise library query
      queryClient.setQueryData(['exerciseLibrary'], mockLibrary);

      return (
        <QueryClientProvider client={queryClient}>
          <div className="w-[900px] max-h-[800px] overflow-auto p-6 bg-background">
            <Story />
          </div>
        </QueryClientProvider>
      );
    },
  ],
  argTypes: {
    selectedExercises: {
      control: 'object',
      description: 'Currently selected exercises',
    },
    onUpdate: {
      action: 'updated',
      description: 'Callback when exercise selection changes',
    },
  },
} satisfies Meta<typeof ExerciseSelection>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state with no exercises selected.
 */
export const Default: Story = {
  args: {
    selectedExercises: [],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
};

/**
 * Component showing the Auxiliary Lifts tab active.
 */
export const AuxiliaryTab: Story = {
  args: {
    selectedExercises: [],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows the auxiliary lifts tab which contains compound movements like Front Squat, Romanian Deadlift, etc.',
      },
    },
  },
};

/**
 * A few auxiliary exercises pre-selected.
 */
export const WithAuxiliarySelected: Story = {
  args: {
    selectedExercises: [
      mockAuxiliaryLifts[0], // Front Squat
      mockAuxiliaryLifts[2], // Romanian Deadlift
      mockAuxiliaryLifts[5], // Barbell Row
    ],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
};

/**
 * Multiple accessory exercises pre-selected.
 */
export const WithAccessoriesSelected: Story = {
  args: {
    selectedExercises: [
      mockAccessories[0], // Lat Pulldown
      mockAccessories[1], // Face Pull
      mockAccessories[2], // Lateral Raise
      mockAccessories[3], // Bicep Curl
      mockAccessories[4], // Tricep Extension
    ],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
};

/**
 * A comprehensive selection with both auxiliary and accessory exercises.
 */
export const FullSelection: Story = {
  args: {
    selectedExercises: [
      // Auxiliary
      mockAuxiliaryLifts[0], // Front Squat
      mockAuxiliaryLifts[2], // Romanian Deadlift
      mockAuxiliaryLifts[5], // Barbell Row
      // Accessories
      mockAccessories[0], // Lat Pulldown
      mockAccessories[1], // Face Pull
      mockAccessories[2], // Lateral Raise
      mockAccessories[3], // Bicep Curl
      mockAccessories[4], // Tricep Extension
      mockAccessories[5], // Leg Curl
      mockAccessories[6], // Calf Raise
    ],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows a realistic selection with multiple exercises from both auxiliary and accessory categories.',
      },
    },
  },
};

/**
 * Minimal selection - user only wants the main lifts with minimal extras.
 */
export const MinimalSelection: Story = {
  args: {
    selectedExercises: [
      mockAuxiliaryLifts[0], // Just one auxiliary
    ],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
};

/**
 * Maximum selection - user has selected many exercises for a high volume program.
 */
export const MaximalSelection: Story = {
  args: {
    selectedExercises: [
      ...mockAuxiliaryLifts,
      ...mockAccessories,
    ],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows the component when all available exercises are selected for a high-volume training program.',
      },
    },
  },
};

/**
 * Loading state - while exercise library is being fetched.
 */
export const Loading: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
          },
        },
      });

      // Don't set any data to simulate loading
      return (
        <QueryClientProvider client={queryClient}>
          <div className="w-[900px] max-h-[800px] overflow-auto p-6 bg-background">
            <Story />
          </div>
        </QueryClientProvider>
      );
    },
  ],
  args: {
    selectedExercises: [],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
};

/**
 * Empty library state - when no exercises are available.
 */
export const EmptyLibrary: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
          },
        },
      });

      // Mock empty library
      queryClient.setQueryData(['exerciseLibrary'], {
        mainLifts: mockMainLifts,
        auxiliaryLifts: [],
        accessories: [],
      });

      return (
        <QueryClientProvider client={queryClient}>
          <div className="w-[900px] max-h-[800px] overflow-auto p-6 bg-background">
            <Story />
          </div>
        </QueryClientProvider>
      );
    },
  ],
  args: {
    selectedExercises: [],
    onUpdate: (exercises) => console.log('Selected exercises updated:', exercises),
  },
};
