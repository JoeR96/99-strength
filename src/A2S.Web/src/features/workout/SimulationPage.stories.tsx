import type { Meta, StoryObj } from '@storybook/react-vite';
import { expect, userEvent, waitFor, within } from 'storybook/test';
import { SimulationPage } from './SimulationPage';

/**
 * /simulate — projects the rest of the program with the real progression rules and
 * simulated AMRAP results. The persistent run streams NDJSON day by day.
 */
const meta = {
  title: 'Pages/Simulator',
  component: SimulationPage,
  parameters: { route: { path: '/simulate' } },
} satisfies Meta<typeof SimulationPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Ready: Story = {};

/** The completed projection: training-max and accessory charts plus one lift in detail. */
export const Projection: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const sessions = await canvas.findByLabelText('Sessions');
    // The field clamps to ≥ 1, so select-and-replace rather than clear.
    await userEvent.tripleClick(sessions);
    await userEvent.keyboard('40');
    const run = canvas.getByRole('button', { name: 'Run Simulation' });
    await waitFor(() => expect(run).toBeEnabled());
    await userEvent.click(run);
    await userEvent.click(
      await canvas.findByRole('button', { name: 'Squat (Barbell)' }, { timeout: 5000 })
    );
  },
};

/** A persistent day-by-day run, streamed as NDJSON and logged as it arrives. */
export const PersistentRun: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const days = await canvas.findByLabelText('Days');
    await userEvent.tripleClick(days);
    await userEvent.keyboard('24');
    const run = canvas.getByRole('button', { name: 'Run Persistent' });
    await waitFor(() => expect(run).toBeEnabled());
    await userEvent.click(run);
    await canvas.findByText(/done/, undefined, { timeout: 5000 });
  },
};
