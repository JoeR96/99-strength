import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, userEvent, within } from 'storybook/test';
import { WorkoutSession } from './WorkoutSession';

/**
 * /workout/session/:day — logging today's session (week 11, day 2: bench day).
 * Working weights come from the training maxes in the program fixture.
 */
const meta = {
  title: 'Pages/Workout Session',
  component: WorkoutSession,
  parameters: { route: { path: '/workout/session/:day', url: '/workout/session/2' } },
} satisfies Meta<typeof WorkoutSession>;

export default meta;
type Story = StoryObj<typeof meta>;

export const BenchDay: Story = {};

/** Mid-session: the first three bench sets logged, AMRAP set up next with a rep count typed in. */
export const LoggingSets: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const card = within(await canvas.findByTestId('exercise-card-bench-press-(barbell)'));
    for (const n of [1, 2, 3]) {
      await userEvent.click(card.getByTestId(`complete-set-${n}`));
    }
    const amrapReps = card.getByTestId('reps-input-4');
    await userEvent.clear(amrapReps);
    await userEvent.type(amrapReps, '14');
    await expect(card.getAllByText('Done')).toHaveLength(3);
  },
};
