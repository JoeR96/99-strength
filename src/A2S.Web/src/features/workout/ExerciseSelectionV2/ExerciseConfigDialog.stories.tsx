import type { Meta, StoryObj } from '@storybook/react';
import { ExerciseConfigDialog } from './ExerciseConfigDialog';
import { EquipmentType, ExerciseCategory, ProgramVariant } from '@/types/workout';
import type { SelectedExercise } from '@/types/workout';

const mockExercise: SelectedExercise = {
  id: '1',
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
};

const mockMainLift: SelectedExercise = {
  id: '2',
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
};

const mockAccessory: SelectedExercise = {
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
  orderInDay: 4,
};

/**
 * ExerciseConfigDialog allows users to configure exercise settings:
 * - Category (Main Lift / Auxiliary / Accessory)
 * - Progression type (Linear / RepsPerSet)
 * - Day assignment
 *
 * ## Features
 * - Modal overlay with backdrop
 * - Three category options with descriptions
 * - Two progression type options
 * - Day selector based on program variant
 * - Save and Cancel actions
 */
const meta = {
  title: 'Workout/ExerciseSelectionV2/ExerciseConfigDialog',
  component: ExerciseConfigDialog,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Dialog for configuring exercise category, progression type, and day assignment.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-screen h-screen flex items-center justify-center bg-background">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    exercise: {
      control: 'object',
      description: 'Exercise to configure',
    },
    isOpen: {
      control: 'boolean',
      description: 'Whether the dialog is open',
    },
    onClose: {
      action: 'closed',
      description: 'Callback when dialog is closed',
    },
    onSave: {
      action: 'saved',
      description: 'Callback when configuration is saved',
    },
    programVariant: {
      control: 'select',
      options: [4, 5, 6],
      description: 'Program variant (determines available days)',
    },
  },
} satisfies Meta<typeof ExerciseConfigDialog>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Configuring an auxiliary lift (4-day program)
 */
export const AuxiliaryLiftFourDay: Story = {
  args: {
    exercise: mockExercise,
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * Configuring a main lift (4-day program)
 */
export const MainLiftFourDay: Story = {
  args: {
    exercise: mockMainLift,
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * Configuring an accessory exercise (4-day program)
 */
export const AccessoryFourDay: Story = {
  args: {
    exercise: mockAccessory,
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * 5-day program variant (5 days available)
 */
export const FiveDayProgram: Story = {
  args: {
    exercise: mockExercise,
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.FiveDay,
  },
};

/**
 * 6-day program variant (all 6 days available)
 */
export const SixDayProgram: Story = {
  args: {
    exercise: mockExercise,
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.SixDay,
  },
};

/**
 * Closed state (dialog not visible)
 */
export const Closed: Story = {
  args: {
    exercise: mockExercise,
    isOpen: false,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * No exercise selected
 */
export const NoExercise: Story = {
  args: {
    exercise: null,
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * Exercise with long name
 */
export const LongExerciseName: Story = {
  args: {
    exercise: {
      id: '4',
      template: {
        name: 'Close Grip Incline Bench Press with Pause',
        equipment: EquipmentType.Barbell,
        defaultRepRange: { minimum: 5, target: 8, maximum: 10 },
        defaultSets: 4,
        description: 'A variation of the bench press performed on an incline bench',
      },
      category: ExerciseCategory.Auxiliary,
      progressionType: 'Linear',
      assignedDay: 2,
      orderInDay: 3,
    },
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.FourDay,
  },
};

/**
 * Bodyweight exercise with RepsPerSet
 */
export const BodyweightExercise: Story = {
  args: {
    exercise: {
      id: '5',
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
    isOpen: true,
    onClose: () => console.log('Dialog closed'),
    onSave: (id, updates) => console.log('Saved:', id, updates),
    programVariant: ProgramVariant.SixDay,
  },
};
