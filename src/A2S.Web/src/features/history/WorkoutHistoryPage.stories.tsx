import type { Meta, StoryObj } from '@storybook/react-vite';
import { userEvent, within } from 'storybook/test';
import { WorkoutHistoryPage } from './WorkoutHistoryPage';

/** /history — 41 sessions over 11 weeks: summary, training calendar, one-rep max and per-exercise progress. */
const meta = {
  title: 'Pages/History',
  component: WorkoutHistoryPage,
  parameters: { route: { path: '/history' } },
} satisfies Meta<typeof WorkoutHistoryPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Opens on the latest session. */
export const Overview: Story = {};

/** Week 10's press day picked from the calendar. */
export const SessionDetail: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(await canvas.findByRole('button', { name: /week 10, day 4/i }));
  },
};

export const ExerciseProgress: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(await canvas.findByRole('button', { name: 'Exercise Progress' }));
  },
};
