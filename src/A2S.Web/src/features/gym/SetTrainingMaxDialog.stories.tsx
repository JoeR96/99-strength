import type { Meta, StoryObj } from '@storybook/react';
import { withAppProviders, phoneParameters } from '@/mocks/storyProviders';
import { BENCH_ID, freshSuggestion, staleSuggestion } from '@/mocks/fixtures/startingNumbers';
import { SetTrainingMaxDialog } from './SetTrainingMaxDialog';

/** "Set your training max" with a Hevy-based suggestion, older history, nothing on Hevy, and Hevy unavailable. */
const meta = {
  title: 'Features/Gym/SetTrainingMaxDialog',
  component: SetTrainingMaxDialog,
  parameters: phoneParameters,
  decorators: [withAppProviders('/gym/1')],
  args: { open: true, workoutId: 'w-1', exerciseId: BENCH_ID, exerciseName: 'Bench Press (Barbell)', onClose: () => {} },
} satisfies Meta<typeof SetTrainingMaxDialog>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithSuggestion: Story = { args: { suggestion: freshSuggestion } };
export const OlderHistory: Story = { args: { suggestion: staleSuggestion } };
export const NoHistory: Story = { args: { suggestion: null } };
export const HevyUnavailable: Story = { args: { suggestion: null, fallbackNote: 'Hevy unavailable — enter it yourself.' } };
