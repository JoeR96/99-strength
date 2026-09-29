import type { Meta, StoryObj } from '@storybook/react-vite';
import { userEvent, within } from 'storybook/test';
import { HevyDataPage } from './HevyDataPage';

/** /hevy/data — workouts pulled back from Hevy, newest first, with per-exercise history. */
const meta = {
  title: 'Pages/Hevy Data',
  component: HevyDataPage,
  parameters: { route: { path: '/hevy/data' } },
} satisfies Meta<typeof HevyDataPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const SyncedWorkouts: Story = {};

export const BenchHistory: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const bench = await canvas.findAllByText('Bench Press (Barbell)', undefined, { timeout: 5000 });
    await userEvent.click(bench[0]);
  },
};
