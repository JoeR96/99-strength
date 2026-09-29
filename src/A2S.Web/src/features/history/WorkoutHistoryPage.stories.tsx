import type { Meta, StoryObj } from '@storybook/react-vite';
import { userEvent, within } from 'storybook/test';
import { WorkoutHistoryPage } from './WorkoutHistoryPage';

/** /history — 41 sessions over 11 weeks: activity calendar and per-exercise progress. */
const meta = {
  title: 'Pages/History',
  component: WorkoutHistoryPage,
  parameters: { route: { path: '/history' } },
} satisfies Meta<typeof WorkoutHistoryPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Calendar: Story = {};

/** Week 10's press day opened from the calendar. */
export const SessionDetail: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(await canvas.findByRole('gridcell', { name: 'Week 10, Day 4' }));
  },
};

export const ExerciseProgress: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(await canvas.findByRole('button', { name: 'Exercise Progress' }));
    const squat = await canvas.findAllByText('Squat (Barbell)');
    await userEvent.click(squat[0]);
  },
};
