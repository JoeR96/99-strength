import type { Meta, StoryObj } from '@storybook/react';
import { HeartRateForm } from './HeartRateSettings';
import { phoneParameters, withAppProviders } from '@/mocks/storyProviders';

const meta = {
  title: 'Features/Settings/HeartRateForm',
  component: HeartRateForm,
  parameters: phoneParameters,
  decorators: [withAppProviders('/settings')],
} satisfies Meta<typeof HeartRateForm>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Max HR 190 with WHOOP's zones: each zone shows its bpm range. */
export const Whoop: Story = { args: { zones: { maxHeartRate: 190, lowerPercents: [50, 60, 70, 80, 90], bands: [] } } };

/** No max HR yet: zones show percentages only and tracks judge on effort. */
export const NoMaxHeartRate: Story = { args: { zones: { maxHeartRate: null, lowerPercents: [50, 60, 70, 80, 90], bands: [] } } };
