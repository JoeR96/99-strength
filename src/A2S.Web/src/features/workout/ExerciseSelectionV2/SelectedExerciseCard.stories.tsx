import type { Meta, StoryObj } from '@storybook/react';
import { SelectedExerciseCard } from './SelectedExerciseCard';
import { EquipmentType, ExerciseCategory } from '@/types/workout';
import type { SelectedExercise } from '@/types/workout';

/**
 * SelectedExerciseCard displays a configured exercise with its settings
 * and provides edit and remove actions.
 *
 * ## Features
 * - Shows exercise name and configuration (category, progression, day)
 * - Drag handle for reordering
 * - Edit button to modify configuration
 * - Remove button to delete from selection
 * - Color-coded category badges
 */
const meta = {
  title: 'Workout/ExerciseSelectionV2/SelectedExerciseCard',
  component: SelectedExerciseCard,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Card component for displaying a configured exercise with edit and remove actions.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[500px] p-6 bg-background">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    exercise: {
      control: 'object',
      description: 'Selected and configured exercise',
    },
    onEdit: {
      action: 'edit-clicked',
      description: 'Callback when edit button is clicked',
    },
    onRemove: {
      action: 'remove-clicked',
      description: 'Callback when remove button is clicked',
    },
    isDragging: {
      control: 'boolean',
      description: 'Whether the card is being dragged',
    },
  },
} satisfies Meta<typeof SelectedExerciseCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Main lift with linear progression
 */
export const MainLiftLinear: Story = {
  args: {
    exercise: {
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
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};

/**
 * Auxiliary lift with linear progression
 */
export const AuxiliaryLinear: Story = {
  args: {
    exercise: {
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
      assignedDay: 2,
      orderInDay: 2,
    },
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};

/**
 * Accessory exercise with RepsPerSet progression
 */
export const AccessoryRepsPerSet: Story = {
  args: {
    exercise: {
      id: '3',
      template: {
        name: 'Dumbbell Lateral Raise',
        equipment: EquipmentType.Dumbbell,
        defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
        defaultSets: 3,
        description: 'Isolation exercise for side deltoids',
      },
      category: ExerciseCategory.Accessory,
      progressionType: 'RepsPerSet',
      assignedDay: 3,
      orderInDay: 3,
    },
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};

/**
 * Bodyweight exercise
 */
export const BodyweightExercise: Story = {
  args: {
    exercise: {
      id: '4',
      template: {
        name: 'Pull-up',
        equipment: EquipmentType.Bodyweight,
        defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
        defaultSets: 3,
        description: 'Bodyweight vertical pull exercise',
      },
      category: ExerciseCategory.Auxiliary,
      progressionType: 'RepsPerSet',
      assignedDay: 4,
      orderInDay: 1,
    },
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};

/**
 * Cable exercise on day 5
 */
export const CableExerciseDayFive: Story = {
  args: {
    exercise: {
      id: '5',
      template: {
        name: 'Face Pull',
        equipment: EquipmentType.Cable,
        defaultRepRange: { minimum: 12, target: 15, maximum: 20 },
        defaultSets: 3,
        description: 'Rear delt and upper back exercise for shoulder health',
      },
      category: ExerciseCategory.Accessory,
      progressionType: 'RepsPerSet',
      assignedDay: 5,
      orderInDay: 4,
    },
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};

/**
 * Machine exercise on day 6
 */
export const MachineExerciseDaySix: Story = {
  args: {
    exercise: {
      id: '6',
      template: {
        name: 'Leg Press',
        equipment: EquipmentType.Machine,
        defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
        defaultSets: 3,
        description: 'Machine-based leg exercise for quad and glute development',
      },
      category: ExerciseCategory.Accessory,
      progressionType: 'RepsPerSet',
      assignedDay: 6,
      orderInDay: 2,
    },
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};

/**
 * Card in dragging state
 */
export const Dragging: Story = {
  args: {
    exercise: {
      id: '7',
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
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: true,
  },
};

/**
 * Exercise with long name
 */
export const LongName: Story = {
  args: {
    exercise: {
      id: '8',
      template: {
        name: 'Close Grip Incline Bench Press with Pause',
        equipment: EquipmentType.Barbell,
        defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
        defaultSets: 4,
        description:
          'A variation of the bench press performed on an incline bench with a narrow grip and pause at the bottom',
      },
      category: ExerciseCategory.Auxiliary,
      progressionType: 'Linear',
      assignedDay: 3,
      orderInDay: 2,
    },
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};

/**
 * Exercise with no description
 */
export const NoDescription: Story = {
  args: {
    exercise: {
      id: '9',
      template: {
        name: 'Plank',
        equipment: EquipmentType.Bodyweight,
        description: '',
      },
      category: ExerciseCategory.Accessory,
      progressionType: 'RepsPerSet',
      assignedDay: 1,
      orderInDay: 5,
    },
    onEdit: (exercise) => console.log('Edit:', exercise.template.name),
    onRemove: (id) => console.log('Remove:', id),
    isDragging: false,
  },
};
