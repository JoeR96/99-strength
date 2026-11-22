import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { PhaseHeadsUpCard } from './PhaseHeadsUpCard';
import { phoneParameters } from '@/mocks/storyProviders';
import { buildHeadsUp } from '@/mocks/fixtures/phaseHeadsUp';

const meta = {
  title: 'Features/Today/PhaseHeadsUpCard',
  component: PhaseHeadsUpCard,
  parameters: phoneParameters,
  args: { onSwap: fn() },
} satisfies Meta<typeof PhaseHeadsUpCard>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Build starts in 3 days: the changes and a Pause Squat → Squat (Barbell) swap to confirm. */
export const WeekBefore: Story = { args: { headsUp: buildHeadsUp(3) } };

/** First day of the phase. */
export const FirstDay: Story = { args: { headsUp: buildHeadsUp(0) } };

/** The swap was already made. */
export const SwapDone: Story = { args: { headsUp: buildHeadsUp(0, true) } };
