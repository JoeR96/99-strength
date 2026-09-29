import type { Meta, StoryObj } from '@storybook/react-vite';
import { DashboardPage } from './DashboardPage';
import { handlers, loadingHandlers, noProgramHandlers } from '@/mocks/handlers';

/**
 * /dashboard — week 11 of a 21-week A2S program (see src/mocks/fixtures/program.ts).
 * Data comes from the MSW handlers in src/mocks; no API monkey-patching.
 */
const meta = {
  title: 'Pages/Dashboard',
  component: DashboardPage,
  parameters: { route: { path: '/dashboard' } },
} satisfies Meta<typeof DashboardPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const MidProgram: Story = {};

export const NoProgram: Story = {
  parameters: { msw: { handlers: [...noProgramHandlers, ...handlers] } },
};

export const Loading: Story = {
  parameters: { msw: { handlers: loadingHandlers } },
};
