import type { Meta, StoryObj } from '@storybook/react';
import { ExerciseTemplateCard } from './ExerciseTemplateCard';
import { EquipmentType } from '@/types/workout';

/**
 * ExerciseTemplateCard displays a single exercise template from the library
 * with an "Add" button to select it for the workout program.
 *
 * ## Features
 * - Shows exercise name, equipment type, and description
 * - Displays default sets and rep ranges
 * - Add button to select the exercise
 */
const meta = {
  title: 'Workout/ExerciseSelectionV2/ExerciseTemplateCard',
  component: ExerciseTemplateCard,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Card component for displaying an exercise template with add functionality.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[400px] p-6 bg-background">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    template: {
      control: 'object',
      description: 'Exercise template data',
    },
    onAdd: {
      action: 'add-clicked',
      description: 'Callback when add button is clicked',
    },
  },
} satisfies Meta<typeof ExerciseTemplateCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Main lift template (Barbell Squat)
 */
export const MainLift: Story = {
  args: {
    template: {
      name: 'Squat',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
      defaultSets: 4,
      description: 'Back squat - primary lower body compound movement',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Auxiliary lift template (Romanian Deadlift)
 */
export const AuxiliaryLift: Story = {
  args: {
    template: {
      name: 'Romanian Deadlift',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 6, target: 8, maximum: 10 },
      defaultSets: 3,
      description: 'Hip-hinge movement targeting hamstrings and glutes',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Dumbbell accessory exercise
 */
export const DumbbellAccessory: Story = {
  args: {
    template: {
      name: 'Dumbbell Lateral Raise',
      equipment: EquipmentType.Dumbbell,
      defaultRepRange: { minimum: 10, target: 12, maximum: 15 },
      defaultSets: 3,
      description: 'Isolation exercise for side deltoids',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Cable machine exercise
 */
export const CableExercise: Story = {
  args: {
    template: {
      name: 'Face Pull',
      equipment: EquipmentType.Cable,
      defaultRepRange: { minimum: 12, target: 15, maximum: 20 },
      defaultSets: 3,
      description: 'Rear delt and upper back exercise for shoulder health',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Bodyweight exercise
 */
export const BodyweightExercise: Story = {
  args: {
    template: {
      name: 'Pull-up',
      equipment: EquipmentType.Bodyweight,
      defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
      defaultSets: 3,
      description: 'Bodyweight vertical pull exercise',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Machine exercise
 */
export const MachineExercise: Story = {
  args: {
    template: {
      name: 'Leg Press',
      equipment: EquipmentType.Machine,
      defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
      defaultSets: 3,
      description: 'Machine-based leg exercise for quad and glute development',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Exercise with no default sets or rep range
 */
export const MinimalData: Story = {
  args: {
    template: {
      name: 'Plank',
      equipment: EquipmentType.Bodyweight,
      description: 'Core stability exercise - hold for time',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Exercise with long name and description
 */
export const LongContent: Story = {
  args: {
    template: {
      name: 'Close Grip Incline Bench Press with Pause',
      equipment: EquipmentType.Barbell,
      defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
      defaultSets: 4,
      description:
        'A variation of the bench press performed on an incline bench with a narrow grip and pause at the bottom. This exercise emphasizes the upper chest and triceps while building strength out of the bottom position.',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};

/**
 * Smith machine exercise
 */
export const SmithMachineExercise: Story = {
  args: {
    template: {
      name: 'Smith Machine Squat',
      equipment: EquipmentType.SmithMachine,
      defaultRepRange: { minimum: 8, target: 10, maximum: 12 },
      defaultSets: 3,
      description: 'Squat variation using the Smith machine for stability',
    },
    onAdd: (template) => console.log('Added:', template.name),
  },
};
