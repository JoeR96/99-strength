import type { Meta, StoryObj } from '@storybook/react';
import { SelectedExercisesList } from './SelectedExercisesList';
import { EquipmentType, ExerciseCategory } from '@/types/workout';
import type { SelectedExercise } from '@/types/workout';

const mockExercises: SelectedExercise[] = [
  {
    id: '1',
    template: {
      name: 'Squat',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
      defaultSets: 4,
      description: 'Back squat - primary lower body compound movement',
    },
    category: ExerciseCategory.MainLift,
    progressionType: 'Linear',
    assignedDay: 1,
    orderInDay: 1,
  },
  {
    id: '2',
    template: {
      name: 'Romanian Deadlift',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
      defaultSets: 3,
      description: 'Hip-hinge movement targeting hamstrings and glutes',
    },
    category: ExerciseCategory.Auxiliary,
    progressionType: 'Linear',
    assignedDay: 1,
    orderInDay: 2,
  },
  {
    id: '3',
    template: {
      name: 'Leg Curl',
      equipment: EquipmentType.Machine,
      defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
      defaultSets: 3,
      description: 'Hamstring isolation exercise',
    },
    category: ExerciseCategory.Accessory,
    progressionType: 'RepsPerSet',
    assignedDay: 1,
    orderInDay: 3,
  },
  {
    id: '4',
    template: {
      name: 'Bench Press',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
      defaultSets: 4,
      description: 'Barbell bench press - primary upper body push movement',
    },
    category: ExerciseCategory.MainLift,
    progressionType: 'Linear',
    assignedDay: 2,
    orderInDay: 1,
  },
  {
    id: '5',
    template: {
      name: 'Incline Dumbbell Press',
      equipment: EquipmentType.Dumbbell,
      defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
      defaultSets: 3,
      description: 'Upper chest emphasis press variation',
    },
    category: ExerciseCategory.Auxiliary,
    progressionType: 'RepsPerSet',
    assignedDay: 2,
    orderInDay: 2,
  },
  {
    id: '6',
    template: {
      name: 'Cable Fly',
      equipment: EquipmentType.Cable,
      defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
      defaultSets: 3,
      description: 'Chest isolation exercise using cables',
    },
    category: ExerciseCategory.Accessory,
    progressionType: 'RepsPerSet',
    assignedDay: 2,
    orderInDay: 3,
  },
  {
    id: '7',
    template: {
      name: 'Deadlift',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
      defaultSets: 3,
      description: 'Conventional deadlift - primary posterior chain movement',
    },
    category: ExerciseCategory.MainLift,
    progressionType: 'Linear',
    assignedDay: 3,
    orderInDay: 1,
  },
  {
    id: '8',
    template: {
      name: 'Pull-up',
      equipment: EquipmentType.Bodyweight,
      defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
      defaultSets: 3,
      description: 'Bodyweight vertical pull exercise',
    },
    category: ExerciseCategory.Auxiliary,
    progressionType: 'RepsPerSet',
    assignedDay: 3,
    orderInDay: 2,
  },
  {
    id: '9',
    template: {
      name: 'Overhead Press',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
      defaultSets: 4,
      description: 'Standing barbell overhead press - primary shoulder movement',
    },
    category: ExerciseCategory.MainLift,
    progressionType: 'Linear',
    assignedDay: 4,
    orderInDay: 1,
  },
];

/**
 * SelectedExercisesList displays selected exercises with drag-and-drop reordering.
 * Can be displayed as a flat list or grouped by training day.
 *
 * ## Features
 * - Drag-and-drop reordering using @dnd-kit
 * - Flat list or grouped by day view
 * - Empty state when no exercises selected
 * - Edit and remove actions for each exercise
 */
const meta = {
  title: 'Workout/ExerciseSelectionV2/SelectedExercisesList',
  component: SelectedExercisesList,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'List of selected exercises with drag-and-drop reordering capability.',
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
    exercises: {
      control: 'object',
      description: 'Array of selected exercises',
    },
    onReorder: {
      action: 'reordered',
      description: 'Callback when exercises are reordered',
    },
    onEdit: {
      action: 'edit-clicked',
      description: 'Callback when edit button is clicked',
    },
    onRemove: {
      action: 'remove-clicked',
      description: 'Callback when remove button is clicked',
    },
    groupByDay: {
      control: 'boolean',
      description: 'Whether to group exercises by training day',
    },
  },
} satisfies Meta<typeof SelectedExercisesList>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Empty state - no exercises selected
 */
export const Empty: Story = {
  args: {
    exercises: [],
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: false,
  },
};

/**
 * Flat list view with a few exercises
 */
export const FlatListSmall: Story = {
  args: {
    exercises: mockExercises.slice(0, 3),
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: false,
  },
};

/**
 * Flat list view with all exercises
 */
export const FlatListFull: Story = {
  args: {
    exercises: mockExercises,
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: false,
  },
};

/**
 * Grouped by day - 4-day program
 */
export const GroupedByDayFourDay: Story = {
  args: {
    exercises: mockExercises,
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: true,
  },
};

/**
 * Grouped by day - single day only
 */
export const GroupedSingleDay: Story = {
  args: {
    exercises: mockExercises.filter((ex) => ex.assignedDay === 1),
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: true,
  },
};

/**
 * Grouped by day - only main lifts
 */
export const GroupedMainLiftsOnly: Story = {
  args: {
    exercises: mockExercises.filter((ex) => ex.category === ExerciseCategory.MainLift),
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: true,
  },
};

/**
 * Single exercise in flat view
 */
export const SingleExercise: Story = {
  args: {
    exercises: [mockExercises[0]],
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: false,
  },
};

/**
 * Mixed exercise types grouped by day
 */
export const MixedExerciseTypes: Story = {
  args: {
    exercises: [
      mockExercises[0], // Main Lift - Day 1
      mockExercises[1], // Auxiliary - Day 1
      mockExercises[2], // Accessory - Day 1
      {
        id: '10',
        template: {
          name: 'Face Pull',
          equipment: EquipmentType.Cable,
          defaultRepRange: { minimum: 12, target: 15, maximum: 20 },
          defaultSets: 3,
          description: 'Rear delt and upper back exercise',
        },
        category: ExerciseCategory.Accessory,
        progressionType: 'RepsPerSet',
        assignedDay: 2,
        orderInDay: 1,
      },
      {
        id: '11',
        template: {
          name: 'Dumbbell Curl',
          equipment: EquipmentType.Dumbbell,
          defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
          defaultSets: 3,
          description: 'Bicep curl',
        },
        category: ExerciseCategory.Accessory,
        progressionType: 'RepsPerSet',
        assignedDay: 2,
        orderInDay: 2,
      },
    ],
    onReorder: (start, end) => console.log('Reorder:', start, end),
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    groupByDay: true,
  },
};
