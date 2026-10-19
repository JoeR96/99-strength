import type { Meta, StoryObj } from '@storybook/react-vite';
import { userEvent, within } from 'storybook/test';
import { ExerciseHistoryChart } from './ExerciseHistoryChart';
import { exerciseHistoryFor } from '@/mocks/fixtures/exerciseHistory';

const squat = exerciseHistoryFor('Squat (Barbell)')!;
const bench = exerciseHistoryFor('Bench Press (Barbell)')!;

/** Weight, volume and estimated 1RM over time for one lift, across two programs. */
const meta = {
  title: 'Features/Charts/Exercise History Chart',
  component: ExerciseHistoryChart,
  decorators: [
    (Story) => (
      <div className="bg-background p-6">
        <div className="mx-auto max-w-4xl rounded-lg border border-border bg-card p-6">
          <Story />
        </div>
      </div>
    ),
  ],
  args: { sessions: squat.sessions, weightUnit: 'Kilograms' },
} satisfies Meta<typeof ExerciseHistoryChart>;

export default meta;
type Story = StoryObj<typeof meta>;

export const SquatMaxWeight: Story = {};

export const SquatEstimatedOneRepMax: Story = {
  play: async ({ canvasElement }) => {
    await userEvent.click(within(canvasElement).getByRole('button', { name: 'Est. 1RM (Epley)' }));
  },
};

export const BenchVolumeLastThreeMonths: Story = {
  args: { sessions: bench.sessions },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: '3M' }));
    await userEvent.click(canvas.getByRole('button', { name: 'Volume' }));
  },
};
