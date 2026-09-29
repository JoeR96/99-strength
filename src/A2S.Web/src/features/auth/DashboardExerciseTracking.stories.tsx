import type { Meta, StoryObj } from '@storybook/react-vite';
import { DashboardExerciseTracking } from './DashboardExerciseTracking';
import { currentWorkout } from '@/mocks/fixtures/program';

/** Dashboard progression charts: training max per main lift, volume per accessory. */
const meta = {
  title: 'Features/Charts/Progression Charts',
  component: DashboardExerciseTracking,
  decorators: [
    (Story) => (
      <div className="bg-background p-6">
        <div className="mx-auto grid max-w-6xl gap-6 md:grid-cols-2 lg:grid-cols-3">
          <Story />
        </div>
      </div>
    ),
  ],
  args: { workout: currentWorkout },
} satisfies Meta<typeof DashboardExerciseTracking>;

export default meta;
type Story = StoryObj<typeof meta>;

export const MidProgram: Story = {};
