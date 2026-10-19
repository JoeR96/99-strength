import type { Meta, StoryObj } from '@storybook/react-vite';
import { SettingsPage } from './SettingsPage';

/** /settings — test-data seeding and program export. */
const meta = {
  title: 'Pages/Settings',
  component: SettingsPage,
  parameters: { route: { path: '/settings' } },
} satisfies Meta<typeof SettingsPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};
