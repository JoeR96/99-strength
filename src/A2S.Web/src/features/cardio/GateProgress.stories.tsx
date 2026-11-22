import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { GateProgress } from './GateProgress';
import { phoneParameters } from '@/mocks/storyProviders';
import { runningGate } from '@/mocks/fixtures/gates';

const meta = {
  title: 'Features/Cardio/GateProgress',
  component: GateProgress,
  parameters: phoneParameters,
  args: { trackName: 'Walk → run', onStart: fn() },
} satisfies Meta<typeof GateProgress>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Trend 91.3 kg, walk builder 1 of 2 weeks, knee niggle 12 Nov. */
export const InProgress: Story = { args: { gate: runningGate(false) } };

/** Every condition met: the Start suggestion appears. */
export const AllMet: Story = { args: { gate: runningGate(true) } };
