import type { Meta, StoryObj } from '@storybook/react-vite';
import { SignUpPage } from './SignUpPage';

/** /sign-up — Clerk's real sign-up widget inside the auth shell (see LoginPage.stories). */
const meta = {
  title: 'Pages/Sign Up',
  component: SignUpPage,
  parameters: { route: { path: '/sign-up/*', url: '/sign-up' }, clerk: 'real' },
} satisfies Meta<typeof SignUpPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = { args: { routing: 'hash' } };
