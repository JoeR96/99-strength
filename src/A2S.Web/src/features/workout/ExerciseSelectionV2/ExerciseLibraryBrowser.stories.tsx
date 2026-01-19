import type { Meta, StoryObj } from '@storybook/react';
import { ExerciseLibraryBrowser } from './ExerciseLibraryBrowser';
import { EquipmentType } from '@/types/workout';
import type { ExerciseTemplate } from '@/types/workout';

/**
 * Mock exercise library with a comprehensive set of exercises
 */
const mockTemplates: ExerciseTemplate[] = [
  // Main Lifts
  {
    name: 'Squat',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Back squat - primary lower body compound movement',
  },
  {
    name: 'Bench Press',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Barbell bench press - primary upper body push movement',
  },
  {
    name: 'Deadlift',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 3,
    description: 'Conventional deadlift - primary posterior chain movement',
  },
  {
    name: 'Overhead Press',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Standing barbell overhead press - primary shoulder movement',
  },

  // Barbell Auxiliary Lifts
  {
    name: 'Front Squat',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
    defaultSets: 3,
    description: 'Front-loaded squat variation emphasizing quads and upper back',
  },
  {
    name: 'Paused Squat',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 3,
    description: 'Squat with pause at bottom to build strength out of the hole',
  },
  {
    name: 'Romanian Deadlift',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
    defaultSets: 3,
    description: 'Hip-hinge movement targeting hamstrings and glutes',
  },
  {
    name: 'Barbell Row',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
    defaultSets: 4,
    description: 'Bent-over row for back thickness and strength',
  },
  {
    name: 'Incline Bench Press',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
    defaultSets: 3,
    description: 'Upper chest emphasis bench press variation',
  },
  {
    name: 'Close Grip Bench Press',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
    defaultSets: 3,
    description: 'Tricep-focused bench press variation',
  },

  // Dumbbell Exercises
  {
    name: 'Dumbbell Bench Press',
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Greater range of motion bench press variation',
  },
  {
    name: 'Dumbbell Row',
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Single-arm row for back development',
  },
  {
    name: 'Dumbbell Shoulder Press',
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Seated or standing shoulder press with dumbbells',
  },
  {
    name: 'Dumbbell Lateral Raise',
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: 'Isolation exercise for side deltoids',
  },
  {
    name: 'Dumbbell Curl',
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Bicep curl with dumbbells',
  },
  {
    name: 'Dumbbell Goblet Squat',
    equipment: EquipmentType.Dumbbell,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: 'Front-loaded squat holding a single dumbbell',
  },

  // Cable Exercises
  {
    name: 'Cable Row',
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: 'Seated cable row for back development',
  },
  {
    name: 'Lat Pulldown',
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Pull-down movement for lats and upper back',
  },
  {
    name: 'Cable Fly',
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: 'Chest isolation exercise using cables',
  },
  {
    name: 'Cable Tricep Pushdown',
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: 'Tricep extension using cable machine',
  },
  {
    name: 'Face Pull',
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 12, target: 15, maximum: 20 },
    defaultSets: 3,
    description: 'Rear delt and upper back exercise for shoulder health',
  },

  // Machine Exercises
  {
    name: 'Leg Press',
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Machine-based leg exercise for quad and glute development',
  },
  {
    name: 'Leg Curl',
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: 'Hamstring isolation exercise',
  },
  {
    name: 'Leg Extension',
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
    defaultSets: 3,
    description: 'Quad isolation exercise',
  },
  {
    name: 'Chest Press Machine',
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Machine-based chest press',
  },
  {
    name: 'Shoulder Press Machine',
    equipment: EquipmentType.Machine,
    defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
    defaultSets: 3,
    description: 'Machine-based overhead press',
  },

  // Bodyweight Exercises
  {
    name: 'Pull-up',
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
    defaultSets: 3,
    description: 'Bodyweight vertical pull exercise',
  },
  {
    name: 'Chin-up',
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
    defaultSets: 3,
    description: 'Underhand grip pull-up variation',
  },
  {
    name: 'Dip',
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
    defaultSets: 3,
    description: 'Bodyweight tricep and chest exercise',
  },
  {
    name: 'Push-up',
    equipment: EquipmentType.Bodyweight,
    defaultRepRange: { minimum: 10, target: 15, maximum: 20 },
    defaultSets: 3,
    description: 'Classic bodyweight push exercise',
  },
  {
    name: 'Plank',
    equipment: EquipmentType.Bodyweight,
    defaultSets: 3,
    description: 'Core stability exercise - hold for time',
  },
];

/**
 * ExerciseLibraryBrowser allows users to browse, search, and filter exercises from the library.
 * Users can add exercises to their program by clicking the "Add" button.
 *
 * ## Features
 * - Search by exercise name or description
 * - Filter by equipment type
 * - Displays exercise metadata (sets, reps, equipment)
 * - Responsive grid layout
 */
const meta = {
  title: 'Workout/ExerciseSelectionV2/ExerciseLibraryBrowser',
  component: ExerciseLibraryBrowser,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Component for browsing and selecting exercises from the exercise library.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[600px] p-6 bg-background">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    templates: {
      control: 'object',
      description: 'Array of exercise templates from the library',
    },
    onAddExercise: {
      action: 'exercise-added',
      description: 'Callback when an exercise is added',
    },
  },
} satisfies Meta<typeof ExerciseLibraryBrowser>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default view with full exercise library
 */
export const Default: Story = {
  args: {
    templates: mockTemplates,
    onAddExercise: (template) => console.log('Added:', template.name),
  },
};

/**
 * Library with only barbell exercises
 */
export const BarbellOnly: Story = {
  args: {
    templates: mockTemplates.filter((t) => t.equipment === EquipmentType.Barbell),
    onAddExercise: (template) => console.log('Added:', template.name),
  },
};

/**
 * Library with only bodyweight exercises
 */
export const BodyweightOnly: Story = {
  args: {
    templates: mockTemplates.filter((t) => t.equipment === EquipmentType.Bodyweight),
    onAddExercise: (template) => console.log('Added:', template.name),
  },
};

/**
 * Small library with just a few exercises
 */
export const SmallLibrary: Story = {
  args: {
    templates: mockTemplates.slice(0, 5),
    onAddExercise: (template) => console.log('Added:', template.name),
  },
};

/**
 * Empty library state
 */
export const EmptyLibrary: Story = {
  args: {
    templates: [],
    onAddExercise: (template) => console.log('Added:', template.name),
  },
};

/**
 * Library with mixed equipment types
 */
export const MixedEquipment: Story = {
  args: {
    templates: [
      ...mockTemplates.filter((t) => t.equipment === EquipmentType.Barbell).slice(0, 3),
      ...mockTemplates.filter((t) => t.equipment === EquipmentType.Dumbbell).slice(0, 3),
      ...mockTemplates.filter((t) => t.equipment === EquipmentType.Cable).slice(0, 3),
      ...mockTemplates.filter((t) => t.equipment === EquipmentType.Bodyweight).slice(0, 3),
    ],
    onAddExercise: (template) => console.log('Added:', template.name),
  },
};
