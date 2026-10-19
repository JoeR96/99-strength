import type { Meta, StoryObj } from '@storybook/react-vite';
import { userEvent, within } from 'storybook/test';
import { ExerciseLibraryPage } from './ExerciseLibraryPage';

/** /exercises — the Hevy exercise catalogue, grouped by muscle, with history drill-down. */
const meta = {
  title: 'Pages/Exercise Library',
  component: ExerciseLibraryPage,
  parameters: { route: { path: '/exercises' } },
} satisfies Meta<typeof ExerciseLibraryPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Grouped: Story = {};

export const FilteredToBarbell: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.type(await canvas.findByPlaceholderText('Search exercises...'), 'press');
    await userEvent.click(canvas.getByRole('button', { name: 'grid' }));
  },
};

/** Clicking an exercise opens its logged history (from Hevy): stats, chart, sessions. */
export const SquatHistory: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.type(
      await canvas.findByPlaceholderText('Search exercises...'),
      'Squat (Barbell)'
    );
    const matches = await canvas.findAllByText('Squat (Barbell)');
    await userEvent.click(matches[0]);
    await within(document.body).findByText(/Max Weight|Weight/, undefined, { timeout: 5000 });
  },
};
