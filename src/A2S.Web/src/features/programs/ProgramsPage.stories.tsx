import type { Meta, StoryObj } from '@storybook/react-vite';
import { ProgramsPage } from './ProgramsPage';
import { handlers, noProgramHandlers } from '@/mocks/handlers';

/** /programs — the active program, a finished one and a draft. */
const meta = {
  title: 'Pages/Programs',
  component: ProgramsPage,
  parameters: { route: { path: '/programs' } },
} satisfies Meta<typeof ProgramsPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ThreePrograms: Story = {};

export const Empty: Story = {
  parameters: { msw: { handlers: [...noProgramHandlers, ...handlers] } },
};
