import type { Meta, StoryObj } from '@storybook/react';
import { TrainingMaxInput } from './TrainingMaxInput';
import { WeightUnit } from '@/types/workout';
import type { TrainingMax } from '@/types/workout';

interface TrainingMaxValues {
  squat: TrainingMax;
  bench: TrainingMax;
  deadlift: TrainingMax;
  overheadPress: TrainingMax;
}

/**
 * TrainingMaxInput component allows users to input their training maxes for the four main lifts.
 * Training maxes should be approximately 90-95% of the user's true 1-rep max and are used to
 * calculate working weights throughout the A2S program.
 *
 * ## Features
 * - Toggle between Kilograms and Pounds
 * - Individual inputs for each main lift
 * - Helpful educational information about training maxes
 * - Step increments of 2.5 for precise weight entry
 */
const meta = {
  title: 'Workout/TrainingMaxInput',
  component: TrainingMaxInput,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Component for inputting training maxes for the main compound lifts in the A2S program.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[800px] p-6 bg-background">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    trainingMaxes: {
      control: 'object',
      description: 'Current training max values for all four main lifts',
    },
    onUpdate: {
      action: 'updated',
      description: 'Callback when training maxes are updated',
    },
  },
} satisfies Meta<typeof TrainingMaxInput>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state with kilogram measurements and typical beginner-intermediate values.
 */
export const Default: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 100, unit: WeightUnit.Kilograms },
      bench: { value: 80, unit: WeightUnit.Kilograms },
      deadlift: { value: 120, unit: WeightUnit.Kilograms },
      overheadPress: { value: 60, unit: WeightUnit.Kilograms },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
};

/**
 * Training maxes displayed in pounds for users in the US.
 */
export const InPounds: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 225, unit: WeightUnit.Pounds },
      bench: { value: 185, unit: WeightUnit.Pounds },
      deadlift: { value: 275, unit: WeightUnit.Pounds },
      overheadPress: { value: 135, unit: WeightUnit.Pounds },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
};

/**
 * Advanced lifter with higher training maxes in kilograms.
 */
export const AdvancedLifter: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 180, unit: WeightUnit.Kilograms },
      bench: { value: 140, unit: WeightUnit.Kilograms },
      deadlift: { value: 220, unit: WeightUnit.Kilograms },
      overheadPress: { value: 95, unit: WeightUnit.Kilograms },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
};

/**
 * Elite powerlifter with very high training maxes in pounds.
 */
export const ElitePowerlifter: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 500, unit: WeightUnit.Pounds },
      bench: { value: 405, unit: WeightUnit.Pounds },
      deadlift: { value: 600, unit: WeightUnit.Pounds },
      overheadPress: { value: 275, unit: WeightUnit.Pounds },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
};

/**
 * Beginner lifter starting with conservative training maxes.
 */
export const BeginnerLifter: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 60, unit: WeightUnit.Kilograms },
      bench: { value: 40, unit: WeightUnit.Kilograms },
      deadlift: { value: 80, unit: WeightUnit.Kilograms },
      overheadPress: { value: 30, unit: WeightUnit.Kilograms },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
};

/**
 * Interactive demo showing the component with starting values that can be modified.
 * Try changing the weight unit toggle or adjusting individual lift values.
 */
export const Interactive: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 100, unit: WeightUnit.Kilograms },
      bench: { value: 80, unit: WeightUnit.Kilograms },
      deadlift: { value: 120, unit: WeightUnit.Kilograms },
      overheadPress: { value: 60, unit: WeightUnit.Kilograms },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
  parameters: {
    docs: {
      description: {
        story: 'Interactive version where you can test weight unit switching and value updates.',
      },
    },
  },
};

/**
 * Female lifter with typical training maxes in kilograms.
 */
export const FemaleLifter: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 70, unit: WeightUnit.Kilograms },
      bench: { value: 45, unit: WeightUnit.Kilograms },
      deadlift: { value: 90, unit: WeightUnit.Kilograms },
      overheadPress: { value: 30, unit: WeightUnit.Kilograms },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
};

/**
 * Zero values - edge case for new users who haven't entered any data yet.
 */
export const EmptyState: Story = {
  args: {
    trainingMaxes: {
      squat: { value: 0, unit: WeightUnit.Kilograms },
      bench: { value: 0, unit: WeightUnit.Kilograms },
      deadlift: { value: 0, unit: WeightUnit.Kilograms },
      overheadPress: { value: 0, unit: WeightUnit.Kilograms },
    },
    onUpdate: (values) => console.log('Training maxes updated:', values),
  },
};
