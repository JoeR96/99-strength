import type { Meta, StoryObj } from '@storybook/react-vite';
import { WorkoutDashboard } from './WorkoutDashboard';
import { handlers, noProgramHandlers } from '@/mocks/handlers';

/** /workout — program overview: blocks, progress, this week, next week, exercises by day. */
const meta = {
  title: 'Pages/Workout',
  component: WorkoutDashboard,
  parameters: { route: { path: '/workout' } },
} satisfies Meta<typeof WorkoutDashboard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const MidProgram: Story = {};

export const NoProgram: Story = {
  parameters: { msw: { handlers: [...noProgramHandlers, ...handlers] } },
};
