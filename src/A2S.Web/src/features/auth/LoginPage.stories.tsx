import type { Meta, StoryObj } from '@storybook/react-vite';
import { LoginPage } from './LoginPage';

/**
 * /sign-in — Clerk's real sign-in widget (development instance, see
 * src/mocks/storybook.tsx for the publishable key) inside the auth shell.
 * Needs network access to *.clerk.accounts.dev.
 */
const meta = {
  title: 'Pages/Sign In',
  component: LoginPage,
  parameters: { route: { path: '/sign-in/*', url: '/sign-in' }, clerk: 'real' },
} satisfies Meta<typeof LoginPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = { args: { routing: 'hash' } };
