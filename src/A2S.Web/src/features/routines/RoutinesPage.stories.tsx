import type { Meta, StoryObj } from '@storybook/react';
import { RoutinesPage } from './RoutinesPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installMobilityRoutinesHandlers, mobilityRoutinesHandlers } from '@/mocks/handlers/mobilityRoutines';

const meta = {
  title: 'Features/Routines/RoutinesPage',
  component: RoutinesPage,
  parameters: phoneParameters,
  decorators: [withAppProviders('/routines')],
} satisfies Meta<typeof RoutinesPage>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = { decorators: [withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.default()))] };
export const Empty: Story = { decorators: [withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.empty()))] };
export const Error: Story = { decorators: [withApiMocks(() => installMobilityRoutinesHandlers(mobilityRoutinesHandlers.error()))] };
