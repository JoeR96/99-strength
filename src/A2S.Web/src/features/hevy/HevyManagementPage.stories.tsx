import type { Meta, StoryObj } from '@storybook/react-vite';
import { HevyManagementPage } from './HevyManagementPage';
import { handlers, hevyDisconnectedHandlers } from '@/mocks/handlers';

/** /hevy — connected account with this week's routines synced into the program folder. */
const meta = {
  title: 'Pages/Hevy',
  component: HevyManagementPage,
  parameters: { route: { path: '/hevy' } },
} satisfies Meta<typeof HevyManagementPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Connected: Story = {};

export const NotConnected: Story = {
  parameters: { msw: { handlers: [...hevyDisconnectedHandlers, ...handlers] } },
};
